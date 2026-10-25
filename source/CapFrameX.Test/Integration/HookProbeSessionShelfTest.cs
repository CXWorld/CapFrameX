using System.Collections.Generic;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookProbeSessionShelfTest
    {
        [TestMethod]
        public void ATargetSwitchAwayAndBackReturnsTheSameSession()
        {
            // Metro 2033 Redux on 2026-10-25: injected through the early path while no target
            // was selected, then testhost became the target for four seconds, then Metro again.
            // The manager used to drop Metro's session at the first switch.
            var shelf = new HookProbeSessionShelf();
            HookCompatibilityProbeSession metro = Session(29796);

            shelf.Park(metro, rearmCount: 2);
            Assert.IsNull(shelf.Find(14384), "testhost has no session of its own");
            Assert.AreSame(metro, shelf.Find(29796));

            Assert.IsTrue(shelf.TryTake(29796, out HookCompatibilityProbeSession back, out int rearms));
            Assert.AreSame(metro, back);
            Assert.AreEqual(2, rearms, "the re-plan limit of the process carries over");
            Assert.AreEqual(0, shelf.Count);
            Assert.IsFalse(shelf.TryTake(29796, out back, out rearms));
        }

        [TestMethod]
        public void ANewerSessionOfTheSameProcessReplacesTheParkedOne()
        {
            var shelf = new HookProbeSessionShelf();
            HookCompatibilityProbeSession first = Session(42);
            HookCompatibilityProbeSession replan = Session(42);

            shelf.Park(first, 0);
            shelf.Park(replan, 1);
            shelf.Park(null, 5);

            Assert.AreEqual(1, shelf.Count);
            Assert.AreSame(replan, shelf.Find(42));
        }

        [TestMethod]
        public void SessionsOfExitedProcessesArePruned()
        {
            var shelf = new HookProbeSessionShelf();
            shelf.Park(Session(10), 0);
            shelf.Park(Session(11), 0);
            shelf.Park(Session(12), 0);
            var alive = new HashSet<int> { 11 };

            shelf.Prune(alive.Contains);

            Assert.AreEqual(1, shelf.Count);
            Assert.IsNotNull(shelf.Find(11));
            shelf.Remove(11);
            Assert.AreEqual(0, shelf.Count);
        }

        private static HookCompatibilityProbeSession Session(int processId)
            => new HookCompatibilityProbeSession(processId, HookCompatibilityStagePlanner.Plan(
                HookCompatibilityStagePlannerTest.Evidence(ffxFg: true, d3d12: true), null, null,
                "hash", true));
    }
}
