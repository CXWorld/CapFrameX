using CapFrameX.Contracts.Localization;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapFrameX.PMD.Powenetics
{
    public class PoweneticsMetricsManager : BindableBase
    {
        static string ZERO_WATT => "0.0 " + CxLang.T("PoweneticsMetricsManager_W");

        private string _allPowerCur = ZERO_WATT;
        private string _allGpuPowerCur = ZERO_WATT;
        private string _allPciExCur = ZERO_WATT;
        private string _pciExSlotCur = ZERO_WATT;
        private string _allCpuPowerCur = ZERO_WATT;
        private string _allAtxPowerCur = ZERO_WATT;

        private string _allPowerAvg = ZERO_WATT;
        private string _allGpuPowerAvg = ZERO_WATT;
        private string _allPciExAvg = ZERO_WATT;
        private string _pciExSlotAvg = ZERO_WATT;
        private string _allCpuPowerAvg = ZERO_WATT;
        private string _allAtxPowerAvg = ZERO_WATT;

        private string _allPowerMax = ZERO_WATT;
        private string _allGpuPowerMax = ZERO_WATT;
        private string _allPciExMax = ZERO_WATT;
        private string _pciExSlotMax = ZERO_WATT;
        private string _allCpuPowerMax = ZERO_WATT;
        private string _allAtxPowerMax = ZERO_WATT;

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

        public bool GpuPowerIncomplete
        {
            get => PciExSlotCur == ZERO_WATT || AllPciExCur == ZERO_WATT;
        }

        public bool SystemPowerIncomplete
        {
            get => AllPciExCur == ZERO_WATT || AllCpuPowerCur == ZERO_WATT || AllAtxPowerCur == ZERO_WATT;
        }


        public string AllGpuPowerCur
        {
            get => Show(_allGpuPowerCur);
            set
            {
                _allGpuPowerCur = value;
                RaisePropertyChanged();
            }
        }

        public string AllGpuPowerAvg
        {
            get => Show(_allGpuPowerAvg);
            set
            {
                _allGpuPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllGpuPowerMax
        {
            get => Show(_allGpuPowerMax);
            set
            {
                _allGpuPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllPciExCur
        {
            get => Show(_allPciExCur);
            set
            {
                _allPciExCur = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SystemPowerIncomplete));
                RaisePropertyChanged(nameof(GpuPowerIncomplete));
            }
        }

        public string AllPciExAvg
        {
            get => Show(_allPciExAvg);
            set
            {
                _allPciExAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllPciExMax
        {
            get => Show(_allPciExMax);
            set
            {
                _allPciExMax = value;
                RaisePropertyChanged();
            }
        }

        public string PciExSlotCur
        {
            get => Show(_pciExSlotCur);
            set
            {
                _pciExSlotCur = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(GpuPowerIncomplete));
            }
        }

        public string PciExSlotAvg
        {
            get => Show(_pciExSlotAvg);
            set
            {
                _pciExSlotAvg = value;
                RaisePropertyChanged();
            }
        }

        public string PciExSlotMax
        {
            get => Show(_pciExSlotMax);
            set
            {
                _pciExSlotMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllCpuPowerCur
        {
            get => Show(_allCpuPowerCur);
            set
            {
                _allCpuPowerCur = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SystemPowerIncomplete));
            }
        }

        public string AllCpuPowerAvg
        {
            get => Show(_allCpuPowerAvg);
            set
            {
                _allCpuPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllCpuPowerMax
        {
            get => Show(_allCpuPowerMax);
            set
            {
                _allCpuPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllAtxPowerCur
        {
            get => Show(_allAtxPowerCur);
            set
            {
                _allAtxPowerCur = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SystemPowerIncomplete));
            }
        }

        public string AllAtxPowerAvg
        {
            get => Show(_allAtxPowerAvg);
            set
            {
                _allAtxPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllAtxPowerMax
        {
            get => Show(_allAtxPowerMax);
            set
            {
                _allAtxPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public string AllPowerCur
        {
            get => Show(_allPowerCur);
            set
            {
                _allPowerCur = value;
                RaisePropertyChanged();
            }
        }

        public string AllPowerAvg
        {
            get => Show(_allPowerAvg);
            set
            {
                _allPowerAvg = value;
                RaisePropertyChanged();
            }
        }

        public string AllPowerMax
        {
            get => Show(_allPowerMax);
            set
            {
                _allPowerMax = value;
                RaisePropertyChanged();
            }
        }

        public int PmdMetricRefreshPeriod { get; set; }

        public int PmdDataWindowSeconds { get; set; }

        static string Show(string value) => CxLang.Instance.TranslateOverlay(value);

        public PoweneticsMetricsManager(int pmdMetricRefreshPeriod, int pmdDataWindowSeconds)
        {
            PmdMetricRefreshPeriod = pmdMetricRefreshPeriod;
            PmdDataWindowSeconds = pmdDataWindowSeconds;
            CxLang.Instance.PropertyChanged += (_, __) => RaisePropertyChanged(string.Empty);
        }

        public void UpdateMetrics(IList<PoweneticsChannel[]> metricsData)
        {
            PmdMetricSet pmdMetricSet;
            int historyLength = (int)(PmdDataWindowSeconds / (PmdMetricRefreshPeriod / 1000d));

            // All GPU
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PoweneticsChannelExtensions.GPUPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allGpuPowerAvgHistory, historyLength);
                AllGpuPowerAvg = $"{_allGpuPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W2")}";
                AllGpuPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W3")}";
                if (pmdMetricSet.Max > _allGpuPowerMaxValue)
                {
                    _allGpuPowerMaxValue = pmdMetricSet.Max;
                    AllGpuPowerMax = $"{_allGpuPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W4")}";
                }
            }

            // All PCI Express
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PoweneticsChannelExtensions.PCIePowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allPciExAvgHistory, historyLength);
                AllPciExAvg = $"{_allPciExAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W5")}";
                AllPciExCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W6")}";
                if (pmdMetricSet.Max > _allPciExMaxValue)
                {
                    _allPciExMaxValue = pmdMetricSet.Max;
                    AllPciExMax = $"{_allPciExMaxValue.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W7")}";
                }
            }

            // PCI Express Slot
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PoweneticsChannelExtensions.PCIeSlotPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _pciExSlotAvgHistory, historyLength);
                PciExSlotAvg = $"{_pciExSlotAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W8")}";
                PciExSlotCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W9")}";
                if (pmdMetricSet.Max > _pciExSlotMaxValue)
                {
                    _pciExSlotMaxValue = pmdMetricSet.Max;
                    PciExSlotMax = $"{_pciExSlotMaxValue.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W10")}";
                }
            }

            // All EPS 12V 
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PoweneticsChannelExtensions.EPSPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allCpuPowerAvgHistory, historyLength);
                AllCpuPowerAvg = $"{_allCpuPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W11")}";
                AllCpuPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W12")}";
                if (pmdMetricSet.Max > _allCpuPowerMaxValue)
                {
                    _allCpuPowerMaxValue = pmdMetricSet.Max;
                    AllCpuPowerMax = $"{_allCpuPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W13")}";
                }
            }

            // All ATX
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PoweneticsChannelExtensions.ATXPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allAtxPowerAvgHistory, historyLength);
                AllAtxPowerAvg = $"{_allAtxPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W14")}";
                AllAtxPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W15")}";
                if (pmdMetricSet.Max > _allAtxPowerMaxValue)
                {
                    _allAtxPowerMaxValue = pmdMetricSet.Max;
                    AllAtxPowerMax = $"{_allAtxPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W16")}";
                }
            }

            // All System
            pmdMetricSet = GetPmdMetricSetByIndexGroup(metricsData, PoweneticsChannelExtensions.SystemPowerIndexGroup);
            lock (_resetHistoryLock)
            {
                UpdateHistory(pmdMetricSet, _allPowerAvgHistory, historyLength);
                AllPowerAvg = $"{_allPowerAvgHistory.Average().ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W17")}";
                AllPowerCur = $"{pmdMetricSet.Average.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W18")}";
                if (pmdMetricSet.Max > _allPowerMaxValue)
                {
                    _allPowerMaxValue = pmdMetricSet.Max;
                    AllPowerMax = $"{_allPowerMaxValue.ToString("F1", CultureInfo.InvariantCulture)} {CxLang.T("PoweneticsMetricsManager_W19")}";
                }
            }
        }

        public PmdMetricSet GetPmdMetricSetByIndexGroup(IList<PoweneticsChannel[]> channelData, int[] indexGroup)
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

            AllGpuPowerAvg = ZERO_WATT;
            AllGpuPowerCur = ZERO_WATT;
            AllGpuPowerMax = ZERO_WATT;
            AllPciExAvg = ZERO_WATT;
            AllPciExCur = ZERO_WATT;
            AllPciExMax = ZERO_WATT;
            PciExSlotAvg = ZERO_WATT;
            PciExSlotCur = ZERO_WATT;
            PciExSlotMax = ZERO_WATT;
            AllCpuPowerAvg = ZERO_WATT;
            AllCpuPowerCur = ZERO_WATT;
            AllCpuPowerMax = ZERO_WATT;
            AllAtxPowerAvg = ZERO_WATT;
            AllAtxPowerCur = ZERO_WATT;
            AllAtxPowerMax = ZERO_WATT;
            AllPowerAvg = ZERO_WATT; ;
            AllPowerCur = ZERO_WATT;
            AllPowerMax = ZERO_WATT;

            _allPowerMaxValue = float.MinValue;
            _allGpuPowerMaxValue = float.MinValue;
            _allPciExMaxValue = float.MinValue;
            _pciExSlotMaxValue = float.MinValue;
            _allCpuPowerMaxValue = float.MinValue;
            _allAtxPowerMaxValue = float.MinValue;
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
