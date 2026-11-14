using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The target policy scans the game (process, image path, Ricochet's driver, a module
    /// snapshot). It ran on the PresentMon reader thread every 2 s for the whole session; now the
    /// rows only read the cached verdict, the scans run in the background, slow down after the
    /// first half minute of a clean target and stop for a denial that is final for the process.
    /// </summary>
    [TestClass]
    public class HookTargetPolicyCacheTest
    {
        private const int Pid = 4242;

        private sealed class Harness
        {
            public long NowMs = 1_000_000;
            public readonly Queue<Action> Background = new Queue<Action>();
            public readonly List<int> Evaluations = new List<int>();
            public readonly List<int> Changes = new List<int>();
            public HookTargetPolicyCache.Verdict Next = new HookTargetPolicyCache.Verdict(true, null);
            public readonly HookTargetPolicyCache Cache;

            public Harness()
            {
                Cache = new HookTargetPolicyCache(pid =>
                {
                    Evaluations.Add(pid);
                    return Next;
                }, () => NowMs, Background.Enqueue);
                Cache.VerdictChanged += Changes.Add;
            }

            public void RunBackground()
            {
                while (Background.Count > 0) Background.Dequeue()();
            }
        }

        [TestMethod]
        public void ScheduleSlowsDownAfterTheStartupPhaseAndKeepsFinalDenials()
        {
            Assert.AreEqual(2000L, HookTargetPolicySchedule.VerdictLifetimeMs(true, null, 0));
            Assert.AreEqual(2000L, HookTargetPolicySchedule.VerdictLifetimeMs(true, null, 29_999));
            Assert.AreEqual(10_000L, HookTargetPolicySchedule.VerdictLifetimeMs(true, null, 30_000));
            Assert.AreEqual(10_000L, HookTargetPolicySchedule.VerdictLifetimeMs(true, null, 3_600_000));

            string[] final =
            {
                HookTargetPolicy.GetAntiCheatReason("EasyAntiCheat_EOS.dll", "easyanticheat"),
                HookTargetPolicy.GetRicochetReason(@"C:\Games\COD\Randgrid.sys", 0, null),
                HookTargetPolicy.GetRicochetReason(null, 5, "atvi-randgrid_msstore"),
            };
            Assert.IsTrue(HookTargetPolicy.IsInjectionBlacklisted("cs2", out string blacklisted));
            foreach (string reason in new List<string>(final) { blacklisted })
            {
                Assert.IsNull(HookTargetPolicySchedule.VerdictLifetimeMs(false, reason, 0), reason);
                Assert.IsNull(HookTargetPolicySchedule.VerdictLifetimeMs(false, reason, 600_000), reason);
            }

            foreach (string reason in new[] { "module scan failed (5)", "module scan returned no modules (18)",
                "module enumeration failed (299)", "process identity check failed (gone)", null })
            {
                Assert.AreEqual(2000L, HookTargetPolicySchedule.VerdictLifetimeMs(false, reason, 0), reason);
                Assert.AreEqual(2000L, HookTargetPolicySchedule.VerdictLifetimeMs(false, reason, 600_000), reason);
            }
        }

        [TestMethod]
        public void RowsNeverEvaluateAndLearnTheFirstVerdictFromTheBackground()
        {
            var h = new Harness();
            Assert.IsFalse(h.Cache.TryGetVerdict(Pid, out _, out _), "no verdict yet");
            Assert.IsFalse(h.Cache.TryGetVerdict(Pid, out _, out _), "still none");
            Assert.AreEqual(0, h.Evaluations.Count, "the reading thread never evaluates");
            Assert.AreEqual(1, h.Background.Count, "one background evaluation, not one per row");

            h.RunBackground();
            CollectionAssert.AreEqual(new[] { Pid }, h.Evaluations);
            CollectionAssert.AreEqual(new[] { Pid }, h.Changes, "the first verdict is reported");
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out bool allowed, out string reason));
            Assert.IsTrue(allowed);
            Assert.IsNull(reason);
        }

        [TestMethod]
        public void AStaleVerdictIsReturnedAndRefreshedInTheBackground()
        {
            var h = new Harness();
            Assert.IsTrue(h.Cache.IsAllowed(Pid, requireFresh: false, out _), "the first verdict is evaluated");
            Assert.AreEqual(1, h.Evaluations.Count);

            h.NowMs += 1999;
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out _, out _));
            Assert.AreEqual(0, h.Background.Count, "fresh for 2 s");

            h.NowMs += 1;
            h.Next = new HookTargetPolicyCache.Verdict(false,
                HookTargetPolicy.GetAntiCheatReason("BEClient_x64.dll", "beclient"));
            Assert.IsTrue(h.Cache.IsAllowed(Pid, requireFresh: false, out _), "stale: the cached verdict");
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out bool staleAllowed, out _));
            Assert.IsTrue(staleAllowed);
            Assert.AreEqual(1, h.Evaluations.Count, "nothing evaluated on the reading threads");
            Assert.AreEqual(1, h.Background.Count, "one refresh for both reads");

            h.RunBackground();
            CollectionAssert.AreEqual(new[] { Pid }, h.Changes, "an anti-cheat module that appears later is reported");
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out bool allowed, out string reason));
            Assert.IsFalse(allowed);
            StringAssert.StartsWith(reason, HookTargetPolicy.AntiCheatReasonPrefix);
        }

        [TestMethod]
        public void AnUnchangedRefreshReportsNothing()
        {
            var h = new Harness();
            h.Cache.IsAllowed(Pid, requireFresh: false, out _);
            h.NowMs += 5000;
            h.Cache.TryGetVerdict(Pid, out _, out _);
            h.RunBackground();
            Assert.AreEqual(2, h.Evaluations.Count);
            Assert.AreEqual(0, h.Changes.Count);
        }

        [TestMethod]
        public void ACleanTargetIsScannedEveryTenSecondsAfterItsFirstHalfMinute()
        {
            var h = new Harness();
            h.Cache.IsAllowed(Pid, requireFresh: false, out _);
            long start = h.NowMs;
            // Read like the rows do, every 100 ms, for two minutes.
            var scansAt = new List<long>();
            for (long t = 0; t <= 120_000; t += 100)
            {
                h.NowMs = start + t;
                int before = h.Evaluations.Count;
                h.Cache.TryGetVerdict(Pid, out _, out _);
                h.RunBackground();
                if (h.Evaluations.Count > before) scansAt.Add(t);
            }
            // 2 s apart up to 30 s (the scan at 30 s is the first with the long lifetime), then 10 s.
            var expected = new List<long>();
            for (long t = 2000; t <= 30_000; t += 2000) expected.Add(t);
            for (long t = 40_000; t <= 120_000; t += 10_000) expected.Add(t);
            CollectionAssert.AreEqual(expected, scansAt);
        }

        [TestMethod]
        public void AFinalDenialIsNotScannedAgainForTheProcess()
        {
            var h = new Harness();
            Assert.IsTrue(HookTargetPolicy.IsInjectionBlacklisted("VALORANT-Win64-Shipping", out string reason));
            h.Next = new HookTargetPolicyCache.Verdict(false, reason);
            Assert.IsFalse(h.Cache.IsAllowed(Pid, requireFresh: false, out _));
            h.NowMs += 3_600_000;
            Assert.IsFalse(h.Cache.IsAllowed(Pid, requireFresh: true, out string cached));
            Assert.AreEqual(reason, cached);
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out _, out _));
            Assert.AreEqual(1, h.Evaluations.Count);
            Assert.AreEqual(0, h.Background.Count);

            // A new process (PID change) is evaluated again.
            h.Cache.Invalidate(Pid);
            h.Next = new HookTargetPolicyCache.Verdict(true, null);
            Assert.IsTrue(h.Cache.IsAllowed(Pid, requireFresh: false, out _));
            Assert.AreEqual(2, h.Evaluations.Count);
        }

        [TestMethod]
        public void ATransientFailureIsRetriedEveryTwoSeconds()
        {
            var h = new Harness();
            h.Next = new HookTargetPolicyCache.Verdict(false, "module scan returned no modules (18)");
            Assert.IsFalse(h.Cache.IsAllowed(Pid, requireFresh: false, out _));
            h.NowMs += 120_000;
            h.Cache.TryGetVerdict(Pid, out _, out _);
            h.RunBackground();
            h.NowMs += 1999;
            h.Cache.TryGetVerdict(Pid, out _, out _);
            Assert.AreEqual(0, h.Background.Count);
            h.NowMs += 1;
            h.Cache.TryGetVerdict(Pid, out _, out _);
            Assert.AreEqual(1, h.Background.Count, "still 2 s after two minutes");
        }

        [TestMethod]
        public void RequireFreshEvaluatesAStaleVerdictOnTheCallingThread()
        {
            var h = new Harness();
            h.Cache.IsAllowed(Pid, requireFresh: true, out _);
            h.Cache.IsAllowed(Pid, requireFresh: true, out _);
            Assert.AreEqual(1, h.Evaluations.Count, "a fresh verdict is reused");
            h.NowMs += 2000;
            h.Next = new HookTargetPolicyCache.Verdict(false, "module scan failed (5)");
            Assert.IsFalse(h.Cache.IsAllowed(Pid, requireFresh: true, out string reason));
            Assert.AreEqual("module scan failed (5)", reason);
            Assert.AreEqual(2, h.Evaluations.Count);
            Assert.AreEqual(0, h.Background.Count);
        }

        [TestMethod]
        public void AnInvalidateDiscardsARefreshThatStartedBeforeIt()
        {
            var h = new Harness();
            Assert.IsFalse(h.Cache.TryGetVerdict(Pid, out _, out _));
            h.Cache.Invalidate(Pid);
            h.RunBackground();
            Assert.AreEqual(0, h.Changes.Count, "the outcome belongs to before the invalidation");
            Assert.IsFalse(h.Cache.TryGetVerdict(Pid, out _, out _), "still no verdict");
            Assert.AreEqual(1, h.Background.Count, "a new evaluation starts");
            h.RunBackground();
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out bool allowed, out _));
            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public void AnotherPidReplacesTheCachedOne()
        {
            var h = new Harness();
            h.Cache.IsAllowed(Pid, requireFresh: false, out _);
            h.Cache.Invalidate(Pid + 1); // another PID's invalidation leaves this one alone
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid, out _, out _));
            Assert.IsFalse(h.Cache.TryGetVerdict(Pid + 1, out _, out _));
            h.RunBackground();
            CollectionAssert.AreEqual(new[] { Pid, Pid + 1 }, h.Evaluations);
            Assert.IsTrue(h.Cache.TryGetVerdict(Pid + 1, out _, out _));
        }
    }
}
