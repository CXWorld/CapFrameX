using System.ComponentModel;

namespace CapFrameX.Data
{
	public class ReportInfo
	{
		[DisplayName("Game")]
		public string Game { get; set; }
		[DisplayName("Creation date")]
		public string Date { get; set; }
		[DisplayName("Creation time")]
		public string Time { get; set; }
		[DisplayName("# samples")]
		public int NumberOfSamples {get; set; }
		[DisplayName("Record time")]
		public string RecordTime { get; set; }
		[DisplayName("CPU")]
		public string Cpu { get; set; }
		[DisplayName("Graphic card")]
		public string GraphicCard { get; set; }
		[DisplayName("RAM")]
		public string Ram { get; set; }
		[DisplayName("Max FPS")]
		public double MaxFps { get; set; }
		[DisplayName("99% quantile")]
		public double NinetyNinePercentQuantileFps { get; set; }
		[DisplayName("95% quantile")]
		public double NinetyFivePercentQuantileFps { get; set; }
		[DisplayName("Average FPS")]
		public double AverageFps { get; set; }
		[DisplayName("5% quantile")]
		public double FivePercentQuantileFps { get; set; }
		[DisplayName("1% quantile")]
		public double OnePercentQuantileFps { get; set; }
		[DisplayName("1% low average")]
        public double OnePercentLowAverageFps { get; set; }
        [DisplayName("0.2 % quantile")]
		public double ZeroDotTwoPercentQuantileFps { get; set; }
		[DisplayName("0.1% quantile")]
        public double ZeroDotOnePercentQuantileFps { get; set; }
		[DisplayName("0.1% low average")]
		public double ZeroDotOnePercentLowAverageFps { get; set; }
		[DisplayName("Min FPS")]
		public double MinFps { get; set; }
		[DisplayName("Adaptive STDEV")]
		public double AdaptiveSTDFps { get; set; }
		[DisplayName("Comment")]
		public string CustomComment { get; set; }
	}
}
