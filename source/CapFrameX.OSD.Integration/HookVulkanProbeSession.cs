using System;
using System.Collections.Generic;

namespace CapFrameX.OSD.Integration
{
    [Flags]
    internal enum VulkanProbeAction { None = 0, Publish = 1, Learn = 2, Invalidate = 4, Exhausted = 8 }

    /// <summary>Pure native-Vulkan probe. Only advertised, color-correct routes enter the ladder.</summary>
    internal sealed class HookVulkanProbeSession
    {
        internal const ulong ConfirmMs = 2000, FailureMs = 5000, StaleMs = 2000;
        private readonly HashSet<VulkanCompositeRoute> _tried = new HashSet<VulkanCompositeRoute>();
        private readonly uint _capabilities;
        private ulong _successSince, _successBaseline, _lastSuccesses, _failureSince, _publishedAt;
        private uint _revision;

        internal HookVulkanProbeSession(VulkanProbeSnapshot initial, HookLearnedProfileEntry learned)
        {
            Context = initial.Context;
            Generation = initial.Generation;
            Signature = initial.Signature;
            BuildHash = initial.BuildHash;
            _capabilities = initial.Capabilities;
            VulkanCompositeRoute preferred = learned?.Verified == true &&
                learned.StageId == HookCompatibilityStageId.VulkanCompute
                ? VulkanCompositeRoute.Compute : VulkanCompositeRoute.Graphics;
            Route = Supports(preferred) ? preferred : NextRoute();
            Exhausted = Route == VulkanCompositeRoute.Suspended;
            _tried.Add(Route);
        }

        internal ulong Context { get; }
        internal ulong Generation { get; }
        internal string Signature { get; }
        internal string BuildHash { get; }
        internal VulkanCompositeRoute Route { get; private set; }
        internal bool Learned { get; private set; }
        internal bool Exhausted { get; private set; }
        internal bool AcknowledgementFailed { get; private set; }
        internal bool HasPersistedOutcome { get; set; }
        internal int StageNumber => Math.Min(_tried.Count, StageCount);
        internal int StageCount => Math.Max(1, ((_capabilities & 1) != 0 ? 1 : 0) +
                                                ((_capabilities & 2) != 0 ? 1 : 0));
        internal HookCompatibilityStage Stage => ToStage(Route);

        internal static HookCompatibilityStage ToStage(VulkanCompositeRoute route)
            => HookCompatibilityStage.Create(route == VulkanCompositeRoute.Graphics
                ? HookCompatibilityStageId.VulkanGraphics : route == VulkanCompositeRoute.Compute
                    ? HookCompatibilityStageId.VulkanCompute : HookCompatibilityStageId.VulkanSuspended);

        internal void Published(uint revision, ulong nowMs)
        {
            _revision = revision;
            _publishedAt = nowMs;
            ResetConfirmation();
        }

        internal bool Matches(VulkanProbeSnapshot snapshot)
            => Context == snapshot.Context && Generation == snapshot.Generation &&
               BuildHash == snapshot.BuildHash && Signature == snapshot.Signature;

        internal VulkanProbeAction Observe(VulkanProbeSnapshot s, ulong nowMs, bool visible)
        {
            if (!Matches(s) || !visible || s.Result == VulkanProbeResult.Dormant ||
                s.TickMs > nowMs || nowMs - s.TickMs > StaleMs)
            {
                ResetConfirmation();
                _publishedAt = nowMs; // paused/minimized time is not an acknowledgement timeout
                return VulkanProbeAction.None;
            }
            if (Exhausted || AcknowledgementFailed) return VulkanProbeAction.None;
            if (s.AppliedRevision != _revision || s.RequestedRoute != Route)
            {
                ResetConfirmation();
                if (nowMs >= _publishedAt && nowMs - _publishedAt >= FailureMs)
                    AcknowledgementFailed = true;
                return VulkanProbeAction.None;
            }
            if (s.Result == VulkanProbeResult.Composited && s.ActualRoute == Route &&
                s.Successes > _lastSuccesses)
            {
                _failureSince = 0;
                if (_successSince == 0)
                {
                    _successSince = nowMs;
                    _successBaseline = s.Successes;
                }
                _lastSuccesses = s.Successes;
                if (!Learned && nowMs - _successSince >= ConfirmMs &&
                    s.Successes >= _successBaseline + 3)
                {
                    Learned = true;
                    return VulkanProbeAction.Learn;
                }
                return VulkanProbeAction.None;
            }
            _successSince = 0;
            _lastSuccesses = s.Successes;
            if (_failureSince == 0) _failureSince = nowMs;
            if (nowMs - _failureSince < FailureMs) return VulkanProbeAction.None;

            Learned = false;
            Route = NextRoute();
            Exhausted = Route == VulkanCompositeRoute.Suspended;
            _tried.Add(Route);
            ResetConfirmation();
            return VulkanProbeAction.Publish | VulkanProbeAction.Invalidate |
                (Exhausted ? VulkanProbeAction.Exhausted : VulkanProbeAction.None);
        }

        private bool Supports(VulkanCompositeRoute route)
            => route == VulkanCompositeRoute.Graphics ? (_capabilities & 1) != 0 :
               route == VulkanCompositeRoute.Compute && (_capabilities & 2) != 0;
        private VulkanCompositeRoute NextRoute()
        {
            foreach (var route in new[] { VulkanCompositeRoute.Graphics, VulkanCompositeRoute.Compute })
                if (Supports(route) && !_tried.Contains(route)) return route;
            return VulkanCompositeRoute.Suspended;
        }
        private void ResetConfirmation()
        {
            _successSince = _successBaseline = _lastSuccesses = _failureSince = 0;
        }
    }
}
