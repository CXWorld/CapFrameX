using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Statistics;

namespace CapFrameX.Contracts.Aggregation
{
	public interface IAggregationEntry
	{
		string GameName { get; }

		string CreationDate { get; }

		string CreationTime { get; }

		double AverageValue { get; }

		double SecondMetricValue { get; }

		double ThirdMetricValue { get; }

		IMetricAnalysis MetricAnalysis { get; }

		IFileRecordInfo FileRecordInfo { get; }
	}
}
