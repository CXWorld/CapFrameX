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

    internal struct NativeHookStatusSnapshot
    {
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

        private const int Magic = 0x31534843; // 'C''H''S''1'
        private const int Version = 1;
        private const int StatusSize = 64;
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

                view = MapViewOfFile(mapping, FileMapRead, 0, 0,
                    new UIntPtr((uint)StatusSize));
                if (view == IntPtr.Zero)
                {
                    error = $"MapViewOfFile failed (Win32 error {Marshal.GetLastWin32Error()})";
                    return false;
                }

                int magic = Marshal.ReadInt32(view, MagicOffset);
                int version = Marshal.ReadInt32(view, VersionOffset);
                int mappedPid = Marshal.ReadInt32(view, ProcessIdOffset);
                if (magic != Magic || version != Version || mappedPid != processId)
                {
                    error = $"invalid hook status header (magic 0x{magic:X8}, version {version}, PID {mappedPid})";
                    return false;
                }

                snapshot = new NativeHookStatusSnapshot
                {
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
