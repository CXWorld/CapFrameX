using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PMD;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;

namespace CapFrameX.Test.PresentMonInterface
{
    /// <summary>
    /// The online FPS metrics come from one sorted copy of the realtime window (OnlineFpsWindow)
    /// instead of one copy and one selection per metric. The values must equal the statistic
    /// provider's on the same window to the bit, and only the target's rows may reach the metric
    /// thread.
    /// </summary>
    [TestClass]
    public class OnlineFpsWindowTest
    {
        private static readonly EMetric[] WindowMetrics =
        {
            EMetric.Average, EMetric.P1, EMetric.P0dot1, EMetric.P0dot2,
            EMetric.OnePercentLowAverage, EMetric.ZerodotOnePercentLowAverage, EMetric.ZerodotTwoPercentLowAverage
        };

        private static FrametimeStatisticProvider CreateProvider(int roundingDigits)
        {
            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.SetupGet(o => o.FpsValuesRoundingDigits).Returns(roundingDigits);
            options.SetupGet(o => o.IntervalAverageWindowTime).Returns(500);
            return new FrametimeStatisticProvider(options.Object);
        }

        // Frame times as a game produces them: a base rate, noise, stutters, some invalid rows.
        private static List<double> RandomWindow(Random random, int count, bool withInvalid)
        {
            var window = new List<double>(count);
            double baseline = 2 + random.NextDouble() * 30;
            for (int i = 0; i < count; i++)
            {
                double roll = random.NextDouble();
                if (withInvalid && roll < 0.01) window.Add(0);
                else if (withInvalid && roll < 0.015) window.Add(double.NaN);
                else if (withInvalid && roll < 0.02) window.Add(-1);
                else if (roll < 0.05) window.Add(baseline * (2 + random.NextDouble() * 8));
                else if (roll < 0.06) window.Add(Math.Round(baseline, 1)); // repeated values
                else window.Add(baseline * (0.8 + random.NextDouble() * 0.4));
            }
            return window;
        }

