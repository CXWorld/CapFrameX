using System;
using System.Collections.Generic;
using CapFrameX.Contracts.Overlay;

namespace CapFrameX.OSD.Integration
{
    internal enum HookProbeActionKind
    {
        /// <summary>Write the stage's flags to the compatibility channel (new sequence).</summary>
        PublishStage,
        /// <summary>The hook can take the new stage without a fresh process.</summary>
        EscalateLive,
        /// <summary>Persist the next stage; it applies on the next launch of this title.</summary>
        ScheduleRestart,
        /// <summary>The stage rendered; persist it as verified.</summary>
        Learn,
        /// <summary>No stage left (or the failure is not a routing problem).</summary>
        GiveUp,
        /// <summary>Hook-free fallback reason for the manager; null clears it.</summary>
        SetFallback,
        SetPollInterval,
        LogVerdict
    }

    internal sealed class HookProbeAction
    {
        internal HookProbeAction(HookProbeActionKind kind, HookCompatibilityStage stage = null,
            string reason = null, int pollIntervalMs = 0,
            HookCompatibilityVerdict verdict = HookCompatibilityVerdict.Pending,
            bool exhausted = false)
        {
            Kind = kind;
            Stage = stage;
            Reason = reason;
            PollIntervalMs = pollIntervalMs;
            Verdict = verdict;
            Exhausted = exhausted;
        }

        internal HookProbeActionKind Kind { get; }
        internal HookCompatibilityStage Stage { get; }
        internal string Reason { get; }
        internal int PollIntervalMs { get; }
        internal HookCompatibilityVerdict Verdict { get; }
        internal bool Exhausted { get; }
    }

    internal enum HookProbeSettlement
    {
        None = 0,
        Learned,
        RestartPending,
        GaveUp,
        QueueRecovery
    }

    /// <summary>
    /// Per-PID state machine driving one stage plan: publishes a stage, observes the hook's
    /// status against the classifier's clocks, and turns each verdict into actions for the
    /// manager to execute. Pure — no timers, no I/O, no locks; the manager owns all of that
    /// and calls in with GetTickCount64 values.
    /// </summary>
    internal sealed class HookCompatibilityProbeSession
    {
        internal const int ProbePollIntervalMs = 250;
        internal const int IdlePollIntervalMs = 1000;

        /// <summary>
        /// After a live escalation the hook needs one poll interval to apply the new flags and
        /// clear its stand-down. Until it echoes the stage's flags in the status block, samples
        /// still describe the old stage; after this long without an echo the flip failed.
        /// </summary>
        internal const ulong LiveApplyGraceMs = 3000;

        private readonly HashSet<string> _tried = new HashSet<string>(StringComparer.Ordinal);
        private HookProbeTimings _timings;
        private ulong _observingSinceTickMs;
        private ulong _lastObservationTickMs;
        private bool _retriedStatusTimeout;
        private ulong _awaitingLiveApplyUntilTickMs;
        private bool _stageClockPaused;
        private int? _recoveryCoverageBaseline;

        internal HookCompatibilityProbeSession(int processId, HookCompatibilityStagePlan plan,
            bool hasRendered = false)
        {
            ProcessId = processId;
            HasRendered = hasRendered;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            StageIndex = plan.StartIndex;
            if (!plan.IsEmpty) _tried.Add(CurrentStage.Key);
        }

        internal int ProcessId { get; }
        internal HookCompatibilityStagePlan Plan { get; }
        internal HookTargetEvidence Evidence => Plan.Evidence;
        internal int StageIndex { get; private set; }
        internal int StageCount => Plan.Ladder.Count;
        internal HookCompatibilityStage CurrentStage =>
            Plan.IsEmpty ? null : Plan.Ladder[StageIndex];
        internal bool ProbingEnabled => Plan.ProbingEnabled;
        internal HookProbeSettlement Settlement { get; private set; }
        internal HookCompatibilityStage PendingStage { get; private set; }
        internal int PendingStageIndex { get; private set; } = -1;
        internal string FallbackReason { get; private set; }
        internal HookCompatibilityVerdict LastVerdict { get; private set; }
        internal NativeHookStatusSnapshot LastSnapshot { get; private set; }
        internal bool HasStatusSample { get; private set; }
        internal bool HasRendered { get; private set; }
        internal bool Observing =>
            Settlement == HookProbeSettlement.None && _timings.InjectionSucceeded &&
            !Plan.IsEmpty;
        internal bool RestartPending => Settlement == HookProbeSettlement.RestartPending;

