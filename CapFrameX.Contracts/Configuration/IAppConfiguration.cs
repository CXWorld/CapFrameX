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

		bool ShowLowParameter { get; set; }

		void AddAppNameToIgnoreList(string nameToBeIgnored);
	}
}
