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

		public string SecondaryMetric 
		{
			get { return Settings.SecondaryMetric; }
			set { Settings.SecondaryMetric = value; Settings.Save(); }
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
	}
}
