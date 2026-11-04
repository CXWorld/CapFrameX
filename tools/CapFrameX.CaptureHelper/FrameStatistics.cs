using System;
using System.Collections.Generic;

namespace CapFrameX.CaptureHelper
{
    /// <summary>
    /// The FPS metrics as CapFrameX computes them (FrametimeStatisticProvider.GetFpsMetricValue), so
    /// both report the same numbers for the same frames.
    /// </summary>
    internal static class FrameStatistics
    {
        /// <summary>Frames per second over the whole capture: frames × 1000 / sum of the frame times.</summary>
        public static double AverageFps(IReadOnlyList<double> frametimes)
        {
            if (frametimes.Count == 0) return double.NaN;
            double sum = 0;
            foreach (var frametime in frametimes)
                sum += frametime;
            return frametimes.Count * 1000.0 / sum;
        }

        /// <summary>
        /// The <paramref name="share"/> quantile of the per-frame FPS (1000 / frame time): P1 is 0.01,
        /// P0.2 is 0.002, so that share of the frames ran at a lower frame rate.
        /// </summary>
        public static double PercentileFps(IReadOnlyList<double> frametimes, double share)
        {
            if (frametimes.Count == 0) return double.NaN;
            var fps = new double[frametimes.Count];
            for (int i = 0; i < fps.Length; i++)
                fps[i] = 1000.0 / frametimes[i];
            Array.Sort(fps);
            return Quantile(fps, share);
        }

        // MathNet.Numerics' Statistics.Quantile, which CapFrameX uses: R-8, approximately
        // median-unbiased, on sorted data.
        private static double Quantile(double[] sorted, double tau)
        {
            double h = (sorted.Length + 1.0 / 3.0) * tau + 1.0 / 3.0;
            int hf = (int)h;
            if (hf <= 0 || tau == 0) return sorted[0];
            if (hf >= sorted.Length || tau == 1) return sorted[sorted.Length - 1];
            return sorted[hf - 1] + (h - hf) * (sorted[hf] - sorted[hf - 1]);
        }
    }
}
