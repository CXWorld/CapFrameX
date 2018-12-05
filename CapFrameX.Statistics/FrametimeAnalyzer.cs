using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Statistics
{
	public class FrametimeAnalyzer : IFrametimeAnalyzer
	{
		private static readonly double[] _lShapeQuantiles
					= new[] { 90.0, 91.0, 92.0, 93.0, 94.0, 95.0, 96.0, 97.0, 98.0, 99.0, 99.5, 99.8, 99.9, 99.95 };

		public double[] GetLShapeQunantiles()
		{
			return _lShapeQuantiles;
		}
	}
}
