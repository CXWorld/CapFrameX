using CapFrameX.Contracts.Configuration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CapFrameX.Configuration
{
    public class CapFrameXConfiguration : IAppConfiguration
    {
        private readonly ISettingsStorage _settingsStorage;

        // Directories
        public string ObservedDirectory
        {
            get => Get<string>(@"MyDocuments\CapFrameX\Captures");
            set => Set(value);
        }

        public string CaptureRootDirectory
        {
            get => Get<string>(@"MyDocuments\CapFrameX\Captures");
            set => Set(value);
        }

        public string ScreenshotDirectory
        {
            get => Get<string>(@"MyDocuments\CapFrameX\Screenshots");
            set => Set(value);
        }

        public string CloudDownloadDirectory
        {
            get => Get<string>(@"MyDocuments\CapFrameX\Captures\Cloud");
            set => Set(value);
        }


        // General Settings
        public int MovingAverageWindowSize
        {
            get => Get<int>(100);
            set => Set(value);
        }

        public int IntervalAverageWindowTime
        {
            get => Get<int>(500);
            set => Set(value);
        }

        public int FpsValuesRoundingDigits
        {
            get => Get<int>(1);
            set => Set(value);
        }

        public string HardwareInfoSource
        {
            get => Get<string>("Auto");
            set => Set(value);
        }

        public string CustomCpuDescription
        {
            get => Get<string>("CPU");
            set => Set(value);
        }

        public string CustomGpuDescription
        {
            get => Get<string>("GPU");
            set => Set(value);
        }

        public string CustomRamDescription
        {
            get => Get<string>("RAM");
            set => Set(value);
        }

        public bool StartMinimized
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool Autostart
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool IsGpuAccelerationActive
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public int HorizontalGraphExportRes
        {
            get => Get<int>(1200);
            set => Set(value);
        }

        public int VerticalGraphExportRes
        {
            get => Get<int>(600);
            set => Set(value);
        }

        public bool UseDarkMode
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        // Record List Settings
        public string RecordingListSortMemberPath
        {
            get => Get<string>("GameName");
            set => Set(value);
        }

        public string RecordingListSortDirection
        {
            get => Get<string>("Ascending");
            set => Set(value);
        }


        // Capture Settings
        public string CaptureHotKey
        {
            get => Get<string>("F11");
            set => Set(value);
        }

        public double CaptureTime
        {
            get => Get<double>(20d);
            set => Set(value);
        }

        public double CaptureDelay
        {
            get => Get<double>(0d);
            set => Set(value);
        }

        public string HotkeySoundMode
        {
            get => Get<string>("Voice");
            set => Set(value);
        }

        public double VoiceSoundLevel
        {
            get => Get<double>(0.25);
            set => Set(value);
        }

        public double SimpleSoundLevel
        {
            get => Get<double>(0.25);
            set => Set(value);
        }

        public string CaptureFileMode
        {
            get => Get<string>("Json");
            set => Set(value);
        }


        // Analysis Settings
        public double StutteringFactor
        {
            get => Get<double>(2.5);
            set => Set(value);
        }

        public double StutteringThreshold
        {
            get => Get<double>(25d);
            set => Set(value);
        }

        public bool UseSingleRecordMaxStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecord99QuantileStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordP95QuantileStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordAverageStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordMedianStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordP5QuantileStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordP1QuantileStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordP0Dot1QuantileStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordP0Dot2QuantileStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordP1LowAverageStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordP0Dot1LowAverageStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordMinStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordAdaptiveSTDStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordCpuFpsPerWattParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordGpuFpsPerWattParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool ShowOutlierWarning
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool ShowThresholdTimes
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool AreThresholdsReversed
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool AreThresholdsPercentage
        {
            get => Get<bool>(false);
            set => Set(value);
        }


        // Aggregation Settings
        public string SecondMetricAggregation
        {
            get => Get<string>("P1");
            set => Set(value);
        }

        public string ThirdMetricAggregation
        {
            get => Get<string>("P0dot2");
            set => Set(value);
        }

        public string RelatedMetricAggregation
        {
            get => Get<string>("Second");
            set => Set(value);
        }

        public int OutlierPercentageAggregation
        {
            get => Get<int>(3);
            set => Set(value);
        }


        // Overlay Settings
        public int OverlayEntryConfigurationFile
        {
            get => Get<int>(0);
            set => Set(value);
        }

        public string OverlayHotKey
        {
            get => Get<string>("Alt+O");
            set => Set(value);
        }

        public string OverlayConfigHotKey
        {
            get => Get<string>("Alt+C");
            set => Set(value);
        }

        public int OSDRefreshPeriod
        {
            get => Get<int>(1000);
            set => Set(value);
        }

        public bool AutoDisableOverlay
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool ToggleGlobalRTSSOSD
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseRunHistory
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public string ResetHistoryHotkey
        {
            get => Get<string>("Alt+R");
            set => Set(value);
        }

        public string RunHistorySecondMetric
        {
            get => Get<string>("P1");
            set => Set(value);
        }

        public string RunHistoryThirdMetric
        {
            get => Get<string>("P0dot2");
            set => Set(value);
        }

        public int SelectedHistoryRuns
        {
            get => Get<int>(3);
            set => Set(value);
        }

        public bool IsOverlayActive
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseAggregation
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool SaveAggregationOnly
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public string OutlierHandling
        {
            get => Get<string>("Replace");
            set => Set(value);
        }

        public string RelatedMetricOverlay
        {
            get => Get<string>("Second");
            set => Set(value);
        }

        public int OutlierPercentageOverlay
        {
            get => Get<int>(3);
            set => Set(value);
        }


        // Comparison Settings
        public string ComparisonFirstMetric
        {
            get => Get<string>("Average");
            set => Set(value);
        }

        public string ComparisonSecondMetric
        {
            get => Get<string>("P1");
            set => Set(value);
        }

        public string ComparisonThirdMetric
        {
            get => Get<string>("P0dot2");
            set => Set(value);
        }
        public string ComparisonContext
        {
            get => Get<string>("CPU");
            set => Set(value);
        }
        public string SecondComparisonContext
        {
            get => Get<string>("GPU");
            set => Set(value);
        }


        // Sensor Settings
        public bool UseSensorLogging
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public int SensorLoggingRefreshPeriod
        {
            get => Get<int>(250);
            set => Set(value);
        }

        public string SensorReportEvaluationMethod
        {
            get => Get<string>("Aggregate");
            set => Set(value);
        }


        // Report Settings
        public bool ReportShowAverageRow
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public IReportDataGridColumnSettings ReportDataGridColumnSettings
        {
            get => Get<ReportDataGridColumnSettings>(new ReportDataGridColumnSettings());
            set => Set(value.Clone());
        }


        // Sync Settings
        public int InputLagOffset
        {
            get => Get<int>(10);
            set => Set(value);
        }

        public string SyncRangeLower
        {
            get => Get<string>(40.ToString());
            set => Set(value);
        }

        public string SyncRangeUpper
        {
            get => Get<string>(120.ToString());
            set => Set(value);
        }


        // Cloud Settings
        public bool ShareProcessListEntries
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool AutoUpdateProcessList
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        T Get<T>(T defaultValue, [CallerMemberName] string key = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(key);
            }

            try
            {
                return _settingsStorage.GetValue<T>(key);
            }
            catch (KeyNotFoundException)
            {
                Set(defaultValue, key);
                return defaultValue;
            }
        }

        void Set<T>(T value, [CallerMemberName] string key = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(key);
            }
            _settingsStorage.SetValue(key, value);
        }

        public CapFrameXConfiguration(ISettingsStorage settingsStorage)
        {
            _settingsStorage = settingsStorage;
            _settingsStorage.Load().Wait();
        }
    }

    public class ReportDataGridColumnSettings : IReportDataGridColumnSettings
    {
        public bool ReportShowCreationDate { get; set; } = true;
        public bool ReportShowCreationTime { get; set; } = true;
        public bool ReportShowNumberOfSamples { get; set; }
        public bool ReportShowRecordTime { get; set; }
        public bool ReportShowCpuName { get; set; }
        public bool ReportShowGpuName { get; set; }
        public bool ReportShowRamName { get; set; }
        public bool ReportShowComment { get; set; } = true;
        public bool ReportShowMaxFPS { get; set; } = true;
        public bool ReportShowP99FPS { get; set; } = true;
        public bool ReportShowP95FS { get; set; } = true;
        public bool ReportShowMedianFPS { get; set; } = true;
        public bool ReportShowAverageFPS { get; set; } = true;
        public bool ReportShowP5FPS { get; set; } = true;
        public bool ReportShowP1FPS { get; set; } = true;
        public bool ReportShowP0Dot2FPS { get; set; } = true;
        public bool ReportShowP0Dot1FPS { get; set; } = true;
        public bool ReportShowP1LowFPS { get; set; } = true;
        public bool ReportShowP0Dot1LowFPS { get; set; } = true;
        public bool ReportShowMinFPS { get; set; } = true;
        public bool ReportShowAdaptiveSTD { get; set; } = true;
        public bool ReportShowCpuFpsPerWatt { get; set; } = true;
        public bool ReportShowGpuFpsPerWatt { get; set; } = true;

        public IReportDataGridColumnSettings Clone()
        {
            return (ReportDataGridColumnSettings)this.MemberwiseClone();
        }
    }
}
