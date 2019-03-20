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

		public string ChartQualityLevel
		{
			get { return Settings.ChartQualityLevel; }
			set { Settings.ChartQualityLevel = value; Settings.Save(); }
		}

		public int FpsValuesRoundingDigits
		{
			get { return Settings.FpsValuesRoundingDigits; }
			set { Settings.FpsValuesRoundingDigits = value; Settings.Save(); }
		}

		public string RecordDataGridIgnoreList
		{
			get { return Settings.RecordDataGridIgnoreList; }
			set { Settings.RecordDataGridIgnoreList = value; Settings.Save(); }
		}

		public bool ShowLowParameter
		{
			get { return Settings.ShowLowParameter; }
			set { Settings.ShowLowParameter = value; Settings.Save(); }
		}

		public void AddAppNameToIgnoreList(string nameToBeIgnored)
		{
			if (!RecordDataGridIgnoreList.Contains(nameToBeIgnored))
				RecordDataGridIgnoreList = RecordDataGridIgnoreList + "; " + nameToBeIgnored;
		}
	}
}
