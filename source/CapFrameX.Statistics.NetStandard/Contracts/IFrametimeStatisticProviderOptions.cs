namespace CapFrameX.Statistics.NetStandard.Contracts
{
	public interface IFrametimeStatisticProviderOptions
	{
		int MovingAverageWindowSize { get; set; }
		int IntervalAverageWindowTime { get; set; }
		int FpsValuesRoundingDigits { get; set; }
	}
}
