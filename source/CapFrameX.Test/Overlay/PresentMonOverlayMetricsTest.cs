using System;
using System.Globalization;
using System.Reactive.Subjects;
using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class PresentMonOverlayMetricsTest
    {
        [TestMethod]
        public void Snapshot_UsesIntervalMeansAndRetainsThemBetweenDeliveryWaves()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "8", "10"));
            fixture.Frames.OnNext(Row(42, "12", "20"));

            var first = fixture.Snapshot();
            AssertValues(first, 100, 10, 15);
            AssertValues(fixture.Snapshot(), 100, 10, 15);

            fixture.Frames.OnNext(Row(42, "20", "40"));
            AssertValues(fixture.Snapshot(), 50, 20, 40);
            AssertValues(first, 100, 10, 15);
        }

        [TestMethod]
        public void Snapshot_UsesOnlyDisplayedFramesForDisplayTime()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "10", "0"));
            fixture.Frames.OnNext(Row(42, "10", "20"));
            fixture.Frames.OnNext(Row(42, "10", "40"));

            AssertValues(fixture.Snapshot(), 100, 10, 30);
            fixture.Frames.OnNext(Row(42, "5", "0"));
            AssertValues(fixture.Snapshot(), 200, 5, 30);
        }

        [TestMethod]
        public void Frames_InvalidRowsAndTimesDoNotAffectTheMeans()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(null);
            fixture.Frames.OnNext(Array.Empty<string>());
            fixture.Frames.OnNext(new[] { "game.exe" });
            fixture.Frames.OnNext(PresentMonCaptureService.COLUMN_HEADER_WITH_PC_LATENCY.Split(','));
            fixture.Frames.OnNext(Row(99, "1", "1", "D3D9"));
            foreach (string invalid in new[] { "", "<error>", "NaN", "Infinity", "-Infinity", "0", "-5", "10000", "1,5" })
                fixture.Frames.OnNext(Row(42, invalid, invalid));

            AssertValues(fixture.Snapshot(), 0, 0, 0);
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                fixture.Frames.OnNext(Row(42, "12.5", "25.5"));
                AssertValues(fixture.Snapshot(), 80, 12.5, 25.5);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [TestMethod]
        public void TargetChange_DiscardsPublishedAndPendingValuesAndRuntime()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "10", "20", "DXGI"));
            AssertValues(fixture.Snapshot(), 100, 10, 20);
            fixture.Frames.OnNext(Row(42, "1", "2", "DXGI"));

            fixture.ProcessIds.OnNext(99);
            fixture.Frames.OnNext(Row(42, "1", "2", "DXGI"));
            var reset = fixture.Snapshot();
            AssertValues(reset, 0, 0, 0);
            Assert.AreEqual(CxLang.Instance.TranslateOverlay("Performance"), reset[0].GroupName);

            fixture.Frames.OnNext(Row(99, "20", "40", "D3D9"));
            var next = fixture.Snapshot();
            AssertValues(next, 50, 20, 40);
            Assert.AreEqual("D3D9", next[0].GroupName);
        }

        [TestMethod]
        public void MissingOrNegativeTarget_RejectsEveryProcess()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            foreach (int target in new[] { 0, -42 })
            {
                fixture.ProcessIds.OnNext(target);
                fixture.Frames.OnNext(Row(target, "10", "20"));
                fixture.Frames.OnNext(Row(42, "10", "20"));
                AssertValues(fixture.Snapshot(), 0, 0, 0);
            }
        }

        [TestMethod]
        public void Disable_UnsubscribesAndPreservesInputUntilEnabledAgain()
        {
            using var fixture = new Fixture();
            Assert.IsFalse(fixture.Frames.HasObservers);
            Assert.AreSame(fixture.Entries, fixture.Snapshot());
            fixture.Metrics.SetEnabled(true);
            Assert.IsTrue(fixture.Frames.HasObservers);
            fixture.Frames.OnNext(Row(42, "10", "20"));
            AssertValues(fixture.Snapshot(), 100, 10, 20);

            fixture.Metrics.SetEnabled(false);
            Assert.IsFalse(fixture.Frames.HasObservers);
            Assert.AreSame(fixture.Entries, fixture.Snapshot());
            fixture.Frames.OnNext(Row(42, "1", "2"));
            fixture.Metrics.SetEnabled(true);
            AssertValues(fixture.Snapshot(), 0, 0, 0);
        }

        [TestMethod]
        public void RepeatedEnable_PreservesTheCurrentInterval()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "10", "20"));
            fixture.Metrics.SetEnabled(true);
            fixture.ProcessIds.OnNext(42);

            AssertValues(fixture.Snapshot(), 100, 10, 20);
        }

        [TestMethod]
        public void CaptureStop_ResetsValuesAndRejectsRowsUntilRestart()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "10", "20"));
            AssertValues(fixture.Snapshot(), 100, 10, 20);

            fixture.CaptureRunning.OnNext(false);
            fixture.Frames.OnNext(Row(42, "1", "2"));
            var stopped = fixture.Snapshot();
            AssertValues(stopped, 0, 0, 0);
            Assert.AreEqual(CxLang.Instance.TranslateOverlay("Performance"), stopped[0].GroupName);

            fixture.CaptureRunning.OnNext(true);
            fixture.Frames.OnNext(Row(42, "20", "40"));
            AssertValues(fixture.Snapshot(), 50, 20, 40);
        }

        [TestMethod]
        public void Snapshot_FillsHiddenEntriesAndResolvesLabelsWithoutEditingTheProfile()
        {
            using var fixture = new Fixture();
            fixture.Entries[0].GroupName = "Game <APP> metrics";
            fixture.Entries[0].ShowOnOverlay = false;
            fixture.Entries[1].IsEntryEnabled = false;
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "10", "20", " DXGI "));
            fixture.Frames.OnNext(Row(42, "10", "20", "<error>"));

            var snapshot = fixture.Snapshot();
            AssertValues(snapshot, 100, 10, 20);
            Assert.AreEqual("Game DXGI metrics", snapshot[0].GroupName);
            Assert.AreEqual("Game <APP> metrics", fixture.Entries[0].GroupName);
            Assert.IsNull(fixture.Entries[0].Value);
            Assert.IsFalse(snapshot[0].ShowOnOverlay);
            Assert.IsFalse(snapshot[1].IsEntryEnabled);
            Assert.AreNotSame(fixture.Entries[0], snapshot[0]);
            Assert.AreSame(fixture.Entries[3], snapshot[3], "Unrelated sensor entries retain their identity.");
        }

        [TestMethod]
        public void Snapshot_ResolvesRuntimeOnNonScalarEntries()
        {
            using var fixture = new Fixture();
            fixture.Entries[3].GroupName = "<APP>";
            fixture.Metrics.SetEnabled(true);
            fixture.Frames.OnNext(Row(42, "10", "20", "D3D9"));

            var snapshot = fixture.Snapshot();
            Assert.AreEqual("D3D9", snapshot[3].GroupName);
            Assert.AreEqual("<APP>", fixture.Entries[3].GroupName);
            Assert.AreEqual(55d, snapshot[3].Value);
        }

        [TestMethod]
        public void Dispose_ReleasesAllSubscriptionsAndCannotRestart()
        {
            using var fixture = new Fixture();
            fixture.Metrics.SetEnabled(true);
            fixture.Metrics.Dispose();
            fixture.Metrics.SetEnabled(true);

            Assert.IsFalse(fixture.Frames.HasObservers);
            Assert.IsFalse(fixture.ProcessIds.HasObservers);
            Assert.IsFalse(fixture.CaptureRunning.HasObservers);
            Assert.AreSame(fixture.Entries, fixture.Snapshot());
        }

        private static string[] Row(int processId, string frametime, string displayTime, string runtime = "DXGI")
        {
            var row = new string[PresentMonCaptureService.MsBetweenDisplayChange_INDEX + 1];
            row[PresentMonCaptureService.ProcessID_INDEX] = processId.ToString(CultureInfo.InvariantCulture);
            row[PresentMonCaptureService.PresentRuntime_INDEX] = runtime;
            row[PresentMonCaptureService.MsBetweenPresents_INDEX] = frametime;
            row[PresentMonCaptureService.MsBetweenDisplayChange_INDEX] = displayTime;
            return row;
        }

        private static void AssertValues(IOverlayEntry[] snapshot, double fps, double frametime, double displayTime)
        {
            Assert.AreEqual(fps, (double)snapshot[0].Value, 1e-9);
            Assert.AreEqual(frametime, (double)snapshot[1].Value, 1e-9);
            Assert.AreEqual(displayTime, (double)snapshot[2].Value, 1e-9);
            Assert.IsTrue(snapshot[0].IsNumeric);
            Assert.IsTrue(snapshot[1].IsNumeric);
            Assert.IsTrue(snapshot[2].IsNumeric);
        }

        private sealed class Fixture : IDisposable
        {
            public Subject<string[]> Frames { get; } = new Subject<string[]>();
            public BehaviorSubject<int> ProcessIds { get; } = new BehaviorSubject<int>(42);
            public BehaviorSubject<bool> CaptureRunning { get; } = new BehaviorSubject<bool>(true);
            public PresentMonOverlayMetrics Metrics { get; }
            public IOverlayEntry[] Entries { get; } = new IOverlayEntry[]
            {
                new OverlayEntryWrapper("Framerate") { GroupName = "<APP>" },
                new OverlayEntryWrapper("Frametime") { GroupName = "<APP>" },
                new OverlayEntryWrapper("DisplayTime") { GroupName = "Displaytime" },
                new OverlayEntryWrapper("CPU Temperature") { GroupName = "CPU", Value = 55d }
            };

            public Fixture()
            {
                var capture = new Mock<ICaptureService>();
                capture.SetupGet(service => service.FrameDataStream).Returns(Frames);
                capture.SetupGet(service => service.CaptureServiceRunningStream).Returns(CaptureRunning);
                var process = new Mock<IRTSSService>();
                process.SetupGet(service => service.ProcessIdStream).Returns(ProcessIds);
                Metrics = new PresentMonOverlayMetrics(capture.Object, process.Object);
            }

            public IOverlayEntry[] Snapshot() => Metrics.ApplySnapshot(Entries);

            public void Dispose()
            {
                Metrics.Dispose();
                Frames.Dispose();
                ProcessIds.Dispose();
                CaptureRunning.Dispose();
                foreach (var entry in Entries)
                    entry.Dispose();
            }
        }
    }
}
