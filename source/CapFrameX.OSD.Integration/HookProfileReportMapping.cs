using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OverlayReporting;

namespace CapFrameX.OSD.Integration
{
    public sealed partial class HookProfileReportService
    {
        internal static ReportProfile Profile(HookCompatibilityStage stage, string signature,
            IEnumerable<HookCompatibilityStage> ladder = null)
            => new ReportProfile
            {
                EvidenceSignature = signature ?? "unknown", Stage = stage?.Id.ToString() ?? "none",
                Source = stage?.Source ?? "", Flags = (uint)(stage?.Flags ?? 0),
                EarlyInjectionModule = stage?.EarlyInjectionModule ?? "",
                InjectionDelayMs = (int)(stage?.InjectionDelay.TotalMilliseconds ?? 0),
                Ladder = ladder?.Select(s => s.Id.ToString() + (s.RequiresEarlyInjection ? "+early" : "")).ToList()
                    ?? new List<string>()
            };

        internal void ProfileOutcome(int pid, HookLearnedProfileEntry entry)
        {
            var profile = Profile(entry.ToStage(), entry.EvidenceSignature);
            profile.EarlySignature = entry.EarlySignature ?? "";
            profile.Verified = entry.Verified;
            profile.Exhausted = entry.Exhausted;
            profile.PendingStage = entry.PendingStageId?.ToString() ?? "";
            profile.PendingFlags = entry.PendingFlags;
            profile.PendingEarlyInjectionModule = entry.PendingEarlyInjectionModule ?? "";
            profile.PendingInjectionDelayMs = entry.PendingInjectionDelayMs;
            profile.LastVerdict = entry.LastVerdict ?? "";
            profile.Attempts = entry.Attempts;
            profile.Ladder = entry.Ladder?.ToList() ?? new List<string>();
            Record(pid, new ReportEvent { Kind = "profile-outcome", Verdict = profile.LastVerdict, Profile = profile });
        }

        internal void Action(int pid, HookCompatibilityProbeSession session, HookProbeAction action)
        {
            if (action.Kind == HookProbeActionKind.SetPollInterval) return;
            Record(pid, new ReportEvent
            {
                Kind = "action", Verdict = action.Verdict.ToString(), State = action.Kind.ToString(),
                Profile = Profile(action.Stage ?? session.CurrentStage, session.Evidence.Signature, session.Plan.Ladder)
            });
        }

        internal void Injection(int pid, bool inProgress, bool succeeded, bool failed)
            => Record(pid, new ReportEvent { Kind = "injection", State = inProgress ? "started" :
                succeeded ? "succeeded" : "stopped", Verdict = failed ? "InjectionFailed" : "" });

        internal void Observe(int pid, bool hasStatus, NativeHookStatusSnapshot s,
            EHookOverlayStatus? state, ulong now, bool visible, bool fallback)
        {
            var native = new ReportNativeStatus
            {
                Version = s.Version, Flags = (uint)s.Flags,
                HeartbeatAgeMs = !hasStatus ? -1 : (long)now - s.LastHeartbeatTickMs,
                LastError = s.LastError, MetricsEntryCount = s.MetricsEntryCount,
                Width = s.ResolutionX, Height = s.ResolutionY, Api = s.Api.ToString(),
                InstallPhase = s.InstallPhase.ToString(), InstallDetail = s.InstallDetail,
                AppliedFlags = s.AppliedFlags, AppliedSequence = s.AppliedSequence,
                PendingRestartFlags = s.PendingRestartFlags, LiveReloadCapabilities = s.LiveReloadCapabilities,
                CoverageAttempts = s.CoverageAttempts, CoverageSubmitted = s.CoverageSubmitted,
                CoverageMissed = s.CoverageMissed, FgTechnology = s.FgTechnology, FgActivity = s.FgActivity,
                FgAuthoritative = s.FgAuthoritative, StreamlineDlssgMode = s.StreamlineDlssgMode,
                QueueState = s.QueueState.ToString(), DeclineReason = s.LastDeclineReason.ToString(),
                RouteSource = s.RouteSource, CompatChannelVersion = s.CompatChannelVersion
            };
            string key = $"{hasStatus}:{state}:{s.Flags}:{s.AppliedFlags}:{s.PendingRestartFlags}:" +
                $"{s.FgTechnology}:{s.FgActivity}:{s.FgAuthoritative}:{s.StreamlineDlssgMode}:" +
                $"{s.QueueState}:{s.LastDeclineReason}:{s.RouteSource}:{s.MetricsEntryCount}:" +
                $"{s.ResolutionX}:{s.ResolutionY}:{s.InstallPhase}:{s.LastError}:{visible}:{fallback}";
            Record(pid, new ReportEvent { Kind = "native-sample", State = state?.ToString() ?? "unknown",
                HasStatus = hasStatus, Native = native, OverlayVisible = visible, Fallback = fallback }, key);
        }

        internal void ObserveVulkan(int pid, VulkanProbeSnapshot s, VulkanProbeAction action,
            ulong now, bool visible, bool fallback, int width, int height)
        {
            var native = new ReportVulkanStatus
            {
                RequestedRoute = s.RequestedRoute.ToString(), ActualRoute = s.ActualRoute.ToString(),
                Result = s.Result.ToString(), AppliedRevision = s.AppliedRevision,
                Capabilities = s.Capabilities, Bitness = s.Bitness, Generation = s.Generation,
                HeartbeatAgeMs = (long)now - (long)s.TickMs, Successes = s.Successes,
                Vendor = s.Vendor, Device = s.Device, Driver = s.Driver, Family = s.Family,
                QueueFlags = s.QueueFlags, Format = s.Format, ColorSpace = s.ColorSpace,
                ImageUsage = s.ImageUsage, Width = width, Height = height
            };
            string key = $"vk:{s.Generation}:{s.RequestedRoute}:{s.ActualRoute}:{s.Result}:" +
                $"{s.AppliedRevision}:{action}:{visible}:{fallback}:{width}:{height}";
            Record(pid, new ReportEvent { Kind = "vulkan-sample", Verdict = action.ToString(),
                HasStatus = true, Vulkan = native, OverlayVisible = visible, Fallback = fallback }, key);
        }
    }
}
