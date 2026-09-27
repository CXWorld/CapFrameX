using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// GPU-independent regression for a working DLSS-FG overlay losing presentation when
    /// FSR-FG takes over. Module evidence and native status are synthetic; planning, verdicts,
    /// the manager's rearm gate and the session state machine are production code.
    /// </summary>
    [TestClass]
    public class HookFrameGenerationSwitchTest
    {
        private const int ProcessId = 4242;
        private const string HookHash = "fg-switch-test";
        private const int DlssFg = 1;
        private const int FsrFg = 3;
        private const NativeHookCompatibilityFlags Generic =
            NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute;
        private const NativeHookCompatibilityFlags GenericWithoutFidelityFx = Generic |
            NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks;

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DlssToFsrAndBack_AfterLearning_RecoversLiveWithoutGameRestart(
            bool fidelityFxResidentAtPlanTime)
        {
            HookTargetEvidence initialEvidence = Evidence(fidelityFxResidentAtPlanTime);
            HookCompatibilityStagePlan plan = HookCompatibilityStagePlanner.Plan(initialEvidence,
                null, null, HookHash, autoCompatibility: true);
            var session = new HookCompatibilityProbeSession(ProcessId, plan);
            Assert.AreEqual(HookCompatibilityStageId.Generic, session.CurrentStage.Id);
            Assert.AreEqual(fidelityFxResidentAtPlanTime,
                plan.Ladder.Any(stage => stage.Id == HookCompatibilityStageId.GenericNoFfxLifecycle));
            session.OnInjectionSucceeded(1000);

            // DLSS-FG renders long enough to settle the original probe. The bug is after
            // learning, not a provider switch while the initial probe is still running.
            Observe(session, Snapshot(DlssFg, Generic, rendering: true), 2000);
            IReadOnlyList<HookProbeAction> learned = Observe(session,
                Snapshot(DlssFg, Generic, rendering: true),
                2000 + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.IsTrue(learned.Any(action => action.Kind == HookProbeActionKind.Learn));

            // The game enables FSR-FG and a FidelityFX replacement swapchain takes ownership.
            // Either its modules were already known, or they appear only at this switch.
            HookTargetEvidence fsrEvidence = Evidence(fidelityFx: true);
            var fsrStandDown = Snapshot(FsrFg, Generic, rendering: false);
            ulong switchTickMs = 6000;
            IReadOnlyList<HookProbeAction> actions = Observe(session, fsrStandDown, switchTickMs);

            if (!fidelityFxResidentAtPlanTime)
            {
                Assert.AreNotEqual(initialEvidence.Signature, fsrEvidence.Signature);
                HookCompatibilityStagePlan refreshed = HookCompatibilityStagePlanner.Replan(
                    fsrEvidence, null, null, HookHash, true, session.CurrentStage);
                HookCompatibilityStage next = refreshed.Ladder.Single(stage =>
                    stage.Id == HookCompatibilityStageId.GenericNoFfxLifecycle &&
                    !stage.RequiresEarlyInjection);
                Assert.IsTrue(HookCompatibilityStagePlanner.CanEscalateLive(
                    session.CurrentStage, next, fsrStandDown),
                    "The newly discovered FidelityFX-free route is live-applicable on this hook.");

                // Regression: the old ladder schedules Generic+early, then the manager rejects
                // RestartPending before scanning the newly loaded modules. Do not bypass that
                // production gate with an unconditional manual re-plan in the test.
                Assert.IsTrue(HookOverlayManager.CanRearmProbeForChangedEvidence(session.Settlement),
                    $"New FSR evidence must permit re-planning the running hook, but the manager " +
                    $"rejects {session.Settlement} (pending: {session.PendingStage?.Key}).");

                session = new HookCompatibilityProbeSession(ProcessId, refreshed);
                switchTickMs += HookOverlayManager.ProbeRearmStandDownMs;
                session.OnInjectionSucceeded(switchTickMs);
                actions = Observe(session, fsrStandDown, switchTickMs);
            }

            Assert.IsFalse(session.RestartPending, "Switching providers must not require a game restart.");
            Assert.IsFalse(actions.Any(action => action.Kind == HookProbeActionKind.ScheduleRestart));
            Assert.AreEqual(HookCompatibilityStageId.GenericNoFfxLifecycle, session.CurrentStage.Id);
            Assert.IsTrue(actions.Any(action => action.Kind == HookProbeActionKind.EscalateLive));
            Assert.AreEqual(GenericWithoutFidelityFx,
                actions.Single(action => action.Kind == HookProbeActionKind.PublishStage).Stage.Flags);

            // A last sample from the old route must not fail the requested route before its echo.
            Assert.AreEqual(0, Observe(session, fsrStandDown, switchTickMs + 250).Count);
            Assert.IsTrue(session.Observing);
            ulong appliedTickMs = switchTickMs + 500;
            var fsrRendering = Snapshot(FsrFg, GenericWithoutFidelityFx, rendering: true);
            Observe(session, fsrRendering, appliedTickMs);
            IReadOnlyList<HookProbeAction> recovered = Observe(session, fsrRendering,
                appliedTickMs + HookCompatibilityVerdictClassifier.ProbeSuccessConfirmMs);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.IsTrue(recovered.Any(action => action.Kind == HookProbeActionKind.Learn &&
                action.Stage.Id == HookCompatibilityStageId.GenericNoFfxLifecycle));
            Assert.IsNull(session.FallbackReason);

            // Switching back to DLSS keeps the compatible native route; live-only routing bits
            // must not be removed just because the frame-generation provider changed again.
            IReadOnlyList<HookProbeAction> backToDlss = Observe(session,
                Snapshot(DlssFg, GenericWithoutFidelityFx, rendering: true), appliedTickMs + 4000);
            Assert.AreEqual(0, backToDlss.Count);
            Assert.AreEqual(HookProbeSettlement.Learned, session.Settlement);
            Assert.AreEqual(GenericWithoutFidelityFx, session.CurrentStage.Flags);
        }

        private static HookTargetEvidence Evidence(bool fidelityFx)
            => HookCompatibilityStagePlannerTest.Evidence(streamline: true, dlssg: true,
                ffxFg: fidelityFx, loaderCopies: fidelityFx ? 1 : 0, d3d12: true,
                attach: HookAttachMode.Late);

        private static IReadOnlyList<HookProbeAction> Observe(HookCompatibilityProbeSession session,
            NativeHookStatusSnapshot snapshot, ulong nowTickMs)
        {
            snapshot.LastHeartbeatTickMs = (long)nowTickMs;
            if ((snapshot.Flags & NativeHookStatusFlags.Rendered) != 0)
            {
                snapshot.Progress = new HookRenderProgress
                {
                    Generation = 1, Presents = nowTickMs, Draws = nowTickMs,
                    LastDrawTickMs = (long)nowTickMs, RouteSource = 1,
                    AppliedFlags = snapshot.AppliedFlags, AppliedSequence = snapshot.AppliedSequence
                };
            }
            HookOverlayStatus status = HookOverlayStatusEvaluator.EvaluateNative(ProcessId,
                "DXGI", snapshot, nowTickMs);
            return session.Observe(true, snapshot, status.State, nowTickMs);
        }

        private static NativeHookStatusSnapshot Snapshot(int technology,
            NativeHookCompatibilityFlags appliedFlags, bool rendering)
        {
            NativeHookStatusFlags flags = NativeHookStatusFlags.Loaded |
                NativeHookStatusFlags.HooksArmed | NativeHookStatusFlags.PresentSeen;
            flags |= rendering
                ? NativeHookStatusFlags.Visible | NativeHookStatusFlags.RendererReady |
                  NativeHookStatusFlags.MetricsConnected | NativeHookStatusFlags.Rendered
                : NativeHookStatusFlags.ForeignPresenter;
            return new NativeHookStatusSnapshot
            {
                Version = HookStatusProbe.Version2,
                Flags = flags,
                Api = NativeHookApi.D3D12,
                InstallPhase = NativeHookInstallPhase.Done,
                AppliedFlags = (uint)appliedFlags,
                LiveReloadCapabilities = 0xE,
                CompatChannelVersion = 2,
                CoverageAttempts = 20,
                CoverageSubmitted = 10,
                FgTechnology = technology,
                FgActivity = 2,
                FgAuthoritative = true,
                StreamlineDlssgMode = technology == DlssFg ? 2 : 1,
                QueueState = NativeHookQueueState.Observed,
                LastDeclineReason = rendering ? NativeHookDeclineReason.None :
                    NativeHookDeclineReason.FidelityFxOwnsPresentation
            };
        }
    }
}
