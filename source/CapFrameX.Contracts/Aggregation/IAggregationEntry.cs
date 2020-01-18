using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
	}
}
