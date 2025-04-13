using CapFrameX.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.PMD.Benchlab
{
    public class BenchlabService : IBenchlabService
    {
        // intital 10 samples per second
        private int _sampleInterval = 100;
        private bool _isServiceRunning;
        private readonly ISubject<SensorSample> _pmdSensorStream = new Subject<SensorSample>();
        private IDisposable _pmdSensorStreamDisposable;

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
                        StartService();
                    }
                }
            }
        }

        public IObservable<SensorSample> PmdSensorStream => _pmdSensorStream.AsObservable();

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

        public void StartService()
        {
            _isServiceRunning = true;

            // set sensor indices
            IList<Sensor> initialSensorList = null;
            Task.Run(async () => initialSensorList = await GetUpdatedSensorListAsync()).Wait();

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
            }
        }

        public void ShutDownService()
        {
            _isServiceRunning = false;
            _pmdSensorStreamDisposable?.Dispose();
            _pmdSensorStreamDisposable = null;
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
