using CapFrameX.Extensions.NetStandard.Attributes;
using System.ComponentModel;

namespace CapFrameX.Statistics.NetStandard.Contracts
{
	public enum EMetric
	{
		[Description("Maximum")]
		[ShortDescription("Max")]
		Max = 1,
		[Description("99% percentile")]
		[ShortDescription("P99")]
		P99 = 2,
		[Description("95% percentile")]
		[ShortDescription("P95")]
		P95 = 3,
		[Description("Average")]
		[ShortDescription("Avg")]
		Average = 4,
		[Description("Median")]
		[ShortDescription("Med")]
		Median = 5,
		[Description("5% percentile")]
		[ShortDescription("P5")]
		P5 = 6,
		[Description("1% percentile")]
		[ShortDescription("P1")]
		P1 = 7,
		[Description("0.2% percentile")]
		[ShortDescription("P0.2")]
		P0dot2 = 8,
		[Description("0.1% percentile")]
		[ShortDescription("P0.1")]
		P0dot1 = 9,
		[Description("1% low average")]
		[ShortDescription("1% Low")]
		OnePercentLow = 10,
		[Description("0.1% low average")]
		[ShortDescription("0.1% Low")]
		ZerodotOnePercentLow = 11,
		[Description("Minimum")]
		[ShortDescription("Min")]
		Min = 12,
		[Description("Adaptive STDEV")]
		[ShortDescription("Adp STDEV")]
		AdaptiveStd = 13,
		[Description("None")]
		[ShortDescription("None")]
		None = 14,
	}
}