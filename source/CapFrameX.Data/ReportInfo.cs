using System.ComponentModel;

namespace CapFrameX.Data
{
	public class ReportInfo
	{
		[DisplayName("Game Name")]
		public string Game { get; set; }
		[DisplayName("Creation date")]
		public string Date { get; set; }
		[DisplayName("Creation time")]
		public string Time { get; set; }
		[DisplayName("# samples")]
		public int NumberOfSamples {get; set; }
		[DisplayName("Record time (s)")]
		public double RecordTime { get; set; }
		[DisplayName("CPU Name")]
		public string Cpu { get; set; }
		[DisplayName("GPU Name")]
		public string GraphicCard { get; set; }
		[DisplayName("RAM")]
		public string Ram { get; set; }
		[DisplayName("Max FPS")]
		public double MaxFps { get; set; }
		[DisplayName("P99 FPS")]
		public double NinetyNinePercentQuantileFps { get; set; }
		[DisplayName("P95 FPS")]
		public double NinetyFivePercentQuantileFps { get; set; }
		[DisplayName("Average FPS")]
		public double AverageFps { get; set; }
		[DisplayName("Median FPS")]
		public double MedianFps { get; set; }
		[DisplayName("P5 FPS")]
		public double FivePercentQuantileFps { get; set; }
		[DisplayName("P1 FPS")]
		public double OnePercentQuantileFps { get; set; }
		[DisplayName("1% low average FPS")]
        public double OnePercentLowAverageFps { get; set; }
        [DisplayName("1% low integral FPS")]
        public double OnePercentLowIntegralFps { get; set; }
        [DisplayName("P0.2 FPS")]
		public double ZeroDotTwoPercentQuantileFps { get; set; }
		[DisplayName("P0.1 FPS")]
        public double ZeroDotOnePercentQuantileFps { get; set; }
		[DisplayName("0.1% low average FPS")]
		public double ZeroDotOnePercentLowAverageFps { get; set; }
        [DisplayName("0.1% low integral FPS")]
        public double ZeroDotOnePercentLowIntegralFps { get; set; }
        [DisplayName("Min FPS")]
		public double MinFps { get; set; }
		[DisplayName("Adaptive STDEV")]
		public double AdaptiveSTDFps { get; set; }
		[DisplayName("CPU FPS / 10W")]
		public double CpuFpsPerWatt { get; set; }
        [DisplayName("GPU FPS / 10W")]
        public double GpuFpsPerWatt { get; set; }
		[DisplayName("App latency (ms)")]
		public double AppLatency { get; set; }
		[DisplayName("CPU Max Thread Usage (%)")]
		public double CpuMaxUsage { get; set; }
		[DisplayName("CPU Power (W)")]
		public double CpuPower { get; set; }
		[DisplayName("CPU Clock (MHz)")]
		public double CpuMaxClock { get; set; }
		[DisplayName("CPU Temp (°C)")]
		public double CpuTemp { get; set; }
		[DisplayName("GPU Usage (%)")]
		public double GpuUsage { get; set; }
		[DisplayName("GPU Power (W)")]
		public double GpuPower { get; set; }
		[DisplayName("GPU TBP Sim(W)")]
		public double GpuTBPSim { get; set; }
		[DisplayName("GPU Clock (MHz)")]
		public double GpuClock { get; set; }
		[DisplayName("GPU Temp (°C)")]
		public double GpuTemp { get; set; }
		[DisplayName("Comment")]
		public string CustomComment { get; set; }
	}
}
