using CapFrameX.Extensions.Attributes;
using System.ComponentModel;

namespace CapFrameX.Statistics
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
		[Description("5% percentile")]
		[ShortDescription("P5")]
		P5 = 5,
		[Description("1% percentile")]
		[ShortDescription("P1")]
		P1 = 6,
		[Description("0.2% percentile")]
		[ShortDescription("P0.2")]
		P0dot2 = 7,
		[Description("0.1% percentile")]
		[ShortDescription("P0.1")]
		P0dot1 = 8,
		[Description("1% low average")]
		[ShortDescription("1% Low")]
		OnePercentLow = 9,
		[Description("0.1% low average")]
		[ShortDescription("0.1% Low")]
		ZerodotOnePercentLow = 10,
		[Description("Minimum")]
		[ShortDescription("Min")]
		Min = 11,
		[Description("Adaptive STDEV")]
		[ShortDescription("Adp STDEV")]
		AdaptiveStd = 12,
		[Description("None")]
		[ShortDescription("None")]
		None = 13,
	}
}