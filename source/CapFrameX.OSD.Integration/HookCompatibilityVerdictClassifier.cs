using CapFrameX.Contracts.Overlay;

namespace CapFrameX.OSD.Integration
{
    internal enum HookCompatibilityVerdict
    {
        /// <summary>Nothing decided yet; keep observing.</summary>
        Pending = 0,
        Success,
        ForeignPresenter,
        EarlyInjectionRequired,
        InstallHung,
        InstallFailed,
        StatusTimeout,
        NoPresent,
        NoQueue,
        RendererStalled,
        OsdCreateFailed,
        /// <summary>Paused / minimized / host dormant: the stage clock stops.</summary>
        Inconclusive,
        /// <summary>A replacement swapchain has no proven presentation queue yet.</summary>
        QueueRebinding
    }

    /// <summary>
    /// Clocks the session keeps for the stage under observation. All in GetTickCount64
    /// milliseconds; a zero "since" value means the condition does not hold right now.
    /// </summary>
    internal struct HookProbeTimings
    {
        public ulong NowTickMs;
        public bool InjectionSucceeded;
        public ulong InjectionSucceededTickMs;
        public ulong LastNativeStatusTickMs;
        public ulong WaitingSinceTickMs;
        public ulong InitializingSinceTickMs;
        public ulong NoQueueSinceTickMs;
        public ulong SuccessSinceTickMs;
        public bool RenderProgressConfirmed;
        /// <summary>Observation time that was not paused by an inconclusive sample.</summary>
        public ulong ActiveElapsedMs;
        /// <summary>Wall time since the stage started observing, pauses included.</summary>
        public ulong TotalElapsedMs;
    }

    /// <summary>
    /// Pure: one status sample plus the session clocks → the verdict on the current stage.
    /// First matching row wins, most severe first.
    /// </summary>
    internal static class HookCompatibilityVerdictClassifier
    {
        internal const ulong ProbeSuccessConfirmMs = 2000;
        internal const ulong ProbeInstallHungMs = 6000;
        internal const ulong ProbeNoQueueMs = 5000;
        internal const ulong ProbeStageBudgetMs = 20000;
        internal const ulong ProbeInconclusiveMaxMs = 60000;

        internal static HookCompatibilityVerdict Classify(HookCompatibilityStage stage,
            bool hasStatus, NativeHookStatusSnapshot snapshot, EHookOverlayStatus? nativeState,
            in HookProbeTimings timings)
        {
            if (!hasStatus)
            {
                return HookOverlayManager.HasHookStatusTimedOut(timings.InjectionSucceeded,
                    timings.InjectionSucceededTickMs, timings.LastNativeStatusTickMs,
                    hasNativeStatus: false, timings.NowTickMs)
                    ? HookCompatibilityVerdict.StatusTimeout
                    : HookCompatibilityVerdict.Pending;
            }

            NativeHookStatusFlags flags = snapshot.Flags;
            if ((flags & NativeHookStatusFlags.Error) != 0)
            {
                if (snapshot.LastError == 2) return HookCompatibilityVerdict.OsdCreateFailed;
                if (snapshot.LastError == 1) return HookCompatibilityVerdict.InstallFailed;
            }

            if ((flags & NativeHookStatusFlags.HooksArmed) == 0)
            {
                bool hung = timings.InjectionSucceeded &&
                    timings.InjectionSucceededTickMs > 0 &&
                    timings.NowTickMs >= timings.InjectionSucceededTickMs &&
                    timings.NowTickMs - timings.InjectionSucceededTickMs >= ProbeInstallHungMs &&
                    (snapshot.Version < HookStatusProbe.Version2 ||
                     snapshot.InstallPhase != NativeHookInstallPhase.Done);
                return hung ? HookCompatibilityVerdict.InstallHung : HookCompatibilityVerdict.Pending;
            }

            if ((flags & NativeHookStatusFlags.EarlyInjectionRequired) != 0)
                return HookCompatibilityVerdict.EarlyInjectionRequired;
            if ((flags & NativeHookStatusFlags.ForeignPresenter) != 0)
                return HookCompatibilityVerdict.ForeignPresenter;

            if (nativeState == EHookOverlayStatus.Idle || nativeState == EHookOverlayStatus.Hidden ||
                (flags & NativeHookStatusFlags.Dormant) != 0)
                return HookCompatibilityVerdict.Inconclusive;

            if (snapshot.Version >= HookStatusProbe.Version2 &&
                (flags & NativeHookStatusFlags.PresentSeen) != 0 &&
                snapshot.QueueState == NativeHookQueueState.BindingUnavailable)
                return HookCompatibilityVerdict.QueueRebinding;

            if (stage != null && stage.IsGeneric && snapshot.Version >= HookStatusProbe.Version2 &&
                (flags & NativeHookStatusFlags.PresentSeen) != 0 &&
                snapshot.QueueState == NativeHookQueueState.None &&
                Elapsed(timings.NoQueueSinceTickMs, timings.NowTickMs) >= ProbeNoQueueMs)
                return HookCompatibilityVerdict.NoQueue;

            if ((flags & NativeHookStatusFlags.PresentSeen) == 0 &&
                HookOverlayManager.HasFirstPresentTimedOut(timings.WaitingSinceTickMs,
                    timings.NowTickMs))
                return HookCompatibilityVerdict.NoPresent;

            if (nativeState == EHookOverlayStatus.Initializing &&
                HookOverlayManager.HasRendererInitializationStalled(
                    timings.InitializingSinceTickMs, timings.NowTickMs))
                return HookCompatibilityVerdict.RendererStalled;

            if (nativeState == EHookOverlayStatus.Active)
            {
                bool coverageProven = stage == null || !stage.IsGeneric ||
                    snapshot.Version < HookStatusProbe.Version2 ||
                    snapshot.CoverageSubmitted > 0;
                if (coverageProven && timings.RenderProgressConfirmed &&
                    Elapsed(timings.SuccessSinceTickMs, timings.NowTickMs) >= ProbeSuccessConfirmMs)
                    return HookCompatibilityVerdict.Success;
                if (snapshot.Progress.HasValue && !timings.RenderProgressConfirmed &&
                    timings.ActiveElapsedMs >= ProbeStageBudgetMs)
                    return HookCompatibilityVerdict.RendererStalled;
                return HookCompatibilityVerdict.Pending;
            }

            if (timings.ActiveElapsedMs >= ProbeStageBudgetMs)
                return HookCompatibilityVerdict.RendererStalled;
            return HookCompatibilityVerdict.Pending;
        }

