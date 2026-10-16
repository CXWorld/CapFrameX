using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookOverlayManagerPollingTest
    {
        private const string GameName = "Game";

        [TestMethod]
        public void CreateProcessStartTraceQuery_UsesValidEventProjection()
        {
            var query = HookOverlayManager.CreateProcessStartTraceQuery();

            Assert.AreEqual("Win32_ProcessStartTrace", query.EventClassName);
            Assert.AreEqual(HookOverlayManager.ProcessStartTraceQuery.ToUpperInvariant(),
                query.QueryString.ToUpperInvariant());
        }

        [TestMethod]
        public void ShouldContinueFastEarlyInjectionProbe_StopsAtIdleLimit()
        {
            int limit = HookOverlayManager.EarlyInjectionIdleTicksBeforeStandDown;

            Assert.IsTrue(HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(
                sawCandidate: false, idleTicks: limit - 1));
            Assert.IsFalse(HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(
                sawCandidate: false, idleTicks: limit));
            Assert.IsTrue(HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(
                sawCandidate: true, idleTicks: limit));
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_KeepsTargetThatIsNotInjectedYet()
        {
            var game = new FakeProcess(GameName, 100);

            var candidates = Select(new[] { new FakeProcess("explorer", 4), game },
                blockedPids: new int[0]);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(game, candidates[0].Process);
            Assert.AreEqual(100, candidates[0].Pid);
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_SkipsAlreadyInjectedTarget()
        {
            var candidates = Select(
                new[] { new FakeProcess("explorer", 4), new FakeProcess(GameName, 100) },
                blockedPids: new[] { 100 });

            Assert.AreEqual(0, candidates.Count);
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_KeepsNewInstanceNextToInjectedOne()
        {
            var secondInstance = new FakeProcess(GameName, 200);

            var candidates = Select(new[] { new FakeProcess(GameName, 100), secondInstance },
                blockedPids: new[] { 100 });

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(secondInstance, candidates[0].Process);
            Assert.AreEqual(200, candidates[0].Pid);
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_PreservesSnapshotOrder()
        {
            var first = new FakeProcess(GameName, 300);
            var second = new FakeProcess("Other", 100);
            var third = new FakeProcess(GameName, 200);

            var candidates = HookOverlayManager.SelectEarlyInjectionCandidates(
                new[] { first, new FakeProcess("explorer", 4), second, third },
                p => p.Name, p => p.Id,
                name => name == GameName || name == "Other", pid => false);

            CollectionAssert.AreEqual(new[] { 300, 100, 200 },
                candidates.Select(c => c.Pid).ToArray());
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_ChecksBlockOnlyForTargetNames()
        {
            var queriedPids = new List<int>();
            var readIds = new List<int>();

            HookOverlayManager.SelectEarlyInjectionCandidates(
                new[]
                {
                    new FakeProcess("explorer", 4),
                    new FakeProcess(GameName, 100),
                    new FakeProcess("svchost", 8),
                },
                p => p.Name,
                p => { readIds.Add(p.Id); return p.Id; },
                name => name == GameName,
                pid => { queriedPids.Add(pid); return false; });

            // The cheap name check must reject unrelated processes before their PID is read
            // or the injection state (which takes the manager's lock) is consulted.
            CollectionAssert.AreEqual(new[] { 100 }, readIds);
            CollectionAssert.AreEqual(new[] { 100 }, queriedPids);
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_SkipsProcessesThatExitDuringTheWalk()
        {
            var survivor = new FakeProcess(GameName, 400);

            var candidates = Select(new[]
                {
                    new FakeProcess(GameName, 100, nameFailure: new InvalidOperationException()),
                    new FakeProcess(GameName, 200, nameFailure: new NotSupportedException()),
                    new FakeProcess(GameName, 300, idFailure: new InvalidOperationException()),
                    survivor,
                },
                blockedPids: new int[0]);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreSame(survivor, candidates[0].Process);
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_DoesNotSwallowUnexpectedFailures()
        {
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => Select(
                new[] { new FakeProcess(GameName, 100,
                    nameFailure: new UnauthorizedAccessException()) },
                blockedPids: new int[0]));
            Assert.ThrowsExactly<NotSupportedException>(() => Select(
                new[] { new FakeProcess(GameName, 100,
                    idFailure: new NotSupportedException()) },
                blockedPids: new int[0]));
        }

        [TestMethod]
        public void SelectEarlyInjectionCandidates_ReturnsNothingForEmptySnapshot()
        {
            Assert.AreEqual(0, Select(new FakeProcess[0], new int[0]).Count);
        }

        [TestMethod]
        public void EarlyInjectionProbe_StandsDownWhileOnlyAnInjectedGameRuns()
        {
            // Regression: the injected game used to count as a candidate on every tick, so the
            // 25 ms full process snapshot kept running until the game exited.
            var snapshot = new[] { new FakeProcess("explorer", 4), new FakeProcess(GameName, 100) };

            int ticks = RunProbeUntilStandDown(snapshot, blockedPids: new[] { 100 },
                maxTicks: 10_000);

            Assert.AreEqual(HookOverlayManager.EarlyInjectionIdleTicksBeforeStandDown, ticks);
        }

        [TestMethod]
        public void EarlyInjectionProbe_StaysAwakeWhileATargetWaitsForInjection()
        {
            var snapshot = new[] { new FakeProcess(GameName, 100) };

            int ticks = RunProbeUntilStandDown(snapshot, blockedPids: new int[0],
                maxTicks: 10_000);

            Assert.AreEqual(-1, ticks, "a pending target must keep the fast probe running");
        }

        private static List<(FakeProcess Process, int Pid)> Select(
            IEnumerable<FakeProcess> snapshot, IEnumerable<int> blockedPids)
        {
            var blocked = new HashSet<int>(blockedPids);
            return HookOverlayManager.SelectEarlyInjectionCandidates(snapshot,
                p => p.Name, p => p.Id,
                name => string.Equals(name, GameName, StringComparison.OrdinalIgnoreCase),
                blocked.Contains);
        }

        // Mirrors the idle accounting of HookOverlayManager.ProbeEarlyInjectionTargets and
        // returns the tick at which the probe stands down, or -1 if it is still running.
        private static int RunProbeUntilStandDown(FakeProcess[] snapshot,
            IEnumerable<int> blockedPids, int maxTicks)
        {
            int idleTicks = 0;
            for (int tick = 1; tick <= maxTicks; tick++)
            {
                bool sawCandidate = Select(snapshot, blockedPids).Count > 0;
                if (sawCandidate)
                {
                    idleTicks = 0;
                    continue;
                }
                idleTicks++;
                if (!HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(sawCandidate,
                        idleTicks))
                    return tick;
            }
            return -1;
        }

        private sealed class FakeProcess
        {
            private readonly string _name;
            private readonly int _id;
            private readonly Exception _nameFailure;
            private readonly Exception _idFailure;

            public FakeProcess(string name, int id, Exception nameFailure = null,
                Exception idFailure = null)
            {
                _name = name;
                _id = id;
                _nameFailure = nameFailure;
                _idFailure = idFailure;
            }

            public string Name => _nameFailure != null ? throw _nameFailure : _name;

            public int Id => _idFailure != null ? throw _idFailure : _id;
        }
    }
}
