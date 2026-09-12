using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookCompatibilityProbeSessionTest
    {
        private const NativeHookStatusFlags Armed =
            NativeHookStatusFlags.Loaded | NativeHookStatusFlags.HooksArmed;
        private const NativeHookStatusFlags Ready = Armed | NativeHookStatusFlags.PresentSeen |
            NativeHookStatusFlags.Visible | NativeHookStatusFlags.RendererReady |
            NativeHookStatusFlags.MetricsConnected | NativeHookStatusFlags.Rendered;

        [TestMethod]
        public void ForeignPresenterOnVendorAware_SchedulesAGenericRestart()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot { Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 2000);

            Assert.IsTrue(session.RestartPending);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.PendingStage.Id);
            CollectionAssert.AreEqual(
                new[] { HookProbeActionKind.LogVerdict, HookProbeActionKind.ScheduleRestart,
                        HookProbeActionKind.SetFallback, HookProbeActionKind.SetPollInterval },
                actions.Select(a => a.Kind).ToArray());
            StringAssert.Contains(session.FallbackReason, "restart the game to apply compatibility stage 2/");
            StringAssert.Contains(session.FallbackReason, "generic D3D12");
            Assert.IsFalse(session.Observing);
        }

        [TestMethod]
        public void ForeignPresenterOnVendorAware_EscalatesLiveWhenTheHookAdvertisesIt()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot
            {
                Version = 2,
                LiveReloadCapabilities = 0x6,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter
            };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 2000);

            Assert.IsFalse(session.RestartPending);
            Assert.IsTrue(session.Observing);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            CollectionAssert.AreEqual(
                new[] { HookProbeActionKind.LogVerdict, HookProbeActionKind.EscalateLive,
                        HookProbeActionKind.PublishStage, HookProbeActionKind.SetFallback },
                actions.Select(a => a.Kind).ToArray());
            Assert.IsNull(session.FallbackReason);

            // Until the hook echoes the new flags the old stand-down is not a verdict.
            Assert.AreEqual(0, session.Observe(true, foreign, EHookOverlayStatus.Initializing, 2300).Count);
            Assert.IsFalse(session.RestartPending);

            // Echo: the generic route is in effect; a clean sample sequence then confirms it.
            var applied = new NativeHookStatusSnapshot
            {
                Version = 2,
                LiveReloadCapabilities = 0x6,
                AppliedFlags = (uint)NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                Flags = Ready,
                CoverageSubmitted = 3
            };
            Assert.AreEqual(0, session.Observe(true, applied, EHookOverlayStatus.Active, 2600).Count);
            Assert.AreEqual(0, session.Observe(true, applied, EHookOverlayStatus.Active, 3000).Count);
            IReadOnlyList<HookProbeAction> learned = session.Observe(true, applied,
                EHookOverlayStatus.Active, 3000 + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.IsTrue(learned.Any(a => a.Kind == HookProbeActionKind.Learn &&
                                           a.Stage.Id == HookCompatibilityStageId.Generic));
        }

        [TestMethod]
        public void ForeignPresenterOnGeneric_EscalatesLiveToTheFidelityFxFreeStage()
        {
            // The mid-session case: a FidelityFX replacement swapchain takes presentation over
            // while the generic route is already running. A hook that advertises the lifecycle
            // bit can drop that claim in place, so this must not end in a restart.
            HookTargetEvidence evidence = HookCompatibilityStagePlannerTest.Evidence(
                streamline: true, ffxFg: true, d3d12: true, attach: HookAttachMode.Late);
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(evidence, null,
                null, "hash", autoCompatibility: true);
            var session = new HookCompatibilityProbeSession(4242, plan);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            session.OnInjectionSucceeded(1000);

            var standDown = new NativeHookStatusSnapshot
            {
                Version = 2,
                LiveReloadCapabilities = 0xE,
                AppliedFlags = (uint)NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute,
                Flags = Armed | NativeHookStatusFlags.PresentSeen |
                        NativeHookStatusFlags.ForeignPresenter
            };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, standDown,
                EHookOverlayStatus.Initializing, 2000);

            Assert.IsFalse(session.RestartPending);
            Assert.AreEqual(HookCompatibilityStageId.GenericNoFfxLifecycle,
                session.CurrentStage.Id);
            CollectionAssert.AreEqual(
                new[] { HookProbeActionKind.LogVerdict, HookProbeActionKind.EscalateLive,
                        HookProbeActionKind.PublishStage, HookProbeActionKind.SetFallback },
                actions.Select(a => a.Kind).ToArray());

            // Only once the hook echoes both routing bits is the new stage judged on its own.
            var applied = new NativeHookStatusSnapshot
            {
                Version = 2,
                LiveReloadCapabilities = 0xE,
                AppliedFlags = (uint)(NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute |
                    NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks),
                Flags = Ready,
                CoverageSubmitted = 5
            };
            Assert.AreEqual(0,
                session.Observe(true, applied, EHookOverlayStatus.Active, 2500).Count);
            IReadOnlyList<HookProbeAction> learned = session.Observe(true, applied,
                EHookOverlayStatus.Active,
                2500 + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.IsTrue(learned.Any(a => a.Kind == HookProbeActionKind.Learn &&
                a.Stage.Id == HookCompatibilityStageId.GenericNoFfxLifecycle));
        }

        [TestMethod]
        public void LiveEscalation_FallsBackToARestartWhenTheHookNeverEchoes()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot
            {
                Version = 2,
                LiveReloadCapabilities = 0x6,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter
            };
            session.Observe(true, foreign, EHookOverlayStatus.Initializing, 2000);

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 2000 + HookCompatibilityProbeSession.LiveApplyGraceMs);

            Assert.IsTrue(session.RestartPending);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.PendingStage.Id);
            Assert.IsTrue(actions.Any(a => a.Kind == HookProbeActionKind.ScheduleRestart));
            StringAssert.Contains(session.FallbackReason, "did not apply the live stage change");
        }

        [TestMethod]
        public void Success_LearnsTheStageAndClearsTheFallback()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);
            var active = new NativeHookStatusSnapshot { Version = 2, Flags = Ready, CoverageSubmitted = 5 };

            Assert.AreEqual(0, session.Observe(true, active, EHookOverlayStatus.Active, 1500).Count);
            IReadOnlyList<HookProbeAction> actions = session.Observe(true, active,
                EHookOverlayStatus.Active, 1500 + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);

            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.IsTrue(actions.Any(a => a.Kind == HookProbeActionKind.Learn));
            Assert.IsTrue(actions.Any(a => a.Kind == HookProbeActionKind.SetFallback && a.Reason == null));
            Assert.IsNull(session.FallbackReason);
        }

        [TestMethod]
        public void LastStageFailure_GivesUpAndMarksTheLadderExhausted()
        {
            HookCompatibilityProbeSession session = Session(streamline: false, d3d12: true);
            // Ladder without FG evidence: vendor-aware and generic only; start generic.
            Assert.AreEqual(2, session.StageCount);
            session.OnInjectionSucceeded(1000);
            var hidden = new NativeHookStatusSnapshot { Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.Visible };

            IReadOnlyList<HookProbeAction> first = session.Observe(true, hidden,
                EHookOverlayStatus.Initializing, 2000);
            IReadOnlyList<HookProbeAction> stalled = session.Observe(true, hidden,
                EHookOverlayStatus.Initializing, 2000 + HookOverlayManager.HookRendererReadyTimeoutMs);

            Assert.AreEqual(0, first.Count);
            Assert.IsTrue(session.RestartPending, "vendor-aware escalates to generic first");
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.PendingStage.Id);
            Assert.IsTrue(stalled.Any(a => a.Kind == HookProbeActionKind.ScheduleRestart));
        }

        [TestMethod]
        public void GenericWithoutFurtherStages_GivesUpExhausted()
        {
            HookTargetEvidence evidence = HookCompatibilityStagePlannerTest.Evidence(d3d12: true);
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(evidence, null,
                null, "hash", true);
            var pendingEntry = HookCompatibilityStagePlannerTest.Entry(
                HookCompatibilityStageId.VendorAware, false, "hash");
            pendingEntry.SetPending(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic), "r");
            plan = HookCompatibilityStagePlanner.Plan(evidence, null, pendingEntry, "hash", true);
            var session = new HookCompatibilityProbeSession(42, plan);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot { Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 2000);

            Assert.AreEqual(HookProbeSettlement.GaveUp, session.Settlement);
            HookProbeAction giveUp = actions.Single(a => a.Kind == HookProbeActionKind.GiveUp);
            Assert.IsTrue(giveUp.Exhausted);
            StringAssert.Contains(session.FallbackReason, "no further compatibility stage");
        }

        [TestMethod]
        public void StatusTimeout_IsRetriedOnceThenGivenUpWithoutExhausting()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);

            IReadOnlyList<HookProbeAction> first = session.Observe(false, default, null,
                1000 + HookOverlayManager.HookHandshakeTimeoutMs);
            Assert.IsTrue(session.RestartPending);
            Assert.AreEqual(session.CurrentStage.Key, session.PendingStage.Key);
            Assert.IsTrue(first.Any(a => a.Kind == HookProbeActionKind.ScheduleRestart));

            // Same session, fresh injection, same silence.
            session.OnInjectionSucceeded(10000);
            IReadOnlyList<HookProbeAction> second = session.Observe(false, default, null,
                10000 + HookOverlayManager.HookHandshakeTimeoutMs);
            Assert.AreEqual(HookProbeSettlement.GaveUp, session.Settlement);
            Assert.IsFalse(second.Single(a => a.Kind == HookProbeActionKind.GiveUp).Exhausted);
        }

        [TestMethod]
        public void LegacyMode_OnlySetsAndClearsTheFallbackReason()
        {
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(
                HookCompatibilityStagePlannerTest.Evidence(streamline: true, d3d12: true), null,
                null, "hash", autoCompatibility: false);
            var session = new HookCompatibilityProbeSession(42, plan);
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot { Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter };
            var active = new NativeHookStatusSnapshot { Version = 2, Flags = Ready };

            IReadOnlyList<HookProbeAction> failed = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 2000);
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual(HookProbeActionKind.SetFallback, failed[0].Kind);
            StringAssert.Contains(failed[0].Reason, "frame-generation runtime");
            Assert.IsFalse(session.RestartPending);

            // The FG toggle went off in-game: the reason clears once the hook renders again.
            session.Observe(true, active, EHookOverlayStatus.Active, 3000);
            IReadOnlyList<HookProbeAction> recovered = session.Observe(true, active,
                EHookOverlayStatus.Active, 3000 + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);
            Assert.IsTrue(recovered.Any(a => a.Kind == HookProbeActionKind.SetFallback && a.Reason == null));
            Assert.IsFalse(recovered.Any(a => a.Kind == HookProbeActionKind.Learn));
        }

        [TestMethod]
        public void RestartPending_TakesARecoveryInsteadOfInsistingOnTheRestart()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot { Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter };
            var active = new NativeHookStatusSnapshot { Version = 2, Flags = Ready };
            session.Observe(true, foreign, EHookOverlayStatus.Initializing, 2000);
            Assert.IsTrue(session.RestartPending);

            session.Observe(true, active, EHookOverlayStatus.Active, 3000);
            IReadOnlyList<HookProbeAction> actions = session.Observe(true, active,
                EHookOverlayStatus.Active, 3000 + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);

            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.IsNull(session.PendingStage);
            Assert.IsTrue(actions.Any(a => a.Kind == HookProbeActionKind.Learn));
        }

        [TestMethod]
        public void InconclusiveSamples_PauseTheStageBudget()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            session.OnInjectionSucceeded(1000);
            var idle = new NativeHookStatusSnapshot { Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen };

            session.Observe(true, idle, EHookOverlayStatus.Idle, 1000 + HookCompatibilityVerdictClassifier.ProbeStageBudgetMs);
            session.Observe(true, idle, EHookOverlayStatus.Idle, 2000 + HookCompatibilityVerdictClassifier.ProbeStageBudgetMs);

            Assert.IsTrue(session.Observing);
            Assert.AreEqual(HookCompatibilityVerdict.Inconclusive, session.LastVerdict);
            Assert.AreEqual((long)HookCompatibilityVerdictClassifier.ProbeStageBudgetMs,
                session.RemainingBudgetMs(2000 + HookCompatibilityVerdictClassifier.ProbeStageBudgetMs));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void HiddenSamples_PauseBothTheBudgetAndQueueTimeout(bool generic)
        {
            HookTargetEvidence evidence = HookCompatibilityStagePlannerTest.Evidence(d3d12: generic);
            HookLearnedProfileEntry entry = generic
                ? HookCompatibilityStagePlannerTest.Entry(HookCompatibilityStageId.Generic, true, "hash")
                : null;
            var session = new HookCompatibilityProbeSession(42,
                HookCompatibilityStagePlanner.Plan(evidence, null, entry, "hash", true));
            session.OnInjectionSucceeded(1000);
            var hidden = new NativeHookStatusSnapshot
            {
                Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen,
                AppliedFlags = (uint)session.CurrentStage.Flags, QueueState = NativeHookQueueState.None
            };

            Assert.AreEqual(0, session.Observe(true, hidden, EHookOverlayStatus.Hidden, 2000).Count);
            Assert.AreEqual(0, session.Observe(true, hidden, EHookOverlayStatus.Hidden, 62000).Count);
            Assert.AreEqual(HookProbeSettlement.None, session.Settlement);
            Assert.AreEqual(HookCompatibilityVerdict.Inconclusive, session.LastVerdict);
            Assert.AreEqual((long)HookCompatibilityVerdictClassifier.ProbeStageBudgetMs,
                session.RemainingBudgetMs(63000), "the UI budget must also stay paused between samples");

            var visible = hidden;
            visible.Flags = Ready;
            visible.CoverageSubmitted = 3;
            session.Observe(true, visible, EHookOverlayStatus.Active, 164000);
            Assert.AreEqual((long)HookCompatibilityVerdictClassifier.ProbeStageBudgetMs,
                session.RemainingBudgetMs(164000), "resuming must not charge the hidden interval");
            Assert.IsTrue(session.Observe(true, visible, EHookOverlayStatus.Active, 166000)
                .Any(action => action.Kind == HookProbeActionKind.Learn));
        }

        [TestMethod]
        public void LearnedStage_FgToggleEscalatesWithoutANewEvidenceSignature()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            LearnVendorStage(session);
            var foreign = new NativeHookStatusSnapshot
            {
                Version = 2, LiveReloadCapabilities = 0xE,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter
            };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 6000);

            Assert.IsTrue(session.Observing);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            Assert.IsTrue(actions.Any(action => action.Kind == HookProbeActionKind.EscalateLive));
            Assert.IsTrue(actions.Any(action => action.Kind == HookProbeActionKind.SetPollInterval &&
                action.PollIntervalMs == HookCompatibilityProbeSession.ProbePollIntervalMs));
            Assert.AreEqual(0, session.Observe(true, foreign, EHookOverlayStatus.Initializing, 6250).Count);
        }

        [TestMethod]
        public void LearnedStage_RendererFailureEnablesFallback()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            LearnVendorStage(session);
            var error = new NativeHookStatusSnapshot
            {
                Version = 2, Flags = Armed | NativeHookStatusFlags.Error, LastError = 2
            };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, error,
                EHookOverlayStatus.Error, 6000);

            Assert.AreEqual(HookProbeSettlement.GaveUp, session.Settlement);
            Assert.IsTrue(actions.Any(action => action.Kind == HookProbeActionKind.SetFallback &&
                action.Reason != null));
            Assert.IsFalse(actions.Single(action => action.Kind == HookProbeActionKind.GiveUp).Exhausted);
        }

        [TestMethod]
        public void LearnedStage_HealthyTimeDoesNotExhaustALaterInitializationBudget()
        {
            HookCompatibilityProbeSession session = Session(streamline: true);
            LearnVendorStage(session);
            var active = new NativeHookStatusSnapshot { Version = 2, Flags = Ready };
            Assert.AreEqual(0, session.Observe(true, active, EHookOverlayStatus.Active, 100000).Count);
            var initializing = new NativeHookStatusSnapshot
            {
                Version = 2, Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.Visible
            };

            Assert.AreEqual(0, session.Observe(true, initializing, EHookOverlayStatus.Initializing, 101000).Count);
            Assert.AreEqual(HookCompatibilityVerdict.Pending, session.LastVerdict);
            Assert.AreEqual(0, session.Observe(true, initializing, EHookOverlayStatus.Initializing, 102000).Count);
        }

        [TestMethod]
        public void VerifiedProfileFailure_TriesTheNextRouteInsteadOfExhaustingTheTitle()
        {
            HookTargetEvidence evidence = HookCompatibilityStagePlannerTest.Evidence(ffxFg: true);
            HookLearnedProfileEntry learned = HookCompatibilityStagePlannerTest.Entry(
                HookCompatibilityStageId.VendorAware, true, "hash");
            var session = new HookCompatibilityProbeSession(42,
                HookCompatibilityStagePlanner.Plan(evidence, null, learned, "hash", true));
            session.OnInjectionSucceeded(1000);
            var foreign = new NativeHookStatusSnapshot
            {
                Version = 2, LiveReloadCapabilities = 0xE,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter
            };

            IReadOnlyList<HookProbeAction> actions = session.Observe(true, foreign,
                EHookOverlayStatus.Initializing, 2000);

            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            Assert.IsFalse(actions.Any(action => action.Kind == HookProbeActionKind.GiveUp));
        }

        [TestMethod]
        public void ReplannedSession_DoesNotAttributeTheOldStandDownToAnUnappliedRoute()
        {
            HookTargetEvidence evidence = HookCompatibilityStagePlannerTest.Evidence(ffxFg: true);
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Replan(evidence,
                null, null, "hash", true, HookCompatibilityStage.Create(HookCompatibilityStageId.VendorAware));
            var session = new HookCompatibilityProbeSession(42, plan);
            session.OnInjectionSucceeded(1000);
            var old = new NativeHookStatusSnapshot
            {
                Version = 2, AppliedFlags = 0, LiveReloadCapabilities = 0xE,
                Flags = Armed | NativeHookStatusFlags.PresentSeen | NativeHookStatusFlags.ForeignPresenter
            };

            session.Observe(true, old, EHookOverlayStatus.Initializing, 1250);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            Assert.AreEqual(0, session.Observe(true, old, EHookOverlayStatus.Initializing, 1500).Count);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);

            // A superset is not an acknowledgement that the requested route is in effect.
            var wrong = old;
            wrong.AppliedFlags = 12;
            Assert.AreEqual(0, session.Observe(true, wrong, EHookOverlayStatus.Initializing, 1750).Count);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
        }

        private static void LearnVendorStage(HookCompatibilityProbeSession session)
        {
            session.OnInjectionSucceeded(1000);
            var active = new NativeHookStatusSnapshot { Version = 2, Flags = Ready };
            session.Observe(true, active, EHookOverlayStatus.Active, 2000);
            session.Observe(true, active, EHookOverlayStatus.Active, 4000);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
        }

        private static HookCompatibilityProbeSession Session(bool streamline, bool d3d12 = true)
        {
            HookTargetEvidence evidence = HookCompatibilityStagePlannerTest.Evidence(
                streamline: streamline, d3d12: d3d12, attach: HookAttachMode.Early);
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(evidence, null,
                null, "hash", autoCompatibility: true);
            return new HookCompatibilityProbeSession(42, plan);
        }
    }
}