        /// <summary>Stage number for status text, 1-based.</summary>
        internal int StageNumber => Plan.IsEmpty ? 0 : StageIndex + 1;

        internal long RemainingBudgetMs(ulong nowTickMs)
        {
            if (!Observing) return 0;
            ulong active = _timings.ActiveElapsedMs;
            if (!_stageClockPaused && nowTickMs > _lastObservationTickMs && _lastObservationTickMs != 0)
                active += nowTickMs - _lastObservationTickMs;
            return active >= HookCompatibilityVerdictClassifier.ProbeStageBudgetMs
                ? 0
                : (long)(HookCompatibilityVerdictClassifier.ProbeStageBudgetMs - active);
        }

        internal IReadOnlyList<HookProbeAction> OnInjectionSucceeded(ulong nowTickMs)
        {
            _timings = default;
            _timings.InjectionSucceeded = true;
            _timings.InjectionSucceededTickMs = nowTickMs;
            _observingSinceTickMs = nowTickMs;
            _lastObservationTickMs = nowTickMs;
            _stageClockPaused = false;
            HasStatusSample = false;
            if (Settlement == HookProbeSettlement.RestartPending)
            {
                // The manager re-injects only into a fresh process; a re-entry here means the
                // caller reused the session. Observe the current stage again.
                Settlement = HookProbeSettlement.None;
                PendingStage = null;
                PendingStageIndex = -1;
                FallbackReason = null;
            }
            return Plan.IsEmpty
                ? Array.Empty<HookProbeAction>()
                : new[] { new HookProbeAction(HookProbeActionKind.SetPollInterval,
                    pollIntervalMs: ProbePollIntervalMs) };
        }

