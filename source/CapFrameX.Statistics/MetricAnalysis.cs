using CapFrameX.Contracts.Statistics;

namespace CapFrameX.Statistics
{
	public class MetricAnalysis : IMetricAnalysis
	{
		public string ResultString { get; set; }
		public double Average { get; set; }
		public double Second { get; set; }
		public double Third { get; set; }
	}
}
