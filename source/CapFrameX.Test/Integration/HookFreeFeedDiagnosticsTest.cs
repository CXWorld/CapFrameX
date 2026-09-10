using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookFreeFeedDiagnosticsTest
    {
        private const string MainChain = "0x1A2B";
        private const string SecondChain = "0x9F9F";

        private static HookFreeFeedDiagnostics Create(List<string> lines, bool enabled = true)
        {
            return new HookFreeFeedDiagnostics(lines.Add) { Enabled = enabled };
        }

        // Steady 100 fps rows on the main chain from t0 (seconds) for the given duration.
        private static double FeedSteadyRows(HookFreeFeedDiagnostics diag, double startSeconds,
            double durationSeconds, double timestampStartMs)
        {
            double now = startSeconds;
            double t = timestampStartMs;
            while (now < startSeconds + durationSeconds)
            {
                diag.OnRowAccepted(MainChain, "DXGI", "Application", 10.0, t, 10.0, now);
                now += 0.010;
                t += 10.0;
            }
            return t;
        }

        [TestMethod]
        public void TargetPidChangeIsLoggedImmediately()
        {
            var lines = new List<string>();
            var diag = Create(lines);

            diag.OnTargetPidChanged(0, 4217, 1.0);
            diag.OnTargetPidChanged(4217, 0, 2.0);

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains(lines[0], "target PID 0 -> 4217");
            StringAssert.Contains(lines[1], "target PID 4217 -> 0");
            StringAssert.Contains(lines[1], "every PresentMon row is dropped");
        }

        [TestMethod]
        public void DisabledSwitchProducesNoLinesAtAll()
        {
            var lines = new List<string>();
            var diag = Create(lines, enabled: false);

            diag.OnTargetPidChanged(0, 4217, 0.0);
            diag.OnRuntimeLabelChanged(null, "DXGI", 0.5);
            double now = 0.0;
            for (int i = 0; i < 200; i++)
            {
                diag.OnRowRejected(9001, 4217, now);
                now += 0.010;
            }
            double t = FeedSteadyRows(diag, now, 3.0, 1000.0);
            diag.OnRowAccepted(SecondChain, "DXGI", "Application", 4000.0, t - 1500.0, 4000.0, now + 3.0);
            FeedSteadyRows(diag, now + 3.01, 12.0, t);
            diag.Tick(now + 20.0);

            Assert.AreEqual(0, lines.Count, string.Join(Environment.NewLine, lines));
        }

        [TestMethod]
        public void EnablingStartsFromFreshReferences()
        {
            var lines = new List<string>();
            var diag = Create(lines, enabled: false);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            diag.OnRuntimeLabelChanged(null, "DXGI", 0.0);
            double t = FeedSteadyRows(diag, 0.0, 2.0, 1000.0);

            // 60 s of silence while the switch is off, then the user enables it: the pause must
            // not be reported as an arrival/source gap, and the summary must still know the PID.
            diag.Enabled = true;
            FeedSteadyRows(diag, 62.0, 11.0, t + 60000.0);

            Assert.AreEqual(1, lines.Count, string.Join(Environment.NewLine, lines));
            StringAssert.Contains(lines[0], "10s: pid=4217");
            StringAssert.Contains(lines[0], "arrivalGaps=0");
            StringAssert.Contains(lines[0], "sourceGaps=0");
            StringAssert.Contains(lines[0], "label='DXGI'");
        }

        [TestMethod]
        public void RejectedStreakIsLoggedOnceAndResumptionOnce()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            // Foreign rows for 1.5 s while the target stays silent.
            double now = 0.0;
            for (int i = 0; i < 150; i++)
            {
                diag.OnRowRejected(9001, 4217, now);
                if (i % 2 == 0) diag.OnRowRejected(9002, 4217, now);
                now += 0.010;
            }

            Assert.AreEqual(1, lines.Count, "one line per streak, not one per rejected row");
            StringAssert.Contains(lines[0], "no rows for target PID 4217");
            StringAssert.Contains(lines[0], "9001");
            StringAssert.Contains(lines[0], "9002");

            diag.OnRowAccepted(MainChain, "DXGI", "Application", 10.0, 1000.0, 10.0, now);
            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains(lines[1], "resumed after 1.5 s");
            StringAssert.Contains(lines[1], "225 foreign rows rejected");
        }

        [TestMethod]
        public void ForeignRowsWhileTargetFlowsDoNotLogAStreak()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            double now = 0.0;
            double t = 1000.0;
            for (int i = 0; i < 300; i++)
            {
                diag.OnRowAccepted(MainChain, "DXGI", "Application", 10.0, t, 10.0, now);
                diag.OnRowRejected(9001, 4217, now);
                now += 0.010;
                t += 10.0;
            }

            Assert.AreEqual(0, lines.Count, "a desktop app presenting alongside the game is not an anomaly");
        }

        [TestMethod]
        public void OutlierAndSourceGapAppearInTheSummary()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            double t = FeedSteadyRows(diag, 0.0, 3.0, 1000.0);
            // 4 s worth of frametime on the same chain, then the timeline jumps 400 ms ahead.
            diag.OnRowAccepted(MainChain, "DXGI", "Application", 4000.0, t, 4000.0, 3.0);
            t += 400.0;
            FeedSteadyRows(diag, 3.01, 8.0, t);

            Assert.AreEqual(1, lines.Count, string.Join(Environment.NewLine, lines));
            StringAssert.Contains(lines[0], "10s: pid=4217");
            StringAssert.Contains(lines[0], "outliers=1");
            StringAssert.Contains(lines[0], "4000 ms swapchain=" + MainChain);
            StringAssert.Contains(lines[0], "sourceGaps=1(max 400ms)");
        }

        [TestMethod]
        public void EnabledSwitchSummarizesEveryWindow()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            FeedSteadyRows(diag, 0.0, 25.0, 1000.0);

            Assert.AreEqual(2, lines.Count, string.Join(Environment.NewLine, lines));
            // ~1000 rows per window; the 10 ms loop accumulates floating-point drift, so the
            // exact count is not asserted.
            StringAssert.Contains(lines[0], "10s: pid=4217 rows=");
            StringAssert.Contains(lines[0], "rejected=0");
            StringAssert.Contains(lines[0], "swapchains=[" + MainChain + ":");
            StringAssert.Contains(lines[0], "runtimes=[DXGI:");
            StringAssert.Contains(lines[0], "frametypes=[Application:");
            StringAssert.Contains(lines[0], "outliers=0");
        }

        [TestMethod]
        public void AdditionalSwapChainIsLoggedOnceAndBackdatedRowsAreCounted()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            double t = FeedSteadyRows(diag, 0.0, 2.0, 1000.0);
            // A rarely presenting second chain: its row is dated 1.5 s into the past.
            diag.OnRowAccepted(SecondChain, "DXGI", "Application", 1500.0, t - 1500.0, 1500.0, 2.0);
            diag.OnRowAccepted(SecondChain, "DXGI", "Application", 1500.0, t - 1400.0, 1500.0, 2.5);
            FeedSteadyRows(diag, 2.51, 8.0, t);

            Assert.IsTrue(lines.Count >= 2, string.Join(Environment.NewLine, lines));
            StringAssert.Contains(lines[0], "additional swapchain " + SecondChain);
            StringAssert.Contains(lines[0], "does not filter by swapchain");
            Assert.AreEqual(1, lines.Count(l => l.Contains("additional swapchain")),
                "the second row of the same chain is not news");
            string summary = lines.Single(l => l.StartsWith("10s:", StringComparison.Ordinal));
            StringAssert.Contains(summary, "backdated=2");
            StringAssert.Contains(summary, "outliers=2");
            StringAssert.Contains(summary, SecondChain + ":2");
        }

        [TestMethod]
        public void DominantSwapChainChangeIsLogged()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            double t = FeedSteadyRows(diag, 0.0, 11.0, 1000.0);
            lines.Clear();
            // The game switches to a new swapchain (e.g. a mode change): all rows now come from it.
            double now = 11.0;
            while (now < 22.0)
            {
                diag.OnRowAccepted(SecondChain, "DXGI", "Application", 10.0, t, 10.0, now);
                now += 0.010;
                t += 10.0;
            }

            Assert.IsTrue(lines.Any(l => l.Contains("dominant swapchain changed " + MainChain + " -> " + SecondChain)),
                string.Join(Environment.NewLine, lines));
        }

        [TestMethod]
        public void ArrivalGapIsCountedWhenRowsStopArriving()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            double t = FeedSteadyRows(diag, 0.0, 2.0, 1000.0);
            // Rows exist for the missing interval (continuous timestamps) but reach us 1.6 s late.
            FeedSteadyRows(diag, 3.6, 8.0, t);

            Assert.AreEqual(1, lines.Count, string.Join(Environment.NewLine, lines));
            StringAssert.Contains(lines[0], "arrivalGaps=1(max 1.6s)");
            StringAssert.Contains(lines[0], "sourceGaps=0");
        }

        [TestMethod]
        public void RuntimeLabelChangeIsLogged()
        {
            var lines = new List<string>();
            var diag = Create(lines);

            diag.OnRuntimeLabelChanged(null, "DXGI", 1.0);
            diag.OnRuntimeLabelChanged("DXGI", "Other", 2.0);

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains(lines[0], "'<none>' -> 'DXGI'");
            StringAssert.Contains(lines[1], "'DXGI' -> 'Other'");
            StringAssert.Contains(lines[1], "rebuilds its scene");
        }

        [TestMethod]
        public void TickFlushesASummaryWithoutRows()
        {
            var lines = new List<string>();
            var diag = Create(lines);
            diag.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();

            diag.Tick(0.0);
            diag.Tick(5.0);
            Assert.AreEqual(0, lines.Count);
            diag.Tick(10.0);
            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains(lines[0], "rows=0");
        }
    }
}
