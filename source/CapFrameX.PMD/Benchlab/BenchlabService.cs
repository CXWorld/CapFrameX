using CapFrameX.Contracts.PMD;
using CapFrameX.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.PMD.Benchlab
{
    public class BenchlabService : IBenchlabService
    {
        private const string SERVICE_NAME = "BENCHLAB Service";
        private const string SERVICE_PROCESS_NAME = "PMD_Service";
        private const string SERVICE_FOLDER_NAME = "benchlab-service";
        private const string SERVICE_EXECUTABLE_NAME = "PMD_Service.exe";

        // intital 10 samples per second
        private int _sampleInterval = 100;
        private bool _isServiceRunning;
        private readonly ISubject<SensorSample> _pmdSensorStream = new Subject<SensorSample>();
        private readonly ISubject<EPmdServiceStatus> _pmdServiceStatusStream = new Subject<EPmdServiceStatus>();
        private IDisposable _pmdSensorStreamDisposable;
        private IDisposable _pmdServiceStatusStreamDisposable;
        private Process _benchlabProcess;

        public int CpuPowerSensorIndex { get; private set; }

        public int GpuPowerSensorIndex { get; private set; }

        public int MainboardPowerSensorIndex { get; private set; }

        public int SytemPowerSensorIndex { get; private set; }

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

        private async Task<IList<Sensor>> GetUpdatedSensorListAsync()
        {
            var json = string.Empty;
            using (var client = new NamedPipeClientStream(".", "BenchlabSensorPipe", PipeDirection.InOut))
            {
                await client.ConnectAsync();

                var writer = new StreamWriter(client) { AutoFlush = true };
                var reader = new StreamReader(client);

                await writer.WriteLineAsync("GetUpdatedSensorList");
                json = await reader.ReadLineAsync();

                // Do not dispose writer/reader separately — they share the pipe stream.
                // The client.Dispose() at the end will clean them up safely.
            }
            var sensorList = JsonConvert.DeserializeObject<List<Sensor>>(json);
            return sensorList ?? new List<Sensor>();
        }

        public async Task StartService()
        {
            IList<Sensor> initialSensorList = null;
            try
            {
                if (!IsBenchlabRunning())
                {
                    var started = TryStartWindowsService(SERVICE_NAME);
                    if (!started)
                    {
                        TryStartBundledService();
                    }
                }

                // Start both the fetch and a 2-second delay
                var fetchTask = GetUpdatedSensorListAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));

                // Wait for whichever completes first
                var completed = await Task.WhenAny(fetchTask, timeoutTask);

                if (completed == fetchTask)
                {
                    // Fetch finished within 2 seconds
                    initialSensorList = await fetchTask;
                }
                else
                {
                    _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Error);
                    return;
                }

                _isServiceRunning = true;
            }
            catch
            {
                _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Error);
                return;
            }

            if (!initialSensorList.IsNullOrEmpty())
            {
                for (var i = 0; i < initialSensorList.Count; i++)
                {
                    var sensor = initialSensorList[i];
                    if (sensor.ShortName == "CPU_P")
                    {
                        CpuPowerSensorIndex = i;
                    }
                    else if (sensor.ShortName == "GPU_P")
                    {
                        GpuPowerSensorIndex = i;
                    }
                    else if (sensor.ShortName == "MB_P")
                    {
                        MainboardPowerSensorIndex = i;
                    }
                    else if (sensor.ShortName == "SYS_P")
                    {
                        SytemPowerSensorIndex = i;
                    }
                }

                StartSensorStream();

                _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Running);
            }
        }

        /// <summary>
        /// Returns true if the named Windows service is currently running.
        /// </summary>
        private static bool IsWindowsServiceRunning(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsBenchlabProcessRunning()
        {
            return Process.GetProcessesByName(SERVICE_PROCESS_NAME).Any();
        }

        private static bool IsBenchlabRunning()
        {
            return IsWindowsServiceRunning(SERVICE_NAME) || IsBenchlabProcessRunning();
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
            catch (InvalidOperationException)
            {
                return false;
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
                return false;
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
                .SelectMany(async _ =>
                {
                    try
                    {
                        return await GetUpdatedSensorListAsync();
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Subscribe(sensorList =>
                {
                    if (sensorList == null)
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

            _pmdSensorStreamDisposable?.Dispose();
            _pmdSensorStreamDisposable = null;

            _pmdServiceStatusStreamDisposable?.Dispose();
            _pmdServiceStatusStreamDisposable = null;

            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Stopped);
            Task.Run(() => StopExternalService());
        }

        private void StopExternalService()
        {
            try
            {
                using (var sc = new ServiceController(SERVICE_NAME))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                }
            }
            catch { }

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
            catch { }
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