        [TestMethod]
        public void WindowMetricsEqualTheProviderToTheBit()
        {
            var random = new Random(20261114);
            foreach (int digits in new[] { 0, 2 })
            {
                var provider = CreateProvider(digits);
                foreach (int count in new[] { 1, 2, 3, 7, 50, 999, 1000, 1001, 4800, 20000, 25000 })
                {
                    for (int run = 0; run < 3; run++)
                    {
                        var timings = RandomWindow(random, count, withInvalid: run > 0);
                        var valid = timings.Where(t => t > 0 && !double.IsNaN(t) && !double.IsInfinity(t)).ToList();
                        var window = new OnlineFpsWindow();
                        window.Load(timings);
                        Assert.AreEqual(valid.Count, window.Count);
                        foreach (var metric in WindowMetrics)
                        {
                            double expected = valid.Count == 0 ? double.NaN : provider.GetFpsMetricValue(valid, metric);
                            double actual = window.Get(metric, digits);
                            Assert.IsTrue(expected.Equals(actual),
                                $"{metric}, {count} frames, run {run}, {digits} digits: provider {expected:R}, window {actual:R}");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void QuantileIsMathNetsR8()
        {
            var random = new Random(7);
            foreach (int count in new[] { 1, 2, 5, 100, 3001 })
            {
                var data = Enumerable.Range(0, count).Select(_ => random.NextDouble() * 100).ToArray();
                var sorted = data.OrderBy(v => v).ToArray();
                foreach (double tau in new[] { 0.0, 0.001, 0.002, 0.01, 0.5, 0.99, 0.999, 1.0 })
                {
                    double expected = MathNet.Numerics.Statistics.Statistics.Quantile(data, tau);
                    Assert.AreEqual(expected, OnlineFpsWindow.Quantile(sorted, count, tau), 0.0,
                        $"tau {tau}, {count} values");
                }
            }
        }

        [TestMethod]
        public void ExeSuffixComparisonMatchesStringReplace()
        {
            string[] columns = { "game.exe", "game", "game.exe.exe", "my.exe.game.exe", ".exe", "", "game.ex",
                "GAME.EXE", "ga.exeme.exe", "x.exe" };
            string[] names = { "game", "", "my.game", "ga", "gameme", "GAME.EXE", "x", "game.ex" };
            foreach (var column in columns)
                foreach (var name in names)
                    Assert.AreEqual(column.Replace(".exe", "") == name, OnlineMetricService.EqualsWithoutExe(column, name),
                        $"'{column}' against '{name}'");
            Assert.IsFalse(OnlineMetricService.EqualsWithoutExe(null, "game"));
            Assert.IsFalse(OnlineMetricService.EqualsWithoutExe("game.exe", null));
        }

        [TestMethod]
        public void OnlyTheTargetsRowsReachTheMetricThread()
        {
            var events = new EventAggregator();
            using var service = CreateService(events, Mock.Of<IAppConfiguration>(), CreateProvider(2));
            var row = Row(1000, 16, 16, "42");
            Assert.IsFalse(service.IsRowOfCurrentProcess(row), "No target selected: no row passes.");
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 42));
            Assert.IsTrue(service.IsRowOfCurrentProcess(row));
            Assert.IsFalse(service.IsRowOfCurrentProcess(Row(1000, 16, 16, "77")), "Another process' row stops.");
            Assert.IsFalse(service.IsRowOfCurrentProcess(Row(1000, 16, 16, "420")), "A PID with the target's digits as prefix stops.");
            Assert.IsFalse(service.IsRowOfCurrentProcess(new[] { "game.exe" }), "A short row stops.");
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 0));
            Assert.IsFalse(service.IsRowOfCurrentProcess(row), "A cleared target lets no row pass.");
        }

        [TestMethod]
        public void ServiceMetricsEqualTheProviderOnTheWindowItHolds()
        {
            foreach (bool displayChange in new[] { false, true })
            {
                var events = new EventAggregator();
                var configuration = new Mock<IAppConfiguration>();
                configuration.SetupGet(c => c.UseDisplayChangeMetrics).Returns(displayChange);
                configuration.SetupGet(c => c.FpsValuesRoundingDigits).Returns(2);
                var provider = CreateProvider(2);
                using var service = CreateService(events, configuration.Object, provider);
                events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                    .Publish(new ViewMessages.CurrentProcessToCapture("game", 42));
                var update = typeof(OnlineMetricService).GetMethod("UpdateOnlineMetrics", BindingFlags.Instance | BindingFlags.NonPublic);
                var random = new Random(displayChange ? 2 : 1);
                var frametimes = new List<double>();
                var displaytimes = new List<double>();
                double time = 1000;
                for (int i = 0; i < 2500; i++)
                {
                    double frametime = 4 + random.NextDouble() * 4 + (random.NextDouble() < 0.02 ? 30 : 0);
                    double displaytime = random.NextDouble() < 0.05 ? 0 : frametime + random.NextDouble() - 0.5;
                    time += frametime;
                    frametimes.Add(frametime);
                    displaytimes.Add(displaytime);
                    update.Invoke(service, new object[] { Row(time, frametime, displaytime, "42") });
                }
                // 2500 frames of about 6.6 ms: all of them inside the 20 s window.
                Assert.IsTrue(time - 1000 < 20000, "the frames must fit the window");
                var validFrames = frametimes.Where(t => t > 0).ToList();
                var validDisplays = displaytimes.Where(t => t > 0).ToList();
                foreach (var metric in WindowMetrics)
                {
                    var source = displayChange && metric != EMetric.Average ? validDisplays : validFrames;
                    double expected = provider.GetFpsMetricValue(source, metric);
                    Assert.AreEqual(expected, service.GetOnlineFpsMetricValue(metric), 0.0,
                        $"{metric}, display change metrics {displayChange}");
                }
            }
        }

        // Regression (2026-11-14, found by Measure-CpuLoad): the demand check's timestamp started at
        // long.MinValue, "now - long.MinValue" overflowed to a negative value, the check never came
        // due, and without a telemetry lease no row reached the metric thread: the classic online
        // metrics and /api/metrics read NaN. This drives the real stream, not UpdateOnlineMetrics.
        [TestMethod]
        [DataRow(true, false, true)]   // an online metric on the overlay
        [DataRow(false, true, true)]   // a telemetry lease (tile design, remote API)
        [DataRow(false, false, false)] // neither: rows stay on the reader thread
        public void RowsOfTheTargetReachTheMetricsThroughTheStream(bool entryShown, bool lease, bool expectValues)
        {
            var events = new EventAggregator();
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupGet(c => c.FpsValuesRoundingDigits).Returns(2);
            var entry = new Mock<IOverlayEntry>();
            entry.SetupGet(e => e.ShowOnOverlay).Returns(entryShown);
            var core = new Mock<IOverlayEntryCore>();
            core.Setup(c => c.GetRealtimeMetricEntry("OnlineAverage")).Returns(entry.Object);
            using var rows = new System.Reactive.Subjects.Subject<string[]>();
            using var service = CreateService(events, configuration.Object, CreateProvider(2), rows, core.Object);
            using var telemetry = lease ? service.AcquireTelemetry() : null;
            events.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Publish(new ViewMessages.CurrentProcessToCapture("game", 42));

            double time = 1000;
            rows.OnNext(Row(time, 10, 10, "42")); // FrameDataStream.Skip(1) drops the header row
            for (int i = 0; i < 200; i++)
            {
                time += 10;
                rows.OnNext(Row(time, 10, 10, i % 2 == 0 ? "42" : "77"));
            }
            double average = double.NaN;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 3000)
            {
                average = service.GetOnlineFpsMetricValue(EMetric.Average);
                if (!expectValues || !double.IsNaN(average)) break;
                System.Threading.Thread.Sleep(20);
            }
            if (expectValues)
                Assert.AreEqual(100d, average, 0.0, "the target's 10 ms frames, the other process' rows filtered");
            else
            {
                System.Threading.Thread.Sleep(200);
                Assert.IsTrue(double.IsNaN(service.GetOnlineFpsMetricValue(EMetric.Average)),
                    "nothing demands the realtime metrics");
            }
        }

