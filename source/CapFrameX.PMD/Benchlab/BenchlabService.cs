using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CapFrameX.Contracts.PMD;

namespace CapFrameX.PMD.Benchlab
{
    public class BenchlabService : IBenchlabService
    {
        private const string SERVICE_NAME = "BENCHLAB Service";
        private const string SERVICE_PROCESS_NAME = "BL_Service";
        private const string SERVICE_FOLDER_NAME = "benchlab-service";
        private const string SERVICE_EXECUTABLE_NAME = "BL_Service.exe";

        private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PipeRequestTimeout = TimeSpan.FromSeconds(2);

        // Initial 10 samples per second.
        private int _sampleInterval = 100;
        private bool _isServiceRunning;
        private readonly ISubject<SensorSample> _pmdSensorStream = new Subject<SensorSample>();
        private readonly ISubject<EPmdServiceStatus> _pmdServiceStatusStream = new Subject<EPmdServiceStatus>();
        private readonly SemaphoreSlim _pipeRequestLock = new SemaphoreSlim(1, 1);
        private IDisposable _pmdSensorStreamDisposable;
        private Process _benchlabProcess;
        private string _devicePipeName;
        private string _selectedDeviceId;

        public int CpuPowerSensorIndex { get; private set; } = -1;

        public int GpuPowerSensorIndex { get; private set; } = -1;

        public int MainboardPowerSensorIndex { get; private set; } = -1;

        public int SytemPowerSensorIndex { get; private set; } = -1;

        public int MinMonitoringInterval { get; set; } = 25;

        public bool IsServiceRunning => _isServiceRunning;

        public int MonitoringInterval
        {
            get => _sampleInterval;
            set
            {
                if (value >= MinMonitoringInterval)
                {
                    _sampleInterval = value;

                    if (_isServiceRunning)
                    {
                        RestartSensorStream();
                    }
                }
            }
        }

        public IObservable<SensorSample> PmdSensorStream => _pmdSensorStream.AsObservable();

        public IObservable<EPmdServiceStatus> PmdServiceStatusStream => _pmdServiceStatusStream.AsObservable();

        public async Task StartService()
        {
            if (_isServiceRunning)
            {
                return;
            }

            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Waiting);

            try
            {
                if (!EnsureBenchlabServiceStarted())
                {
                    throw new InvalidOperationException("The BENCHLAB service could not be started.");
                }

                var devices = await GetDevicesFromCompatibleServiceAsync();
                var device = BenchlabProtocol.SelectDevice(devices);
                if (device == null)
                {
                    throw new InvalidOperationException("The BENCHLAB service reported no connected device.");
                }

                SelectDevice(device);

                IList<Sensor> initialSensorList;
                using (var cts = new CancellationTokenSource(PipeRequestTimeout))
                {
                    initialSensorList = await GetUpdatedSensorListAsync(cts.Token);
                }

                if (!UpdatePowerSensorIndices(initialSensorList))
                {
                    throw new InvalidDataException("The BENCHLAB device does not expose all required power sensors.");
                }

                _isServiceRunning = true;
            }
            catch
            {
                _isServiceRunning = false;
                _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Error);
                return;
            }

