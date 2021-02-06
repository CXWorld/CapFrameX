using CapFrameX.Statistics.NetStandard.Contracts;
using System.Windows.Forms;

namespace CapFrameX.Contracts.Configuration
{
	public interface IAppConfiguration : IFrametimeStatisticProviderOptions
	{
		double StutteringFactor { get; set; }

		double StutteringThreshold { get; set; }

		string ObservedDirectory { get; set; }

		string ScreenshotDirectory { get; set; }

		bool UseSingleRecordMaxStatisticParameter { get; set; }

		bool UseSingleRecord99QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP95QuantileStatisticParameter { get; set; }

		bool UseSingleRecordAverageStatisticParameter { get; set; }

		bool UseSingleRecordMedianStatisticParameter { get; set; }

		bool UseSingleRecordP5QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP1QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot1QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot2QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP1LowAverageStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot1LowAverageStatisticParameter { get; set; }

		bool UseSingleRecordMinStatisticParameter { get; set; }

		bool UseSingleRecordAdaptiveSTDStatisticParameter { get; set; }

		bool UseSingleRecordCpuFpsPerWattParameter { get; set; }

		bool UseSingleRecordGpuFpsPerWattParameter { get; set; }

		string CaptureHotKey { get; set; }

		string OverlayHotKey { get; set; }

		string OverlayConfigHotKey { get; set; }

		bool AutoDisableOverlay { get; set; }

		bool ToggleGlobalRTSSOSD { get; set; }

		string HotkeySoundMode { get; set; }

		double CaptureTime { get; set; }

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
		
		string CloudDownloadDirectory { get; set; }
		
		bool SaveAggregationOnly { get; set; }

		int OutlierPercentageOverlay { get; set; }

		string RelatedMetricOverlay { get; set; }

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
		
		bool AreThresholdsPercentage { get; set; }

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

		bool ReportShowResolution { get; set; }

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

		bool ReportShowP5FPS { get; set; }

		bool ReportShowP1FPS { get; set; }

		bool ReportShowP0Dot2FPS { get; set; }

		bool ReportShowP0Dot1FPS { get; set; }

		bool ReportShowP1LowFPS { get; set; }

		bool ReportShowP0Dot1LowFPS { get; set; }

		bool ReportShowMinFPS { get; set; }

		bool ReportShowAdaptiveSTD { get; set; }

		bool ReportShowCpuFpsPerWatt { get; set; }

		bool ReportShowGpuFpsPerWatt { get; set; }
	}
}
