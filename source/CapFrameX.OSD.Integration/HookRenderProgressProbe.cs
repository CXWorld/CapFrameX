using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    internal struct HookRenderProgress
    {
        public uint Generation;
        public ulong Presents, Draws;
        public long LastDrawTickMs;
        public int RouteSource;
        public uint AppliedFlags, AppliedSequence, PendingRestartFlags;
    }

    internal static class HookRenderProgressProbe
    {
        internal const int Size = 64;
        internal const int Magic = 0x31505243; // CRP1

        internal static string MappingName(int pid) => $"Local\\CfxOsdHookProgressV1_{pid}";

        internal static HookRenderProgress? Read(int pid)
        {
            try
            {
                using var mapping = MemoryMappedFile.OpenExisting(MappingName(pid), MemoryMappedFileRights.Read);
                using var view = mapping.CreateViewAccessor(0, Size, MemoryMappedFileAccess.Read);
                return TryRead(view, pid, out var progress) ? progress : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is ArgumentException)
            {
                return null;
            }
        }

        internal static bool TryRead(MemoryMappedViewAccessor view, int pid, out HookRenderProgress progress)
        {
            progress = default;
            if (view.Capacity < Size || view.ReadInt32(0) != Magic || view.ReadInt32(4) != 1 ||
                view.ReadInt32(8) != pid || view.ReadInt32(12) != Size) return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint sequence = view.ReadUInt32(16);
                if ((sequence & 1) != 0) continue;
                Thread.MemoryBarrier();
                var sample = new HookRenderProgress
                {
                    Generation = view.ReadUInt32(20), Presents = view.ReadUInt64(24),
                    Draws = view.ReadUInt64(32), LastDrawTickMs = view.ReadInt64(40),
                    RouteSource = view.ReadInt32(48), AppliedFlags = view.ReadUInt32(52),
                    AppliedSequence = view.ReadUInt32(56), PendingRestartFlags = view.ReadUInt32(60)
                };
                Thread.MemoryBarrier();
                if (sequence != view.ReadUInt32(16)) continue;
                progress = sample;
                return true;
            }
            return false;
        }
    }
}
