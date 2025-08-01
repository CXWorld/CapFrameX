using CapFrameX.Contracts.PMD;
using CapFrameX.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        // intital 10 samples per second
        private int _sampleInterval = 100;
        private bool _isServiceRunning;
        private readonly ISubject<SensorSample> _pmdSensorStream = new Subject<SensorSample>();
        private readonly ISubject<EPmdServiceStatus> _pmdServiceStatusStream = new Subject<EPmdServiceStatus>();
        private IDisposable _pmdSensorStreamDisposable;
        private IDisposable _pmdServiceStatusStreamDisposable;

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
                        ShutDownService();
                        Task.Run(async () => await StartService());
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
                var isServiceRunning = GetServiceRunningStatus(SERVICE_NAME);
                if (!isServiceRunning)
                {
                    _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Stopped);
                    return;
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

                _pmdSensorStreamDisposable = Observable.Interval(TimeSpan.FromMilliseconds(MonitoringInterval))
                    .SelectMany(async _ => await GetUpdatedSensorListAsync())
                    .Subscribe(sensorList =>
                    {
                        var sensorSample = new SensorSample
                        {
                            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Sensors = sensorList
                        };
                        _pmdSensorStream.OnNext(sensorSample);
                    });

                _pmdServiceStatusStreamDisposable = Observable.Interval(TimeSpan.FromMilliseconds(1000))
                    .Subscribe(_ =>
                    {
                        var isServiceRunning = GetServiceRunningStatus(SERVICE_NAME);
                        if (!isServiceRunning)
                        {
                            _isServiceRunning = false;
                            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Stopped);
                        }
                        else
                        {
                            _isServiceRunning = true;
                            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Running);
                        }
                    });
            }
        }

        /// <summary>
        /// Returns true if the named Windows service is currently running.
        /// </summary>
        static bool GetServiceRunningStatus(string serviceName)
        {
            using (ServiceController sc = new ServiceController(serviceName))
            {
                return sc.Status == ServiceControllerStatus.Running;
            }
        }

        public void ShutDownService()
        {
            _isServiceRunning = false;

            _pmdSensorStreamDisposable?.Dispose();
            _pmdSensorStreamDisposable = null;

            _pmdServiceStatusStreamDisposable?.Dispose();
            _pmdServiceStatusStreamDisposable = null;
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
