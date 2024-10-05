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
        [Description("GPU Active Average")]
        [ShortDescription("GPU Avg")]
        GpuActiveAverage = 5,
        [Description("Median")]
		[ShortDescription("Med")]
		Median = 6,
		[Description("5% percentile")]
		[ShortDescription("P5")]
		P5 = 7,
		[Description("1% percentile")]
		[ShortDescription("P1")]
		P1 = 8,
        [Description("GPU Active 1% percentile")]
        [ShortDescription("GPU P1")]
        GpuActiveP1 = 9,
        [Description("0.2% percentile")]
		[ShortDescription("P0.2")]
		P0dot2 = 10,
		[Description("0.1% percentile")]
		[ShortDescription("P0.1")]
		P0dot1 = 11,
		[Description("1% low average")]
		[ShortDescription("1% Low Avg")]
		OnePercentLowAverage = 12,
        [Description("GPU Active 1% low average")]
        [ShortDescription("GPU 1% Low Avg")]
        GpuActiveOnePercentLowAverage = 13,
        [Description("0.2% low average")]
		[ShortDescription("0.2% Low Avg")]
		ZerodotTwoPercentLowAverage = 14,
		[Description("0.1% low average")]
		[ShortDescription("0.1% Low Avg")]
		ZerodotOnePercentLowAverage = 15,
        [Description("1% low integral")]
        [ShortDescription("1% Low Int")]
        OnePercentLowIntegral = 16,
		[Description("0.2% low integral")]
		[ShortDescription("0.2% Low Int")]
		ZerodotTwoPercentLowIntegral = 17,
		[Description("0.1% low integral")]
        [ShortDescription("0.1% Low Int")]
        ZerodotOnePercentLowIntegral = 18,
        [Description("Minimum")]
		[ShortDescription("Min")]
		Min = 19,
		[Description("Adaptive STDEV")]
		[ShortDescription("Adp STDEV")]
		AdaptiveStd = 20,
		[Description("CPU FPS per 10 Watts")]
		[ShortDescription("CPU FPS/10W")]
		CpuFpsPerWatt = 21,
        [Description("GPU FPS per 10 Watts")]
        [ShortDescription("GPU FPS/10W")]
        GpuFpsPerWatt = 22,
        [Description("CPU Active Average")]
        [ShortDescription("CPU Avg")]
        CpuActiveAverage = 23,
        [Description("None")]
		[ShortDescription("None")]
		None = 24,
	}
}