using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CapFrameX.Statistics.NetStandard
{
	public static class SessionExtensions
	{
		[Flags]
		private enum TimingSeries
		{
			StartTimes = 1,
			Frametimes = 2,
			DisplayChangeTimes = 4,
			GpuActiveTimes = 8,
			CpuActiveTimes = 16,
			AnimationErrors = 32,
			PcLatencies = 64
		}

		private sealed class SessionTimingData
		{
			public ISessionRun[] Runs;
			public ISessionCaptureData[] CaptureData;
			public double[][] StartTimeSources;
			public double[][] FrametimeSources;
			public double[][] DisplayChangeSources;
			public double[][] GpuActiveSources;
			public double[][] CpuActiveSources;
			public double[][] AnimationErrorSources;
			public double[][] PcLatencySources;
			public double[] StartTimes;
			public double[] Frametimes;
			public double[] DisplayChangeTimes;
			public double[] GpuActiveTimes;
			public double[] CpuActiveTimes;
			public double[] AnimationErrors;
			public double[] PcLatencies;
		}

		private static readonly ConditionalWeakTable<ISession, SessionTimingData> TimingDataCache =
			new ConditionalWeakTable<ISession, SessionTimingData>();

		private static SessionTimingData GetTimingData(ISession session, TimingSeries requestedSeries)
		{
			var timingData = TimingDataCache.GetValue(session, _ => new SessionTimingData());
			lock (timingData)
			{
				if (!IsTimingStructureCurrent(session, timingData))
				{
					timingData.Runs = session?.Runs?.ToArray() ?? Array.Empty<ISessionRun>();
					timingData.CaptureData = timingData.Runs
						.Select(run => run?.CaptureData).ToArray();
					ClearTimingSeries(timingData);
				}

				if ((requestedSeries & TimingSeries.StartTimes) != 0)
					EnsureTimingSeries(session, timingData, data => data.TimeInSeconds,
						run => run.CaptureData.TimeInSeconds, ref timingData.StartTimeSources, ref timingData.StartTimes);
				if ((requestedSeries & TimingSeries.Frametimes) != 0)
					EnsureTimingSeries(session, timingData, data => data.MsBetweenPresents,
						run => run.CaptureData.MsBetweenPresents, ref timingData.FrametimeSources, ref timingData.Frametimes);
				if ((requestedSeries & TimingSeries.DisplayChangeTimes) != 0)
					EnsureTimingSeries(session, timingData, data => data.MsBetweenDisplayChange,
						run => run.CaptureData.MsBetweenDisplayChange, ref timingData.DisplayChangeSources, ref timingData.DisplayChangeTimes);
				if ((requestedSeries & TimingSeries.GpuActiveTimes) != 0)
					EnsureTimingSeries(session, timingData, data => data.GpuActive,
						run => run.CaptureData.GpuActive, ref timingData.GpuActiveSources, ref timingData.GpuActiveTimes);
				if ((requestedSeries & TimingSeries.CpuActiveTimes) != 0)
					EnsureTimingSeries(session, timingData, data => data.CpuActive,
						run => run.CaptureData.CpuActive, ref timingData.CpuActiveSources, ref timingData.CpuActiveTimes);
				if ((requestedSeries & TimingSeries.AnimationErrors) != 0)
					EnsureTimingSeries(session, timingData, data => data.AnimationError,
						run => run.CaptureData.AnimationError, ref timingData.AnimationErrorSources, ref timingData.AnimationErrors);
				if ((requestedSeries & TimingSeries.PcLatencies) != 0)
					EnsureTimingSeries(session, timingData, data => data.PcLatency,
						run => run.CaptureData.PcLatency, ref timingData.PcLatencySources, ref timingData.PcLatencies);

				return timingData;
			}
		}

		private static bool IsTimingStructureCurrent(ISession session, SessionTimingData timingData)
		{
			if (session?.Runs == null || timingData.Runs == null
				|| timingData.Runs.Length != session.Runs.Count)
				return false;

			for (int i = 0; i < timingData.Runs.Length; i++)
			{
				var run = session.Runs[i];
				var captureData = run?.CaptureData;
				if (!ReferenceEquals(timingData.Runs[i], run)
					|| !ReferenceEquals(timingData.CaptureData[i], captureData))
					return false;
			}

			return true;
		}

		private static void EnsureTimingSeries(ISession session, SessionTimingData timingData,
			Func<ISessionCaptureData, double[]> captureSelector, Func<ISessionRun, double[]> runSelector,
			ref double[][] sources, ref double[] flattened)
		{
			bool isCurrent = sources != null && sources.Length == timingData.CaptureData.Length;
			if (isCurrent)
			{
				for (int i = 0; i < sources.Length; i++)
				{
					var current = timingData.CaptureData[i] == null
						? null : captureSelector(timingData.CaptureData[i]);
					if (!ReferenceEquals(sources[i], current))
					{
						isCurrent = false;
						break;
					}
				}
			}

			if (isCurrent && timingData.Runs.Length > 1)
				isCurrent = FlattenedValuesMatch(sources, flattened);

			if (!isCurrent)
			{
				sources = GetSourceArrays(timingData.CaptureData, captureSelector);
				flattened = FlattenOrReuse(session, runSelector);
			}
		}

		private static void ClearTimingSeries(SessionTimingData timingData)
		{
			timingData.StartTimeSources = null;
			timingData.FrametimeSources = null;
			timingData.DisplayChangeSources = null;
			timingData.GpuActiveSources = null;
			timingData.CpuActiveSources = null;
			timingData.AnimationErrorSources = null;
			timingData.PcLatencySources = null;
			timingData.StartTimes = null;
			timingData.Frametimes = null;
			timingData.DisplayChangeTimes = null;
			timingData.GpuActiveTimes = null;
			timingData.CpuActiveTimes = null;
			timingData.AnimationErrors = null;
			timingData.PcLatencies = null;
		}

		private static bool FlattenedValuesMatch(double[][] sources, double[] flattened)
		{
			int index = 0;
			foreach (var source in sources)
			{
				if (source == null)
					continue;

				for (int i = 0; i < source.Length; i++)
				{
					if (index >= flattened.Length
						|| BitConverter.DoubleToInt64Bits(source[i]) != BitConverter.DoubleToInt64Bits(flattened[index]))
						return false;
					index++;
				}
			}

			return index == flattened.Length;
		}

		private static double[][] GetSourceArrays(ISessionCaptureData[] captureData,
			Func<ISessionCaptureData, double[]> selector)
		{
			return captureData.Select(data => data == null ? null : selector(data)).ToArray();
		}

		private static T[] FlattenOrReuse<T>(ISession session, Func<ISessionRun, T[]> selector)
		{
			if (session?.Runs == null || session.Runs.Count == 0)
				return Array.Empty<T>();
			if (session.Runs.Count == 1)
				return selector(session.Runs[0]) ?? Array.Empty<T>();

			return session.Runs.SelectMany(run => selector(run) ?? Array.Empty<T>()).ToArray();
		}

		private static IList<double> FilterDataWithinTimeWindow(IList<double> startTimes, IList<double> data,
            double startTime, double endTime, FrametimeStatisticProvider statisticProvider = null,
            ERemoveOutlierMethod removeOutlierMethod = ERemoveOutlierMethod.None,
            Func<double, bool> isValidValue = null)
		{
            int count = Math.Min(startTimes.Count, data.Count);
            var filteredData = new List<double>(count);
            var valueValidator = isValidValue ?? IsValidTimingValue;

            for (int i = 0; i < count; i++)
            {
                double time = startTimes[i];
                double value = data[i];

                if (time >= startTime && time <= endTime && valueValidator(value))
                {
                    filteredData.Add(value);
                }
            }

            return statisticProvider == null
                ? filteredData
                : statisticProvider.GetOutlierAdjustedSequence(filteredData, removeOutlierMethod);
		}

		private static IList<Point> FilterDataPointsWithinTimeWindow(IList<double> startTimes, IList<double> data,
            double startTime, double endTime, FrametimeStatisticProvider statisticProvider = null,
            ERemoveOutlierMethod removeOutlierMethod = ERemoveOutlierMethod.None,
            Func<double, bool> isValidValue = null)
		{
            int count = Math.Min(startTimes.Count, data.Count);
            var filteredPoints = new List<Point>(count);
            var valueValidator = isValidValue ?? IsValidTimingValue;

            for (int i = 0; i < count; i++)
            {
                double time = startTimes[i];
                double value = data[i];

                if (time >= startTime && time <= endTime && valueValidator(value))
                {
                    filteredPoints.Add(new Point(time, value));
                }
            }

            if (removeOutlierMethod == ERemoveOutlierMethod.DeciPercentile && filteredPoints.Count > 0)
            {
                var adjustedValues = statisticProvider.GetOutlierAdjustedSequence(
                    filteredPoints.Select(point => point.Y).ToArray(), removeOutlierMethod);
                var remainingValueCounts = adjustedValues
                    .GroupBy(value => value)
                    .ToDictionary(group => group.Key, group => group.Count());
                var adjustedPoints = new List<Point>(adjustedValues.Count);

                foreach (Point point in filteredPoints)
                {
                    int remainingCount;
                    if (remainingValueCounts.TryGetValue(point.Y, out remainingCount) && remainingCount > 0)
                    {
                        adjustedPoints.Add(point);
                        remainingValueCounts[point.Y] = remainingCount - 1;
                    }
                }

                return adjustedPoints;
            }

            return filteredPoints;
		}

        private static bool IsValidTimingValue(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsValidActiveTimeValue(double value)
        {
            return value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

		public static IList<double> GetFrametimeTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.Frametimes);

            return FilterDataWithinTimeWindow(timingData.StartTimes, timingData.Frametimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod);
        }

        public static IList<double> GetDisplayChangeTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.DisplayChangeTimes);

            return FilterDataWithinTimeWindow(timingData.StartTimes, timingData.DisplayChangeTimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod);
        }

        public static IList<double> GetGpuActiveTimeTimeWindow(this ISession session, double startTime, double endTime,
			IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.GpuActiveTimes);

			return FilterDataWithinTimeWindow(timingData.StartTimes, timingData.GpuActiveTimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod, IsValidActiveTimeValue);
		}

        public static IList<double> GetCpuActiveTimeTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.CpuActiveTimes);

            return FilterDataWithinTimeWindow(timingData.StartTimes, timingData.CpuActiveTimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod, IsValidActiveTimeValue);
        }

        public static IList<double> GetAnimationErrorTimeWindow(this ISession session, double startTime, double endTime)
        {
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.AnimationErrors);
            int count = Math.Min(timingData.StartTimes.Length, timingData.AnimationErrors.Length);
            var values = new List<double>(count);

            for (int i = 0; i < count; i++)
            {
                if (timingData.StartTimes[i] >= startTime && timingData.StartTimes[i] <= endTime
                    && !double.IsNaN(timingData.AnimationErrors[i]) && !double.IsInfinity(timingData.AnimationErrors[i]))
                {
                    values.Add(timingData.AnimationErrors[i]);
                }
            }

            return values;
        }

        public static IList<Point> GetFrametimePointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.Frametimes);

			return FilterDataPointsWithinTimeWindow(timingData.StartTimes, timingData.Frametimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod);
        }

        public static IList<Point> GetDisplayChangeTimePointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.DisplayChangeTimes);

            return FilterDataPointsWithinTimeWindow(timingData.StartTimes, timingData.DisplayChangeTimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod);
        }

        public static IList<Point> GetGpuActiveTimePointsTimeWindow(this ISession session, double startTime, double endTime,
			IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
		{
			var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.GpuActiveTimes);

			return FilterDataPointsWithinTimeWindow(timingData.StartTimes, timingData.GpuActiveTimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod, IsValidActiveTimeValue);
		}

        public static IList<Point> GetCpuActiveTimePointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.CpuActiveTimes);

            return FilterDataPointsWithinTimeWindow(timingData.StartTimes, timingData.CpuActiveTimes, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod, IsValidActiveTimeValue);
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
                powerValuesFiltered = session.Runs.Where(r => r.PmdCpuPower != null && r.PmdCpuPower.Length > 0);
            else if (hardware == "GPU")
                powerValuesFiltered = session.Runs.Where(r => r.PmdGpuPower != null && r.PmdGpuPower.Length > 0);

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
                powerValuesFiltered = session.Runs.Where(r => r.PmdCpuPower != null && r.PmdCpuPower.Length > 0);
            else if (hardware == "GPU")
                powerValuesFiltered = session.Runs.Where(r => r.PmdGpuPower != null && r.PmdGpuPower.Length > 0);


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
            var filteredTimes = session.Runs.Where(r => r.SensorData2.MeasureTime.Values != null && r.SensorData2.MeasureTime.Values.Count > 0);
            if (filteredTimes == null || !filteredTimes.Any())
                return null;

            // Get Measure Times
            var times = filteredTimes.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();

            // Search for Power Values
            IEnumerable<ISessionRun> powerValuesFiltered = null;
            if (hardware == "CPU")
                powerValuesFiltered = session.Runs.Where(r => r.SensorData2.CpuPower != null && r.SensorData2.CpuPower.Length > 0);
            else if (hardware == "GPU")
            {
                if (useTBP)
                {
                    powerValuesFiltered = session.Runs.Where(r => r.SensorData2.GpuTBPSim != null && r.SensorData2.GpuTBPSim.Length > 0);

                    if (powerValuesFiltered == null || !powerValuesFiltered.Any())
                        powerValuesFiltered = session.Runs.Where(r => r.SensorData2.GpuPower != null && r.SensorData2.GpuPower.Length > 0);
                }
                else
                    powerValuesFiltered = session.Runs.Where(r => r.SensorData2.GpuPower != null && r.SensorData2.GpuPower.Length > 0);
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

            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.PcLatencies);
            var times = timingData.StartTimes;
            var latencies = timingData.PcLatencies;
            int count = Math.Min(times.Length, latencies.Length);

            if (latencies.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], latencies[i]));
                }
            }

            return list;
        }

        public static IList<Point> GetAnimationErrorPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();

            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.AnimationErrors);
            var times = timingData.StartTimes;
            var animationErrors = timingData.AnimationErrors;
            int count = Math.Min(times.Length, animationErrors.Length);

            if (animationErrors.Any())
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new Point(times[i], animationErrors[i]));
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
                    var timingData = GetTimingData(session, TimingSeries.StartTimes);
                    var timeIntervalAveragePoints = timeIntervalAverageFilter
                        .ProcessSamples(intervalFrametimePoints, startTime, endTime, timingData.StartTimes.Last());
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
                    var timingData = GetTimingData(session, TimingSeries.StartTimes);
                    var timeIntervalAveragePoints = timeIntervalAverageFilter
                        .ProcessSamples(intervalDisplayChangeTimePoints, startTime, endTime, timingData.StartTimes.Last());
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
					var timingData = GetTimingData(session, TimingSeries.StartTimes);
					var timeIntervalAveragePoints = timeIntervalAverageFilter
						.ProcessSamples(intervalGpuActiveTimePoints, startTime, endTime, timingData.StartTimes.Last());
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
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.Frametimes);

            return GetTimingDistributionPoints(timingData.StartTimes, timingData.Frametimes, startTime, endTime,
                options, eRemoveOutlierMethod);
        }

        public static IList<Point> GetDisplayTimeDistributionPoints(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            var timingData = GetTimingData(session, TimingSeries.StartTimes | TimingSeries.DisplayChangeTimes);

            return GetTimingDistributionPoints(timingData.StartTimes, timingData.DisplayChangeTimes, startTime, endTime,
                options, eRemoveOutlierMethod);
        }

        private static IList<Point> GetTimingDistributionPoints(IList<double> frameStartTimes, IList<double> timingValues,
            double startTime, double endTime, IFrametimeStatisticProviderOptions options,
            ERemoveOutlierMethod eRemoveOutlierMethod)
        {
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var filteredFrameTimes = FilterDataWithinTimeWindow(frameStartTimes, timingValues, startTime, endTime,
                frametimeStatisticProvider, eRemoveOutlierMethod);

            if (filteredFrameTimes.Count == 0)
            {
                return new List<Point>();
            }

            const double increment = 0.1;
            double maxValue = filteredFrameTimes.Max();
            double totalSum = filteredFrameTimes.Sum();
            var binSums = new Dictionary<int, double>();

            // Build only populated bins. This is O(samples) and cannot allocate a huge
            // dense array when a capture contains a very large hitch.
            for (int i = 0; i < filteredFrameTimes.Count; i++)
            {
                double value = filteredFrameTimes[i];
                double scaledValue = value / increment;
                double roundedScaledValue = Math.Round(scaledValue);
                if (Math.Abs(scaledValue - roundedScaledValue) < 1E-9)
                    scaledValue = roundedScaledValue;

                int binIndex = (int)Math.Floor(scaledValue);

                // Bins are [start, end), except for the final bin which includes
                // the maximum. Keep an exact maximum boundary in that final bin.
                if (value == maxValue && binIndex > 0 && scaledValue == roundedScaledValue)
                {
                    binIndex--;
                }

                double currentSum;
                binSums.TryGetValue(binIndex, out currentSum);
                binSums[binIndex] = currentSum + value;
            }

            return binSums
                .OrderBy(pair => pair.Key)
                .Select(pair => new Point(
                    Math.Round((pair.Key + 1) * increment, 10),
                    pair.Value / totalSum * 100))
                .ToList();
        }
    }
}
