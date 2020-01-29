using CapFrameX.Contracts.Configuration;

namespace CapFrameX.Configuration
{
	public class CapFrameXConfiguration : IAppConfiguration
	{
		private static Properties.Settings Settings => Properties.Settings.Default;

		public int MovingAverageWindowSize
		{
			get { return Settings.MovingAverageWindowSize; }
			set { Settings.MovingAverageWindowSize = value; Settings.Save(); }
		}

		public double StutteringFactor
		{
			get { return Settings.StutteringFactor; }
			set { Settings.StutteringFactor = value; Settings.Save(); }
		}

		public string ObservedDirectory
		{
			get { return Settings.ObservedDirectory; }
			set { Settings.ObservedDirectory = value; Settings.Save(); }
		}

		public string ScreenshotDirectory
		{
			get { return Settings.ScreenshotDirectory; }
			set { Settings.ScreenshotDirectory = value; Settings.Save(); }
		}

		public string LoggingDirectory
		{
			get { return Settings.LoggingDirectory; }
			set { Settings.LoggingDirectory = value; Settings.Save(); }
		}

		public int FpsValuesRoundingDigits
		{
			get { return Settings.FpsValuesRoundingDigits; }
			set { Settings.FpsValuesRoundingDigits = value; Settings.Save(); }
		}

