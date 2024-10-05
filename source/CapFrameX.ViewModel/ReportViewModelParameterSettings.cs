using CapFrameX.Contracts.Configuration;

namespace CapFrameX.ViewModel
{
    public partial class ReportViewModel
    {

        private IReportDataGridColumnSettings _settings;

        partial void InitializeReportParameters()
        {
            _settings = _appConfiguration.ReportDataGridColumnSettings;
        }

        public bool ShowCreationDate
        {
            get { return _settings.ReportShowCreationDate; }
            set
            {
                _settings.ReportShowCreationDate = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;
                RaisePropertyChanged();
            }
        }
        public bool ShowCreationTime
        {
            get { return _settings.ReportShowCreationTime; }
            set
            {
                _settings.ReportShowCreationTime = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowNumberOfSamples
        {
            get { return _settings.ReportShowNumberOfSamples; }
            set
            {
                _settings.ReportShowNumberOfSamples = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowRecordTime
        {
            get { return _settings.ReportShowRecordTime; }
            set
            {
                _settings.ReportShowRecordTime = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;
                RaisePropertyChanged();
            }
        }
        public bool ShowCpuName
        {
            get { return _settings.ReportShowCpuName; }
            set
            {
                _settings.ReportShowCpuName = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowGpuName
        {
            get { return _settings.ReportShowGpuName; }
            set
            {
                _settings.ReportShowGpuName = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowRamName
        {
            get { return _settings.ReportShowRamName; }
            set
            {
                _settings.ReportShowRamName = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowComment
        {
            get { return _settings.ReportShowComment; }
            set
            {
                _settings.ReportShowComment = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowMaxFPS
        {
            get { return _settings.ReportShowMaxFPS; }
            set
            {
                _settings.ReportShowMaxFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP99FPS
        {
            get { return _settings.ReportShowP99FPS; }
            set
            {
                _settings.ReportShowP99FPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP95FS
        {
            get { return _settings.ReportShowP95FS; }
            set
            {
                _settings.ReportShowP95FS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowMedianFPS
        {
            get { return _settings.ReportShowMedianFPS; }
            set
            {
                _settings.ReportShowMedianFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowAverageFPS
        {
            get { return _settings.ReportShowAverageFPS; }
            set
            {
                _settings.ReportShowAverageFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuActiveTimeAverage
        {
            get { return _settings.ReportShowGpuActiveTimeAverage; }
            set
            {
                _settings.ReportShowGpuActiveTimeAverage = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP5FPS
        {
            get { return _settings.ReportShowP5FPS; }
            set
            {
                _settings.ReportShowP5FPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP1FPS
        {
            get { return _settings.ReportShowP1FPS; }
            set
            {
                _settings.ReportShowP1FPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }


        public bool ShowP0Dot2FPS
        {
            get { return _settings.ReportShowP0Dot2FPS; }
            set
            {
                _settings.ReportShowP0Dot2FPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP0Dot1FPS
        {
            get { return _settings.ReportShowP0Dot1FPS; }
            set
            {
                _settings.ReportShowP0Dot1FPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP1LowAverageFPS
        {
            get { return _settings.ReportShowP1LowAverageFPS; }
            set
            {
                _settings.ReportShowP1LowAverageFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }


        public bool ShowP0Dot1LowAverageFPS
        {
            get { return _settings.ReportShowP0Dot1LowAverageFPS; }
            set
            {
                _settings.ReportShowP0Dot1LowAverageFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;
                RaisePropertyChanged();
            }
        }

        public bool ShowP1LowIntegralFPS
        {
            get { return _settings.ReportShowP1LowIntegralFPS; }
            set
            {
                _settings.ReportShowP1LowIntegralFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP0Dot1LowIntegralFPS
        {
            get { return _settings.ReportShowP0Dot1LowIntegralFPS; }
            set
            {
                _settings.ReportShowP0Dot1LowIntegralFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;
                RaisePropertyChanged();
            }
        }

        public bool ShowMinFPS
        {
            get { return _settings.ReportShowMinFPS; }
            set
            {
                _settings.ReportShowMinFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowAdaptiveSTD
        {
            get { return _settings.ReportShowAdaptiveSTD; }
            set
            {
                _settings.ReportShowAdaptiveSTD = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowCpuFpsPerWatt
        {
            get { return _settings.ReportShowCpuFpsPerWatt; }
            set
            {
                _settings.ReportShowCpuFpsPerWatt = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuFpsPerWatt
        {
            get { return _settings.ReportShowGpuFpsPerWatt; }
            set
            {
                _settings.ReportShowGpuFpsPerWatt = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowAppLatency
        {
            get { /*return _settings.ReportShowAppLatency;*/ return false; }
            set
            {
                _settings.ReportShowAppLatency = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuActiveTimeDeviation
        {
            get { return _settings.ReportShowGpuActiveTimeDeviation; }
            set
            {
                _settings.ReportShowGpuActiveTimeDeviation = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowCpuMaxUsage
        {
            get { return _settings.ReportShowCpuMaxUsage; }
            set
            {
                _settings.ReportShowCpuMaxUsage = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowCpuPower
        {
            get { return _settings.ReportShowCpuPower; }
            set
            {
                _settings.ReportShowCpuPower = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowCpuMaxClock
        {
            get { return _settings.ReportShowCpuMaxClock; }
            set
            {
                _settings.ReportShowCpuMaxClock = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowCpuTemp
        {
            get { return _settings.ReportShowCpuTemp; }
            set
            {
                _settings.ReportShowCpuTemp = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
        public bool ShowGpuUsage
        {
            get { return _settings.ReportShowGpuUsage; }
            set
            {
                _settings.ReportShowGpuUsage = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuPower
        {
            get { return _settings.ReportShowGpuPower; }
            set
            {
                _settings.ReportShowGpuPower = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuTBPSim
        {
            get { return _settings.ReportShowGpuTBPSim; }
            set
            {
                _settings.ReportShowGpuTBPSim = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuClock
        {
            get { return _settings.ReportShowGpuClock; }
            set
            {
                _settings.ReportShowGpuClock = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowGpuTemp
        {
            get { return _settings.ReportShowGpuTemp; }
            set
            {
                _settings.ReportShowGpuTemp = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }
    }
}
