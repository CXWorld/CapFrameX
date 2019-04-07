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

		public bool UseMaxStatisticParameter
		{
			get { return Settings.UseMaxStatisticParameter; }
			set { Settings.UseMaxStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP99QuantileStatisticParameter
		{
			get { return Settings.UseP99QuantileStatisticParameter; }
			set { Settings.UseP99QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP95QuantileStatisticParameter
		{
			get { return Settings.UseP95QuantileStatisticParameter; }
			set { Settings.UseP95QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseAverageStatisticParameter
		{
			get { return Settings.UseAverageStatisticParameter; }
			set { Settings.UseAverageStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP5QuantileStatisticParameter
		{
			get { return Settings.UseP5QuantileStatisticParameter; }
			set { Settings.UseP5QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP1QuantileStatisticParameter
		{
			get { return Settings.UseP1QuantileStatisticParameter; }
			set { Settings.UseP1QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP0Dot1QuantileStatisticParameter
		{
			get { return Settings.UseP0Dot1QuantileStatisticParameter; }
			set { Settings.UseP0Dot1QuantileStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP1LowAverageStatisticParameter
		{
			get { return Settings.UseP1LowAverageStatisticParameter; }
			set { Settings.UseP1LowAverageStatisticParameter = value; Settings.Save(); }
		}

		public bool UseP0Dot1LowAverageStatisticParameter
		{
			get { return Settings.UseP0Dot1LowAverageStatisticParameter; }
			set { Settings.UseP0Dot1LowAverageStatisticParameter = value; Settings.Save(); }
		}

		public bool UseMinStatisticParameter
		{
			get { return Settings.UseMinStatisticParameter; }
			set { Settings.UseMinStatisticParameter = value; Settings.Save(); }
		}

		public bool UseAdaptiveSTDStatisticParameter
		{
			get { return Settings.UseAdaptiveSTDStatisticParameter; }
			set { Settings.UseAdaptiveSTDStatisticParameter = value; Settings.Save(); }
		}

		public void AddAppNameToIgnoreList(string nameToBeIgnored)
		{
			if (!RecordDataGridIgnoreList.Contains(nameToBeIgnored))
				RecordDataGridIgnoreList = RecordDataGridIgnoreList + "; " + nameToBeIgnored;
		}
	}
}
