using System.ComponentModel;

namespace CapFrameX.Statistics
{
	public enum EMetric
	{
		[Description("Maximum")]
		Max = 1,
		[Description("99% percentile")]
		P99 = 2,
		[Description("95% percentile")]
		P95 = 3,
		[Description("Average")]
		Average = 4,
		[Description("5% percentile")]
		P5 = 5,
		[Description("1% percentile")]
		P1 = 6,
		[Description("0.2% percentile")]
		P0dot2 = 7,
		[Description("0.1% percentile")]
		P0dot1 = 8,
		[Description("1% low average")]
		OnePercentLow = 9,
		[Description("0.1% low average")]
		ZerodotOnePercentLow = 10,
		[Description("Minimum")]
		Min = 11,
		[Description("Adaptive STD")]
		AdaptiveStd = 12
	}
}