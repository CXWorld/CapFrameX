using Prism.Mvvm;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapFrameX.PMD
{
    public class PmdMetricsManager : BindableBase
    {
        private string _allPowerAvg = "0.0W";
        private string _allGpuPowerAvg = "0.0W";
        private string _allPciExAvg = "0.0W";
        private string _pciExSlotAvg = "0.0W";
        private string _allCpuPowerAvg = "0.0W";
        private string _allAtxPowerAvg = "0.0W";

        private string _allPowerMax = "0.0W";
        private string _allGpuPowerMax = "0.0W";
        private string _allPciExMax = "0.0W";
        private string _pciExSlotMax = "0.0W";
        private string _allCpuPowerMax = "0.0W";
        private string _allAtxPowerMax = "0.0W";

        private float _allPowerMaxValue = float.MinValue;
        private float _allGpuPowerMaxValue = float.MinValue;
        private float _allPciExMaxValue = float.MinValue;
        private float _pciExSlotMaxValue = float.MinValue;
        private float _allCpuPowerMaxValue = float.MinValue;
        private float _allAtxPowerMaxValue = float.MinValue;

        private readonly List<float> _allPowerAvgHistory = new List<float>();
        private readonly List<float> _allGpuPowerAvgHistory = new List<float>();
        private readonly List<float> _allPciExAvgHistory = new List<float>();
        private readonly List<float> _pciExSlotAvgHistory = new List<float>();
        private readonly List<float> _allCpuPowerAvgHistory = new List<float>();
        private readonly List<float> _allAtxPowerAvgHistory = new List<float>();

        private readonly object _resetHistoryLock = new object();

        public string AllGpuPowerAvg
        {
            get => _allGpuPowerAvg;
            set
            {
                _allGpuPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllGpuPowerMax
        {
            get => _allGpuPowerMax;
            set
            {
                _allGpuPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllPciExAvg
        {
            get => _allPciExAvg;
            set
            {
                _allPciExAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllPciExMax
        {
            get => _allPciExMax;
            set
            {
                _allPciExMax = value;
                RaisePropertyChanged();
            }
        }

        public string PciExSlotAvg
        {
            get => _pciExSlotAvg;
            set
            {
                _pciExSlotAvg = value;
                RaisePropertyChanged();
            }
        }

        public string PciExSlotMax
        {
            get => _pciExSlotMax;
            set
            {
                _pciExSlotMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllCpuPowerAvg
        {
            get => _allCpuPowerAvg;
            set
            {
                _allCpuPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllCpuPowerMax
        {
            get => _allCpuPowerMax;
            set
            {
                _allCpuPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllAtxPowerAvg
        {
            get => _allAtxPowerAvg;
            set
            {
                _allAtxPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllAtxPowerMax
        {
            get => _allAtxPowerMax;
            set
            {
                _allAtxPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllPowerAvg
        {
            get => _allPowerAvg;
            set
            {
                _allPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllPowerMax
        {
            get => _allPowerMax;
            set
            {
                _allPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public int PmdMetricRefreshPeriod { get; set; }

        public int PmdDataWindowSeconds { get; set; }

        public PmdMetricsManager(int pmdMetricRefreshPeriod, int pmdDataWindowSeconds)
        {
            PmdMetricRefreshPeriod = pmdMetricRefreshPeriod;
            PmdDataWindowSeconds = pmdDataWindowSeconds;
        }

        public void UpdateMetrics(IList<PmdChannel[]> metricsData)
        {
            PmdMetricSet pmdMetricSet;
            int historyLength = (int)(PmdDataWindowSeconds / (PmdMetricRefreshPeriod / 1000d));

            // All GPU
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PmdChannelExtensions.GPUPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allGpuPowerAvgHistory, historyLength);
                AllGpuPowerAvg = $"{_allGpuPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                if(pmdMetricSet.Max > _allGpuPowerMaxValue)
                {
                    _allGpuPowerMaxValue = pmdMetricSet.Max;
                    AllGpuPowerMax = $"{_allGpuPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // All PCI Express
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PmdChannelExtensions.PCIePowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allPciExAvgHistory, historyLength);
                AllPciExAvg = $"{_allPciExAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _allPciExMaxValue)
                {
                    _allPciExMaxValue = pmdMetricSet.Max;
                    AllPciExMax = $"{_allPciExMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // PCI Express Slot
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PmdChannelExtensions.PCIeSlotPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _pciExSlotAvgHistory, historyLength);
                PciExSlotAvg = $"{_pciExSlotAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _pciExSlotMaxValue)
                {
                    _pciExSlotMaxValue = pmdMetricSet.Max;
                    PciExSlotMax = $"{_pciExSlotMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // All EPS 12V 
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PmdChannelExtensions.EPSPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allCpuPowerAvgHistory, historyLength);
                AllCpuPowerAvg = $"{_allCpuPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _allCpuPowerMaxValue)
                {
                    _allCpuPowerMaxValue = pmdMetricSet.Max;
                    AllCpuPowerMax = $"{_allCpuPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // All ATX
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PmdChannelExtensions.ATXPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allAtxPowerAvgHistory, historyLength);
                AllAtxPowerAvg = $"{_allAtxPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _allAtxPowerMaxValue)
                {
                    _allAtxPowerMaxValue = pmdMetricSet.Max;
                    AllAtxPowerMax = $"{_allAtxPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }

            // All System
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PmdChannelExtensions.SystemPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allPowerAvgHistory, historyLength);
                AllPowerAvg = $"{_allPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} W";
                if (pmdMetricSet.Max > _allPowerMaxValue)
                {
                    _allPowerMaxValue = pmdMetricSet.Max;
                    AllPowerMax = $"{_allPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} W";
                }
            }
        }

        public PmdMetricSet GetPmdMetricSetByIndexGroup(IList<PmdChannel[]> channelData, int[] indexGroup)
        {
            float max = float.MinValue;
            float min = float.MaxValue;
            double sum = 0;

            foreach (var channel in channelData)
            {
                var currentChannlesSumPower = indexGroup.Sum(index => channel[index].Value);
                sum += currentChannlesSumPower;

                if (currentChannlesSumPower > max)
                    max = currentChannlesSumPower;

                if (currentChannlesSumPower < min)
                    min = currentChannlesSumPower;
            }

            return new PmdMetricSet()
            {
                Min = min,
                Average = (float)(sum / channelData.Count),
                Max = max,
            };
        }

        public void ResetHistory()
        {
            lock (_resetHistoryLock)
            {
                _allPowerAvgHistory.Clear();
                _allGpuPowerAvgHistory.Clear();
                _allPciExAvgHistory.Clear();
                _pciExSlotAvgHistory.Clear();
                _allCpuPowerAvgHistory.Clear();
                _allAtxPowerAvgHistory.Clear();
            }
        }


        private void UpdateHistory(PmdMetricSet pmdMetricSet, List<float> allPowerAvgHistory, int historyLength)
        {
            allPowerAvgHistory.Add(pmdMetricSet.Average);

            int count = allPowerAvgHistory.Count;
            if (count > historyLength)
                allPowerAvgHistory.RemoveRange(0, count - historyLength);
        }
    }

    public struct PmdMetricSet
    {
        public float Min { get; set; }
        public float Average { get; set; }
        public float Max { get; set; }
    }
}
