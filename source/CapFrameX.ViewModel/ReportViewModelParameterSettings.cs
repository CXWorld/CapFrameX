using CapFrameX.Contracts.Configuration;
using System;
using System.Collections.Generic;

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

        public bool ShowP1LowFPS
        {
            get { return _settings.ReportShowP1LowFPS; }
            set
            {
                _settings.ReportShowP1LowFPS = value;
                _appConfiguration.ReportDataGridColumnSettings = _settings;

                RaisePropertyChanged();
            }
        }

        public bool ShowP0Dot1LowFPS
        {
            get { return _settings.ReportShowP0Dot1LowFPS; }
            set
            {
                _settings.ReportShowP0Dot1LowFPS = value;
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
    }
}
