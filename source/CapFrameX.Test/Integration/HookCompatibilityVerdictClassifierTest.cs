using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookCompatibilityVerdictClassifierTest
    {
        private static readonly HookCompatibilityStage Vendor =
            HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware);
        private static readonly HookCompatibilityStage Generic =
            HookCompatibilityStage.Create(HookCompatibilityStageId.Generic);

        [TestMethod]
        public void Classify_MissingStatusBecomesATimeoutAfterTheHandshakeWindow()
        {
            HookProbeTimings timings = Timings(now: 1000 + HookOverlayManager.HookHandshakeTimeoutMs - 1);
            Assert.AreEqual(HookCompatibilityVerdict.Pending,
                HookCompatibilityVerdictClassifier.Classify(Vendor, false, default, null, in timings));

            timings = Timings(now: 1000 + HookOverlayManager.HookHandshakeTimeoutMs);
            Assert.AreEqual(HookCompatibilityVerdict.StatusTimeout,
                HookCompatibilityVerdictClassifier.Classify(Vendor, false, default, null, in timings));
        }

        [TestMethod]
        public void Classify_ErrorsComeFirst()
        {
            var osd = new NativeHookStatusSnapshot { Flags = NativeHookStatusFlags.Loaded | NativeHookStatusFlags.Error, LastError = 2 };
            var install = new NativeHookStatusSnapshot { Flags = NativeHookStatusFlags.Loaded | NativeHookStatusFlags.Error, LastError = 1 };
            HookProbeTimings timings = Timings(now: 2000);

            Assert.AreEqual(HookCompatibilityVerdict.OsdCreateFailed,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, osd, EHookOverlayStatus.Error, in timings));
            Assert.AreEqual(HookCompatibilityVerdict.InstallFailed,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, install, EHookOverlayStatus.Error, in timings));
        }

        [TestMethod]
        public void Classify_AnUnarmedHookIsHungAfterSixSeconds()
        {
            var loaded = new NativeHookStatusSnapshot
            {
                Version = 2,
                Flags = NativeHookStatusFlags.Loaded,
                InstallPhase = NativeHookInstallPhase.FidelityFxExports
            };

            HookProbeTimings before = Timings(now: 1000 + HookCompatibilityVerdictClassifier.ProbeInstallHungMs - 1);
            HookProbeTimings after = Timings(now: 1000 + HookCompatibilityVerdictClassifier.ProbeInstallHungMs);

            Assert.AreEqual(HookCompatibilityVerdict.Pending,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, loaded, EHookOverlayStatus.Initializing, in before));
            Assert.AreEqual(HookCompatibilityVerdict.InstallHung,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, loaded, EHookOverlayStatus.Initializing, in after));
        }

        [TestMethod]
        public void Classify_FlagsAndIdleAreReportedDirectly()
        {
            HookProbeTimings timings = Timings(now: 2000);
            var early = new NativeHookStatusSnapshot { Flags = Armed | NativeHookStatusFlags.EarlyInjectionRequired };
            var foreign = new NativeHookStatusSnapshot { Flags = Armed | NativeHookStatusFlags.ForeignPresenter };
            var idle = new NativeHookStatusSnapshot { Flags = Armed | NativeHookStatusFlags.PresentSeen };

            Assert.AreEqual(HookCompatibilityVerdict.EarlyInjectionRequired,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, early, EHookOverlayStatus.Initializing, in timings));
            Assert.AreEqual(HookCompatibilityVerdict.ForeignPresenter,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, foreign, EHookOverlayStatus.Initializing, in timings));
            Assert.AreEqual(HookCompatibilityVerdict.Inconclusive,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, idle, EHookOverlayStatus.Idle, in timings));
        }

        [TestMethod]
        public void Classify_NoQueueOnlyForTheGenericRouteOfAVersion2Hook()
        {
            var snapshot = new NativeHookStatusSnapshot
            {
                Version = 2,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.Visible,
                QueueState = NativeHookQueueState.None
            };
            HookProbeTimings timings = Timings(now: 10000, noQueueSince: 10000 - HookCompatibilityVerdictClassifier.ProbeNoQueueMs);

            Assert.AreEqual(HookCompatibilityVerdict.NoQueue,
                HookCompatibilityVerdictClassifier.Classify(Generic, true, snapshot, EHookOverlayStatus.Initializing, in timings));
            Assert.AreNotEqual(HookCompatibilityVerdict.NoQueue,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, snapshot, EHookOverlayStatus.Initializing, in timings));
            snapshot.Version = 1;
            Assert.AreNotEqual(HookCompatibilityVerdict.NoQueue,
                HookCompatibilityVerdictClassifier.Classify(Generic, true, snapshot, EHookOverlayStatus.Initializing, in timings));
        }

        [TestMethod]
        public void Classify_UsesTheExistingPresentAndRendererTimeouts()
        {
            var waiting = new NativeHookStatusSnapshot { Flags = Armed };
            var initializing = new NativeHookStatusSnapshot { Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.Visible };

            HookProbeTimings noPresent = Timings(now: 20000, waitingSince: 20000 - HookOverlayManager.HookFirstPresentTimeoutMs);
            HookProbeTimings stalled = Timings(now: 20000, initializingSince: 20000 - HookOverlayManager.HookRendererReadyTimeoutMs);

            Assert.AreEqual(HookCompatibilityVerdict.NoPresent,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, waiting, EHookOverlayStatus.Waiting, in noPresent));
            Assert.AreEqual(HookCompatibilityVerdict.RendererStalled,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, initializing, EHookOverlayStatus.Initializing, in stalled));
        }

        [TestMethod]
        public void Classify_SuccessNeedsConfirmationAndCoverageOnTheGenericRoute()
        {
            var active = new NativeHookStatusSnapshot
            {
                Version = 2,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.Visible |
                        NativeHookStatusFlags.RendererReady | NativeHookStatusFlags.MetricsConnected |
                        NativeHookStatusFlags.Rendered
            };
            HookProbeTimings fresh = Timings(now: 5000, successSince: 5000 - HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs + 1);
            HookProbeTimings confirmed = Timings(now: 5000, successSince: 5000 - HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);

            Assert.AreEqual(HookCompatibilityVerdict.Pending,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, active, EHookOverlayStatus.Active, in fresh));
            Assert.AreEqual(HookCompatibilityVerdict.Success,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, active, EHookOverlayStatus.Active, in confirmed));
            // Generic + V2: the route must have submitted at least one draw.
            Assert.AreEqual(HookCompatibilityVerdict.Pending,
                HookCompatibilityVerdictClassifier.Classify(Generic, true, active, EHookOverlayStatus.Active, in confirmed));
            active.CoverageSubmitted = 1;
            Assert.AreEqual(HookCompatibilityVerdict.Success,
                HookCompatibilityVerdictClassifier.Classify(Generic, true, active, EHookOverlayStatus.Active, in confirmed));
        }

        [TestMethod]
        public void Classify_TheStageBudgetEndsAnUndecidedStage()
        {
            var initializing = new NativeHookStatusSnapshot
            {
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.Visible
            };
            HookProbeTimings timings = Timings(now: 30000);
            timings.ActiveElapsedMs = HookCompatibilityVerdictClassifier.ProbeStageBudgetMs;

            Assert.AreEqual(HookCompatibilityVerdict.RendererStalled,
                HookCompatibilityVerdictClassifier.Classify(Vendor, true, initializing,
                    EHookOverlayStatus.Initializing, in timings));
        }

        [TestMethod]
        public void Classify_HiddenIsInconclusiveEvenAfterTheBudgetAndQueueTimeout()
        {
            var hidden = new NativeHookStatusSnapshot
            {
                Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen,
                QueueState = NativeHookQueueState.None
            };
            HookProbeTimings timings = Timings(now: 30000, noQueueSince: 1000);
            timings.ActiveElapsedMs = HookCompatibilityVerdictClassifier.ProbeStageBudgetMs;

            Assert.AreEqual(HookCompatibilityVerdict.Inconclusive,
                HookCompatibilityVerdictClassifier.Classify(Generic, true, hidden,
                    EHookOverlayStatus.Hidden, in timings));
        }

        private const NativeHookStatusFlags Armed =
            NativeHookStatusFlags.Loaded | NativeHookStatusFlags.HooksArmed;

        private static HookProbeTimings Timings(ulong now, ulong waitingSince = 0,
            ulong initializingSince = 0, ulong noQueueSince = 0, ulong successSince = 0)
            => new HookProbeTimings
            {
                NowTickMs = now,
                InjectionSucceeded = true,
                InjectionSucceededTickMs = 1000,
                WaitingSinceTickMs = waitingSince,
                InitializingSinceTickMs = initializingSince,
                NoQueueSinceTickMs = noQueueSince,
                SuccessSinceTickMs = successSince
            };
    }
}
