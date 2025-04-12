using MathNet.Numerics.Interpolation;
using System.Linq;

namespace CapFrameX.PMD
{
    public class PoweneticsDataProcessing
    {
        public static PoweneticsSample[] GetMappedPmdData(PoweneticsSample[] referenceSamples, PoweneticsSample[] mappingSamples)
		{
            var linearSpline = LinearSpline.Interpolate(
                mappingSamples.Select(sample => sample.Time), 
                mappingSamples.Select(sample => sample.Value));

			return referenceSamples
                .Select(sample => new PoweneticsSample() 
                { 
                    Time = sample.Time, 
                    Value = linearSpline.Interpolate(sample.Time) 
                }).ToArray();
        }
    }
}
