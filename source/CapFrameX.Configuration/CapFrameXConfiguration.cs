using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace CapFrameX.Configuration
{
    public class CapFrameXConfiguration : IAppConfiguration
    {
        private readonly ISettingsStorage _settingsStorage;
        private readonly ISubject<(string key, object value)> _onValueChanged = new Subject<(string key, object value)>();
        public IObservable<(string key, object value)> OnValueChanged => _onValueChanged.AsObservable();

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

        public DateTime LastAppNotificationTimestamp
        {
            get => Get<DateTime>(DateTime.MinValue);
            set => Set(value);
        }

        public bool AppNotificationsActive
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public string WebservicePort
        {
            get => Get<string>("1337");
            set => Set(value);
        }

        public bool UseTBPSim
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseDisplayChangeMetrics
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseAdlFallback
		{
			get => Get<bool>(false);
			set => Set(value);
		}


		public string PingURL
        {
            get => Get<string>("google.com");
            set => Set(value);
        }

        // Record List Settings

        public bool FixedExpanderPosition
        {
            get => Get<bool>(false);
            set => Set(value);
        }
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

        public bool IsRecordInfoExpanded
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public int[] RecordListHeaderOrder
        {
            get => Get<int[]>(Enumerable.Range(0, 7).ToArray());
            set => Set(value);
        }

        public bool RecordListShowCreationTime
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool RecordListShowComment
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool RecordListShowIsAggregated
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool RecordListShowCpuName
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool RecordListShowGpuName
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool RecordListShowRamName
        {
            get => Get<bool>(true);
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

        public bool UseGlobalCaptureTime
        {
            get => Get<bool>(true);
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

        public bool UseSingleRecordGpuActiveAverageStatisticParameter
        {
            get => Get<bool>(false);
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

        public bool UseSingleRecordGpuActiveP1QuantileStatisticParameter
        {
            get => Get<bool>(false);
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
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordGpuActiveP1LowAverageStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordP0Dot1LowAverageStatisticParameter
        {
            get => Get<bool>(true);
            set => Set(value);
        }

        public bool UseSingleRecordP1LowIntegralStatisticParameter
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool UseSingleRecordP0Dot1LowIntegralStatisticParameter
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

        public bool AreThresholdValuesAbsolute
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool AnalysisRangeSliderRealTime
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

		public string ThreadAffinityHotkey
		{
			get => Get<string>("Control+A");
			set => Set(value);
		}

        public string ResetMetricsHotkey
		{
			get => Get<string>("Alt+M");
			set => Set(value);
		}

		public int OSDRefreshPeriod
        {
            get => Get<int>(1000);
            set => Set(value);
        }

        public int MetricInterval
		{
			get => Get<int>(20);
			set => Set(value);
		}

		public bool AutoDisableOverlay
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseThreadAffinity
		{
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool OSDCustomPosition
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public int OSDPositionX
        {
            get => Get<int>(0);
            set => Set(value);
        }
        public int OSDPositionY
        {
            get => Get<int>(0);
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

        public bool HideOverlay
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public bool ShowSystemTimeSeconds
        {
            get => Get<bool>(false);
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

        public string FirstMetricBarColor
        {
            get => Get<string>("#2297F3");
            set => Set(value);
        }
        public string SecondMetricBarColor
        {
            get => Get<string>("#F17D20");
            set => Set(value);
        }
        public string ThirdMetricBarColor
        {
            get => Get<string>("#FFB400");
            set => Set(value);
        }
        public bool ComparisonRangeSliderRealTime
        {
            get => Get<bool>(false);
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

        public bool ReportUsePMDValues
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

        // RTSS Frametimes
        public bool CaptureRTSSFrameTimes
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        // PMD Service
        public bool UseVirtualMode
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        public int DownSamplingSize
        {
            get => Get<int>(10);
            set => Set(value);
        }

        public int PmdChartRefreshPeriod
        {
            get => Get<int>(100);
            set => Set(value);
        }

        public int PmdMetricRefreshPeriod
        {
            get => Get<int>(500);
            set => Set(value);
        }

        public string DownSamplingMode
        {
            get => Get<string>("Average");
            set => Set(value);
        }

        public int ChartDownSamplingSize
        {
            get => Get<int>(4);
            set => Set(value);
        }

        public bool UsePmdDataLogging
        {
            get => Get<bool>(false);
            set => Set(value);
        }

        // General Management
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
            _onValueChanged.OnNext((key, value));
        }

        public CapFrameXConfiguration(ILogger<CapFrameXConfiguration> logger, ISettingsStorage settingsStorage)
        {
            _settingsStorage = settingsStorage;
            try
            {
                _settingsStorage.Load().Wait();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error Loading Configuration");
                MessageBox.Show(ex.Message + Environment.NewLine + ex.InnerException?.Message
                    + "\n\n" + "Possible solution: delete AppSettings.json under MyDocuments\\CapFrameX\\Configuration" +
                    " and then restart CapFrameX.", "Error");
                throw;
            }
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
        public bool ReportShowGpuActiveTimeAverage { get; set; } = false;
        public bool ReportShowP5FPS { get; set; } = true;
        public bool ReportShowP1FPS { get; set; } = true;
        public bool ReportShowGpuActiveP1FPS { get; set; } = false;
        public bool ReportShowP0Dot2FPS { get; set; } = true;
        public bool ReportShowP0Dot1FPS { get; set; } = true;
        public bool ReportShowP1LowAverageFPS { get; set; } = true;
        public bool ReportShowGpuActiveP1LowAverageFPS { get; set; } = false;
        public bool ReportShowP0Dot1LowAverageFPS { get; set; } = true;
        public bool ReportShowP1LowIntegralFPS { get; set; } = true;
        public bool ReportShowP0Dot1LowIntegralFPS { get; set; } = true;
        public bool ReportShowMinFPS { get; set; } = true;
        public bool ReportShowAdaptiveSTD { get; set; } = true;
        public bool ReportShowCpuFpsPerWatt { get; set; } = true;
        public bool ReportShowGpuFpsPerWatt { get; set; } = true;
        public bool ReportShowAppLatency { get; set; } = false;
        public bool ReportShowGpuActiveTimeDeviation { get; set; } = false;
        public bool ReportShowCpuMaxUsage { get; set; } = false;
        public bool ReportShowCpuPower { get; set; } = false;
        public bool ReportShowCpuMaxClock { get; set; } = false;
        public bool ReportShowCpuTemp { get; set; } = false;
        public bool ReportShowGpuUsage { get; set; } = false;
        public bool ReportShowGpuPower { get; set; } = false;
        public bool ReportShowGpuTBPSim { get; set; } = false;
        public bool ReportShowGpuClock { get; set; } = false;
        public bool ReportShowGpuTemp { get; set; } = false;

        public IReportDataGridColumnSettings Clone()
        {
            return (ReportDataGridColumnSettings)MemberwiseClone();
        }
    }
}
