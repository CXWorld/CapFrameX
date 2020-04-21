namespace CapFrameX.Statistics.NetStandard.Contracts
{
	public interface IMetricAnalysis
	{
		string ResultString { get; }
		double Average { get; }
		double Second { get; }
		double Third { get; }
	}
}
