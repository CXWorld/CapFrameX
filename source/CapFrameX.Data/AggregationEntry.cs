using CapFrameX.Contracts.Aggregation;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Statistics;

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

		public IMetricAnalysis MetricAnalysis { get; set; }

		public IFileRecordInfo FileRecordInfo { get; set; }
	}
}
