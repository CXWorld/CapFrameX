#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// Wire contracts v1/v2. Kept identical in CapFrameX.UpdateServer/OverlayProfileReport.cs.
// Only explicit fields below are transmitted; never serialize a learned-store entry directly.
namespace CapFrameX.OverlayReporting
{
    public sealed class OverlayProfileReport
    {
        [JsonRequired]
        public int SchemaVersion { get; set; } = 1;
        [JsonRequired]
        public int ConsentVersion { get; set; } = 1;
        public Guid ReportId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid SessionId { get; set; }
        public int Sequence { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string AppVersion { get; set; } = "";
        public string AppChannel { get; set; } = "";
        public string OsVersion { get; set; } = "";
        public string OsArchitecture { get; set; } = "";
        public string AttachMode { get; set; } = "";
        public string GraphicsApi { get; set; } = "";
        public string HookBuild { get; set; } = "";
        public ReportBinary Game { get; set; } = new();
        public ReportBinary? Hook { get; set; }
        public List<ReportGpu> Gpus { get; set; } = new();
        public List<ReportBinary> Modules { get; set; } = new();
        public string ContextStatus { get; set; } = "pending";
        public DateTime ContextObservedUtc { get; set; }
        public string EndReason { get; set; } = "checkpoint";
        public long DurationMs { get; set; }
        public int DroppedEvents { get; set; }
        public List<ReportEvent> Events { get; set; } = new();
    }

    public sealed class ReportBinary
    {
        public string Name { get; set; } = "";
        public string FileVersion { get; set; } = "";
        public string ProductVersion { get; set; } = "";
        public string Architecture { get; set; } = "unknown";
        public string Sha256 { get; set; } = "";
        public long Size { get; set; }
        // system, application, or unknown; the actual path never leaves the client.
        public string Location { get; set; } = "unknown";
        public string ReadStatus { get; set; } = "unavailable";
    }

    public sealed class ReportGpu
    {
        public string Name { get; set; } = "";
        public string DriverVersion { get; set; } = "";
        public string VendorId { get; set; } = "";
        public string DeviceId { get; set; } = "";
    }

    public sealed class ReportProfile
    {
        public string EvidenceSignature { get; set; } = "";
        public string EarlySignature { get; set; } = "";
        public string Stage { get; set; } = "";
        public string Source { get; set; } = "";
        public uint Flags { get; set; }
        public string EarlyInjectionModule { get; set; } = "";
        public int InjectionDelayMs { get; set; }
        public bool Verified { get; set; }
        public bool Exhausted { get; set; }
        public string PendingStage { get; set; } = "";
        public uint PendingFlags { get; set; }
        public string PendingEarlyInjectionModule { get; set; } = "";
        public int PendingInjectionDelayMs { get; set; }
        public string LastVerdict { get; set; } = "";
        public int Attempts { get; set; }
        public List<string> Ladder { get; set; } = new();
    }

    public sealed class ReportEvent
    {
        public long ElapsedMs { get; set; }
        public string Kind { get; set; } = "";
        public string Verdict { get; set; } = "";
        public string State { get; set; } = "";
        public bool HasStatus { get; set; }
        public bool OverlayVisible { get; set; }
        public bool Fallback { get; set; }
        public ReportProfile? Profile { get; set; }
        public ReportNativeStatus? Native { get; set; }
        public ReportVulkanStatus? Vulkan { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReportHostState? Host { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReportModuleChange? ModuleChange { get; set; }
    }

    public sealed class ReportHostState
    {
        public string Runtime { get; set; } = "unknown";
        public string Window { get; set; } = "unknown";
        public bool OverlayRequested { get; set; }
        public string FallbackSource { get; set; } = "none";
        public string FallbackReason { get; set; } = "none";
    }

    public sealed class ReportModuleChange
    {
        // First observed by the bounded context scan, not an exact DLL load timestamp.
        public List<string> Added { get; set; } = new();
        public List<string> Removed { get; set; } = new();
        public List<string> Updated { get; set; } = new();
    }

    public sealed class ReportRenderProgress
    {
        public uint Generation { get; set; }
        public ulong Presents { get; set; }
        public ulong Draws { get; set; }
        public long LastDrawAgeMs { get; set; }
        public int RouteSource { get; set; }
        public uint AppliedFlags { get; set; }
        public uint AppliedSequence { get; set; }
    }

    public sealed class ReportNativeStatus
    {
        public int Version { get; set; }
        public uint Flags { get; set; }
        public long HeartbeatAgeMs { get; set; }
        public int LastError { get; set; }
        public int MetricsEntryCount { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Api { get; set; } = "";
        public string InstallPhase { get; set; } = "";
        public int InstallDetail { get; set; }
        public uint AppliedFlags { get; set; }
        public uint AppliedSequence { get; set; }
        public uint PendingRestartFlags { get; set; }
        public uint LiveReloadCapabilities { get; set; }
        // Cumulative native generic-route counters; these do NOT cover every vendor-proxy draw.
        public int CoverageAttempts { get; set; }
        public int CoverageSubmitted { get; set; }
        public int CoverageMissed { get; set; }
        public int FgTechnology { get; set; }
        public int FgActivity { get; set; }
        public bool FgAuthoritative { get; set; }
        public int StreamlineDlssgMode { get; set; }
        public string QueueState { get; set; } = "";
        public string DeclineReason { get; set; } = "";
        public int RouteSource { get; set; }
        public int CompatChannelVersion { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReportRenderProgress? Progress { get; set; }
    }

    public sealed class ReportVulkanStatus
    {
        public string RequestedRoute { get; set; } = "";
        public string ActualRoute { get; set; } = "";
        public string Result { get; set; } = "";
        public uint AppliedRevision { get; set; }
        public uint Capabilities { get; set; }
        public uint Bitness { get; set; }
        public ulong Generation { get; set; }
        public long HeartbeatAgeMs { get; set; }
        public ulong Successes { get; set; }
        public uint Vendor { get; set; }
        public uint Device { get; set; }
        public uint Driver { get; set; }
        public uint Family { get; set; }
        public uint QueueFlags { get; set; }
        public uint Format { get; set; }
        public uint ColorSpace { get; set; }
        public uint ImageUsage { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
