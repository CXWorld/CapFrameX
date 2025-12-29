using CapFrameX.Statistics.NetStandard.Contracts;
using MathNet.Numerics.Statistics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Statistics.NetStandard
{
    public class FrametimeStatisticProvider : IStatisticProvider
    {
        public static readonly double[] FPSTHRESHOLDS = new double[] { 10, 15, 30, 45, 60, 75, 90, 120, 144, 240 }.Reverse().ToArray();

        private const double TAU = 0.999;
        private readonly IFrametimeStatisticProviderOptions _options;

        // Thread-local reusable buffers to avoid allocations in hot paths
        [ThreadStatic]
        private static double[] _fpsBuffer;
        [ThreadStatic]
        private static double[] _sortBuffer;

        public FrametimeStatisticProvider(IFrametimeStatisticProviderOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Gets or creates a thread-local buffer for FPS calculations.
        /// </summary>
        private static double[] GetFpsBuffer(int minSize)
        {
            if (_fpsBuffer == null || _fpsBuffer.Length < minSize)
            {
                _fpsBuffer = new double[Math.Max(minSize, 1024)];
            }
            return _fpsBuffer;
        }

        /// <summary>
        /// Gets or creates a thread-local buffer for sorting operations.
        /// </summary>
        private static double[] GetSortBuffer(int minSize)
        {
            if (_sortBuffer == null || _sortBuffer.Length < minSize)
            {
                _sortBuffer = new double[Math.Max(minSize, 1024)];
            }
            return _sortBuffer;
        }

        public double GetAdaptiveStandardDeviation(IList<double> sequence, double timeWindow)
        {
            var timeBasedMovingAverageFilter = new TimeBasedMovingAverage(timeWindow);
            var timeBasedMovingAverage = timeBasedMovingAverageFilter.ProcessSamples(sequence);

            if (timeBasedMovingAverage.Count != sequence.Count)
            {
                throw new InvalidDataException("Different sample count data vs. filtered data");
            }

            var sumResidualSquares = sequence.Select((val, i) => Math.Pow(val - timeBasedMovingAverage[i], 2)).Sum();
            return Math.Sqrt(sumResidualSquares / (sequence.Count - 1));
        }

        public double GetStutteringCountPercentage(IList<double> sequence, double stutteringFactor)
        {
            var average = sequence.Average();
            var stutteringCount = sequence.Count(element => element > stutteringFactor * average);

            return 100 * (double)stutteringCount / sequence.Count;
        }

        public double GetOnlineStutteringTimePercentage(IList<double> sequence, double stutteringFactor)
        {
            var average = sequence.Average();
            var stutteringTime = sequence.Where(element => element > stutteringFactor * average).Sum();


            return 100 * stutteringTime / sequence.Sum();
        }

        public double GetStutteringTimePercentage(IList<double> sequence, double stutteringFactor)
        {
            var average = GetMovingAverage(sequence);

            double stutteringTime = 0.0;

            for (int i = 0; i < average.Count; i++)
            {
                if (sequence[i] > stutteringFactor * average[i])
                    stutteringTime += sequence[i];
            }

            return 100 * stutteringTime / sequence.Sum();
        }

        public double GetLowFPSTimePercentage(IList<double> sequence, double stutteringFactor, double lowFPSThreshold)
        {
            var average = GetMovingAverage(sequence);

            double lowFPSTime = 0.0;

            for (int i = 0; i < average.Count; i++)
            {
                if (sequence[i] <= stutteringFactor * average[i] && 1000 / sequence[i] < lowFPSThreshold)
                    lowFPSTime += sequence[i];
            }

            return 100 * lowFPSTime / sequence.Sum();
        }

        public IList<double> GetMovingAverage(IList<double> sequence)
        {
            var average = sequence.Average();
            var sampleBasedMovingAverageFilter = new SampleBasedMovingAverage(Convert.ToInt32(Math.Sqrt(average) * 10));

            return sampleBasedMovingAverageFilter.ProcessSamples(sequence);
        }

        public IList<double> GetTimeBasedMovingAverage(IList<double> sequence, double timeWindow)
        {
            var average = sequence.Average();
            var timeBasedMovingAverageFilter = new TimeBasedMovingAverage(timeWindow);

            return timeBasedMovingAverageFilter.ProcessSamples(sequence);
        }

        public IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method)
        {
            IList<double> adjustedSequence = null;

            switch (method)
            {
                case ERemoveOutlierMethod.DeciPercentile:
                    {
                        var deciPercentile = sequence.Quantile(TAU);
                        adjustedSequence = new List<double>();

                        foreach (var element in sequence)
                        {
                            if (element < deciPercentile)
                                adjustedSequence.Add(element);
                        }
                    }
                    break;
                case ERemoveOutlierMethod.InterquartileRange:
                    break;
                case ERemoveOutlierMethod.ThreeSigma:
                    break;
                case ERemoveOutlierMethod.TwoDotFiveSigma:
                    break;
                case ERemoveOutlierMethod.None:
                    adjustedSequence = sequence;
                    break;
                default:
                    adjustedSequence = sequence;
                    break;
            }

            return adjustedSequence;
        }


        public double GetPQuantileSequence(IList<double> sequence, double pQuantile)
        {
            return sequence.Quantile(pQuantile);
        }

        /// <summary>
        /// Equivalent x% low integral metric definition to MSI Afterburner.
        /// Optimized to use thread-local buffer for sorting to avoid allocations.
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="pQuantile"></param>
        /// <returns></returns>
        public double GetPercentageHighIntegralSequence(IList<double> sequence, double pQuantile)
        {
            if (!sequence.Any())
                return double.NaN;

            int count = sequence.Count;

            // Use thread-local buffer for sorting to avoid allocation
            var sortBuffer = GetSortBuffer(count);

            // Copy sequence to buffer
            for (int i = 0; i < count; i++)
            {
                sortBuffer[i] = sequence[i];
            }

            // Sort in descending order (using Array.Sort then reverse, or custom comparison)
            Array.Sort(sortBuffer, 0, count);
            // Reverse to get descending order
            int left = 0;
            int right = count - 1;
            while (left < right)
            {
                double temp = sortBuffer[left];
                sortBuffer[left] = sortBuffer[right];
                sortBuffer[right] = temp;
                left++;
                right--;
            }

            // Calculate total time
            double totalTime = 0;
            for (int i = 0; i < count; i++)
            {
                totalTime += sortBuffer[i];
            }

            var percentLowTime = totalTime * (1 - pQuantile);
            var lowTimeSum = 0d;
            var percentLowIndex = 0;

            for (int i = 0; i < count; i++)
            {
                lowTimeSum += sortBuffer[i];
                percentLowIndex = i;

                if (lowTimeSum >= percentLowTime)
                    break;
            }

            return sortBuffer[percentLowIndex];
        }

        /// <summary>
        /// x% low average metric
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="pQuantile"></param>
        /// <returns></returns>
        public double GetPercentageHighAverageSequence(IList<double> sequence, double pQuantile)
        {
            if (!sequence.Any())
                return double.NaN;

            var quantile = GetPQuantileSequence(sequence, pQuantile);
            var subSequenceLow = sequence.Where(element => element >= quantile);

            return subSequenceLow.Average();
        }

        /// <summary>
        /// Calculate FPS metric values.
        /// frametimes.
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="metric"></param>
        /// <returns>metric value</returns>
        public double GetFpsMetricValue(IList<double> sequence, EMetric metric)
        {
            if (!sequence.Any()) return double.NaN;

            try
            {
                double metricValue;

                // Use thread-local buffer to avoid allocation for FPS conversion
                var fpsBuffer = GetFpsBuffer(sequence.Count);
                int count = sequence.Count;
                for (int i = 0; i < count; i++)
                {
                    fpsBuffer[i] = 1000.0 / sequence[i];
                }

                // Create an ArraySegment that acts as IList<double> for the buffer
                var fps = new ArraySegment<double>(fpsBuffer, 0, count);

                switch (metric)
                {
                    case EMetric.Max:
                        metricValue = GetMax(fps);
                        break;
                    case EMetric.P99:
                        metricValue = GetPQuantileSequence(fps, 0.99);
                        break;
                    case EMetric.P95:
                        metricValue = GetPQuantileSequence(fps, 0.95);
                        break;
                    case EMetric.Average:
                    case EMetric.GpuActiveAverage:
                        metricValue = sequence.Count * 1000 / sequence.Sum();
                        break;
                    case EMetric.Median:
                        metricValue = GetPQuantileSequence(fps, 0.5);
                        break;
                    case EMetric.P5:
                        metricValue = GetPQuantileSequence(fps, 0.05);
                        break;
                    case EMetric.P1:
                    case EMetric.GpuActiveP1:
                        metricValue = GetPQuantileSequence(fps, 0.01);
                        break;
                    case EMetric.P0dot2:
                        metricValue = GetPQuantileSequence(fps, 0.002);
                        break;
                    case EMetric.P0dot1:
                        metricValue = GetPQuantileSequence(fps, 0.001);
                        break;
                    case EMetric.OnePercentLowAverage:
                    case EMetric.GpuActiveOnePercentLowAverage:
                        metricValue = 1000 / GetPercentageHighAverageSequence(sequence, 1 - 0.01);
                        break;
                    case EMetric.ZerodotTwoPercentLowAverage:
                        metricValue = 1000 / GetPercentageHighAverageSequence(sequence, 1 - 0.002);
                        break;
                    case EMetric.ZerodotOnePercentLowAverage:
                        metricValue = 1000 / GetPercentageHighAverageSequence(sequence, 1 - 0.001);
                        break;
                    case EMetric.OnePercentLowIntegral:
                        metricValue = 1000 / GetPercentageHighIntegralSequence(sequence, 1 - 0.01);
                        break;
                    case EMetric.ZerodotTwoPercentLowIntegral:
                        metricValue = 1000 / GetPercentageHighIntegralSequence(sequence, 1 - 0.002);
                        break;
                    case EMetric.ZerodotOnePercentLowIntegral:
                        metricValue = 1000 / GetPercentageHighIntegralSequence(sequence, 1 - 0.001);
                        break;
                    case EMetric.Min:
                        metricValue = GetMin(fps);
                        break;
                    case EMetric.AdaptiveStd:
                        // For AdaptiveStd, we need to pass as IList - use a temporary list
                        var fpsList = new List<double>(count);
                        for (int i = 0; i < count; i++)
                            fpsList.Add(fpsBuffer[i]);
                        metricValue = GetAdaptiveStandardDeviation(fpsList, _options.IntervalAverageWindowTime);
                        break;
                    default:
                        metricValue = double.NaN;
                        break;
                }

                return Math.Round(metricValue, _options.FpsValuesRoundingDigits, MidpointRounding.AwayFromZero);
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// Gets the maximum value from an array segment without allocations.
        /// </summary>
        private static double GetMax(ArraySegment<double> segment)
        {
            if (segment.Count == 0)
                return double.NaN;

            double max = segment.Array[segment.Offset];
            for (int i = segment.Offset + 1; i < segment.Offset + segment.Count; i++)
            {
                if (segment.Array[i] > max)
                    max = segment.Array[i];
            }
            return max;
        }

        /// <summary>
        /// Gets the minimum value from an array segment without allocations.
        /// </summary>
        private static double GetMin(ArraySegment<double> segment)
        {
            if (segment.Count == 0)
                return double.NaN;

            double min = segment.Array[segment.Offset];
            for (int i = segment.Offset + 1; i < segment.Offset + segment.Count; i++)
            {
                if (segment.Array[i] < min)
                    min = segment.Array[i];
            }
            return min;
        }

        public double GetFrametimeMetricValue(IList<double> sequence, EMetric metric)
        {

            // Percentile metrics reversed EMetric.P99 for FPS turns into P1 for frametimes

            double metricValue;
            switch (metric)
            {
                case EMetric.Max:
                    metricValue = sequence.Max();
                    break;
                case EMetric.P99:
                    metricValue = GetPQuantileSequence(sequence, 0.01);
                    break;
                case EMetric.P95:
                    metricValue = GetPQuantileSequence(sequence, 0.05);
                    break;
                case EMetric.Average:
                case EMetric.GpuActiveAverage:
                case EMetric.CpuActiveAverage:
                    metricValue = sequence.Sum() / sequence.Count;
                    break;
                case EMetric.Median:
                    metricValue = GetPQuantileSequence(sequence, 0.5);
                    break;
                case EMetric.P5:
                    metricValue = GetPQuantileSequence(sequence, 0.95);
                    break;
                case EMetric.P1:
                case EMetric.GpuActiveP1:
                    metricValue = GetPQuantileSequence(sequence, 0.99);
                    break;
                case EMetric.P0dot2:
                    metricValue = GetPQuantileSequence(sequence, 0.998);
                    break;
                case EMetric.P0dot1:
                    metricValue = GetPQuantileSequence(sequence, 0.999);
                    break;
                case EMetric.OnePercentLowAverage:
                case EMetric.GpuActiveOnePercentLowAverage:
                    metricValue = GetPercentageHighAverageSequence(sequence, 1 - 0.01);
                    break;
                case EMetric.ZerodotTwoPercentLowAverage:
                    metricValue = GetPercentageHighAverageSequence(sequence, 1 - 0.002);
                    break;
                case EMetric.ZerodotOnePercentLowAverage:
                    metricValue = GetPercentageHighAverageSequence(sequence, 1 - 0.001);
                    break;
                case EMetric.OnePercentLowIntegral:
                    metricValue = GetPercentageHighIntegralSequence(sequence, 1 - 0.01);
                    break;
                case EMetric.ZerodotTwoPercentLowIntegral:
                    metricValue = GetPercentageHighIntegralSequence(sequence, 1 - 0.002);
                    break;
                case EMetric.ZerodotOnePercentLowIntegral:
                    metricValue = GetPercentageHighIntegralSequence(sequence, 1 - 0.001);
                    break;
                case EMetric.Min:
                    metricValue = sequence.Min();
                    break;
                case EMetric.AdaptiveStd:
                    metricValue = GetAdaptiveStandardDeviation(sequence, _options.IntervalAverageWindowTime);
                    break;
                default:
                    metricValue = double.NaN;
                    break;
            }

            return Math.Round(metricValue, _options.FpsValuesRoundingDigits, MidpointRounding.AwayFromZero);
        }

        public double GetPhysicalMetricValue(IList<double> sequence, EMetric metric, double coefficient)
        {
            double metricValue;
            switch (metric)
            {
                case EMetric.CpuFpsPerWatt:
                    if (coefficient > 0)
                        metricValue = ((sequence.Count * 1000 / sequence.Sum()) / coefficient) * 10;
                    else
                        metricValue = 0;
                    break;
                case EMetric.GpuFpsPerWatt:
                    if (coefficient > 0)
                        metricValue = ((sequence.Count * 1000 / sequence.Sum()) / coefficient) * 10;
                    else
                        metricValue = 0;
                    break;
                default:
                    metricValue = 0;
                    break;
            }

            return Math.Round(metricValue, 2, MidpointRounding.AwayFromZero);
        }

        public IList<double>[] GetDiscreteDistribution(IList<double> sequence)
        {
            var min = sequence.Min();
            var max = sequence.Max();

            var binWidth = CalculateOptimalBinWidth(sequence);
            int count = (int)Math.Round((max - min) / binWidth, 0);

            if (count == 4)
            {
                double[] minimalBinIntervals = LinearSpace(min, max, count + 1);
                var histogram = Histogram(sequence, minimalBinIntervals);
                count -= histogram.Count(bin => bin == 0);
            }

            if (count < 1)
                return Array.Empty<List<double>>();

            double[] binIntervals = LinearSpace(min, max, count + 1);
            return Distribution(sequence, binIntervals);
        }

        private double CalculateOptimalBinWidth(IList<double> sequence)
        {
            double xMax = sequence.Max(), xMin = sequence.Min();
            int minBins = 4, maxBins = 16;
            double[] N = Enumerable.Range(minBins, maxBins - minBins)
                .Select(v => (double)v).ToArray();
            double[] D = N.Select(v => (xMax - xMin) / v).ToArray();
            double[] C = new double[D.Length];

            for (int i = 0; i < N.Length; i++)
            {
                double[] binIntervals = LinearSpace(xMin, xMax, (int)N[i] + 1);
                int[] ki = Histogram(sequence, binIntervals);
                ki = ki.Skip(1).Take(ki.Length - 2).ToArray();

                double mean = ki.Average();
                double variance = ki.Select(v => Math.Pow(v - mean, 2)).Sum() / N[i];

                C[i] = (2 * mean - variance) / (Math.Pow(D[i], 2));
            }

            double minC = C.Min();
            int index = C.Select((c, ix) => new { Value = c, Index = ix })
                .Where(c => c.Value == minC).First().Index;

            // optimal bin width
            return D[index];
        }

        private int[] Histogram(IList<double> data, double[] binEdges)
        {
            int[] counts = new int[binEdges.Length - 1];

            for (int i = 0; i < binEdges.Length - 1; i++)
            {
                double lower = binEdges[i], upper = binEdges[i + 1];

                for (int j = 0; j < data.Count; j++)
                {
                    if (data[j] >= lower && data[j] <= upper)
                    {
                        counts[i]++;
                    }
                }
            }

            return counts;
        }

        private IList<double>[] Distribution(IList<double> data, double[] binEdges)
        {
            List<double>[] counts = new List<double>[binEdges.Length - 1];

            for (int i = 0; i < binEdges.Length - 1; i++)
            {
                counts[i] = new List<double>();
                double lower = binEdges[i];
                double upper = binEdges[i + 1];

                for (int j = 0; j < data.Count; j++)
                {
                    if (data[j] >= lower && data[j] <= upper)
                    {
                        counts[i].Add(data[j]);
                    }
                }
            }

            return counts;
        }

        private double[] LinearSpace(double a, double b, int count)
        {
            double[] output = new double[count];

            for (int i = 0; i < count; i++)
            {
                output[i] = a + ((i * (b - a)) / (count - 1));
            }

            return output;
        }

        public IMetricAnalysis GetMetricAnalysis(IList<double> frametimes, IList<double> displaytimes,
            bool useDisplayChangeMetrics, string secondMetric, string thirdMetric)
        {
            var average = GetFpsMetricValue(frametimes, EMetric.Average);

            double secondMetricValue;
            double thrirdMetricValue;

            if (!useDisplayChangeMetrics)
            {
                secondMetricValue = GetFpsMetricValue(frametimes, secondMetric.ConvertToEnum<EMetric>());
                thrirdMetricValue = GetFpsMetricValue(frametimes, thirdMetric.ConvertToEnum<EMetric>());
            }
            else
            {
                secondMetricValue = GetFpsMetricValue(displaytimes, secondMetric.ConvertToEnum<EMetric>());
                thrirdMetricValue = GetFpsMetricValue(displaytimes, thirdMetric.ConvertToEnum<EMetric>());
            }

            string numberFormat = string.Format("F{0}", _options.FpsValuesRoundingDigits);
            var cultureInfo = CultureInfo.InvariantCulture;

            string secondMetricString =
                secondMetric.ConvertToEnum<EMetric>() != EMetric.None ?
                " | " + $"{secondMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
                $"{secondMetricValue.ToString(numberFormat, cultureInfo)} " +
                $"FPS" : string.Empty;

            string thirdMetricString =
                thirdMetric.ConvertToEnum<EMetric>() != EMetric.None ?
                " | " + $"{thirdMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
                $"{thrirdMetricValue.ToString(numberFormat, cultureInfo)} " +
                $"FPS" : string.Empty;

            return new MetricAnalysis()
            {
                ResultString = $"Avg={average.ToString(numberFormat, cultureInfo)} " +
                $"FPS" + secondMetricString + thirdMetricString,
                Average = average,
                Second = secondMetricValue,
                Third = thrirdMetricValue
            };
        }

        public bool[] GetOutlierAnalysis(IList<IMetricAnalysis> metricAnalysisSet, string relatedMetric, int outlierPercentage)
        {
            var averageValues = metricAnalysisSet.Select(analysis => analysis.Average).ToList();
            var secondMetricValues = metricAnalysisSet.Select(analysis => analysis.Second).ToList();
            var thirdMetricValues = metricAnalysisSet.Select(analysis => analysis.Third).ToList();

            bool[] outlierFlags = Enumerable.Repeat(false, metricAnalysisSet.Count).ToArray();

            if (relatedMetric == "Average")
            {
                outlierFlags = GetOutlierFlags(averageValues, outlierPercentage);
            }
            else if (relatedMetric == "Second")
            {
                outlierFlags = GetOutlierFlags(secondMetricValues, outlierPercentage);
            }
            else if (relatedMetric == "Third")
            {
                outlierFlags = GetOutlierFlags(thirdMetricValues, outlierPercentage);
            }

            return outlierFlags;
        }

        private bool[] GetOutlierFlags(IList<double> metricValues, int outlierPercentage)
        {
            bool[] outlierFlags = Enumerable.Repeat(false, metricValues.Count).ToArray();
            var median = GetPQuantileSequence(metricValues, 0.5);

            for (int i = 0; i < metricValues.Count; i++)
            {
                if ((Math.Abs(metricValues[i] - median) / median) * 100d > outlierPercentage)
                {
                    outlierFlags[i] = true;
                }
            }

            return outlierFlags;
        }

        public IList<int> GetFpsThresholdCounts(IList<double> frametimes, bool isReversed)
        {
            var fps = frametimes.Select(ft => 1000 / ft);
            var thresholds = isReversed ? FPSTHRESHOLDS.Reverse().ToArray() : FPSTHRESHOLDS;

            var counts = new List<int>(thresholds.Length);

            foreach (var threshold in thresholds)
            {
                counts.Add(fps.Count(val => isReversed ? val > threshold : val < threshold));
            }

            return counts;
        }

        public IList<double> GetFpsThresholdTimes(IList<double> frametimes, bool isReversed)
        {
            var fps = frametimes.Select(ft => 1000 / ft);
            var thresholds = isReversed ? FPSTHRESHOLDS.Reverse().ToArray() : FPSTHRESHOLDS;

            var times = new List<double>(thresholds.Length);

            foreach (var threshold in thresholds)
            {
                times.Add(frametimes.Where(val => isReversed ? val < 1000 / threshold : val > 1000 / threshold).Sum());
            }

            return times;
        }

        public IList<double> GetFrametimeVariancePercentages(ISession session)
        {

            if (!session.Runs.Any())
                return new List<double>();


            // Create bins for variance thresholds
            int threshold2Count = 0;
            int threshold4Count = 0;
            int threshold8Count = 0;
            int threshold12Count = 0;
            int thresholdOver12Count = 0;

            // Get frametime variances
            double varianceCount = 0.0;
            foreach (var run in session.Runs)
            {
                var frametimes = run.CaptureData.MsBetweenPresents.ToArray();
                for (int i = 1; i < frametimes.Count(); i++)
                {
                    double variance = Math.Abs(frametimes[i] - frametimes[i - 1]);

                    if (variance < 2)
                        threshold2Count++;
                    else if (variance < 4)
                        threshold4Count++;
                    else if (variance < 8)
                        threshold8Count++;
                    else if (variance < 12)
                        threshold12Count++;
                    else
                        thresholdOver12Count++;

                    varianceCount++;
                }
            }


            // Add percentage of variance bins to List
            IList<double> variancePercentages = new List<double>
            {
                Math.Round(threshold2Count / varianceCount, 4, MidpointRounding.AwayFromZero),
                Math.Round(threshold4Count / varianceCount, 4, MidpointRounding.AwayFromZero),
                Math.Round(threshold8Count / varianceCount, 4, MidpointRounding.AwayFromZero),
                Math.Round(threshold12Count / varianceCount, 4, MidpointRounding.AwayFromZero),
                Math.Round(thresholdOver12Count / varianceCount, 4, MidpointRounding.AwayFromZero)
            };

            return variancePercentages;
        }
    }
}
