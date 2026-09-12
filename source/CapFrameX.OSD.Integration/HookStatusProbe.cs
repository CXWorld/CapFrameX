using System;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    [Flags]
    internal enum NativeHookStatusFlags : uint
    {
        Loaded = 1u << 0,
        HooksArmed = 1u << 1,
        PresentSeen = 1u << 2,
        RendererReady = 1u << 3,
        Visible = 1u << 4,
        MetricsConnected = 1u << 5,
        Rendered = 1u << 6,
        Dormant = 1u << 7,
        Error = 1u << 8,
        ForeignPresenter = 1u << 9,
        EarlyInjectionRequired = 1u << 10
    }

    /// <summary>
    /// Mirrors <c>cfxhook::HookInstallPhase</c>: the InstallHooks milestone the hook last
    /// published. A block that still shows anything but <see cref="Done"/> while HooksArmed
    /// stays clear names the step the installation stopped in. <see cref="None"/> is what a
    /// version-1 hook reports; <see cref="Unknown"/> is a value this reader has no name for.
    /// </summary>
    internal enum NativeHookInstallPhase
    {
        Unknown = -1,
        None = 0,
        Guard = 1,
        StatusInit = 2,
        CompatInit = 3,
        DelayImportRoute = 4,
        StatsInit = 5,
        D3D12Init = 6,
        HostWatch = 7,
        MinHookInit = 8,
        TelemetryConfigure = 9,
        ModuleNotifications = 10,
        D3D12QueueHook = 11,
        LoadedTelemetryHooks = 12,
        StreamlineProxy = 13,
        XeFgProxy = 14,
        FidelityFxExports = 15,
        DxgiHooks = 16,
        Done = 17
    }

    /// <summary>Mirrors <c>cfxhook::HookQueueState</c>.</summary>
    internal enum NativeHookQueueState
    {
        Unknown = -1,
        None = 0,
        Observed = 1,
        Explicit = 2,
        DeviceMismatch = 3,
        TransitionDeferred = 4
    }

    /// <summary>
    /// Mirrors <c>cfxhook::HookDeclineReason</c>: why the hook's most recent present drew no
    /// overlay. A successful draw resets it to <see cref="None"/>.
    /// </summary>
    internal enum NativeHookDeclineReason
    {
        Unknown = -1,
        None = 0,
        ExternalMutation = 1,
        Dormant = 2,
        FidelityFxOwnsPresentation = 3,
        StreamlineBlocksNative = 4,
        XeFgProxyNoQueue = 5,
        ForeignNativePresent = 6,
        FgAuthoritativeNoRoute = 7,
        FgStandDown = 8,
        OwnerDeferral = 9,
        Hidden = 10,
        StreamlineNoCreationQueue = 11,
        XeFgIndeterminateNoDraw = 12,
        D3D12NoQueue = 13,
        D3D12DeviceMismatch = 14,
        D3D12TransitionDeferred = 15
    }

    internal struct NativeHookStatusSnapshot
    {
        /// <summary>Block version the hook published: 1 (64 bytes) or 2 (128 bytes).</summary>
        public int Version;
        public NativeHookStatusFlags Flags;
        public long LastHeartbeatTickMs;
        public long LastStateChangeTickMs;
        public int LastError;
        public int SteadyRefcount;
        public int ReleaseThreshold;
        public int MetricsEntryCount;
        // Backbuffer extent of the swapchain the hook presents into. Both are 0 until the first
        // present established one — the hook never publishes a half-known extent.
        public int ResolutionX;
        public int ResolutionY;
        // Graphics API of the hooked swapchain, 0 until a present proved the device type.
        public NativeHookApi Api;

        // ---- Version 2. Every field reads as its zero value from a version-1 block. ----
        public NativeHookInstallPhase InstallPhase;
        /// <summary>(module index &lt;&lt; 8 | export index + 1) while FidelityFX exports are armed.</summary>
        public int InstallDetail;
        /// <summary>Compatibility flags the hook actually runs with (NativeHookCompatibilityFlags).</summary>
        public uint AppliedFlags;
        public uint AppliedSequence;
        /// <summary>Requested flag bits that only a fresh process can honour.</summary>
        public uint PendingRestartFlags;
        /// <summary>Flag bits this hook build can apply while the game keeps running.</summary>
        public uint LiveReloadCapabilities;
        public int CoverageAttempts;
        public int CoverageSubmitted;
        public int CoverageMissed;
        /// <summary>FrameGenerationTechnology: 0 unknown, 1 DLSS, 2 XeSS, 3 FSR.</summary>
        public int FgTechnology;
        /// <summary>FrameGenerationActivity: 0 unknown, 1 inactive, 2 active.</summary>
        public int FgActivity;
        public bool FgAuthoritative;
        /// <summary>StreamlineDlssgMode: 0 unknown, 1 off, 2 on.</summary>
        public int StreamlineDlssgMode;
        public NativeHookQueueState QueueState;
        public NativeHookDeclineReason LastDeclineReason;
        /// <summary>OverlayPresentSource of the most recent present + 1; 0 before any present.</summary>
        public int RouteSource;
        /// <summary>Which compatibility channel version supplied the flags: 0 none, 1, 2.</summary>
        public int CompatChannelVersion;
        public long LastFlagsAppliedTickMs;
    }

    /// <summary>
    /// Mirrors <c>cfxhook::HookStatusApi</c>. Vulkan is absent by design: Vulkan titles are
    /// served by the implicit layer, which never writes the hook status block.
    /// </summary>
    internal enum NativeHookApi
    {
        Unknown = 0,
        D3D11 = 1,
        D3D12 = 2
    }

    internal static class HookStatusProbe
    {
        internal const ulong HeartbeatStaleAfterMs = 3000;
        internal const int Version1 = 1;
        internal const int Version2 = 2;
        internal const int StatusSizeV1 = 64;
        internal const int StatusSizeV2 = 128;

        private const int Magic = 0x31534843; // 'C''H''S''1'
        private const int MagicOffset = 0;
        private const int VersionOffset = 4;
        private const int ProcessIdOffset = 8;
        private const int FlagsOffset = 12;
        private const int LastHeartbeatOffset = 16;
        private const int LastStateChangeOffset = 24;
        private const int LastErrorOffset = 32;
        private const int SteadyRefcountOffset = 36;
        private const int ReleaseThresholdOffset = 40;
        private const int MetricsEntryCountOffset = 44;
        // Carved out of the native block's former reserved[4], so the layout and version are
        // unchanged and an older hook simply keeps reporting 0 here.
        private const int ResolutionXOffset = 48;
        private const int ResolutionYOffset = 52;
        private const int ApiOffset = 56;
        // Version 2 appends its fields after the untouched 64-byte V1 block.
        private const int InstallPhaseOffset = 64;
        private const int InstallDetailOffset = 68;
        private const int AppliedFlagsOffset = 72;
        private const int AppliedSequenceOffset = 76;
        private const int PendingRestartFlagsOffset = 80;
        private const int LiveReloadCapabilitiesOffset = 84;
        private const int CoverageAttemptsOffset = 88;
        private const int CoverageSubmittedOffset = 92;
        private const int CoverageMissedOffset = 96;
        private const int FgTelemetryOffset = 100;
        private const int QueueStateOffset = 104;
        private const int LastDeclineReasonOffset = 108;
        private const int RouteSourceOffset = 112;
        private const int CompatChannelVersionOffset = 116;
        private const int LastFlagsAppliedOffset = 120;
        private const int ErrorFileNotFound = 2;
        private const uint FileMapRead = 0x0004;

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenFileMappingW(uint desiredAccess, bool inheritHandle,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr mapping, uint desiredAccess,
            uint fileOffsetHigh, uint fileOffsetLow, UIntPtr numberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr view);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Reads the injected hook's status mapping. A missing mapping is the normal state until
        /// the hook has installed itself and is reported as a plain false — the status pipeline
        /// polls this for every published status, so it must never raise an exception per call.
        /// </summary>
        internal static bool TryRead(int processId, out NativeHookStatusSnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = null;
            if (processId <= 0)
            {
                error = "invalid target PID";
                return false;
            }

            string mappingName = GetMappingName(processId);
            IntPtr mapping = IntPtr.Zero;
            IntPtr view = IntPtr.Zero;
            try
            {
                mapping = OpenFileMappingW(FileMapRead, false, mappingName);
                if (mapping == IntPtr.Zero)
                {
                    int openError = Marshal.GetLastWin32Error();
                    // Normal until the injected worker has installed the native hooks.
                    if (openError != ErrorFileNotFound)
                        error = $"OpenFileMapping failed (Win32 error {openError})";
                    return false;
                }

                // Map the whole section: a V1 hook created 64 bytes, a V2 hook 128, and the
                // section size — not this reader — decides which fields exist.
                view = MapViewOfFile(mapping, FileMapRead, 0, 0, UIntPtr.Zero);
                if (view == IntPtr.Zero)
                {
                    error = $"MapViewOfFile failed (Win32 error {Marshal.GetLastWin32Error()})";
                    return false;
                }

                int magic = Marshal.ReadInt32(view, MagicOffset);
                int version = Marshal.ReadInt32(view, VersionOffset);
                int mappedPid = Marshal.ReadInt32(view, ProcessIdOffset);
                if (magic != Magic || (version != Version1 && version != Version2) ||
                    mappedPid != processId)
                {
                    error = $"invalid hook status header (magic 0x{magic:X8}, version {version}, PID {mappedPid})";
                    return false;
                }

                snapshot = new NativeHookStatusSnapshot
                {
                    Version = version,
                    Flags = unchecked((NativeHookStatusFlags)(uint)Marshal.ReadInt32(
                        view, FlagsOffset)),
                    LastHeartbeatTickMs = Marshal.ReadInt64(view, LastHeartbeatOffset),
                    LastStateChangeTickMs = Marshal.ReadInt64(view, LastStateChangeOffset),
                    LastError = Marshal.ReadInt32(view, LastErrorOffset),
                    SteadyRefcount = Marshal.ReadInt32(view, SteadyRefcountOffset),
                    ReleaseThreshold = Marshal.ReadInt32(view, ReleaseThresholdOffset),
                    MetricsEntryCount = Marshal.ReadInt32(view, MetricsEntryCountOffset),
                    ResolutionX = Marshal.ReadInt32(view, ResolutionXOffset),
                    ResolutionY = Marshal.ReadInt32(view, ResolutionYOffset),
                    Api = ToApi(Marshal.ReadInt32(view, ApiOffset))
                };
                if (version >= Version2)
                {
                    int fg = Marshal.ReadInt32(view, FgTelemetryOffset);
                    snapshot.InstallPhase = ToInstallPhase(Marshal.ReadInt32(view, InstallPhaseOffset));
                    snapshot.InstallDetail = Marshal.ReadInt32(view, InstallDetailOffset);
                    snapshot.AppliedFlags = unchecked((uint)Marshal.ReadInt32(view, AppliedFlagsOffset));
                    snapshot.AppliedSequence = unchecked((uint)Marshal.ReadInt32(view, AppliedSequenceOffset));
                    snapshot.PendingRestartFlags = unchecked((uint)Marshal.ReadInt32(view, PendingRestartFlagsOffset));
                    snapshot.LiveReloadCapabilities = unchecked((uint)Marshal.ReadInt32(view, LiveReloadCapabilitiesOffset));
                    snapshot.CoverageAttempts = Marshal.ReadInt32(view, CoverageAttemptsOffset);
                    snapshot.CoverageSubmitted = Marshal.ReadInt32(view, CoverageSubmittedOffset);
                    snapshot.CoverageMissed = Marshal.ReadInt32(view, CoverageMissedOffset);
                    snapshot.FgTechnology = fg & 0x3;
                    snapshot.FgActivity = (fg >> 2) & 0x3;
                    snapshot.FgAuthoritative = (fg & 0x10) != 0;
                    snapshot.StreamlineDlssgMode = (fg >> 5) & 0x3;
                    snapshot.QueueState = ToQueueState(Marshal.ReadInt32(view, QueueStateOffset));
                    snapshot.LastDeclineReason = ToDeclineReason(Marshal.ReadInt32(view, LastDeclineReasonOffset));
                    snapshot.RouteSource = Marshal.ReadInt32(view, RouteSourceOffset);
                    snapshot.CompatChannelVersion = Marshal.ReadInt32(view, CompatChannelVersionOffset);
                    snapshot.LastFlagsAppliedTickMs = Marshal.ReadInt64(view, LastFlagsAppliedOffset);
                }
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
            finally
            {
                if (view != IntPtr.Zero)
                    UnmapViewOfFile(view);
                if (mapping != IntPtr.Zero)
                    CloseHandle(mapping);
            }
        }

        // An unrecognized value means the hook is newer than this reader; report it as unknown
        // rather than let it be cast into a meaningless enum member.
        private static NativeHookApi ToApi(int value)
            => value == (int)NativeHookApi.D3D11 ? NativeHookApi.D3D11
                : value == (int)NativeHookApi.D3D12 ? NativeHookApi.D3D12
                : NativeHookApi.Unknown;

        private static NativeHookInstallPhase ToInstallPhase(int value)
            => value >= (int)NativeHookInstallPhase.None && value <= (int)NativeHookInstallPhase.Done
                ? (NativeHookInstallPhase)value
                : NativeHookInstallPhase.Unknown;

        private static NativeHookQueueState ToQueueState(int value)
            => value >= (int)NativeHookQueueState.None &&
               value <= (int)NativeHookQueueState.TransitionDeferred
                ? (NativeHookQueueState)value
                : NativeHookQueueState.Unknown;

        private static NativeHookDeclineReason ToDeclineReason(int value)
            => value >= (int)NativeHookDeclineReason.None &&
               value <= (int)NativeHookDeclineReason.D3D12TransitionDeferred
                ? (NativeHookDeclineReason)value
                : NativeHookDeclineReason.Unknown;

        internal static string GetMappingName(int processId)
            => $"Local\\CfxOsdHookStatusV1_{processId}";

        internal static ulong CurrentTickCount => GetTickCount64();

        internal static long GetHeartbeatAgeMilliseconds(long heartbeatTickMs, ulong nowTickMs)
        {
            if (heartbeatTickMs <= 0) return -1;
            ulong heartbeat = unchecked((ulong)heartbeatTickMs);
            if (nowTickMs < heartbeat) return 0;
            ulong age = nowTickMs - heartbeat;
            return age > long.MaxValue ? long.MaxValue : (long)age;
        }
    }
}
