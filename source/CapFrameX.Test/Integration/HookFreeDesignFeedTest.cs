using System;
using System.Collections.Generic;
using System.IO;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The hook-free renderer takes the values of a tile design over once per OSD refresh. Before,
    /// every snapshot of the design publisher reached it: one per sensor poll (twice per second
    /// while a capture logs sensors) and one per 500 ms heartbeat, each of which repainted and
    /// presented the root surface of the overlay window. In Doom: The Dark Ages (RTX 5090, FSR FG,
    /// 2026-11-09) the FUSION design repainted 33 to 59 times in 10 s instead of 10, and every
    /// present of the window cost the game frame in flight GPU time.
    /// </summary>
    [TestClass]
    public class HookFreeDesignFeedTest
    {
        private const HookFreeDesignTrigger Snapshot = HookFreeDesignTrigger.Snapshot;
        private const HookFreeDesignTrigger Refresh = HookFreeDesignTrigger.Refresh;

        [TestMethod]
        public void ValuesOfTheLoadedDesign_WaitForTheOsdRefresh()
        {
            Assert.AreEqual(HookFreeDesignUpdate.None,
                HookFreeDesignFeed.Decide(Snapshot, hasDesign: true, designLoaded: true, designChanged: false,
                    metricsChanged: true),
                "A snapshot with new values alone must not reach the renderer.");
            Assert.AreEqual(HookFreeDesignUpdate.Metrics,
                HookFreeDesignFeed.Decide(Refresh, hasDesign: true, designLoaded: true, designChanged: false,
                    metricsChanged: true),
                "The OSD refresh applies the values.");
        }

        [TestMethod]
        public void UnchangedValues_AreNeverAppliedAgain()
        {
            foreach (HookFreeDesignTrigger trigger in new[] { Snapshot, Refresh })
            {
                Assert.AreEqual(HookFreeDesignUpdate.None,
                    HookFreeDesignFeed.Decide(trigger, hasDesign: true, designLoaded: true, designChanged: false,
                        metricsChanged: false), trigger.ToString());
                Assert.AreEqual(HookFreeDesignUpdate.None,
                    HookFreeDesignFeed.Decide(trigger, hasDesign: true, designLoaded: true, designChanged: false,
                        metricsChanged: false, everySnapshot: true), trigger + ", former cadence");
            }
        }

        [TestMethod]
        [DataRow(false, false, DisplayName = "the first design")]
        [DataRow(true, true, DisplayName = "another design, or the same one bound to the sensors")]
        [DataRow(false, true, DisplayName = "the first design after a restart of the renderer")]
        public void ADesignThatIsNewOrChanged_IsAppliedAtOnce(bool designLoaded, bool designChanged)
        {
            foreach (HookFreeDesignTrigger trigger in new[] { Snapshot, Refresh })
                foreach (bool metricsChanged in new[] { false, true })
                    Assert.AreEqual(HookFreeDesignUpdate.Template,
                        HookFreeDesignFeed.Decide(trigger, hasDesign: true, designLoaded, designChanged, metricsChanged),
                        $"{trigger}, metrics changed: {metricsChanged}");
        }

        [TestMethod]
        public void ARemovedDesign_IsClearedAtOnce_AndOnlyOnce()
        {
            foreach (HookFreeDesignTrigger trigger in new[] { Snapshot, Refresh })
            {
                Assert.AreEqual(HookFreeDesignUpdate.Clear,
                    HookFreeDesignFeed.Decide(trigger, hasDesign: false, designLoaded: true, designChanged: true,
                        metricsChanged: true), trigger.ToString());
                Assert.AreEqual(HookFreeDesignUpdate.None,
                    HookFreeDesignFeed.Decide(trigger, hasDesign: false, designLoaded: false, designChanged: true,
                        metricsChanged: true), trigger + ": nothing is loaded");
            }
        }

        [TestMethod]
        public void TheFormerCadence_AppliesValuesWithEverySnapshot()
        {
            Assert.AreEqual(HookFreeDesignUpdate.Metrics,
                HookFreeDesignFeed.Decide(Snapshot, hasDesign: true, designLoaded: true, designChanged: false,
                    metricsChanged: true, everySnapshot: true));
        }

        // A capture that logs sensors at 500 ms, with the publisher's 500 ms heartbeat in between
        // and an OSD refresh of 1 s: the renderer gets its values once per refresh.
        [TestMethod]
        public void TenSecondsOfSnapshots_ReachTheRendererOncePerRefresh()
        {
            ulong published = 0, applied = 0;
            int updates = 0, former = 0;
            ulong formerApplied = 0;
            for (int ms = 0; ms < 10_000; ms += 250)
            {
                // Telemetry at 0 and 500, the heartbeat at 250 and 750; the values differ each time.
                ++published;
                if (HookFreeDesignFeed.Decide(Snapshot, true, true, false, published != applied) == HookFreeDesignUpdate.Metrics)
                {
                    applied = published;
                    ++updates;
                }
                if (HookFreeDesignFeed.Decide(Snapshot, true, true, false, published != formerApplied, everySnapshot: true)
                    == HookFreeDesignUpdate.Metrics)
                {
                    formerApplied = published;
                    ++former;
                }
                if (ms % 1000 != 0) continue;
                if (HookFreeDesignFeed.Decide(Refresh, true, true, false, published != applied) == HookFreeDesignUpdate.Metrics)
                {
                    applied = published;
                    ++updates;
                }
            }
            Assert.AreEqual(10, updates, "One update per OSD refresh.");
            Assert.AreEqual(40, former, "The former cadence passed every snapshot on.");
            Assert.AreNotEqual(published, applied, "The snapshots since the last refresh wait for the next one.");
            Assert.AreEqual(HookFreeDesignUpdate.Metrics,
                HookFreeDesignFeed.Decide(Refresh, true, true, false, published != applied),
                "The next refresh carries the newest values.");
        }

        [TestMethod]
        public void TheFormerCadence_IsSelectedByTheLegacyRepaintMode()
        {
            string directory = Path.Combine(Path.GetTempPath(), "cfx-design-feed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "OsdDebug.json");
            try
            {
                Assert.IsFalse(HookFreeDesignFeed.AppliesEverySnapshot(path), "No file: the refresh cadence.");
                var cases = new Dictionary<string, bool>
                {
                    ["{\"hookFreeRepaint\":\"legacy\"}"] = true,
                    ["{\"verboseLog\":true,\"hookFreeRepaint\":\"legacy\",\"hookFreeWarp\":true}"] = true,
                    ["{\"hookFreeRepaint\":\"partial\"}"] = false,
                    ["{\"hookFreeRepaint\":\"skip\"}"] = false,
                    ["{\"hookFreeRepaint\":\"partialPresent\"}"] = false,
                    ["{\"hookFreeRepaint\":true}"] = false,
                    ["{\"verboseLog\":true}"] = false,
                    ["{ not json"] = false,
                    [""] = false,
                };
                foreach (var test in cases)
                {
                    File.WriteAllText(path, test.Key);
                    Assert.AreEqual(test.Value, HookFreeDesignFeed.AppliesEverySnapshot(path), test.Key);
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void TheFeedSummary_CountsWhatReachedTheRenderer()
        {
            var lines = new List<string>();
            var diagnostics = new HookFreeFeedDiagnostics(lines.Add) { Enabled = true };
            diagnostics.OnTargetPidChanged(0, 4217, 0.0);
            lines.Clear();
            diagnostics.Tick(0.0); // anchors the summary window
            diagnostics.OnDesignFeed(Snapshot, HookFreeDesignUpdate.Template, waits: false);
            for (int i = 0; i < 30; i++)
                diagnostics.OnDesignFeed(Snapshot, HookFreeDesignUpdate.None, waits: i % 3 != 0);
            for (int i = 0; i < 10; i++)
                diagnostics.OnDesignFeed(Refresh, HookFreeDesignUpdate.Metrics, waits: false);
            diagnostics.OnDesignFeed(Snapshot, HookFreeDesignUpdate.Metrics, waits: false);
            diagnostics.Tick(10.5);

            Assert.AreEqual(1, lines.Count, string.Join(Environment.NewLine, lines));
            StringAssert.Contains(lines[0],
                "design=[templates:1 metrics:10 on refreshes, 1 on snapshots; 20 snapshots waited for a refresh]");

            diagnostics.Tick(21.0);
            StringAssert.Contains(lines[1],
                "design=[templates:0 metrics:0 on refreshes, 0 on snapshots; 0 snapshots waited for a refresh]",
                "Every summary window counts anew.");
        }
    }
}
