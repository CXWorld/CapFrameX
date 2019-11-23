namespace CapFrameX.Contracts.Configuration
{
	public interface IAppConfiguration
	{
		int MovingAverageWindowSize { get; set; }

		double StutteringFactor { get; set; }

		string ObservedDirectory { get; set; }

		string ScreenshotDirectory { get; set; }

		int FpsValuesRoundingDigits { get; set; }

		bool UseSingleRecordMaxStatisticParameter { get; set; }

		bool UseSingleRecord99QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP95QuantileStatisticParameter { get; set; }

		bool UseSingleRecordAverageStatisticParameter { get; set; }

		bool UseSingleRecordP5QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP1QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot1QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot2QuantileStatisticParameter { get; set; }

		bool UseSingleRecordP1LowAverageStatisticParameter { get; set; }

		bool UseSingleRecordP0Dot1LowAverageStatisticParameter { get; set; }

		bool UseSingleRecordMinStatisticParameter { get; set; }

		bool UseSingleRecordAdaptiveSTDStatisticParameter { get; set; }

		string CaptureHotKey { get; set; }

		string HotkeySoundMode { get; set; }

		int CaptureTime { get; set; }

		double VoiceSoundLevel { get; set; }

		double SimpleSoundLevel { get; set; }

		string SecondaryMetric { get; set; }

		string ComparisonContext { get; set; }

		string RecordingListSortMemberPath { get; set; }

		string RecordingListSortDirection { get; set; }
	}
}