        internal IReadOnlyList<HookProbeAction> Observe(bool hasStatus,
            NativeHookStatusSnapshot snapshot, EHookOverlayStatus? nativeState, ulong nowTickMs)
        {
            if (Plan.IsEmpty || !_timings.InjectionSucceeded)
                return Array.Empty<HookProbeAction>();
            if (hasStatus)
            {
                LastSnapshot = snapshot;
                HasStatusSample = true;
                _timings.LastNativeStatusTickMs = nowTickMs;
            }

            HookCompatibilityStage stage = CurrentStage;
            if (Settlement == HookProbeSettlement.QueueRecovery)
            {
                // Fallback intentionally hides the native renderer. Requiring Active here
                // would prevent it from ever rendering again. A fresh Present with a proven
                // queue permits one live retry; successful new submissions must confirm it.
                const NativeHookStatusFlags required = NativeHookStatusFlags.HooksArmed |
                    NativeHookStatusFlags.PresentSeen;
                const NativeHookStatusFlags blocked = NativeHookStatusFlags.Dormant |
                    NativeHookStatusFlags.Error | NativeHookStatusFlags.ForeignPresenter |
                    NativeHookStatusFlags.EarlyInjectionRequired;
                long age = HookStatusProbe.GetHeartbeatAgeMilliseconds(
                    snapshot.LastHeartbeatTickMs, nowTickMs);
                if (!hasStatus || snapshot.Version < HookStatusProbe.Version2 ||
                    snapshot.QueueState != NativeHookQueueState.Explicit ||
                    (snapshot.Flags & required) != required || (snapshot.Flags & blocked) != 0 ||
                    snapshot.PendingRestartFlags != 0 || snapshot.AppliedFlags != (uint)stage.Flags ||
                    age < 0 || (ulong)age > HookStatusProbe.HeartbeatStaleAfterMs)
                    return Array.Empty<HookProbeAction>();

                Settlement = HookProbeSettlement.None;
                FallbackReason = null;
                _recoveryCoverageBaseline = snapshot.CoverageSubmitted;
                ResetStageClocks(nowTickMs);
                return new[] { new HookProbeAction(HookProbeActionKind.SetFallback) };
            }
            if (_awaitingLiveApplyUntilTickMs != 0)
            {
                bool applied = hasStatus &&
                    snapshot.AppliedFlags == (uint)stage.Flags && snapshot.PendingRestartFlags == 0;
                if (applied)
                {
                    // The hook runs the new stage from here; its clocks start now and this
                    // sample already counts for the new stage.
                    _awaitingLiveApplyUntilTickMs = 0;
                    ResetStageClocks(nowTickMs);
                }
                else if (nowTickMs < _awaitingLiveApplyUntilTickMs)
                {
                    return Array.Empty<HookProbeAction>();
                }
                else
                {
                    // No echo: the flip did not reach the hook. Apply the stage the slow way.
                    _awaitingLiveApplyUntilTickMs = 0;
                    return ScheduleRestartFor(stage, stage,
                        "the hook did not apply the live stage change",
                        HookCompatibilityVerdict.Pending);
                }
            }
            UpdateClocks(hasStatus, snapshot, nativeState, nowTickMs);
            _timings.NowTickMs = nowTickMs;
            HookCompatibilityVerdict verdict = HookCompatibilityVerdictClassifier.Classify(
                stage, hasStatus, snapshot, nativeState, in _timings);
            if (hasStatus && nativeState == EHookOverlayStatus.Active &&
                (snapshot.Flags & NativeHookStatusFlags.Rendered) != 0 &&
                snapshot.QueueState != NativeHookQueueState.BindingUnavailable)
                HasRendered = true;
            // Coverage is cumulative for the process. Old draws before an FG switch cannot
            // verify the recovery; require submissions after the fallback was lifted.
            if (verdict == HookCompatibilityVerdict.Success && _recoveryCoverageBaseline.HasValue &&
                snapshot.CoverageSubmitted == _recoveryCoverageBaseline.Value)
                verdict = _timings.ActiveElapsedMs >= HookCompatibilityVerdictClassifier.ProbeStageBudgetMs
                    ? HookCompatibilityVerdict.QueueRebinding : HookCompatibilityVerdict.Pending;
            LastVerdict = verdict;

            // After a restart was scheduled the hook stays resident; if it recovers on its own
            // (an in-game FG toggle), take the win instead of insisting on the restart.
            if (Settlement == HookProbeSettlement.RestartPending)
            {
                if (verdict != HookCompatibilityVerdict.Success)
                    return Array.Empty<HookProbeAction>();
                Settlement = HookProbeSettlement.None;
                PendingStage = null;
                PendingStageIndex = -1;
            }
            else if (Settlement == HookProbeSettlement.Learned)
            {
                if (verdict == HookCompatibilityVerdict.Success ||
                    verdict == HookCompatibilityVerdict.Pending ||
                    verdict == HookCompatibilityVerdict.Inconclusive)
                    return Array.Empty<HookProbeAction>();

                // Learning does not end health monitoring. FG can be switched on with every
                // provider DLL already resident, so no evidence-signature change is required.
                Settlement = HookProbeSettlement.None;
                var actions = new List<HookProbeAction>
                {
                    new HookProbeAction(HookProbeActionKind.SetPollInterval,
                        pollIntervalMs: ProbePollIntervalMs)
                };
                actions.AddRange(Fail(stage, verdict, snapshot, nowTickMs));
                return actions;
            }
            else if (Settlement != HookProbeSettlement.None)
            {
                return RecoverableFallback(verdict, snapshot);
            }

            switch (verdict)
            {
                case HookCompatibilityVerdict.Pending:
                case HookCompatibilityVerdict.Inconclusive:
                    return Array.Empty<HookProbeAction>();

                case HookCompatibilityVerdict.Success:
                    Settlement = HookProbeSettlement.Learned;
                    _recoveryCoverageBaseline = null;
                    FallbackReason = null;
                    return ProbingEnabled
                        ? new[]
                        {
                            new HookProbeAction(HookProbeActionKind.LogVerdict, stage,
                                verdict: verdict),
                            new HookProbeAction(HookProbeActionKind.Learn, stage),
                            new HookProbeAction(HookProbeActionKind.SetFallback),
                            new HookProbeAction(HookProbeActionKind.SetPollInterval,
                                pollIntervalMs: IdlePollIntervalMs)
                        }
                        : new[]
                        {
                            new HookProbeAction(HookProbeActionKind.SetFallback),
                            new HookProbeAction(HookProbeActionKind.SetPollInterval,
                                pollIntervalMs: IdlePollIntervalMs)
                        };

                default:
                    return Fail(stage, verdict, snapshot, nowTickMs);
            }
        }

