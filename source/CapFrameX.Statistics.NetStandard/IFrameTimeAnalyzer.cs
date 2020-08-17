namespace CapFrameX.Statistics.NetStandard
{
	public interface IFrametimeAnalyzer
	{
		double[] GetLShapeQuantiles(ELShapeMetrics LShapeMetric);
	}
}
