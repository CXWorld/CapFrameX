namespace CapFrameX.Contracts.Statistics
{
	public interface IMetricAnalysis
	{
		string ResultString { get; }
		double Average { get; }
		double Second { get; }
		double Third { get; }
	}
}
