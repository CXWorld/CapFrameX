using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Statistics.NetStandard
{
	public class MetricAnalysis : IMetricAnalysis
	{
		public string ResultString { get; set; }
		public double Average { get; set; }
		public double Second { get; set; }
		public double Third { get; set; }
	}
}
