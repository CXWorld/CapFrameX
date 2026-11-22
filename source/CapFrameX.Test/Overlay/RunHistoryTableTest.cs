using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using CapFrameX.Sensor;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Overlay
{
    // The run history tile (feature request 487): the runs of a session with their numbers, the
    // deviation the outlier detection compares, outliers and the aggregated result, as one text reading.
    [TestClass]
    public class RunHistoryTableTest
    {
        [TestMethod]
        public void ThePayloadCarriesTheSessionTheRunsAndTheResultInTheRenderersFormat()
        {
            var snapshot = new RunHistorySnapshot(5, "P1", "P0dot2", "Second", new[]
            {
                new RunHistoryRun(144.24, 118.31, 92.1, "P1", "P0dot2", -0.42, false),
                new RunHistoryRun(131.9, 102.6, 71.4, "P1", "P0dot2", -13.2, true)
            }, new RunHistoryRun(144.6, 119, 92.6, "P1", "P0dot2"));

            string payload = RunHistoryTablePayload.Build(snapshot, true, 1, text => text);

            var lines = payload.Split('\n');
            Assert.AreEqual("RH1\t1\t5\t1\t1", lines[0], "The header names the session's runs, the digits and the reference column.");
            Assert.AreEqual("C\tAvg\tP1\tP0.2", lines[1]);
            Assert.AreEqual("R\t144.2\t118.3\t92.1\t-0.42\t0", lines[2]);
            Assert.AreEqual("R\t131.9\t102.6\t71.4\t-13.20\t1", lines[3], "An outlier carries its flag.");
            Assert.AreEqual("A\t144.6\t119.0\t92.6", lines[4]);
            Assert.AreEqual("T\tResult\tOff", lines[5], "The result label loses the colon of the classic rows.");
        }

        [TestMethod]
        public void TheReferenceColumnFollowsTheOutlierMetricAndSkipsMetricsThatAreSwitchedOff()
        {
            RunHistorySnapshot Snapshot(string second, string third, string reference) =>
                new RunHistorySnapshot(3, second, third, reference, Array.Empty<RunHistoryRun>(), null);
            string Header(RunHistorySnapshot snapshot) => RunHistoryTablePayload.Build(snapshot, true, 1, t => t).Split('\n')[0];
            string Captions(RunHistorySnapshot snapshot) => RunHistoryTablePayload.Build(snapshot, true, 1, t => t).Split('\n')[1];

            Assert.IsTrue(Header(Snapshot("P1", "P0dot2", "Average")).EndsWith("\t0", StringComparison.Ordinal));
            Assert.IsTrue(Header(Snapshot("P1", "P0dot2", "Third")).EndsWith("\t2", StringComparison.Ordinal));
            Assert.AreEqual("C\tAvg\tP0.2", Captions(Snapshot("None", "P0dot2", "Third")));
            Assert.IsTrue(Header(Snapshot("None", "P0dot2", "Third")).EndsWith("\t1", StringComparison.Ordinal),
                "Without a second metric the third one is the second column.");
            Assert.IsTrue(Header(Snapshot("None", "P0dot2", "Second")).EndsWith("\t-1", StringComparison.Ordinal),
                "A switched off reference metric has no column to compare.");
            Assert.AreEqual("C\tAvg", Captions(Snapshot("None", "None", "Average")));
        }

        [TestMethod]
        public void ARunOfAnotherMetricLeavesItsCellsEmptyAndADisabledHistorySaysSo()
        {
            var snapshot = new RunHistorySnapshot(3, "P1", "P0dot2", "Average", new[]
            {
                new RunHistoryRun(140, 115, 90, "P5", "P0dot2", 0, false)
            }, null);

            string payload = RunHistoryTablePayload.Build(snapshot, false, 0, text => text == "Off" ? "Aus" : text == "Result:" ? "Ergebnis:" : text);

            var lines = payload.Split('\n');
            Assert.AreEqual("RH1\t0\t3\t0\t0", lines[0]);
            Assert.AreEqual("R\t140\t-\t90\t0.00\t0", lines[2], "A value measured in another metric is not shown under this caption.");
            Assert.AreEqual("T\tErgebnis\tAus", lines.Last());
        }

        [TestMethod]
        public void TheLargestSessionFitsTheTextReadingTheRendererAccepts()
        {
            var runs = Enumerable.Range(0, 20).Select(index => new RunHistoryRun(1444.123456 + index, 1180.5, -920.25,
                "OnePercentLowAverage", "ZeroDotOnePercentLowIntegral", -10.123456, index % 2 == 0)).ToArray();
            var snapshot = new RunHistorySnapshot(20, "OnePercentLowAverage", "ZeroDotOnePercentLowIntegral", "Third", runs, runs[0]);
            // Chinese takes three bytes per character.
            string payload = RunHistoryTablePayload.Build(snapshot, true, 6, text => text + " 运行历史结果");

            Assert.IsTrue(Encoding.UTF8.GetByteCount(payload) <= RunHistoryTablePayload.MaximumBytes,
                $"{Encoding.UTF8.GetByteCount(payload)} bytes are more than the renderer reads.");
            Assert.AreEqual(20, payload.Split('\n').Count(line => line.StartsWith("R\t", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void TextsTooLongForTheRendererLeaveTheSessionWithoutRunsInsteadOfEmptyingEveryTile()
        {
            var runs = Enumerable.Range(0, 3).Select(index => new RunHistoryRun(144, 118, 92, "P1", "P0dot2", 0, false)).ToArray();
            string payload = RunHistoryTablePayload.Build(new RunHistorySnapshot(3, "P1", "P0dot2", "Average", runs, null),
                true, 1, text => new string('x', 3000));

            Assert.IsTrue(Encoding.UTF8.GetByteCount(payload) <= RunHistoryTablePayload.MaximumBytes);
            var lines = payload.Split('\n');
            Assert.AreEqual("RH1\t1\t3\t1\t0", lines[0]);
            Assert.AreEqual("C\txxxxxxxxxxxx\txxxxxxxxxxxx\txxxxxxxxxxxx", lines[1]);
            Assert.AreEqual(2, lines.Length);
        }

        [TestMethod]
        public void TabsAndLineBreaksInTranslatedTextsCannotBreakTheTable()
        {
            string payload = RunHistoryTablePayload.Build(new RunHistorySnapshot(1, "P1", "None", "Average",
                Array.Empty<RunHistoryRun>(), null), true, 1, text => "A\tB\nC");

            var lines = payload.Split('\n');
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("C\tA B C\tA B C", lines[1]);
        }

        [TestMethod]
        public void ASessionPublishesItsRunsWithTheDeviationTheOutlierDetectionCompares()
        {
            using var fixture = new HistoryFixture(runs: 3, reference: "Second");

            fixture.AddRun(144, 118, 92);
            var first = fixture.Service.RunHistoryTable;
            Assert.AreEqual(3, first.Slots);
            Assert.AreEqual(1, first.Runs.Count);
            Assert.IsTrue(double.IsNaN(first.Runs[0].Deviation), "One run has nothing to deviate from.");

            fixture.AddRun(146, 121, 94);
            fixture.AddRun(131, 100, 70);
            var table = fixture.Service.RunHistoryTable;

            Assert.AreEqual(3, table.Runs.Count);
            // The median of the second metric is 118.
            Assert.AreEqual(0, table.Runs[0].Deviation, 1e-9);
            Assert.AreEqual((121 - 118) / 118d * 100, table.Runs[1].Deviation, 1e-9);
            Assert.AreEqual((100 - 118) / 118d * 100, table.Runs[2].Deviation, 1e-9);
            CollectionAssert.AreEqual(new[] { false, false, true }, table.Runs.Select(run => run.IsOutlier).ToArray(),
                "The run whose deviation exceeds the 3 % threshold is the outlier.");
            Assert.IsNull(table.Result, "A session with an outlier to replace has no result yet.");
            Assert.AreEqual("P1", table.SecondMetric);
            Assert.AreEqual("Second", table.ReferenceMetric);
        }

        [TestMethod]
        public void AReplacedOutlierLeavesTheTableAndTheAggregationGivesItsResult()
        {
            using var fixture = new HistoryFixture(runs: 3, reference: "Second");
            fixture.AddRun(144, 118, 92);
            fixture.AddRun(146, 121, 94);
            fixture.AddRun(131, 100, 70);

            fixture.AddRun(145, 119, 93);
            var table = fixture.Service.RunHistoryTable;

            CollectionAssert.AreEqual(new[] { 144d, 146d, 145d }, table.Runs.Select(run => run.Average).ToArray(),
                "The outlier made room for the next run.");
            Assert.IsFalse(table.Runs.Any(run => run.IsOutlier));
            Assert.IsNotNull(table.Result);
            Assert.AreEqual(145, table.Result.Average, 1e-9, "The result is the aggregation of the runs' frames.");

            fixture.Service.ResetHistory();
            Assert.AreEqual(0, fixture.Service.RunHistoryTable.Runs.Count);
            Assert.IsNull(fixture.Service.RunHistoryTable.Result);
        }

        [TestMethod]
        public async Task TheTelemetryOffersTheTableAsATextReadingThatSurvivesWithoutATarget()
        {
            using var fixture = new HistoryFixture(runs: 2, reference: "Average");
            fixture.AddRun(144, 118, 92);
            var sensors = new Mock<ISensorService>();
            sensors.Setup(service => service.GetSensorEntries()).ReturnsAsync(Array.Empty<ISensorEntry>());
            using var stream = new Subject<(DateTime, Dictionary<ISensorEntry, float>)>();
            sensors.SetupGet(service => service.SensorSnapshotStream).Returns(stream);
            using var telemetry = new OverlayTelemetryService(sensors.Object, Mock.Of<ISensorConfig>(),
                overlay: fixture.Service, appConfiguration: fixture.Configuration.Object);

            var source = (await telemetry.GetSourcesAsync()).Single(item => item.SemanticKey == RunHistoryTablePayload.SemanticKey);
            Assert.AreEqual("metric/RunHistoryTable", source.Id);
            Assert.IsTrue(source.IsText);
            Assert.IsFalse(source.IsProcessSpecific, "The table stays when no game is listed.");

            var next = telemetry.Snapshots.FirstAsync().ToTask();
            stream.OnNext((DateTime.UtcNow, new Dictionary<ISensorEntry, float>()));
            var snapshot = await next.WaitAsync(TimeSpan.FromSeconds(5));
            string payload = (string)snapshot.MetricValues[source.Id];
            StringAssert.StartsWith(payload, "RH1\t1\t2\t1\t0\nC\tAvg\tP1\tP0.2\nR\t144.0\t118.0\t92.0\t-\t0");
        }

        // A run history of `runs` runs with aggregation and outlier replacement at 3 %. Each run's
        // frame times carry its three metrics, which the statistics mock reads back.
        private sealed class HistoryFixture : IDisposable
        {
            private readonly Subject<TimeSpan> _ticks = new Subject<TimeSpan>();
            private readonly BehaviorSubject<(DateTime, Dictionary<ISensorEntry, float>)> _snapshots =
                new BehaviorSubject<(DateTime, Dictionary<ISensorEntry, float>)>((DateTime.UtcNow, new Dictionary<ISensorEntry, float>()));
            private readonly Subject<(string key, object value)> _changes = new Subject<(string key, object value)>();

            public Mock<IAppConfiguration> Configuration { get; } = new Mock<IAppConfiguration>();
            public OverlayService Service { get; }

            public HistoryFixture(int runs, string reference)
            {
                Configuration.SetupAllProperties();
                Configuration.WireOverlayRenderer(EOverlayRenderer.HookFree);
                Configuration.SetupGet(config => config.OnValueChanged).Returns(_changes);
                Configuration.Object.SelectedHistoryRuns = runs;
                Configuration.Object.RunHistorySecondMetric = "P1";
                Configuration.Object.RunHistoryThirdMetric = "P0dot2";
                Configuration.Object.UseRunHistory = true;
                Configuration.Object.UseAggregation = true;
                Configuration.Object.OutlierHandling = "Replace";
                Configuration.Object.RelatedMetricOverlay = reference;
                Configuration.Object.OutlierPercentageOverlay = 3;
                Configuration.Object.FpsValuesRoundingDigits = 1;

                var statistics = new Mock<IStatisticProvider>();
                statistics.Setup(provider => provider.GetMetricAnalysis(It.IsAny<IList<double>>(), It.IsAny<IList<double>>(),
                        It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((IList<double> frametimes, IList<double> _, bool _, string _, string _) =>
                    {
                        // A run carries (average, second, third); the aggregation all runs one after another.
                        var triples = frametimes.Chunk(3).ToArray();
                        return new MetricAnalysis
                        {
                            Average = triples.Average(t => t[0]), Second = triples.Average(t => t[1]), Third = triples.Average(t => t[2]),
                            ResultString = "result"
                        };
                    });
                statistics.Setup(provider => provider.GetPQuantileSequence(It.IsAny<IList<double>>(), It.IsAny<double>()))
                    .Returns((IList<double> values, double p) => Median(values));
                statistics.Setup(provider => provider.GetOutlierAnalysis(It.IsAny<IList<IMetricAnalysis>>(), It.IsAny<string>(), It.IsAny<int>()))
                    .Returns((IList<IMetricAnalysis> analyses, string metric, int percentage) =>
                    {
                        var values = analyses.Select(a => metric == "Second" ? a.Second : metric == "Third" ? a.Third : a.Average).ToList();
                        double median = Median(values);
                        return values.Select(value => Math.Abs(value - median) / median * 100 > percentage).ToArray();
                    });

                var sensors = new Mock<ISensorService>();
                sensors.Setup(service => service.GetSensorEntries()).ReturnsAsync(Array.Empty<ISensorEntry>());
                sensors.SetupGet(service => service.OsdUpdateStream).Returns(_ticks);
                sensors.SetupGet(service => service.SensorSnapshotStream).Returns(_snapshots);
                var rtss = new Mock<IRTSSService>();
                var passthrough = new Mock<IOverlayFrameMetrics>();
                passthrough.Setup(metrics => metrics.ApplySnapshot(It.IsAny<IOverlayEntry[]>())).Returns((IOverlayEntry[] entries) => entries);
                var records = new Mock<IRecordManager>();
                records.Setup(manager => manager.SaveSessionRunsToFile(It.IsAny<IEnumerable<ISessionRun>>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ISessionInfo>>())).ReturnsAsync(true);
                Service = new OverlayService(statistics.Object, sensors.Object, new Mock<IOverlayEntryProvider>().Object,
                    Configuration.Object, Mock.Of<ILogger<OverlayService>>(), records.Object, rtss.Object,
                    new OverlayEntryCore(), Mock.Of<ILogEntryManager>(), Mock.Of<IRemoteOverlayDemand>(), passthrough.Object);
                Service.ResetHistory();
            }

            public void AddRun(double average, double second, double third)
            {
                var data = new Mock<ISessionCaptureData>();
                data.SetupGet(capture => capture.MsBetweenPresents).Returns(new[] { average, second, third });
                data.SetupGet(capture => capture.MsBetweenDisplayChange).Returns(new[] { average, second, third });
                var run = new Mock<ISessionRun>();
                run.SetupGet(session => session.CaptureData).Returns(data.Object);
                Service.AddRunToHistory(run.Object, "game.exe", null);
            }

            private static double Median(IList<double> values)
            {
                var sorted = values.OrderBy(value => value).ToArray();
                return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
            }

            public void Dispose()
            {
                Service.ShutdownOverlayService();
                _ticks.Dispose();
                _snapshots.Dispose();
                _changes.Dispose();
            }
        }
    }
}