        private IReadOnlyList<HookProbeAction> Fail(HookCompatibilityStage stage,
            HookCompatibilityVerdict verdict, NativeHookStatusSnapshot snapshot, ulong nowTickMs)
        {
            // An FG swapchain replacement is a live binding problem, not evidence that
            // injection should happen earlier at the next launch. Keep this stage and
            // never persist an early-injection retry or exhaust the learned profile.
            if (verdict == HookCompatibilityVerdict.QueueRebinding ||
                (verdict == HookCompatibilityVerdict.NoQueue && HasRendered))
            {
                Settlement = HookProbeSettlement.QueueRecovery;
                LastVerdict = HookCompatibilityVerdict.QueueRebinding;
                FallbackReason = HookCompatibilityVerdictClassifier.DescribeVerdict(
                    LastVerdict, snapshot, _timings.LastNativeStatusTickMs);
                return new[]
                {
                    new HookProbeAction(HookProbeActionKind.LogVerdict, stage, verdict: LastVerdict),
                    new HookProbeAction(HookProbeActionKind.SetFallback, reason: FallbackReason),
                    new HookProbeAction(HookProbeActionKind.SetPollInterval,
                        pollIntervalMs: ProbePollIntervalMs)
                };
            }
            string description = HookCompatibilityVerdictClassifier.DescribeVerdict(verdict,
                snapshot, _timings.LastNativeStatusTickMs);

            if (!ProbingEnabled)
            {
                // Legacy behaviour: one stage, its verdict is the fallback reason. The flag-driven
                // verdicts stay recoverable (an in-game FG toggle clears them again).
                if (IsRecoverable(verdict))
                {
                    FallbackReason = description;
                    return new[]
                    {
                        new HookProbeAction(HookProbeActionKind.SetFallback, reason: description)
                    };
                }
                Settlement = HookProbeSettlement.GaveUp;
                FallbackReason = description;
                return new[]
                {
                    new HookProbeAction(HookProbeActionKind.SetFallback, reason: description),
                    new HookProbeAction(HookProbeActionKind.SetPollInterval,
                        pollIntervalMs: IdlePollIntervalMs)
                };
            }

            HookCompatibilityStage next;
            bool exhausted = false;
            if (verdict == HookCompatibilityVerdict.StatusTimeout && !_retriedStatusTimeout)
            {
                _retriedStatusTimeout = true;
                next = stage;
            }
            else
            {
                next = HookCompatibilityStagePlanner.ResolveEscalation(stage, verdict, snapshot,
                    Evidence, Plan.Ladder, _tried, out exhausted);
            }

            if (next == null)
            {
                Settlement = HookProbeSettlement.GaveUp;
                FallbackReason = exhausted
                    ? $"{description}; no further compatibility stage to try"
                    : description;
                return new[]
                {
                    new HookProbeAction(HookProbeActionKind.LogVerdict, stage, verdict: verdict),
                    new HookProbeAction(HookProbeActionKind.GiveUp, stage, FallbackReason,
                        verdict: verdict, exhausted: exhausted),
                    new HookProbeAction(HookProbeActionKind.SetFallback, reason: FallbackReason),
                    new HookProbeAction(HookProbeActionKind.SetPollInterval,
                        pollIntervalMs: IdlePollIntervalMs)
                };
            }

            int nextIndex = IndexOf(next);
            if (HookCompatibilityStagePlanner.CanEscalateLive(stage, next, snapshot))
            {
                StageIndex = nextIndex;
                _tried.Add(next.Key);
                ResetStageClocks(nowTickMs);
                _awaitingLiveApplyUntilTickMs = nowTickMs + LiveApplyGraceMs;
                FallbackReason = null;
                return new[]
                {
                    new HookProbeAction(HookProbeActionKind.LogVerdict, stage, verdict: verdict),
                    new HookProbeAction(HookProbeActionKind.EscalateLive, next),
                    new HookProbeAction(HookProbeActionKind.PublishStage, next),
                    new HookProbeAction(HookProbeActionKind.SetFallback)
                };
            }

            return ScheduleRestartFor(stage, next, description, verdict);
        }

        private IReadOnlyList<HookProbeAction> ScheduleRestartFor(HookCompatibilityStage from,
            HookCompatibilityStage next, string description, HookCompatibilityVerdict verdict)
        {
            int nextIndex = IndexOf(next);
            Settlement = HookProbeSettlement.RestartPending;
            PendingStage = next;
            PendingStageIndex = nextIndex;
            FallbackReason = $"{description}; restart the game to apply compatibility stage " +
                $"{nextIndex + 1}/{StageCount} ({next.DisplayName})";
            return new[]
            {
                new HookProbeAction(HookProbeActionKind.LogVerdict, from, verdict: verdict),
                new HookProbeAction(HookProbeActionKind.ScheduleRestart, next, FallbackReason,
                    verdict: verdict),
                new HookProbeAction(HookProbeActionKind.SetFallback, reason: FallbackReason),
                new HookProbeAction(HookProbeActionKind.SetPollInterval,
                    pollIntervalMs: IdlePollIntervalMs)
            };
        }

