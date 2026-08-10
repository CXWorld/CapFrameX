using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Injecting into a live D3D12 device that other overlays already draw into killed LEGO Batman
    /// (UE 5.6, AMD) with a GPU device removal one second after the hook's first publish — while
    /// the identical switch had worked 25 minutes earlier, so it is a race rather than a fixed
    /// incompatibility. The gate therefore requires BOTH signals: neither is usable alone, because
    /// RTSS sits in every game while it runs (CapFrameX starts it itself for the RTSS renderer) and
    /// the Steam overlay is in every Steam title by default.
    /// </summary>
    [TestClass]
    public class ForeignOverlayGateTest
    {
        [TestMethod]
        public void Blocks_OnlyWhenMidSessionAndForeignOverlayPresent()
        {
            Assert.IsTrue(HookOverlayManager.IsForeignOverlayInjectionBlocked(
                startTimeKnown: true, midSession: true, moduleScanOk: true, foreignModuleCount: 1),
                "a mid-session injection into a process another overlay hooks must be blocked");
        }

        [TestMethod]
        public void Allows_GameStartedAfterTheHookWasEnabled()
        {
            // The normal case: the overlay was already on when the game launched. This must stay
            // completely untouched — it is how the hook overlay is used.
            Assert.IsFalse(HookOverlayManager.IsForeignOverlayInjectionBlocked(
                startTimeKnown: true, midSession: false, moduleScanOk: true, foreignModuleCount: 3),
                "foreign overlays alone must never block; they are present in nearly every game");
        }

        [TestMethod]
        public void Allows_MidSessionWithoutAnyForeignOverlay()
        {
            Assert.IsFalse(HookOverlayManager.IsForeignOverlayInjectionBlocked(
                startTimeKnown: true, midSession: true, moduleScanOk: true, foreignModuleCount: 0),
                "a mid-session injection on its own has not been observed to fail");
        }

        [TestMethod]
        public void FallsOpen_WhenTheProcessStartTimeIsUnreadable()
        {
            Assert.IsFalse(HookOverlayManager.IsForeignOverlayInjectionBlocked(
                startTimeKnown: false, midSession: true, moduleScanOk: true, foreignModuleCount: 2),
                "an unknown process age must not cost the overlay");
        }

        [TestMethod]
        public void FallsOpen_WhenTheModuleScanFails()
        {
            Assert.IsFalse(HookOverlayManager.IsForeignOverlayInjectionBlocked(
                startTimeKnown: true, midSession: true, moduleScanOk: false, foreignModuleCount: 0),
                "an unreadable module list must not cost the overlay");
        }

        [TestMethod]
        public void ForeignOverlayScan_OfOurOwnProcessSucceedsAndOnlyReportsKnownOverlays()
        {
            // Exercises the real module enumeration against a process we can always open. Do NOT
            // assert an empty result: RTSS injects RTSSHooks64.dll globally — it is even in
            // explorer.exe — so the test host legitimately carries one while RTSS runs. What must
            // hold regardless of the machine is that every reported name really is one of the
            // known overlays, i.e. the substring matching produces no false positives.
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

            bool ok = HookTargetPolicy.TryGetForeignOverlayModules(pid, out string[] modules,
                out string error);

            Assert.IsTrue(ok, $"module scan of our own process failed: {error}");
            foreach (string module in modules)
            {
                bool known = module.IndexOf("rtsshooks", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || module.IndexOf("gameoverlayrenderer", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || module.IndexOf("discordhook", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || module.IndexOf("eosovh", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || module.IndexOf("graphics-hook", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || module.IndexOf("nvspcap", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Assert.IsTrue(known, $"'{module}' matched but is not a known overlay hook");
            }
        }

        [TestMethod]
        public void MidSession_IsFalseWhenTheHookCameFromTheStoredConfiguration()
        {
            // CapFrameX restarted next to an already running game: the hook is enabled from the
            // configuration, not by a renderer switch. Treating that as mid-session disabled the
            // overlay for every open game after a CapFrameX restart.
            var gameStart = new System.DateTime(2026, 7, 28, 20, 50, 0, System.DateTimeKind.Utc);

            Assert.IsFalse(HookOverlayManager.IsMidSession(gameStart, System.DateTime.MinValue),
                "a hook enabled at startup must never make a running game mid-session");
        }

        [TestMethod]
        public void MidSession_IsTrueOnlyForAGameThatPredatesTheRuntimeSwitch()
        {
            var switchedOn = new System.DateTime(2026, 7, 28, 21, 0, 0, System.DateTimeKind.Utc);
            var startedBefore = switchedOn.AddMinutes(-5);
            var startedAfter = switchedOn.AddMinutes(5);

            Assert.IsTrue(HookOverlayManager.IsMidSession(startedBefore, switchedOn),
                "a game running when the user switched the renderer is a mid-session injection");
            Assert.IsFalse(HookOverlayManager.IsMidSession(startedAfter, switchedOn),
                "a game launched afterwards is the normal case");
        }

        [TestMethod]
        public void ForeignOverlayScan_RejectsAnInvalidPid()
        {
            Assert.IsFalse(HookTargetPolicy.TryGetForeignOverlayModules(0, out string[] modules,
                out string error));
            Assert.AreEqual(0, modules.Length);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }
    }
}