        private static string[] Row(double timestamp, double frametime, double displaytime, string pid)
        {
            var row = Enumerable.Repeat("0", 31).ToArray();
            row[0] = "game.exe";
            row[PresentMonCaptureService.ProcessID_INDEX] = pid;
            row[PresentMonCaptureService.PresentRuntime_INDEX] = "DXGI";
            row[18] = timestamp.ToString(CultureInfo.InvariantCulture);
            row[PresentMonCaptureService.MsBetweenPresents_INDEX] = frametime.ToString("R", CultureInfo.InvariantCulture);
            row[PresentMonCaptureService.MsBetweenDisplayChange_INDEX] = displaytime.ToString("R", CultureInfo.InvariantCulture);
            return row;
        }

        private static OnlineMetricService CreateService(EventAggregator events, IAppConfiguration configuration,
            IStatisticProvider provider, IObservable<string[]> rows = null, IOverlayEntryCore core = null)
        {
            var capture = new Mock<ICaptureService>();
            capture.SetupGet(c => c.FrameDataStream).Returns(rows ?? Observable.Never<string[]>());
            capture.SetupGet(c => c.CPUStartQPCTimeInMs_Index).Returns(18);
            capture.SetupGet(c => c.CpuBusy_Index).Returns(20);
            capture.SetupGet(c => c.GpuBusy_Index).Returns(24);
            var powenetics = new Mock<IPoweneticsService>();
            powenetics.SetupGet(p => p.PmdStatusStream).Returns(Observable.Never<EPmdDriverStatus>());
            var benchlab = new Mock<IBenchlabService>();
            benchlab.SetupGet(p => p.PmdServiceStatusStream).Returns(Observable.Never<EPmdServiceStatus>());
            return new OnlineMetricService(provider, capture.Object, events, core ?? Mock.Of<IOverlayEntryCore>(),
                powenetics.Object, benchlab.Object, configuration);
        }
    }
}
