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

		public void AddAppNameToIgnoreList(string nameToBeIgnored)
		{
			if (!RecordDataGridIgnoreList.Contains(nameToBeIgnored))
				RecordDataGridIgnoreList = RecordDataGridIgnoreList + "; " + nameToBeIgnored;
		}
	}
}
