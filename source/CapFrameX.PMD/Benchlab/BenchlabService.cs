using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CapFrameX.Contracts.PMD;
using Microsoft.Win32;

namespace CapFrameX.PMD.Benchlab
{
    public class BenchlabService : IBenchlabService
    {
        private const string SERVICE_NAME = "BENCHLAB Service";
        private const string SERVICE_PROCESS_NAME = "BL_Service";
        private const string SERVICE_FOLDER_NAME = "benchlab-service";
        private const string SERVICE_EXECUTABLE_NAME = "BL_Service.exe";
        private const string LEGACY_SERVICE_EXECUTABLE_NAME = "PMD_Service.exe";
        private const int SERVICE_AUTO_START = 2;
        private const uint SC_MANAGER_CONNECT = 0x0001;
        private const uint SERVICE_CHANGE_CONFIG = 0x0002;
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        private const uint SERVICE_DEMAND_START = 3;

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
        private ChildProcessJob _benchlabProcessJob;
        private string _devicePipeName;
        private string _selectedDeviceId;

        public int CpuPowerSensorIndex { get; private set; } = -1;

        public int GpuPowerSensorIndex { get; private set; } = -1;

        public int MainboardPowerSensorIndex { get; private set; } = -1;

        public int SytemPowerSensorIndex { get; private set; } = -1;

        public int MinMonitoringInterval { get; set; } = 25;

        public bool IsServiceRunning => _isServiceRunning;

        public bool EnsureDemandStartMode()
        {
            if (!TryGetWindowsServiceStartType(SERVICE_NAME, out var startType))
            {
                return false;
            }

            if (!ShouldConfigureDemandStart(startType))
            {
                return true;
            }

            // The legacy installer registered BENCHLAB Service as an automatic LocalSystem
            // service. Stop that boot-started instance before changing its configuration; a
            // manually configured/running service is deliberately left untouched.
            if (IsWindowsServiceRunning(SERVICE_NAME) && !TryStopWindowsService(SERVICE_NAME))
            {
                return false;
            }

            return TrySetWindowsServiceDemandStart(SERVICE_NAME);
        }

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
                if (!EnsureDemandStartMode() || !EnsureBenchlabServiceStarted())
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
                PublishSensorSample(initialSensorList);
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

        private static bool TryGetWindowsServiceStartType(string serviceName, out int? startType)
        {
            startType = null;

            try
            {
                using (var serviceKey = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
                {
                    if (serviceKey == null)
                    {
                        return true;
                    }

                    var startValue = serviceKey.GetValue("Start");
                    if (startValue == null)
                    {
                        return false;
                    }

                    startType = Convert.ToInt32(startValue);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static bool ShouldConfigureDemandStart(int? startType)
        {
            return startType == SERVICE_AUTO_START;
        }

        private static bool TrySetWindowsServiceDemandStart(string serviceName)
        {
            var serviceManager = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (serviceManager == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var service = OpenService(serviceManager, serviceName, SERVICE_CHANGE_CONFIG);
                if (service == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    return ChangeServiceConfig(
                        service,
                        SERVICE_NO_CHANGE,
                        SERVICE_DEMAND_START,
                        SERVICE_NO_CHANGE,
                        null,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(serviceManager);
            }
        }

        private static bool IsLegacyWindowsService(string serviceName)
        {
            try
            {
                using (var serviceKey = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
                {
                    return IsLegacyServiceImagePath(serviceKey?.GetValue("ImagePath") as string);
                }
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsLegacyServiceImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            var expandedImagePath = Environment.ExpandEnvironmentVariables(imagePath).Trim();
            string executablePath;

            if (expandedImagePath.StartsWith("\"", StringComparison.Ordinal))
            {
                var closingQuoteIndex = expandedImagePath.IndexOf('\"', 1);
                if (closingQuoteIndex < 0)
                {
                    return false;
                }

                executablePath = expandedImagePath.Substring(1, closingQuoteIndex - 1);
            }
            else
            {
                var argumentSeparatorIndex = expandedImagePath.IndexOfAny(new[] { ' ', '\t' });
                executablePath = argumentSeparatorIndex < 0
                    ? expandedImagePath
                    : expandedImagePath.Substring(0, argumentSeparatorIndex);
            }

            return string.Equals(
                Path.GetFileName(executablePath),
                LEGACY_SERVICE_EXECUTABLE_NAME,
                StringComparison.OrdinalIgnoreCase);
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
            // The former PMD_Service uses the same Windows service name but does not
            // expose the discovery pipe. Starting it first would cost the complete
            // compatibility timeout before the bundled service can take over.
            if (IsLegacyWindowsService(SERVICE_NAME))
            {
                if (IsWindowsServiceRunning(SERVICE_NAME) && !TryStopWindowsService(SERVICE_NAME))
                {
                    return false;
                }

                return TryStartBundledService();
            }

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

            Process process = null;
            ChildProcessJob processJob = null;
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                processJob = ChildProcessJob.Attach(process);
                _benchlabProcess = process;
                _benchlabProcessJob = processJob;
                return true;
            }
            catch
            {
                processJob?.Dispose();

                try
                {
                    if (process != null && !process.HasExited)
                    {
                        process.Kill(true);
                        process.WaitForExit(2000);
                    }
                }
                catch
                {
                }
                finally
                {
                    process?.Dispose();
                }

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

                    PublishSensorSample(sensorList);
                });
        }

        private void PublishSensorSample(IList<Sensor> sensorList)
        {
            var sensorSample = new SensorSample
            {
                TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sensors = sensorList
            };
            _pmdSensorStream.OnNext(sensorSample);
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
            StopExternalService();
        }

        private void StopExternalService()
        {
            _benchlabProcessJob?.Dispose();
            _benchlabProcessJob = null;

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
                            var closeRequested = process.CloseMainWindow();
                            if ((!closeRequested || !process.WaitForExit(500)) && !process.HasExited)
                            {
                                process.Kill(true);
                                process.WaitForExit(2000);
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

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenService(
            IntPtr serviceManager,
            string serviceName,
            uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig(
            IntPtr service,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password,
            string displayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr serviceHandle);
    }
}
