using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.PresentMonInterface
{
    /// <summary>
    /// The FPS metrics of one realtime window (the online metrics' <c>MetricInterval</c>, 20 s by
    /// default), computed from one sorted copy of its valid frame times.
    /// <para>
    /// Every online FPS metric used to copy the whole window under the window lock and hand the copy
    /// to <see cref="IStatisticProvider.GetFpsMetricValue"/>, whose quantile (MathNet's
    /// <c>Quantile</c>) copies it again: up to six arrays of 20 s of frames per overlay refresh, at
    /// 530 frames per second and more on the large object heap. One copy and one sort now serve
    /// them all. The results equal the provider's to the bit (<c>OnlineFpsWindowTest</c>): the
    /// quantile is MathNet's R-8 on the same order statistics, and the averages run over the
    /// frame times in window order, as the provider sums them.
    /// </para>
    /// </summary>
    internal sealed class OnlineFpsWindow
    {
        private readonly List<double> _ordered = new List<double>(); // valid frame times, window order
        private double[] _sortedFrametimes = Array.Empty<double>();
        private double[] _sortedFps = Array.Empty<double>();

        public int Count => _ordered.Count;

        /// <summary>The window version (<see cref="OnlineMetricService"/>) these statistics belong to.</summary>
        public long Version { get; set; } = -1;

        /// <summary>The window epoch (a reset starts a new one) these statistics belong to.</summary>
        public long Epoch { get; set; } = -1;

        /// <summary><see cref="Environment.TickCount64"/> when they were computed.</summary>
        public long LoadedAtMs { get; set; }

        /// <summary>The metrics this window answers; the others go to the statistic provider.</summary>
        public static bool Supports(EMetric metric)
        {
            switch (metric)
            {
                case EMetric.Average:
                case EMetric.P1:
                case EMetric.P0dot1:
                case EMetric.P0dot2:
                case EMetric.OnePercentLowAverage:
                case EMetric.ZerodotOnePercentLowAverage:
                case EMetric.ZerodotTwoPercentLowAverage:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Takes the valid frame times (positive and finite) of the window, in order.</summary>
        public void Load(IEnumerable<double> timings)
        {
            _ordered.Clear();
            if (timings != null)
            {
                foreach (double timing in timings)
                {
                    if (timing > 0 && !double.IsNaN(timing) && !double.IsInfinity(timing))
                        _ordered.Add(timing);
                }
            }

            int count = _ordered.Count;
            if (_sortedFrametimes.Length < count)
            {
                int capacity = Math.Max(count, _sortedFrametimes.Length * 2);
                _sortedFrametimes = new double[capacity];
                _sortedFps = new double[capacity];
            }
            _ordered.CopyTo(_sortedFrametimes, 0);
            Array.Sort(_sortedFrametimes, 0, count);
            // 1000 / frame time falls as the frame time rises: the frame rates in ascending order are
            // the frame times in descending order, each divided as the provider divides them.
            for (int i = 0; i < count; i++)
                _sortedFps[i] = 1000.0 / _sortedFrametimes[count - 1 - i];
        }

        /// <summary>
        /// The metric for the loaded frame times, rounded to <paramref name="roundingDigits"/> as the
        /// provider rounds its FPS metrics (<c>FpsValuesRoundingDigits</c>); NaN without any.
        /// </summary>
        public double Get(EMetric metric, int roundingDigits)
        {
            if (Count == 0) return double.NaN;
            try
            {
                double value;
                switch (metric)
                {
                    case EMetric.Average:
                        value = Count * 1000 / _ordered.Sum();
                        break;
                    case EMetric.P1:
                        value = Quantile(_sortedFps, Count, 0.01);
                        break;
                    case EMetric.P0dot2:
                        value = Quantile(_sortedFps, Count, 0.002);
                        break;
                    case EMetric.P0dot1:
                        value = Quantile(_sortedFps, Count, 0.001);
                        break;
                    case EMetric.OnePercentLowAverage:
                        value = 1000 / HighAverage(1 - 0.01);
                        break;
                    case EMetric.ZerodotTwoPercentLowAverage:
                        value = 1000 / HighAverage(1 - 0.002);
                        break;
                    case EMetric.ZerodotOnePercentLowAverage:
                        value = 1000 / HighAverage(1 - 0.001);
                        break;
                    default:
                        return double.NaN;
                }
                return Math.Round(value, roundingDigits, MidpointRounding.AwayFromZero);
            }
            catch (Exception)
            {
                return double.NaN; // as the provider: an invalid rounding setting reads as no value
            }
        }

        // The average of the frame times at or above their tau-quantile, in window order, as
        // FrametimeStatisticProvider.GetPercentageHighAverageSequence computes it.
        private double HighAverage(double tau)
        {
            double quantile = Quantile(_sortedFrametimes, Count, tau);
            return _ordered.Where(element => element >= quantile).Average();
        }

        // MathNet's R-8 quantile (ArrayStatistics.QuantileInplace, which Statistics.Quantile calls)
        // on data sorted in ascending order: the same two order statistics, the same interpolation.
        internal static double Quantile(double[] sorted, int count, double tau)
        {
            if (tau < 0d || tau > 1d || count == 0) return double.NaN;
            double h = (count + 1d / 3d) * tau + 1d / 3d;
            int hf = (int)h;
            if (hf <= 0 || tau == 0d) return sorted[0];
            if (hf >= count || tau == 1d) return sorted[count - 1];
            double a = sorted[hf - 1];
            double b = sorted[hf];
            return a + (h - hf) * (b - a);
        }
    }
}
