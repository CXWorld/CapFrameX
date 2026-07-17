using System;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        Error = 1u << 8
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
    }

    internal static class HookStatusProbe
    {
        internal const ulong HeartbeatStaleAfterMs = 3000;

        private const int Magic = 0x31534843; // 'C''H''S''1'
        private const int Version = 1;
        private const long StatusSize = 64;
        private const long MagicOffset = 0;
        private const long VersionOffset = 4;
        private const long ProcessIdOffset = 8;
        private const long FlagsOffset = 12;
        private const long LastHeartbeatOffset = 16;
        private const long LastStateChangeOffset = 24;
        private const long LastErrorOffset = 32;
        private const long SteadyRefcountOffset = 36;
        private const long ReleaseThresholdOffset = 40;
        private const long MetricsEntryCountOffset = 44;

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

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

            string mappingName = $"Local\\CfxOsdHookStatusV1_{processId}";
            try
            {
                using (var mapping = MemoryMappedFile.OpenExisting(
                    mappingName, MemoryMappedFileRights.Read))
                using (var view = mapping.CreateViewAccessor(
                    0, StatusSize, MemoryMappedFileAccess.Read))
                {
                    int magic = view.ReadInt32(MagicOffset);
                    int version = view.ReadInt32(VersionOffset);
                    int mappedPid = view.ReadInt32(ProcessIdOffset);
                    if (magic != Magic || version != Version || mappedPid != processId)
                    {
                        error = $"invalid hook status header (magic 0x{magic:X8}, version {version}, PID {mappedPid})";
                        return false;
                    }

                    snapshot = new NativeHookStatusSnapshot
                    {
                        Flags = unchecked((NativeHookStatusFlags)(uint)view.ReadInt32(FlagsOffset)),
                        LastHeartbeatTickMs = view.ReadInt64(LastHeartbeatOffset),
                        LastStateChangeTickMs = view.ReadInt64(LastStateChangeOffset),
                        LastError = view.ReadInt32(LastErrorOffset),
                        SteadyRefcount = view.ReadInt32(SteadyRefcountOffset),
                        ReleaseThreshold = view.ReadInt32(ReleaseThresholdOffset),
                        MetricsEntryCount = view.ReadInt32(MetricsEntryCountOffset)
                    };
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                // Normal until the injected worker has installed the native hooks.
                return false;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                       ex is IOException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

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