        /// <summary>The wording the hook-free fallback carries for a failed stage.</summary>
        internal static string DescribeVerdict(HookCompatibilityVerdict verdict,
            NativeHookStatusSnapshot snapshot, ulong lastNativeStatusTickMs)
        {
            switch (verdict)
            {
                case HookCompatibilityVerdict.EarlyInjectionRequired:
                    return "XeSS-FG was initialized before the hook captured its " +
                        "authoritative D3D12 queue; early injection is required";
                case HookCompatibilityVerdict.ForeignPresenter:
                {
                    string technology = HookOverlayStatusEvaluator
                        .DescribeFrameGenerationTechnology(snapshot.FgTechnology);
                    return technology == null
                        ? "a frame-generation runtime (FSR FG / DLSS FG / XeSS FG) is " +
                          "presenting this game; the in-game overlay stands down"
                        : $"a frame-generation runtime ({technology}) is presenting this " +
                          "game; the in-game overlay stands down";
                }
                case HookCompatibilityVerdict.NoPresent:
                    return "the in-game hook did not observe a DXGI Present within " +
                        $"{HookOverlayManager.HookFirstPresentTimeoutMs / 1000} seconds after it was armed";
                case HookCompatibilityVerdict.RendererStalled:
                {
                    string decline = HookOverlayStatusEvaluator.DescribeDeclineReason(
                        snapshot.LastDeclineReason);
                    string text = "the in-game renderer did not initialize within " +
                        $"{HookOverlayManager.HookRendererReadyTimeoutMs / 1000} seconds";
                    return decline == null ? text : $"{text} ({decline})";
                }
                case HookCompatibilityVerdict.InstallHung:
                {
                    string phase = HookOverlayStatusEvaluator.DescribeInstallPhase(
                        snapshot.InstallPhase, snapshot.InstallDetail);
                    string text = "the in-game hook did not finish installing within " +
                        $"{ProbeInstallHungMs / 1000} seconds";
                    return phase == null ? text : $"{text} (stopped in phase {phase})";
                }
                case HookCompatibilityVerdict.InstallFailed:
                    return "DXGI hook installation failed";
                case HookCompatibilityVerdict.OsdCreateFailed:
                    return "OSD renderer creation failed";
                case HookCompatibilityVerdict.NoQueue:
                    return "no compatible D3D12 command queue was observed within " +
                        $"{ProbeNoQueueMs / 1000} seconds";
                case HookCompatibilityVerdict.QueueRebinding:
                    return "waiting for the replacement swapchain's presentation queue; " +
                        "live recovery remains enabled";
                case HookCompatibilityVerdict.StatusTimeout:
                    return lastNativeStatusTickMs > 0
                        ? $"native hook status was unavailable for more than {HookOverlayManager.HookHandshakeTimeoutMs / 1000} seconds"
                        : $"native hook did not publish status within {HookOverlayManager.HookHandshakeTimeoutMs / 1000} seconds after injection";
                default:
                    return verdict.ToString();
            }
        }

        private static ulong Elapsed(ulong sinceTickMs, ulong nowTickMs)
            => sinceTickMs == 0 || nowTickMs < sinceTickMs ? 0 : nowTickMs - sinceTickMs;
    }
}
