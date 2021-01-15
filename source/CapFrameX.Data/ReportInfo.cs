using System.ComponentModel;

namespace CapFrameX.Data
{
	public class ReportInfo
	{
		[DisplayName("Game")]
		public string Game { get; set; }
		[DisplayName("Resolution")]
		public string Resolution { get; set; }
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
		[DisplayName("99% percentile")]
		public double NinetyNinePercentQuantileFps { get; set; }
		[DisplayName("95% percentile")]
		public double NinetyFivePercentQuantileFps { get; set; }
		[DisplayName("Average FPS")]
		public double AverageFps { get; set; }
		[DisplayName("Median FPS")]
		public double MedianFps { get; set; }
		[DisplayName("5% percentile")]
		public double FivePercentQuantileFps { get; set; }
		[DisplayName("1% percentile")]
		public double OnePercentQuantileFps { get; set; }
		[DisplayName("1% low average")]
        public double OnePercentLowAverageFps { get; set; }
        [DisplayName("0.2% percentile")]
		public double ZeroDotTwoPercentQuantileFps { get; set; }
		[DisplayName("0.1% percentile")]
        public double ZeroDotOnePercentQuantileFps { get; set; }
		[DisplayName("0.1% low average")]
		public double ZeroDotOnePercentLowAverageFps { get; set; }
		[DisplayName("Min FPS")]
		public double MinFps { get; set; }
		[DisplayName("Adaptive STDEV")]
		public double AdaptiveSTDFps { get; set; }
		[DisplayName("CPU FPS / 10W")]
		public double CpuFpsPerWatt { get; set; }
        [DisplayName("GPU FPS / 10W")]
        public double GpuFpsPerWatt { get; set; }
        [DisplayName("Comment")]
		public string CustomComment { get; set; }
	}
}
