using CapFrameX.Contracts.Aggregation;

namespace CapFrameX.Data
{
	public class AggregationEntry : IAggregationEntry
	{
		public string GameName { get; set; }

		public string CreationDate { get; set; }

		public string CreationTime { get; set; }

		public double AverageValue { get; set; }

		public double SecondMetricValue { get; set; }

		public double ThirdMetricValue { get; set; }
	}
}
