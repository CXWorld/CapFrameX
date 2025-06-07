using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Statistics.NetStandard
{
    public static class SessionExtensions
    {
		private static IList<double> FilterDataWithinTimeWindow(IList<double> startTimes, IList<double> data, double startTime, double endTime)
		{
			return startTimes.Zip(data, (t, y) => new { t, y })
					 .Where(pair => pair.t >= startTime && pair.t <= endTime)
					 .Select(pair => pair.y)
					 .ToList();
		}

		private static IList<Point> FilterDataPointsWithinTimeWindow(IList<double> startTimes, IList<double> data, double startTime, double endTime)
		{
			return startTimes.Zip(data, (t, y) => new Point(t, y))
					 .Where(point => point.X >= startTime && point.X <= endTime)
					 .ToList();
		}

		public static IList<double> GetFrametimeTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var frameStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            var frametimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod)
                ?? Enumerable.Empty<double>().ToList();

            return FilterDataWithinTimeWindow(frameStartTimes, frametimes, startTime, endTime);
        }

        public static IList<double> GetDisplayChangeTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var dispalyStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            var displayChangeTimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenDisplayChange).ToArray(), eRemoveOutlierMethod)
                ?? Enumerable.Empty<double>().ToList();

            return FilterDataWithinTimeWindow(dispalyStartTimes, displayChangeTimes, startTime, endTime);
        }

        public static IList<double> GetGpuActiveTimeTimeWindow(this ISession session, double startTime, double endTime,
			IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
			var frameStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();

            var gpuActiveTimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.GpuActive).ToArray(), eRemoveOutlierMethod)
				?? Enumerable.Empty<double>().ToList();

			return FilterDataWithinTimeWindow(frameStartTimes, gpuActiveTimes, startTime, endTime);
		}

		public static IList<Point> GetFrametimePointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
			var frameStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
			var frametimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod)
				?? Enumerable.Empty<double>().ToList();

			return FilterDataPointsWithinTimeWindow(frameStartTimes, frametimes, startTime, endTime);
        }

        public static IList<Point> GetDisplayChangeTimePointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var displayStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            var displayChangeTimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenDisplayChange).ToArray(), eRemoveOutlierMethod)
                ?? Enumerable.Empty<double>().ToList();

            return FilterDataPointsWithinTimeWindow(displayStartTimes, displayChangeTimes, startTime, endTime);
        }

        public static IList<Point> GetGpuActiveTimePointsTimeWindow(this ISession session, double startTime, double endTime,
			IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
			var frameStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();

			var gpuActiveTimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.GpuActive).ToArray(), eRemoveOutlierMethod)
				?? Enumerable.Empty<double>().ToList();

			return FilterDataPointsWithinTimeWindow(frameStartTimes, gpuActiveTimes, startTime, endTime);
		}

		public static IList<Point> GetFrametimePoints(this ISession session)
        {
            if (!session.Runs.Any())
                return null;

            var frametimesPointsWindow = new List<Point>();
            var frametimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray();
            var frameStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            if (frametimes.Any() && frameStartTimes.Any())
            {
                for (int i = 0; i < frametimes.Count(); i++)
                {
                    frametimesPointsWindow.Add(new Point(frameStartTimes[i], frametimes[i]));
                }
            }

            return frametimesPointsWindow;
        }

        public static IList<Point> GetPmdPowerPoints(this ISession session, string hardware)
        {
            if (!session.Runs.Any())
                return null;

            var pmdPowerPoints = new List<Point>();
            IEnumerable<ISessionRun> powerValuesFiltered = null;

            if (hardware == "CPU")
                powerValuesFiltered = session.Runs.Where(r => !r.PmdCpuPower.IsNullOrEmpty());
            else if (hardware == "GPU")
                powerValuesFiltered = session.Runs.Where(r => !r.PmdGpuPower.IsNullOrEmpty());

            if (powerValuesFiltered == null || !powerValuesFiltered.Any())
                return null;

            float[] powerValues = null;

            if (hardware == "CPU")
                powerValues = powerValuesFiltered.SelectMany(r => r.PmdCpuPower).ToArray();
            else if (hardware == "GPU")
                powerValues = powerValuesFiltered.SelectMany(r => r.PmdGpuPower).ToArray();

            if (powerValues == null)
                return null;

            var startTimes = powerValues.Select((x, i) => 1E-03 * i * session.Runs.First().SampleTime).ToArray();

            if (powerValues.Any() && startTimes.Any())
            {
                for (int i = 0; i < Math.Min(powerValues.Length, startTimes.Length); i++)
                {
                    pmdPowerPoints.Add(new Point(startTimes[i], powerValues[i]));
                }
            }

            return pmdPowerPoints;
        }

        public static IList<Point> GetAveragePmdPowerPoints(this ISession session, string hardware)
        {
            if (!session.Runs.Any())
                return null;

            var pmdPowerPoints = new List<Point>();
            IEnumerable<ISessionRun> powerValuesFiltered = null;

            if (hardware == "CPU")
                powerValuesFiltered = session.Runs.Where(r => !r.PmdCpuPower.IsNullOrEmpty());
            else if (hardware == "GPU")
                powerValuesFiltered = session.Runs.Where(r => !r.PmdGpuPower.IsNullOrEmpty());


            if (powerValuesFiltered == null || !powerValuesFiltered.Any())
                return null;

            float[] powerValues = null;

            if (hardware == "CPU")
                powerValues = powerValuesFiltered.SelectMany(r => r.PmdCpuPower).ToArray();
            else if (hardware == "GPU")
                powerValues = powerValuesFiltered.SelectMany(r => r.PmdGpuPower).ToArray();

            if (powerValues == null)
                return null;

            var startTimes = powerValues.Select((x, i) => 1E-03 * i * session.Runs.First().SampleTime).ToArray();
            var frametimeStatisticProvider = new FrametimeStatisticProvider(null);

            var avgPowerValues = frametimeStatisticProvider.GetTimeBasedMovingAverage(powerValues.Select(x => (double)x).ToList(), 2000d);

            if (avgPowerValues.Any() && startTimes.Any())
            {
                for (int i = 0; i < Math.Min(avgPowerValues.Count, startTimes.Length); i++)
                {
                    pmdPowerPoints.Add(new Point(startTimes[i], avgPowerValues[i]));
                }
            }

            return pmdPowerPoints;
        }

        public static IList<Point> GetSensorPowerPoints(this ISession session, string hardware, bool useTBP = false)
        {
            if (!session.Runs.Any() || !session.Runs.Where(r => r.SensorData2 != null).Any())
                return null;

            var list = new List<Point>();

            // Search for Measure Times
            var filteredTimes = session.Runs.Where(r => !r.SensorData2.MeasureTime.Values.IsNullOrEmpty());
            if (filteredTimes == null || !filteredTimes.Any())
                return null;

            // Get Measure Times
            var times = filteredTimes.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();

            // Search for Power Values
            IEnumerable<ISessionRun> powerValuesFiltered = null;
            if (hardware == "CPU")
                powerValuesFiltered = session.Runs.Where(r => !r.SensorData2.CpuPower.IsNullOrEmpty());
            else if (hardware == "GPU")
            {
                if (useTBP)
                {
                    powerValuesFiltered = session.Runs.Where(r => !r.SensorData2.GpuTBPSim.IsNullOrEmpty());

                    if (powerValuesFiltered == null || !powerValuesFiltered.Any())
                        powerValuesFiltered = session.Runs.Where(r => !r.SensorData2.GpuPower.IsNullOrEmpty());
                }
                else
                    powerValuesFiltered = session.Runs.Where(r => !r.SensorData2.GpuPower.IsNullOrEmpty());
            }


            if (powerValuesFiltered == null || !powerValuesFiltered.Any())
                return null;

            //Get Power Values
            int[] powers = null;
            if (hardware == "CPU")
                powers = session.Runs.SelectMany(r => r.SensorData2.CpuPower).ToArray();
            else if (hardware == "GPU")
            {
                if (useTBP)
                {
                    powers = session.Runs.SelectMany(r => r.SensorData2.GpuTBPSim).ToArray();
                    if (powers == null || !powers.Any())
                        powers = session.Runs.SelectMany(r => r.SensorData2.GpuPower).ToArray();
                }
                else
                    powers = session.Runs.SelectMany(r => r.SensorData2.GpuPower).ToArray();
            }


            if (powers == null || !powers.Any())
                return null;

            if (powers.Any())
            {
                for (int i = 0; i < Math.Min(times.Length, powers.Length); i++)
                {
                    list.Add(new Point(times[i], powers[i]));
                }
            }

            return list;
        }

        /// <summary>
        /// Source: https://github.com/GameTechDev/PresentMon
        /// Formular: LatencyMs =~ MsBetweenPresents + MsUntilDisplayed - previous(MsInPresentAPI)
        /// </summary>
        /// <returns></returns>

        public static IList<double> CalculateInputLagTimes(this ISession session, EInputLagType type)
        {
            var inputLagTimes = new List<double>();

            foreach (var run in session.Runs)
            {
                var frameTimes = run.CaptureData.MsBetweenPresents.ToArray();
                var appMissed = run.CaptureData.Dropped.ToArray();
                var untilDisplayedTimes = run.CaptureData.MsUntilDisplayed.ToArray();
                var inPresentAPITimes = run.CaptureData.MsInPresentAPI.ToArray();
                var currentRunInputLagTimes = new List<double>();

                var count = frameTimes.Count();
                var prevDisplayedFrameInputLagTime = double.NaN;
                var i = 0;
                while (i < count)
                {
                    var droppedFramesInputLagTime = 0.0;
                    while (i < count && appMissed[i])
                    {
                        droppedFramesInputLagTime += frameTimes[i];
                        ++i;
                    }

                    if (i < count)
                    {
                        var displayedFrameInputLagTime = frameTimes[i] + untilDisplayedTimes[i];

                        var upperBoundInputLagTime = prevDisplayedFrameInputLagTime + droppedFramesInputLagTime + displayedFrameInputLagTime;
                        var lowerBoundInputLagTime = double.IsNaN(upperBoundInputLagTime) ? double.NaN : untilDisplayedTimes[i];

                        if (type == EInputLagType.Expected)
                            currentRunInputLagTimes.Add(0.5 * (lowerBoundInputLagTime + upperBoundInputLagTime));
                        else if (type == EInputLagType.UpperBound)
                            currentRunInputLagTimes.Add(upperBoundInputLagTime);
                        else if (type == EInputLagType.LowerBound)
                            currentRunInputLagTimes.Add(lowerBoundInputLagTime);

                        prevDisplayedFrameInputLagTime = i > 0 ? frameTimes[i] - inPresentAPITimes[i - 1] : double.NaN;
                        ++i;
                    }
                }

                inputLagTimes.AddRange(currentRunInputLagTimes);
            }

            return inputLagTimes;
        }

        public static double GetSyncRangePercentage(this ISession session, int syncRangeLower, int syncRangeUpper)
        {
            var displayTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenDisplayChange);
            if (!displayTimes.Any())
            {
                return 0d;
            }

            bool IsInRange(double value)
            {
                int hz = (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

                if (hz >= syncRangeLower && hz <= syncRangeUpper)
                    return true;
                else
                    return false;
            };

            return displayTimes.Select(time => 1000d / time)
                .Count(hz => IsInRange(hz)) / (double)displayTimes.Count();
        }

        public static IList<Point> GetGPULoadPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();

            if (session.Runs.Any(r => r.SensorData2 == null))
                return list;

            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var loads = session.Runs.SelectMany(r => r.SensorData2.GpuUsage).ToArray();
            int count = Math.Min(times.Count(), loads.Count());

            if (loads.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], loads[i]));
                }
            }

            return list;
        }

        public static IList<Point> GetCPULoadPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();

            if (session.Runs.Any(r => r.SensorData2 == null))
                return list;

            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var loads = session.Runs.SelectMany(r => r.SensorData2.CpuUsage).ToArray();
            int count = Math.Min(times.Count(), loads.Count());

            if (loads.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], loads[i]));
                }
            }

            return list;
        }

        public static IList<Point> GetCPUMaxThreadLoadPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();

            if (session.Runs.Any(r => r.SensorData2 == null))
                return list;

            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var loads = session.Runs.SelectMany(r => r.SensorData2.CpuMaxThreadUsage).ToArray();
            int count = Math.Min(times.Count(), loads.Count());

            if (loads.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], loads[i]));
                }
            }

            return list;
        }

        public static IList<Point> GetGpuPowerLimitPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();

            if (session.Runs.Any(r => r.SensorData2 == null))
                return list;

            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var limits = session.Runs.SelectMany(r => r.SensorData2.GPUPowerLimit).Select(limit => limit * 100).ToArray();
            int count = Math.Min(times.Count(), limits.Count());

            if (limits.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], limits[i]));
                }
            }

            return list;
        }

        public static IList<Point> GetPcLatencyPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();

            if (session.Runs.Any(r => r.SensorData2 == null))
                return list;

            var times = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            var latencies = session.Runs.SelectMany(r => r.CaptureData.PcLatency).ToArray();
            int count = Math.Min(times.Count(), latencies.Count());

            if (latencies.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], latencies[i]));
                }
            }

            return list;
        }

        public static IList<Point> GetFpsPointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None,
            EFilterMode filterMode = EFilterMode.None)
        {
            IList<Point> fpsPoints = null;

            switch (filterMode)
            {
                case EFilterMode.TimeIntervalAverage:
                    var intervalFrametimePoints = session.GetFrametimePointsTimeWindow(0, endTime, options, eRemoveOutlierMethod);
                    var timeIntervalAverageFilter = new IntervalTimeAverageFilter(options.IntervalAverageWindowTime);
                    var timeIntervalAveragePoints = timeIntervalAverageFilter
                        .ProcessSamples(intervalFrametimePoints, startTime, endTime, session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last());
                    fpsPoints = timeIntervalAveragePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
                    break;
                default:
                    var frametimePoints = session.GetFrametimePointsTimeWindow(startTime, endTime, options, eRemoveOutlierMethod);
                    fpsPoints = frametimePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
                    break;
            }

            return fpsPoints;
        }

        public static IList<Point> GetDisplayFpsPointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None,
            EFilterMode filterMode = EFilterMode.None)
        {
            IList<Point> displayFpsPoints = null;

            switch (filterMode)
            {
                case EFilterMode.TimeIntervalAverage:
                    var intervalDisplayChangeTimePoints = session.GetDisplayChangeTimePointsTimeWindow(0, endTime, options, eRemoveOutlierMethod);
                    var timeIntervalAverageFilter = new IntervalTimeAverageFilter(options.IntervalAverageWindowTime);
                    var timeIntervalAveragePoints = timeIntervalAverageFilter
                        .ProcessSamples(intervalDisplayChangeTimePoints, startTime, endTime, session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last());
                    displayFpsPoints = timeIntervalAveragePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
                    break;
                default:
                    var displayChangeTimePoints = session.GetDisplayChangeTimePointsTimeWindow(startTime, endTime, options, eRemoveOutlierMethod);
                    displayFpsPoints = displayChangeTimePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
                    break;
            }

            return displayFpsPoints;
        }

        public static IList<Point> GetGpuActiveFpsPointsTimeWindow(this ISession session, double startTime, double endTime,
			IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None,
			EFilterMode filterMode = EFilterMode.None)
		{
			IList<Point> fpsPoints = null;

			switch (filterMode)
			{
				case EFilterMode.TimeIntervalAverage:
                    var intervalGpuActiveTimePoints = session.GetGpuActiveTimePointsTimeWindow(0, endTime, options, eRemoveOutlierMethod);
                    var timeIntervalAverageFilter = new IntervalTimeAverageFilter(options.IntervalAverageWindowTime);
					var timeIntervalAveragePoints = timeIntervalAverageFilter
						.ProcessSamples(intervalGpuActiveTimePoints, startTime, endTime, session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last());
					fpsPoints = timeIntervalAveragePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
					break;
				default:
                    var gpuActiveTimePoints = session.GetGpuActiveTimePointsTimeWindow(startTime, endTime, options, eRemoveOutlierMethod);
                    fpsPoints = gpuActiveTimePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
					break;
			}

			return fpsPoints;
		}

		public static bool HasValidSensorData(this ISession session)
        {
            return session.Runs.All(run => run.SensorData2 != null && run.SensorData2.MeasureTime.Values.Any());
        }

        public static string GetPresentationMode(this IEnumerable<ISessionRun> runs)
        {
            var presentModes = runs.SelectMany(r => r.CaptureData.PresentMode);
            var orderedByFrequency = presentModes.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => x.Key);
            var presentMode = (EPresentMode)orderedByFrequency.First();
            switch (presentMode)
            {
                case EPresentMode.HardwareLegacyFlip:
                case EPresentMode.HardwareLegacyCopyToFrontBuffer:
                    return "Fullscreen Exclusive";
                case EPresentMode.HardwareComposedIndependentFlip:
                case EPresentMode.HardwareIndependentFlip:
                    return "Fullscreen Optimized or Borderless";
                case EPresentMode.ComposedFlip:
                case EPresentMode.ComposedCopyWithGPUGDI:
                    return "Windowed or Borderless";
                default:
                    return "Unknown";
            }
        }

        public static double GetGpuActiveDeviationPercentage(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimesAverage = GetFrametimeTimeWindow(session, startTime, endTime, options).Average();
            var gpuActiveTimesAverage = GetGpuActiveTimeTimeWindow(session, startTime, endTime, options).Average();

            return Math.Round(Math.Abs((gpuActiveTimesAverage - frametimesAverage) / frametimesAverage  * 100), MidpointRounding.AwayFromZero);
        }

        public static IList<Point> GetFrametimeDistributionPoints(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {

            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var frameStartTimes = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            var frametimes = frametimeStatisticProvider?
                .GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod)
                ?? Enumerable.Empty<double>().ToList();

            var filteredFrameTimes = FilterDataWithinTimeWindow(frameStartTimes, frametimes, startTime, endTime);

            // Bin increments
            double increments = 0.05;
            double maxValue = filteredFrameTimes.Max();


            // Create Bins (start, end)
            List<(double start, double end)> bins = new List<(double, double)>();
            for (double start = 0; start < maxValue; start += increments)
            {
                double end = Math.Round(start + increments, 10);
                bins.Add((start, end));
            }

            // Expand last bin if maxValue doesn't fit
            if (bins.Count == 0 || bins.Last().end < maxValue)
            {
                double start = bins.Count > 0 ? bins.Last().end : 0;
                double end = Math.Round(start + increments, 10);
                bins.Add((start, end));
            }


            // Distribution list (X = bin threshold, Y = percentage)
            double totalSum = filteredFrameTimes.Sum();
            List<Point> frametimeDistribution = new List<Point>();

            foreach (var (start, end) in bins)
            {
                bool isLastBin = (start, end) == bins.Last();

                // sum of values in bin
                double binSum = filteredFrameTimes
                    .Where(w => isLastBin ? (w >= start && w <= end) : (w >= start && w < end))
                    .Sum();

                if (binSum > 0 )
                {
                    double percentage = (binSum / totalSum) * 100;
                    frametimeDistribution.Add(new Point(end, percentage));
                }
            }

            return frametimeDistribution;
        }
    }
}
