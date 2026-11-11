using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Statistics.NetStandard
{
    /// <summary>
    /// Default x range of a frametime distribution chart. A few extreme frames, such as a stall of
    /// tens of seconds, would otherwise stretch the axis until the actual distribution fills only
    /// its leftmost pixels. The chart keeps every point; zooming out still shows the excluded ones.
    /// </summary>
    public static class FrametimeDistributionRange
    {
        /// <summary>
        /// Share of the frames, by count, that forms the body of the distribution. By time a single
        /// long stall can outweigh it: 22.7 s in a 36 min capture are 1% of the time.
        /// </summary>
        public const double BodyFrameShare = 0.999;

        /// <summary>
        /// Points beyond this multiple of the body's end stay outside the default range, so the body
        /// keeps at least a fifth of the axis.
        /// </summary>
        public const double TailFactor = 5;

        /// <summary>
        /// Returns the x value of the last point to show by default: the last point, unless points
        /// lie beyond <see cref="TailFactor"/> times the frametime that <see cref="BodyFrameShare"/>
        /// of the frames stay within. Returns NaN without points.
        /// </summary>
        /// <param name="distributionPoints">
        /// Points of <see cref="SessionExtensions.GetFrametimeDistributionPoints"/> or
        /// <see cref="SessionExtensions.GetDisplayTimeDistributionPoints"/>, ordered by x.
        /// </param>
        public static double GetReadableMaximum(IList<Point> distributionPoints)
        {
            if (distributionPoints == null || distributionPoints.Count == 0)
                return double.NaN;

            // Y is the bin's share of the time; dividing by the bin center turns it into a value
            // proportional to the bin's frame count.
            const double halfBin = SessionExtensions.DistributionBinWidth / 2;
            var frameWeights = distributionPoints
                .Select(point => point.Y / Math.Max(point.X - halfBin, halfBin))
                .ToArray();
            double bodyWeight = BodyFrameShare * frameWeights.Sum();

            double bodyEnd = distributionPoints[distributionPoints.Count - 1].X;
            double cumulativeWeight = 0;
            for (int i = 0; i < distributionPoints.Count; i++)
            {
                cumulativeWeight += frameWeights[i];
                if (cumulativeWeight >= bodyWeight)
                {
                    bodyEnd = distributionPoints[i].X;
                    break;
                }
            }

            double limit = TailFactor * bodyEnd;
            return distributionPoints.Last(point => point.X <= limit).X;
        }
    }
}
