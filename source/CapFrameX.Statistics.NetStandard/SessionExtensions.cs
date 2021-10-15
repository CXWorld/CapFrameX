using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Statistics.NetStandard
{
    public static class SessionExtensions
    {
        public static IList<double> GetFrametimeTimeWindow(this ISession session, double startTime, double endTime, IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            IList<double> frametimesTimeWindow = new List<double>();
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);
            var frameStarts = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            var frametimes = frametimeStatisticProvider?.GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod);

            if (frametimes.Any() && frameStarts.Any())
            {
                for (int i = 0; i < frametimes.Count(); i++)
                {
                    if (frameStarts[i] >= startTime && frameStarts[i] <= endTime)
                    {
                        frametimesTimeWindow.Add(frametimes[i]);
                    }
                }
            }

            return frametimesTimeWindow;
        }

        public static IList<Point> GetFrametimePointsTimeWindow(this ISession session, double startTime, double endTime, IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None)
        {
            IList<Point> frametimesPointsWindow = new List<Point>();
            var frametimeStatisticProvider = new FrametimeStatisticProvider(options);

            var frametimes = frametimeStatisticProvider?.GetOutlierAdjustedSequence(session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToArray(), eRemoveOutlierMethod);
            var frameStarts = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).ToArray();
            if (frametimes.Any() && frameStarts.Any())
            {
                for (int i = 0; i < frametimes.Count(); i++)
                {
                    if (frameStarts[i] >= startTime && frameStarts[i] <= endTime)
                    {
                        frametimesPointsWindow.Add(new Point(frameStarts[i], frametimes[i]));
                    }
                }
            }

            return frametimesPointsWindow;
        }

        /// <summary>
        /// Source: https://github.com/GameTechDev/PresentMon
        /// Formular: LatencyMs =~ MsBetweenPresents + MsUntilDisplayed - previous(MsInPresentAPI)
        /// </summary>
        /// <returns></returns>

        public static IList<double> CalculateInputLagTimes( this ISession session, EInputLagType type)
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
                        var lowerBoundInputLagTime = untilDisplayedTimes[i];

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
                int hz = (int)Math.Round(value, 0);

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
            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var loads = session.Runs.SelectMany(r => r.SensorData2.GpuUsage).ToArray();

            if (loads.Any())
            {
                for (int i = 0; i < times.Count(); i++)
                {
                    list.Add(new Point(times[i], loads[i]));
                }
            }
            return list;
        }

        public static IList<Point> GetCPULoadPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();
            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var loads = session.Runs.SelectMany(r => r.SensorData2.CpuUsage).ToArray();

            if (loads.Any())
            {
                for (int i = 0; i < times.Count(); i++)
                {
                    list.Add(new Point(times[i], loads[i]));
                }
            }
            return list;
        }

        public static IList<Point> GetCPUMaxThreadLoadPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();
            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var loads = session.Runs.SelectMany(r => r.SensorData2.CpuMaxThreadUsage).ToArray();

            if (loads.Any())
            {
                for (int i = 0; i < times.Count(); i++)
                {
                    list.Add(new Point(times[i], loads[i]));
                }
            }
            return list;
        }

        public static IList<Point> GetGpuPowerLimitPointTimeWindow(this ISession session)
        {
            var list = new List<Point>();
            var times = session.Runs.SelectMany(r => r.SensorData2.MeasureTime.Values).ToArray();
            var limits = session.Runs.SelectMany(r => r.SensorData2.GPUPowerLimit).Select(limit => limit * 100).ToArray();

            if (limits.Any())
            {
                for (int i = 0; i < times.Count(); i++)
                {
                    list.Add(new Point(times[i], limits[i]));
                }
            }
            return list;
        }

        public static IList<Point> GetFpsPointsTimeWindow(this ISession session, double startTime, double endTime,
            IFrametimeStatisticProviderOptions options, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None,
            EFilterMode filterMode = EFilterMode.None)
        {
            IList<Point> fpsPoints = null;
            var frametimePoints = session.GetFrametimePointsTimeWindow(startTime, endTime, options, eRemoveOutlierMethod);
            var intervalFrametimePoints = session.GetFrametimePointsTimeWindow(0, endTime, options, eRemoveOutlierMethod);
            switch (filterMode)
            {
                case EFilterMode.TimeIntervalAverage:
                    var timeIntervalAverageFilter = new IntervalTimeAverageFilter(options.IntervalAverageWindowTime);
                    var timeIntervalAveragePoints = timeIntervalAverageFilter
                        .ProcessSamples(intervalFrametimePoints.Select(pnt => pnt.Y).ToList(), startTime * 1000, endTime * 1000, session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last() * 1000);
                    fpsPoints = timeIntervalAveragePoints.Select(pnt => new Point(pnt.X / 1000, 1000 / pnt.Y)).ToList();
                    break;
                default:
                    fpsPoints = frametimePoints.Select(pnt => new Point(pnt.X, 1000 / pnt.Y)).ToList();
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
    }
}
