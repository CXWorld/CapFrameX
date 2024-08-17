using MathNet.Numerics.Interpolation;
using System.Linq;

namespace CapFrameX.PMD
{
    public class PmdDataProcessing
    {
        public static PmdSample[] GetMappedPmdData(PmdSample[] referenceSamples, PmdSample[] mappingSamples)
		{
            var linearSpline = LinearSpline.Interpolate(
                mappingSamples.Select(sample => sample.Time), 
                mappingSamples.Select(sample => sample.Value));

			return referenceSamples
                .Select(sample => new PmdSample() 
                { 
                    Time = sample.Time, 
                    Value = linearSpline.Interpolate(sample.Time) 
                }).ToArray();
        }
    }
}