            StartSensorStream();
            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Running);
        }

        private async Task<IList<BenchlabDeviceInfo>> GetDevicesFromCompatibleServiceAsync()
        {
            try
            {
                return await GetDevicesWhenReadyAsync();
            }
            catch
            {
                // An installed pre-2.0 service can be running under the same Windows service
                // name but does not expose the discovery pipe. Replace it for this session
                // with the bundled service that implements the current protocol.
                if (!IsWindowsServiceRunning(SERVICE_NAME)
                    || !TryStopWindowsService(SERVICE_NAME)
                    || !TryStartBundledService())
                {
                    throw;
                }

                return await GetDevicesWhenReadyAsync();
            }
        }

        private async Task<IList<BenchlabDeviceInfo>> GetDevicesWhenReadyAsync()
        {
            using (var cts = new CancellationTokenSource(DiscoveryTimeout))
            {
                var json = await SendPipeCommandAsync(
                    BenchlabProtocol.DiscoveryPipeName,
                    BenchlabProtocol.ListDevicesCommand,
                    cts.Token);
                return BenchlabProtocol.DeserializeDevices(json);
            }
        }

        private async Task<IList<Sensor>> GetUpdatedSensorListAsync(CancellationToken cancellationToken)
        {
            await _pipeRequestLock.WaitAsync(cancellationToken);
            try
            {
                Exception lastError = null;
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(_devicePipeName))
                        {
                            var devices = await GetDevicesAsync(cancellationToken);
                            var device = BenchlabProtocol.SelectDevice(devices, _selectedDeviceId);
                            if (device == null)
                            {
                                throw new InvalidOperationException("The selected BENCHLAB device is not connected.");
                            }

                            SelectDevice(device);
                        }

                        var json = await SendPipeCommandAsync(
                            _devicePipeName,
                            BenchlabProtocol.GetUpdatedSensorListCommand,
                            cancellationToken);
                        return BenchlabProtocol.DeserializeSensors(json);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        _devicePipeName = null;
                    }
                }

                throw lastError ?? new InvalidOperationException("The BENCHLAB sensor request failed.");
            }
            finally
            {
                _pipeRequestLock.Release();
            }
        }

        private static async Task<IList<BenchlabDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken)
        {
            var json = await SendPipeCommandAsync(
                BenchlabProtocol.DiscoveryPipeName,
                BenchlabProtocol.ListDevicesCommand,
                cancellationToken);
            return BenchlabProtocol.DeserializeDevices(json);
        }

        private static async Task<string> SendPipeCommandAsync(
            string pipeName,
            string command,
            CancellationToken cancellationToken)
        {
            using (var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous))
            {
                await client.ConnectAsync(cancellationToken);

                var utf8WithoutByteOrderMark = new UTF8Encoding(false);
                using (var writer = new StreamWriter(client, utf8WithoutByteOrderMark, 1024, true) { AutoFlush = true })
                using (var reader = new StreamReader(client, utf8WithoutByteOrderMark, true, 1024, true))
                {
                    await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
                    var response = await reader.ReadLineAsync(cancellationToken);
                    if (response == null)
                    {
                        throw new EndOfStreamException("The BENCHLAB service closed the pipe without a response.");
                    }

                    return response;
                }
            }
        }

        private void SelectDevice(BenchlabDeviceInfo device)
        {
            _selectedDeviceId = device.DeviceId;
            _devicePipeName = device.PipeName;
        }

        private bool UpdatePowerSensorIndices(IList<Sensor> sensors)
        {
            if (!BenchlabProtocol.TryGetPowerSensorIndices(
                sensors,
                out var cpuPowerSensorIndex,
                out var gpuPowerSensorIndex,
                out var mainboardPowerSensorIndex,
                out var systemPowerSensorIndex))
            {
                return false;
            }

            CpuPowerSensorIndex = cpuPowerSensorIndex;
            GpuPowerSensorIndex = gpuPowerSensorIndex;
            MainboardPowerSensorIndex = mainboardPowerSensorIndex;
            SytemPowerSensorIndex = systemPowerSensorIndex;
            return true;
        }

        private static bool IsWindowsServiceRunning(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsBenchlabProcessRunning()
        {
            var processes = Process.GetProcessesByName(SERVICE_PROCESS_NAME);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static bool IsBenchlabRunning()
        {
            return IsWindowsServiceRunning(SERVICE_NAME) || IsBenchlabProcessRunning();
        }

        private bool EnsureBenchlabServiceStarted()
        {
            if (IsBenchlabRunning())
            {
                return true;
            }

            return TryStartWindowsService(SERVICE_NAME) || TryStartBundledService();
        }

        private static bool TryStartWindowsService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        return true;
                    }

                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    }

                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryStopWindowsService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        return true;
                    }

                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    return sc.Status == ServiceControllerStatus.Stopped;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool TryStartBundledService()
        {
            if (IsBenchlabProcessRunning())
            {
                return true;
            }

            var executablePath = GetBundledServicePath();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _benchlabProcess = Process.Start(startInfo);
                return _benchlabProcess != null;
            }
            catch
            {
                return false;
            }
        }

        private void StartSensorStream()
        {
            _pmdSensorStreamDisposable?.Dispose();
            _pmdSensorStreamDisposable = Observable.Interval(TimeSpan.FromMilliseconds(MonitoringInterval))
                .Select(_ => Observable.FromAsync(async () =>
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(PipeRequestTimeout))
                        {
                            return await GetUpdatedSensorListAsync(cts.Token);
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }))
                .Concat()
                .Subscribe(sensorList =>
                {
                    if (sensorList == null || !UpdatePowerSensorIndices(sensorList))
                    {
                        HandleServiceError();
                        return;
                    }

                    var sensorSample = new SensorSample
                    {
                        TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Sensors = sensorList
                    };
                    _pmdSensorStream.OnNext(sensorSample);
                });
        }

        private void HandleServiceError()
        {
            _isServiceRunning = false;
            _devicePipeName = null;

            _pmdSensorStreamDisposable?.Dispose();
            _pmdSensorStreamDisposable = null;

            var isServiceRunning = IsBenchlabRunning();
            _pmdServiceStatusStream.OnNext(isServiceRunning ? EPmdServiceStatus.Error : EPmdServiceStatus.Stopped);
        }

        private void RestartSensorStream()
        {
            if (!_isServiceRunning)
            {
                return;
            }

            StartSensorStream();
        }

        private static string GetBundledServicePath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, SERVICE_FOLDER_NAME, SERVICE_EXECUTABLE_NAME);
        }

        public void ShutDownService()
        {
            _isServiceRunning = false;
            _devicePipeName = null;
            _selectedDeviceId = null;

            _pmdSensorStreamDisposable?.Dispose();
            _pmdSensorStreamDisposable = null;

            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Stopped);
            Task.Run(() => StopExternalService());
        }

        private void StopExternalService()
        {
            TryStopWindowsService(SERVICE_NAME);

            try
            {
                var processes = Process.GetProcessesByName(SERVICE_PROCESS_NAME);
                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.CloseMainWindow();
                            if (!process.WaitForExit(2000))
                            {
                                process.Kill();
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _benchlabProcess?.Dispose();
                _benchlabProcess = null;
            }
        }

        public IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<SensorSample> sensorData)
        {
            var minTimeStamp = sensorData.First().TimeStamp;
            foreach (var sample in sensorData)
            {
                yield return new Point((sample.TimeStamp - minTimeStamp) * 1E-03, sample.Sensors[CpuPowerSensorIndex].Value);
            }
        }

        public IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<SensorSample> sensorData)
        {
            var minTimeStamp = sensorData.First().TimeStamp;
            foreach (var sample in sensorData)
            {
                yield return new Point((sample.TimeStamp - minTimeStamp) * 1E-03, sample.Sensors[GpuPowerSensorIndex].Value);
            }
        }
    }
}
