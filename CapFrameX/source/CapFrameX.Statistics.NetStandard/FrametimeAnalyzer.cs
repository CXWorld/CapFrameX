using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Statistics.NetStandard
{
	public class FrametimeAnalyzer : IFrametimeAnalyzer
	{
		private static readonly double[] _lShapeFrametimeQuantiles
					= new[] { 90.0, 91.0, 92.0, 93.0, 94.0, 95.0, 96.0, 97.0, 98.0, 99.0, 99.5, 99.8, 99.9, 99.95 };

		private static readonly double[] _lShapeFPSQuantiles
			= new[] { 0.05, 0.1, 0.2, 0.5, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };

		public double[] GetLShapeQuantiles(ELShapeMetrics LShapeMetric)
		{

			return LShapeMetric == ELShapeMetrics.Frametimes ? _lShapeFrametimeQuantiles : _lShapeFPSQuantiles;
		}
	}
}