        // Legacy mode after a recoverable verdict: keep the fallback reason in step with the
        // hook's flags, exactly like the manager did before stages existed.
        private IReadOnlyList<HookProbeAction> RecoverableFallback(
            HookCompatibilityVerdict verdict, NativeHookStatusSnapshot snapshot)
        {
            if (Settlement != HookProbeSettlement.GaveUp || ProbingEnabled)
                return Array.Empty<HookProbeAction>();
            if (verdict == HookCompatibilityVerdict.Success)
            {
                Settlement = HookProbeSettlement.None;
                FallbackReason = null;
                return new[] { new HookProbeAction(HookProbeActionKind.SetFallback) };
            }
            return Array.Empty<HookProbeAction>();
        }

        private static bool IsRecoverable(HookCompatibilityVerdict verdict)
            => verdict == HookCompatibilityVerdict.ForeignPresenter ||
               verdict == HookCompatibilityVerdict.EarlyInjectionRequired ||
               verdict == HookCompatibilityVerdict.StatusTimeout;

        private void UpdateClocks(bool hasStatus, NativeHookStatusSnapshot snapshot,
            EHookOverlayStatus? nativeState, ulong nowTickMs)
        {
            ulong delta = !_stageClockPaused && nowTickMs > _lastObservationTickMs
                ? nowTickMs - _lastObservationTickMs
                : 0;
            _lastObservationTickMs = nowTickMs;
            _timings.TotalElapsedMs = nowTickMs > _observingSinceTickMs
                ? nowTickMs - _observingSinceTickMs
                : 0;

            bool inconclusive = hasStatus &&
                (nativeState == EHookOverlayStatus.Idle || nativeState == EHookOverlayStatus.Hidden ||
                 (snapshot.Flags & NativeHookStatusFlags.Dormant) != 0);
            _stageClockPaused = inconclusive;
            if (inconclusive)
            {
                // Paused, deliberately hidden or dormant: the phase clocks restart afterwards.
                _timings.WaitingSinceTickMs = 0;
                _timings.InitializingSinceTickMs = 0;
                _timings.NoQueueSinceTickMs = 0;
                _timings.SuccessSinceTickMs = 0;
                return;
            }
            // Minutes of healthy rendering are not part of a later failure's probe budget.
            if (Settlement == HookProbeSettlement.Learned && nativeState == EHookOverlayStatus.Active)
                _timings.ActiveElapsedMs = 0;
            else
                _timings.ActiveElapsedMs += delta;
            if (!hasStatus) return;

            NativeHookStatusFlags flags = snapshot.Flags;
            bool armed = (flags & NativeHookStatusFlags.HooksArmed) != 0;
            bool presentSeen = (flags & NativeHookStatusFlags.PresentSeen) != 0;
            _timings.WaitingSinceTickMs = armed && !presentSeen
                ? (_timings.WaitingSinceTickMs == 0 ? nowTickMs : _timings.WaitingSinceTickMs)
                : 0;
            _timings.InitializingSinceTickMs = nativeState == EHookOverlayStatus.Initializing
                ? (_timings.InitializingSinceTickMs == 0 ? nowTickMs : _timings.InitializingSinceTickMs)
                : 0;
            bool noQueue = presentSeen && snapshot.Version >= HookStatusProbe.Version2 &&
                snapshot.QueueState == NativeHookQueueState.None;
            _timings.NoQueueSinceTickMs = noQueue
                ? (_timings.NoQueueSinceTickMs == 0 ? nowTickMs : _timings.NoQueueSinceTickMs)
                : 0;
            _timings.SuccessSinceTickMs = nativeState == EHookOverlayStatus.Active
                ? (_timings.SuccessSinceTickMs == 0 ? nowTickMs : _timings.SuccessSinceTickMs)
                : 0;
        }

        private void ResetStageClocks(ulong nowTickMs)
        {
            bool injected = _timings.InjectionSucceeded;
            ulong injectedAt = _timings.InjectionSucceededTickMs;
            ulong lastStatus = _timings.LastNativeStatusTickMs;
            _timings = default;
            _timings.InjectionSucceeded = injected;
            _timings.InjectionSucceededTickMs = injectedAt;
            _timings.LastNativeStatusTickMs = lastStatus;
            _observingSinceTickMs = nowTickMs;
            _lastObservationTickMs = nowTickMs;
            _stageClockPaused = false;
        }

        private int IndexOf(HookCompatibilityStage stage)
        {
            for (int i = 0; i < Plan.Ladder.Count; i++)
            {
                if (ReferenceEquals(Plan.Ladder[i], stage) || Plan.Ladder[i].SameRouting(stage))
                    return i;
            }
            return StageIndex;
        }
    }
}
