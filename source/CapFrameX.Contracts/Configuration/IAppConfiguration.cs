using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Windows.Forms;

namespace CapFrameX.Contracts.Configuration
{
	public interface IAppConfiguration : IFrametimeStatisticProviderOptions, IPmdServiceConfiguration
	{
		/// <summary>
		/// Emits an events when a configuration entry has been changes
		/// </summary>
		// Example code to add in constructor
		/*
            _appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.*key*))
                .Select(x => (*type*)x.value)
                .Subscribe(value =>
                {
                    do stuff();

                });
		*/
		IObservable<(string key, object value)> OnValueChanged { get; }

		double StutteringFactor { get; set; }

		double StutteringThreshold { get; set; }

		string ObservedDirectory { get; set; }

		string ScreenshotDirectory { get; set; }

		bool UseSingleRecordMaxStatisticParameter { get; set; }

		bool UseSingleRecord99QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP95QuantileStatisticParameter { get; set; }

		bool UseSingleRecordAverageStatisticParameter { get; set; }

        bool UseSingleRecordGpuActiveAverageStatisticParameter { get; set; }

        bool UseSingleRecordMedianStatisticParameter { get; set; }

		bool UseSingleRecordP5QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP1QuantileStatisticParameter { get; set; }

        bool UseSingleRecordGpuActiveP1QuantileStatisticParameter { get; set; }

        bool UseSingleRecordP0Dot1QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot2QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP1LowAverageStatisticParameter { get; set; }

        bool UseSingleRecordGpuActiveP1LowAverageStatisticParameter { get; set; }

        bool UseSingleRecordP0Dot1LowAverageStatisticParameter { get; set; }

        bool UseSingleRecordP1LowIntegralStatisticParameter { get; set; }

        bool UseSingleRecordP0Dot1LowIntegralStatisticParameter { get; set; }

        bool UseSingleRecordMinStatisticParameter { get; set; }

		bool UseSingleRecordAdaptiveSTDStatisticParameter { get; set; }

		bool UseSingleRecordCpuFpsPerWattParameter { get; set; }

		bool UseSingleRecordGpuFpsPerWattParameter { get; set; }

		string CaptureHotKey { get; set; }

		string OverlayHotKey { get; set; }

		string OverlayConfigHotKey { get; set; }

		string ThreadAffinityHotkey { get; set; }

		string ResetMetricsHotkey { get; set; }

		bool UseThreadAffinity { get; set; }

		bool AutoDisableOverlay { get; set; }

		bool OSDCustomPosition { get; set; }

		int OSDPositionX { get; set; }

		int OSDPositionY { get; set; }

		bool ToggleGlobalRTSSOSD { get; set; }

		string HotkeySoundMode { get; set; }

		double CaptureTime { get; set; }

		bool UseGlobalCaptureTime { get; set; }

		double CaptureDelay { get; set; }

		double VoiceSoundLevel { get; set; }

		double SimpleSoundLevel { get; set; }

		string ComparisonFirstMetric { get; set; }

		string ComparisonSecondMetric { get; set; }

		string ComparisonThirdMetric { get; set; }

		string RunHistorySecondMetric { get; set; }

		string RunHistoryThirdMetric { get; set; }

		string ComparisonContext { get; set; }

		string SecondComparisonContext { get; set; }

		string RecordingListSortMemberPath { get; set; }

		string RecordingListSortDirection { get; set; }

		string SyncRangeLower { get; set; }

		string SyncRangeUpper { get; set; }

		bool ShowOutlierWarning { get; set; }

		string HardwareInfoSource { get; set; }

		string CustomCpuDescription { get; set; }

		string CustomGpuDescription { get; set; }

		string CustomRamDescription { get; set; }	
		
		bool IsOverlayActive { get; set; }

		string ResetHistoryHotkey { get; set; }

		bool UseRunHistory { get; set; }

		bool UseAggregation { get; set; }

		string OutlierHandling { get; set; }

		int SelectedHistoryRuns { get; set; }

		int OSDRefreshPeriod { get; set; }

		int MetricInterval { get; set; }


		string CloudDownloadDirectory { get; set; }
		
		bool SaveAggregationOnly { get; set; }

		int OutlierPercentageOverlay { get; set; }

		string RelatedMetricOverlay { get; set; }
		/// <summary>
		/// Set to disable RTSS output but keeping the overlay service up and running to pull values via webservice
		/// </summary>
		bool HideOverlay { get; set; }

		bool ShowSystemTimeSeconds { get; set; }

		int InputLagOffset { get; set; }

		string SecondMetricAggregation { get; set; }

		string ThirdMetricAggregation { get; set; }

		string RelatedMetricAggregation { get; set; }