		public bool UseSingleRecordMaxStatisticParameter
		{
			get { return Settings.UseSingleRecordMaxStatisticParameter; }
			set { Settings.UseSingleRecordMaxStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecord99QuantileStatisticParameter
		{
			get { return Settings.UseSingleRecordP99QuantileStatisticParameter; }
			set { Settings.UseSingleRecordP99QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordP95QuantileStatisticParameter
		{
			get { return Settings.UseSingleRecordP95QuantileStatisticParameter; }
			set { Settings.UseSingleRecordP95QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordAverageStatisticParameter
		{
			get { return Settings.UseSingleRecordAverageStatisticParameter; }
			set { Settings.UseSingleRecordAverageStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordP5QuantileStatisticParameter
		{
			get { return Settings.UseSingleRecordP5QuantileStatisticParameter; }
			set { Settings.UseSingleRecordP5QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordP1QuantileStatisticParameter
		{
			get { return Settings.UseSingleRecordP1QuantileStatisticParameter; }
			set { Settings.UseSingleRecordP1QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordP0Dot1QuantileStatisticParameter
		{
			get { return Settings.UseSingleRecordP0Dot1QuantileStatisticParameter; }
			set { Settings.UseSingleRecordP0Dot1QuantileStatisticParameter = value; Settings.Save(); }
		}

        public bool UseSingleRecordP0Dot2QuantileStatisticParameter
        {
            get { return Settings.UseSingleRecordP0Dot2QuantileStatisticParameter; }
            set { Settings.UseSingleRecordP0Dot2QuantileStatisticParameter = value; Settings.Save(); }
        }

        public bool UseSingleRecordP1LowAverageStatisticParameter
		{
			get { return Settings.UseSingleRecordP1LowAverageStatisticParameter; }
			set { Settings.UseSingleRecordP1LowAverageStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordP0Dot1LowAverageStatisticParameter
		{
			get { return Settings.UseSingleRecordP0Dot1LowAverageStatisticParameter; }
			set { Settings.UseSingleRecordP0Dot1LowAverageStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordMinStatisticParameter
		{
			get { return Settings.UseSingleRecordMinStatisticParameter; }
			set { Settings.UseSingleRecordMinStatisticParameter = value; Settings.Save(); }
		}

		public bool UseSingleRecordAdaptiveSTDStatisticParameter
		{
			get { return Settings.UseSingleRecordAdaptiveSTDStatisticParameter; }
			set { Settings.UseSingleRecordAdaptiveSTDStatisticParameter = value; Settings.Save(); }
		}

        public string CaptureHotKey
        {
            get { return Settings.CaptureHotKey; }
            set { Settings.CaptureHotKey = value; Settings.Save(); }
        }

		public string OverlayHotKey
		{
			get { return Settings.OverlayHotKey; }
			set { Settings.OverlayHotKey = value; Settings.Save(); }
		}

		public string HotkeySoundMode
        {
            get { return Settings.HotkeySoundMode; }
            set { Settings.HotkeySoundMode = value; Settings.Save(); }
        }

        public int CaptureTime
        {
            get { return Settings.CaptureTime; }
            set { Settings.CaptureTime = value; Settings.Save(); }
        }

        public double VoiceSoundLevel
        {
            get { return Settings.VoiceSoundLevel; }
            set { Settings.VoiceSoundLevel = value; Settings.Save(); }
        }

        public double SimpleSoundLevel
        {
            get { return Settings.SimpleSoundLevel; }
            set { Settings.SimpleSoundLevel = value; Settings.Save(); }
        }

		public string SecondMetric 
		{
			get { return Settings.SecondMetric; }
			set { Settings.SecondMetric = value; Settings.Save(); }
		}

		public string ThirdMetric
		{
			get { return Settings.ThirdMetric; }
			set { Settings.ThirdMetric = value; Settings.Save(); }
		}

		public string SecondMetricOverlay
		{
			get { return Settings.SecondMetricOverlay; }
			set { Settings.SecondMetricOverlay = value; Settings.Save(); }
		}

		public string ThirdMetricOverlay
		{
			get { return Settings.ThirdMetricOverlay; }
			set { Settings.ThirdMetricOverlay = value; Settings.Save(); }
		}

		public int SelectedHistoryRuns
		{
			get { return Settings.SelectedHistoryRuns; }
			set { Settings.SelectedHistoryRuns = value; Settings.Save(); }
		}

		public string OutlierHandling
		{
			get { return Settings.OutlierHandling; }
			set { Settings.OutlierHandling = value; Settings.Save(); }
		}

		public int OSDRefreshPeriod
		{
			get { return Settings.OSDRefreshPeriod; }
			set { Settings.OSDRefreshPeriod = value; Settings.Save(); }
		}

		public string ComparisonContext
		{
			get { return Settings.ComparisonContext; }
			set { Settings.ComparisonContext = value; Settings.Save(); }
		}

		public string RecordingListSortMemberPath
		{
			get { return Settings.RecordingListSortMemberPath; }
			set { Settings.RecordingListSortMemberPath = value; Settings.Save(); }
		}

		public string RecordingListSortDirection
		{
			get { return Settings.RecordingListSortDirection; }
			set { Settings.RecordingListSortDirection = value; Settings.Save(); }
		}

		public string SyncRangeLower
		{
			get { return Settings.SyncRangeLower; }
			set { Settings.SyncRangeLower = value; Settings.Save(); }
		}

		public string SyncRangeUpper
		{
			get { return Settings.SyncRangeUpper; }
			set { Settings.SyncRangeUpper = value; Settings.Save(); }
		}

		public bool ShowOutlierWarning
		{
			get { return Settings.ShowOutlierWarning; }
			set { Settings.ShowOutlierWarning = value; Settings.Save(); }
		}

		public string HardwareInfoSource
		{
			get { return Settings.HardwareInfoSource; }
			set { Settings.HardwareInfoSource = value; Settings.Save(); }
		}

		public string CustomCpuDescription
		{
			get { return Settings.CustomCpuDescription; }
			set { Settings.CustomCpuDescription = value; Settings.Save(); }
		}

		public string CustomGpuDescription
		{
			get { return Settings.CustomGpuDescription; }
			set { Settings.CustomGpuDescription = value; Settings.Save(); }
		}

		public string CustomRamDescription
		{
			get { return Settings.CustomRamDescription; }
			set { Settings.CustomRamDescription = value; Settings.Save(); }
		}

		public bool IsOverlayActive
		{
			get { return Settings.IsOverlayActive; }
			set { Settings.IsOverlayActive = value; Settings.Save(); }
		}

		public string ResetHistoryHotkey
		{
			get { return Settings.ResetHistoryHotkey; }
			set { Settings.ResetHistoryHotkey = value; Settings.Save(); }
		}

		public bool UseRunHistory
		{
			get { return Settings.UseRunHistory; }
			set { Settings.UseRunHistory = value; Settings.Save(); }
		}

		public bool UseAggregation
		{
			get { return Settings.UseAggregation; }
			set { Settings.UseAggregation = value; Settings.Save(); }
		}

		public bool SaveAggregationOnly
		{
			get { return Settings.SaveAggregationOnly; }
			set { Settings.SaveAggregationOnly = value; Settings.Save(); }
		}

		public int OutlierPercentageOverlay
		{
			get { return Settings.OutlierPercentageOverlay; }
			set { Settings.OutlierPercentageOverlay = value; Settings.Save(); }
		}

		public string RelatedMetricOverlay
		{
			get { return Settings.RelatedMetricOverlay; }
			set { Settings.RelatedMetricOverlay = value; Settings.Save(); }
		}

		public int InputLagOffset
		{
			get { return Settings.InputLagOffset; }
			set { Settings.InputLagOffset = value; Settings.Save(); }
		}

		public string SecondMetricAggregation
		{
			get { return Settings.SecondMetricAggregation; }
			set { Settings.SecondMetricAggregation = value; Settings.Save(); }
		}

		public string ThirdMetricAggregation
		{
			get { return Settings.ThirdMetricAggregation; }
			set { Settings.ThirdMetricAggregation = value; Settings.Save(); }
		}
		public string RelatedMetricAggregation
		{
			get { return Settings.RelatedMetricAggregation; }
			set { Settings.RelatedMetricAggregation = value; Settings.Save(); }
		}
		public int OutlierPercentageAggregation
		{
			get { return Settings.OutlierPercentageAggregation; }
			set { Settings.OutlierPercentageAggregation = value; Settings.Save(); }
		}

		public bool AreThresholdsReversed
		{
			get { return Settings.AreThresholdsReversed; }
			set { Settings.AreThresholdsReversed = value; Settings.Save(); }
		}
	}
}
