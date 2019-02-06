namespace CapFrameX.Contracts.Configuration
{
	public interface IAppConfiguration
	{
		int MovingAverageWindowSize { get; set; }

		double StutteringFactor { get; set; }

		string ObservedDirectory { get; set; }

		string ChartQualityLevel { get; set; }

		int FpsValuesRoundingDigits { get; set; }

		string RecordDataGridIgnoreList { get; set; }

		void AddAppNameToIgnoreList(string nameToBeIgnored);
	}
}