		int OutlierPercentageAggregation { get; set; }

		bool AreThresholdsReversed { get; set; }

		string CaptureRootDirectory { get; set; }

		bool ShareProcessListEntries { get; set; }

		bool AutoUpdateProcessList { get; set; }

		bool UseSensorLogging { get; set; }
		/// <summary>
		/// Toggle between absolute and percentage values for threshold bar charts
		/// </summary>
		bool AreThresholdValuesAbsolute { get; set; }

		int OverlayEntryConfigurationFile { get; set; }

		int SensorLoggingRefreshPeriod { get; set; }

		bool ShowThresholdTimes { get; set; }

		string CaptureFileMode { get; set; }

		bool StartMinimized { get; set; }

		bool Autostart { get; set; }

		bool IsGpuAccelerationActive { get; set; }

		int HorizontalGraphExportRes { get; set; }

		int VerticalGraphExportRes { get; set; }

		bool ReportShowAverageRow { get; set; }

		IReportDataGridColumnSettings ReportDataGridColumnSettings { get; set; }

		string SensorReportEvaluationMethod { get; set; }

		bool UseDarkMode { get; set; }

		bool FixedExpanderPosition { get; set; }

		bool IsRecordInfoExpanded { get; set; }

		bool RecordListShowCreationTime { get; set; }

		bool RecordListShowComment { get; set; }

		bool RecordListShowIsAggregated { get; set; }

		bool RecordListShowCpuName { get; set; }

		bool RecordListShowGpuName { get; set; }

		bool RecordListShowRamName { get; set; }

		int[] RecordListHeaderOrder { get; set; }
		/// <summary>
		/// Time, when the app notification was last closed. When older than the timestamp of current squidex notification, a notification is shown again.
		/// </summary>
		DateTime LastAppNotificationTimestamp { get; set; }

		bool AppNotificationsActive { get; set; }

		string WebservicePort { get; set; }

		bool CaptureRTSSFrameTimes { get; set; }
		/// <summary>
		/// When available, use the simulated TBP value instead of the normal "GPU Total" value for analysis like FPS/W
		/// </summary>
		bool UseTBPSim { get; set; }

		bool UseAdlFallback { get; set; }

		string FirstMetricBarColor { get; set; }

		string SecondMetricBarColor { get; set; }

		string ThirdMetricBarColor { get; set; }

        string PingURL { get; set; }
    }

	public interface IReportDataGridColumnSettings
    {
		bool ReportShowCreationDate { get; set; }

		bool ReportShowCreationTime { get; set; }

		bool ReportShowNumberOfSamples { get; set; }

		bool ReportShowRecordTime { get; set; }

		bool ReportShowCpuName { get; set; }

		bool ReportShowGpuName { get; set; }

		bool ReportShowRamName { get; set; }

		bool ReportShowComment { get; set; }

		bool ReportShowMaxFPS { get; set; }

		bool ReportShowP99FPS { get; set; }

		bool ReportShowP95FS { get; set; }

		bool ReportShowMedianFPS { get; set; }

		bool ReportShowAverageFPS { get; set; }

        bool ReportShowGpuActiveAverageFPS { get; set; }

        bool ReportShowP5FPS { get; set; }

		bool ReportShowP1FPS { get; set; }

        bool ReportShowGpuActiveP1FPS { get; set; }

        bool ReportShowP0Dot2FPS { get; set; }

		bool ReportShowP0Dot1FPS { get; set; }

		bool ReportShowP1LowAverageFPS { get; set; }

        bool ReportShowGpuActiveP1LowAverageFPS { get; set; }

        bool ReportShowP0Dot1LowAverageFPS { get; set; }

        bool ReportShowP1LowIntegralFPS { get; set; }

        bool ReportShowP0Dot1LowIntegralFPS { get; set; }

        bool ReportShowMinFPS { get; set; }

		bool ReportShowAdaptiveSTD { get; set; }

		bool ReportShowCpuFpsPerWatt { get; set; }

		bool ReportShowGpuFpsPerWatt { get; set; }

        bool ReportShowAppLatency { get; set; }

        bool ReportShowGpuActiveTimeDeviation { get; set; }

        bool ReportShowCpuMaxUsage { get; set; }

		bool ReportShowCpuPower { get; set; }

		bool ReportShowCpuMaxClock { get; set; }

		bool ReportShowCpuTemp{ get; set; }

		bool ReportShowGpuUsage { get; set; }

		bool ReportShowGpuPower { get; set; }

		bool ReportShowGpuTBPSim { get; set; }

		bool ReportShowGpuClock { get; set; }

		bool ReportShowGpuTemp { get; set; }

		IReportDataGridColumnSettings Clone();
	}
}
