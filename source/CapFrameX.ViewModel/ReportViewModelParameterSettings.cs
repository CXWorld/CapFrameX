using System;
using System.Collections.Generic;

namespace CapFrameX.ViewModel
{
    public partial class ReportViewModel
    {
        private bool _showCreationDate;
        private bool _showCreationTime;
        private bool _showNumberOfSamples;
        private bool _showRecordTime;
        private bool _showCpuName;
        private bool _showGpuName;
        private bool _showRamName;
        private bool _showComment;
        private bool _showMaxFPS;
        private bool _showP99FPS;
        private bool _showP95FS;
        private bool _showMedianFPS;
        private bool _showAverageFPS;
        private bool _showP5FPS;
        private bool _showP1FPS;
        private bool _showP0Dot2FPS;
        private bool _showP0Dot1FPS;
        private bool _showP1LowFPS;
        private bool _showP0Dot1LowFPS;
        private bool _showMinFPS;
        private bool _showAdaptiveSTD;
        private bool _showCpuFpsPerWatt;
        private bool _showGpuFpsPerWatt;

        partial void InitializeReportParameters()
        {
            ShowCreationDate = _appConfiguration.ReportShowCreationDate;
            ShowCreationTime = _appConfiguration.ReportShowCreationTime;
            ShowNumberOfSamples = _appConfiguration.ReportShowNumberOfSamples;
            ShowRecordTime = _appConfiguration.ReportShowRecordTime;
            ShowCpuName = _appConfiguration.ReportShowCpuName;
            ShowGpuName = _appConfiguration.ReportShowGpuName;
            ShowRamName = _appConfiguration.ReportShowRamName;
            ShowComment = _appConfiguration.ReportShowComment;
            ShowMaxFPS = _appConfiguration.ReportShowMaxFPS;
            ShowP99FPS = _appConfiguration.ReportShowP99FPS;
            ShowP95FS = _appConfiguration.ReportShowP95FS;
            ShowMedianFPS = _appConfiguration.ReportShowMedianFPS;
            ShowAverageFPS = _appConfiguration.ReportShowAverageFPS;
            ShowP5FPS = _appConfiguration.ReportShowP5FPS;
            ShowP1FPS = _appConfiguration.ReportShowP1FPS;
            ShowP0Dot2FPS = _appConfiguration.ReportShowP0Dot2FPS;
            ShowP0Dot1FPS = _appConfiguration.ReportShowP0Dot1FPS;
            ShowP1LowFPS = _appConfiguration.ReportShowP1LowFPS;
            ShowP0Dot1LowFPS = _appConfiguration.ReportShowP0Dot1LowFPS;
            ShowMinFPS = _appConfiguration.ReportShowMinFPS;
            ShowAdaptiveSTD = _appConfiguration.ReportShowAdaptiveSTD;
            ShowCpuFpsPerWatt = _appConfiguration.ReportShowCpuFpsPerWatt;
            ShowGpuFpsPerWatt = _appConfiguration.ReportShowGpuFpsPerWatt;
        }

        public bool ShowCreationDate
        {
            get { return _showCreationDate; }
            set
            {
                _showCreationDate = value;
                _appConfiguration.ReportShowCreationDate = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowCreationTime
        {
            get { return _showCreationTime; }
            set
            {
                _showCreationTime = value;
                _appConfiguration.ReportShowCreationTime = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowNumberOfSamples
        {
            get { return _showNumberOfSamples; }
            set
            {
                _showNumberOfSamples = value;
                _appConfiguration.ReportShowNumberOfSamples = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowRecordTime
        {
            get { return _showRecordTime; }
            set
            {
                _showRecordTime = value;
                _appConfiguration.ReportShowRecordTime = value;
                RaisePropertyChanged();
            }
        }
        public bool ShowCpuName
        {
            get { return _showCpuName; }
            set
            {
                _showCpuName = value;
                _appConfiguration.ReportShowCpuName = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowGpuName
        {
            get { return _showGpuName; }
            set
            {
                _showGpuName = value;
                _appConfiguration.ReportShowGpuName = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowRamName
        {
            get { return _showRamName; }
            set
            {
                _showRamName = value;
                _appConfiguration.ReportShowRamName = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowComment
        {
            get { return _showComment; }
            set
            {
                _showComment = value;
                _appConfiguration.ReportShowComment = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowMaxFPS
        {
            get { return _showMaxFPS; }
            set
            {
                _showMaxFPS = value;
                _appConfiguration.ReportShowMaxFPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP99FPS
        {
            get { return _showP99FPS; }
            set
            {
                _showP99FPS = value;
                _appConfiguration.ReportShowP99FPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP95FS
        {
            get { return _showP95FS; }
            set
            {
                _showP95FS = value;
                _appConfiguration.ReportShowP95FS = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowMedianFPS
        {
            get { return _showMedianFPS; }
            set
            {
                _showMedianFPS = value;
                _appConfiguration.ReportShowMedianFPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowAverageFPS
        {
            get { return _showAverageFPS; }
            set
            {
                _showAverageFPS = value;
                _appConfiguration.ReportShowAverageFPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP5FPS
        {
            get { return _showP5FPS; }
            set
            {
                _showP5FPS = value;
                _appConfiguration.ReportShowP5FPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP1FPS
        {
            get { return _showP1FPS; }
            set
            {
                _showP1FPS = value;
                _appConfiguration.ReportShowP1FPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP0Dot2FPS
        {
            get { return _showP0Dot2FPS; }
            set
            {
                _showP0Dot2FPS = value;
                _appConfiguration.ReportShowP0Dot2FPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP0Dot1FPS
        {
            get { return _showP0Dot1FPS; }
            set
            {
                _showP0Dot1FPS = value;
                _appConfiguration.ReportShowP0Dot1FPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP1LowFPS
        {
            get { return _showP1LowFPS; }
            set
            {
                _showP1LowFPS = value;
                _appConfiguration.ReportShowP1LowFPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowP0Dot1LowFPS
        {
            get { return _showP0Dot1LowFPS; }
            set
            {
                _showP0Dot1LowFPS = value;
                _appConfiguration.ReportShowP0Dot1LowFPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowMinFPS
        {
            get { return _showMinFPS; }
            set
            {
                _showMinFPS = value;
                _appConfiguration.ReportShowMinFPS = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowAdaptiveSTD
        {
            get { return _showAdaptiveSTD; }
            set
            {
                _showAdaptiveSTD = value;
                _appConfiguration.ReportShowAdaptiveSTD = value;

                RaisePropertyChanged();
            }
        }

        public bool ShowCpuFpsPerWatt
        {
            get { return _showCpuFpsPerWatt; }
            set
            {
                _showCpuFpsPerWatt = value;
                _appConfiguration.ReportShowCpuFpsPerWatt = value;

                RaisePropertyChanged();
            }
        }
        public bool ShowGpuFpsPerWatt
        {
            get { return _showGpuFpsPerWatt; }
            set
            {
                _showGpuFpsPerWatt = value;
                _appConfiguration.ReportShowGpuFpsPerWatt = value;

                RaisePropertyChanged();
            }
        }
    }
}
