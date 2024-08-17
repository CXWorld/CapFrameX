namespace CapFrameX.Statistics.NetStandard.Contracts
{
	public interface IFrametimeAnalyzer
	{
		double[] GetLShapeQuantiles(ELShapeMetrics LShapeMetric);
	}
}
