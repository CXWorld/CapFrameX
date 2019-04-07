namespace CapFrameX.Contracts.Configuration
{
	public interface IAppConfiguration
	{
		int MovingAverageWindowSize { get; set; }

		double StutteringFactor { get; set; }

		string ObservedDirectory { get; set; }

		string ScreenshotDirectory { get; set; }

		string ChartQualityLevel { get; set; }

		int FpsValuesRoundingDigits { get; set; }

		string RecordDataGridIgnoreList { get; set; }

		bool UseMaxStatisticParameter { get; set; }

		bool UseP99QuantileStatisticParameter { get; set; }

		bool UseP95QuantileStatisticParameter { get; set; }

		bool UseAverageStatisticParameter { get; set; }

		bool UseP5QuantileStatisticParameter { get; set; }

		bool UseP1QuantileStatisticParameter { get; set; }

		bool UseP0Dot1QuantileStatisticParameter { get; set; }

		bool UseP1LowAverageStatisticParameter { get; set; }

		bool UseP0Dot1LowAverageStatisticParameter { get; set; }

		bool UseMinStatisticParameter { get; set; }

		bool UseAdaptiveSTDStatisticParameter { get; set; }

		void AddAppNameToIgnoreList(string nameToBeIgnored);
	}
}
