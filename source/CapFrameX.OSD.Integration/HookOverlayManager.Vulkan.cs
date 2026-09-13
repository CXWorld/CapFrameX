using System;
using CapFrameX.Contracts.Overlay;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    public sealed partial class HookOverlayManager
    {
        private readonly object _vulkanLearningGate = new object();
        private HookVulkanCompatibilityChannel _vulkanChannel;
        private HookVulkanProbeSession _vulkanSession;
        private int _vulkanLearningPid;
        private volatile string _vulkanFallbackReason;

        private void ResetVulkanLearning()
        {
            lock (_vulkanLearningGate)
            {
                if (_vulkanChannel != null)
                {
                    try
                    {
                        _vulkanChannel.Publish(VulkanCompositeRoute.Automatic, 0, false,
                            HookStatusProbe.CurrentTickCount, true);
                    }
                    finally { _vulkanChannel.Dispose(); }
                }
                _vulkanChannel = null;
                _vulkanSession = null;
                _vulkanLearningPid = 0;
                _vulkanFallbackReason = null;
            }
        }

        private HookOverlayStatus PollVulkanLearning(int pid, VulkanActivitySnapshot activity, ulong nowMs)
        {
            lock (_vulkanLearningGate)
            {
                if (_disposed || pid != _currentPid || !_enabled || !_autoCompatibility)
                {
                    ResetVulkanLearning();
                    return null;
                }
                if (_vulkanLearningPid != pid) ResetVulkanLearning();
                if (_vulkanChannel == null)
                {
                    if (!HookVulkanCompatibilityChannel.TryOpen(pid, out _vulkanChannel)) return null;
                    _vulkanLearningPid = pid;
                }
                if (!_vulkanChannel.TryRead(out VulkanProbeSnapshot snapshot))
                {
                    _vulkanSession?.Observe(default, nowMs, false);
                    return new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, "Vulkan",
                        $"PID {pid}, Vulkan: waiting for consistent compatibility telemetry.");
                }
                if (!TryReadProcessIdentity(pid, out string name, out string path)) return null;
                _learnedStore.TryGet(name, snapshot.Signature, out HookLearnedProfileEntry learned);
                bool reset = _vulkanSession?.HasPersistedOutcome == true && learned == null;
                bool publish = _vulkanSession == null || !_vulkanSession.Matches(snapshot) || reset;
                if (publish)
                {
                    _vulkanSession = new HookVulkanProbeSession(snapshot, learned);
                    _vulkanFallbackReason = null;
                    Log.Information("HookOverlay: Vulkan plan for pid {pid} ('{process}'), evidence {signature}, " +
                        "layer {build}, generation {generation}: {stage}", pid, name, snapshot.Signature,
                        snapshot.BuildHash, snapshot.Generation, _vulkanSession.Stage.DisplayName);
                }

                HookVulkanProbeSession session = _vulkanSession;
                VulkanCompositeRoute preceding = session.Route;
                VulkanProbeAction action = publish ? VulkanProbeAction.None :
                    session.Observe(snapshot, nowMs, _appConfiguration.IsOverlayActive);
                publish |= (action & VulkanProbeAction.Publish) != 0;
                uint revision = _vulkanChannel.Publish(session.Route, session.Context, true, nowMs, publish);
                if (publish) session.Published(revision, nowMs);

                if ((action & (VulkanProbeAction.Learn | VulkanProbeAction.Invalidate)) != 0)
                {
                    bool verified = (action & VulkanProbeAction.Learn) != 0;
                    var stage = HookVulkanProbeSession.ToStage(verified ? session.Route : preceding);
                    _learnedStore.Upsert(name, path, session.Signature, null, session.BuildHash, entry =>
                    {
                        entry.SetStage(stage);
                        entry.SetPending(null, null);
                        entry.Verified = verified;
                        entry.Exhausted = session.Exhausted;
                        entry.LastVerdict = verified ? "Success" : "VulkanCompositeFailed";
                        entry.LastVerdictDetail = $"native route {snapshot.ActualRoute}, result {snapshot.Result}";
                        entry.Attempts++;
                    });
                    session.HasPersistedOutcome = true;
                    Log.Information("HookOverlay: Vulkan verdict for pid {pid} ('{process}'), evidence {signature}: " +
                        "{verdict}, stage {stage}, successful presents {draws}", pid, name, session.Signature,
                        verified ? "Success" : "Failed", stage.DisplayName, snapshot.Successes);
                }

                _vulkanFallbackReason = session.Exhausted
                    ? "no supported Vulkan composite route succeeded; reset learned profiles to retry"
                    : session.AcknowledgementFailed ? "the Vulkan layer did not acknowledge its compatibility stage" : null;
                if (!_appConfiguration.IsOverlayActive || snapshot.Result == VulkanProbeResult.Dormant)
                    return new HookOverlayStatus(EHookOverlayStatus.Hidden, pid, "Vulkan",
                        $"PID {pid}, Vulkan: compatibility observation paused while the overlay is hidden.");
                if (snapshot.TickMs > nowMs || nowMs - snapshot.TickMs > HookVulkanProbeSession.StaleMs)
                    return new HookOverlayStatus(EHookOverlayStatus.Idle, pid, "Vulkan",
                        $"PID {pid}, Vulkan: waiting for fresh presents; no failure learned.");
                return new HookOverlayStatus(session.Exhausted || session.AcknowledgementFailed
                    ? EHookOverlayStatus.Fallback : session.Learned ? EHookOverlayStatus.Active : EHookOverlayStatus.Probing,
                    pid, "Vulkan", $"PID {pid}, Vulkan: {session.Stage.DisplayName}; " +
                    $"stage {session.StageNumber}/{session.StageCount}, " +
                    $"{(session.Learned ? "verified" : "verifying fresh composited presents")}.",
                    heartbeatAgeMilliseconds: (long)(nowMs - snapshot.TickMs),
                    renderResolution: HookOverlayStatusEvaluator.FormatResolution(activity.ResolutionX, activity.ResolutionY),
                    renderApi: "Vulkan");
            }
        }
    }
}
