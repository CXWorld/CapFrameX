using Prism.Mvvm;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapFrameX.PMD.Benchlab
{
    public class BenchlabMetricsManager : BindableBase
    {
        const string ZERO_WATT = "0.0 W";

        private string _systemPowerCur = ZERO_WATT;
        private string _gpuPowerCur = ZERO_WATT;
        private string _cpuPowerCur = ZERO_WATT;
        private string _mainboardPowerCur = ZERO_WATT;

        private string _systemPowerAvg = ZERO_WATT;
        private string _gpuPowerAvg = ZERO_WATT;
        private string _cpuPowerAvg = ZERO_WATT;
        private string _mainboardPowerAvg = ZERO_WATT;

        private string _systemPowerMax = ZERO_WATT;
        private string _gpuPowerMax = ZERO_WATT;
        private string _cpuPowerMax = ZERO_WATT;
        private string _mainboardPowerMax = ZERO_WATT;

        private float _systemPowerMaxValue = float.MinValue;
        private float _gpuPowerMaxValue = float.MinValue;
        private float _cpuPowerMaxValue = float.MinValue;
        private float _mainboardPowerMaxValue = float.MinValue;

        private readonly List<float> _systemPowerAvgHistory = new List<float>();
        private readonly List<float> _gpuPowerAvgHistory = new List<float>();
        private readonly List<float> _cpuPowerAvgHistory = new List<float>();
        private readonly List<float> _mainboardPowerAvgHistory = new List<float>();

        private readonly object _resetHistoryLock = new object();
        private readonly IBenchlabService _benchlabService;

        public string SystemPowerCur
        {
            get => _systemPowerCur;
            set
            {
                _systemPowerCur = value;
                RaisePropertyChanged();
            }
        }

        public string GpuPowerCur
        {
            get => _gpuPowerCur;
            set
            {
                _gpuPowerCur = value;
                RaisePropertyChanged();
            }
        }

        public string CpuPowerCur
        {
            get => _cpuPowerCur;
            set
            {
                _cpuPowerCur = value;
                RaisePropertyChanged();
            }
        }

        public string MainboardPowerCur
        {
            get => _mainboardPowerCur;
            set
            {
                _mainboardPowerCur = value;
                RaisePropertyChanged();
            }
        }

        public string SystemPowerAvg
        {
            get => _systemPowerAvg;
            set
            {
                _systemPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string GpuPowerAvg
        {
            get => _gpuPowerAvg;
            set
            {
                _gpuPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string CpuPowerAvg
        {
            get => _cpuPowerAvg;
            set
            {
                _cpuPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string MainboardPowerAvg
        {
            get => _mainboardPowerAvg;
            set
            {
                _mainboardPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string SystemPowerMax
        {
            get => _systemPowerMax;
            set
            {
                _systemPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string GpuPowerMax
        {
            get => _gpuPowerMax;
            set
            {
                _gpuPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string CpuPowerMax
        {
            get => _cpuPowerMax;
            set
            {
                _cpuPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string MainboardPowerMax
        {
            get => _mainboardPowerMax;
            set
            {
                _mainboardPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public int PmdMetricRefreshPeriod { get; set; }

        public int PmdDataWindowSeconds { get; set; }

        public BenchlabMetricsManager(IBenchlabService benchlabService, int pmdMetricRefreshPeriod, int pmdDataWindowSeconds)
        {
            _benchlabService = benchlabService;
            PmdMetricRefreshPeriod = pmdMetricRefreshPeriod;
            PmdDataWindowSeconds = pmdDataWindowSeconds;
        }

        public void ResetHistory()
        {
            lock (_resetHistoryLock)
            {
                _systemPowerAvgHistory.Clear();
                _gpuPowerAvgHistory.Clear();
                _cpuPowerAvgHistory.Clear();
                _mainboardPowerAvgHistory.Clear();
            }

            SystemPowerCur = ZERO_WATT;
            GpuPowerCur = ZERO_WATT;
            CpuPowerCur = ZERO_WATT;
            MainboardPowerCur = ZERO_WATT;
            SystemPowerAvg = ZERO_WATT;
            GpuPowerAvg = ZERO_WATT;
            CpuPowerAvg = ZERO_WATT;
            MainboardPowerAvg = ZERO_WATT;
            SystemPowerMax = ZERO_WATT;
            GpuPowerMax = ZERO_WATT;
            CpuPowerMax = ZERO_WATT;
            MainboardPowerMax = ZERO_WATT;

            _systemPowerMaxValue = float.MinValue;
            _gpuPowerMaxValue = float.MinValue;
            _cpuPowerMaxValue = float.MinValue;
            _mainboardPowerMaxValue = float.MinValue;
        }

        public void UpdateMetrics(IList<SensorSample> metricsData)
        {
            PmdMetricSet pmdMetricSet;
            int historyLength = (int)(PmdDataWindowSeconds / (PmdMetricRefreshPeriod / 1000d));

            // GPU Power
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, _benchlabService.GpuPowerSensorIndex);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _gpuPowerAvgHistory, historyLength);
                GpuPowerAvg = $"{_gpuPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                GpuPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _gpuPowerMaxValue)
                {
                    _gpuPowerMaxValue = pmdMetricSet.Max;
                    GpuPowerMax = $"{_gpuPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // CPU Power
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, _benchlabService.CpuPowerSensorIndex);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _cpuPowerAvgHistory, historyLength);
                CpuPowerAvg = $"{_cpuPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                CpuPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _cpuPowerMaxValue)
                {
                    _cpuPowerMaxValue = pmdMetricSet.Max;
                    CpuPowerMax = $"{_cpuPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // Mainboard Power
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, _benchlabService.MainboardPowerSensorIndex);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _mainboardPowerAvgHistory, historyLength);
                MainboardPowerAvg = $"{_mainboardPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                MainboardPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _mainboardPowerMaxValue)
                {
                    _mainboardPowerMaxValue = pmdMetricSet.Max;
                    MainboardPowerMax = $"{_mainboardPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // System Power
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, _benchlabService.SytemPowerSensorIndex);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _systemPowerAvgHistory, historyLength);
                SystemPowerAvg = $"{_systemPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                SystemPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _systemPowerMaxValue)
                {
                    _systemPowerMaxValue = pmdMetricSet.Max;
                    SystemPowerMax = $"{_systemPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }
        }

        public PmdMetricSet GetPmdMetricSetByIndexGroup(IList<SensorSample> sensorSamples, int index)
        {
            float max = float.MinValue;
            float min = float.MaxValue;
            double sum = 0;

            foreach (var sensorSample in sensorSamples)
            {
                var currentSensorPower = (float)sensorSample.Sensors[index].Value;
                sum += currentSensorPower;

                if (currentSensorPower > max)
                    max = currentSensorPower;

                if (currentSensorPower < min)
                    min = currentSensorPower;
            }

            return new PmdMetricSet()
            {
                Min = min,
                Average = (float)(sum / sensorSamples.Count),
                Max = max,
            };
        }

        private void UpdateHistory(PmdMetricSet pmdMetricSet, List<float> historyData, int historyLength)
        {
            historyData.Add(pmdMetricSet.Average);

            int count = historyData.Count;
            if (count > historyLength)
                historyData.RemoveRange(0, count - historyLength);
        }
    }
}
