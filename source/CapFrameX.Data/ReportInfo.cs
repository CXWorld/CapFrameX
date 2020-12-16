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
		[DisplayName("99th percentile")]
		public double NinetyNinePercentQuantileFps { get; set; }
		[DisplayName("95th percentile")]
		public double NinetyFivePercentQuantileFps { get; set; }
		[DisplayName("Average FPS")]
		public double AverageFps { get; set; }
		[DisplayName("Median FPS")]
		public double MedianFps { get; set; }
		[DisplayName("5th percentile")]
		public double FivePercentQuantileFps { get; set; }
		[DisplayName("1th percentile")]
		public double OnePercentQuantileFps { get; set; }
		[DisplayName("1% low average")]
        public double OnePercentLowAverageFps { get; set; }
        [DisplayName("0.2th percentile")]
		public double ZeroDotTwoPercentQuantileFps { get; set; }
		[DisplayName("0.1th percentile")]
        public double ZeroDotOnePercentQuantileFps { get; set; }
		[DisplayName("0.1% low average")]
		public double ZeroDotOnePercentLowAverageFps { get; set; }
		[DisplayName("Min FPS")]
		public double MinFps { get; set; }
		[DisplayName("Adaptive STDEV")]
		public double AdaptiveSTDFps { get; set; }
		[DisplayName("CPU FPS/W")]
		public double CpuFpsPerWatt { get; set; }
		//[DisplayName("GPU FPS/W")]
		//public double GpuFpsPerWatt { get; set; }
		[DisplayName("Comment")]
		public string CustomComment { get; set; }
	}
}
