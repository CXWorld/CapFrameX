using System;
using System.Buffers.Binary;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// The frame counters of the in-game hook at one publication. Every counter only grows; the
    /// difference between two publications is what happened in between.
    /// </summary>
    internal readonly struct HookFrameCounts
    {
        internal HookFrameCounts(ulong tickMs, ulong displayedFrames, ulong proxyPresents,
            ulong streamlineFrames, ulong fsrPreparedFrames, ulong fsrGeneratedFrames,
            ulong xessFrames)
        {
            TickMs = tickMs;
            DisplayedFrames = displayedFrames;
            ProxyPresents = proxyPresents;
            StreamlineFrames = streamlineFrames;
            FsrPreparedFrames = fsrPreparedFrames;
            FsrGeneratedFrames = fsrGeneratedFrames;
            XessFrames = xessFrames;
        }

        /// <summary>GetTickCount64 of the hook when it read the counters.</summary>
        internal ulong TickMs { get; }
        /// <summary>Native Presents: the frames that went to the display.</summary>
        internal ulong DisplayedFrames { get; }
        /// <summary>
        /// The application's Presents to the swapchain of a frame generation runtime. Only
        /// counted on the routes that hook that swapchain.
        /// </summary>
        internal ulong ProxyPresents { get; }
        /// <summary>Frames the application asked Streamline a new frame token for.</summary>
        internal ulong StreamlineFrames { get; }
        /// <summary>FidelityFX frame generation prepare dispatches, one per application frame.</summary>
        internal ulong FsrPreparedFrames { get; }
        /// <summary>FidelityFX frame generation dispatches, one per generated frame.</summary>
        internal ulong FsrGeneratedFrames { get; }
        /// <summary>Frames the application tagged XeSS-FG constants for.</summary>
        internal ulong XessFrames { get; }
    }

    /// <summary>
    /// Reads how many frames the in-game hook saw reach the display and how many the application
    /// rendered for its frame generation (<c>Local\CfxOsdHookFrameCountsV1_&lt;pid&gt;</c>,
    /// frame_generation_counts.cpp). A side channel beside the hook status block: a hook from
    /// before it simply has no such mapping.
    /// </summary>
    internal static class HookFrameCountsProbe
    {
        internal const int Size = 80;
        internal const int Magic = 0x31464643; // CFF1
        internal const int Version = 1;
        internal const int PublicationOffset = 16;
        internal const int TickOffset = 24;
        internal const int CountsOffset = 32;
        internal const int CountCount = 6;

        internal static string MappingName(int pid) => $"Local\\CfxOsdHookFrameCountsV1_{pid}";

        /// <summary>
        /// False without the mapping, which is the normal state for every process without a
        /// current hook, and while the hook is writing.
        /// </summary>
        internal static bool TryRead(int pid, out HookFrameCounts counts)
        {
            counts = default;
            return pid > 0 &&
                   HookSideChannel.TryCopy(MappingName(pid), Size, PublicationOffset,
                       out byte[] block) &&
                   TryParse(block, pid, out counts);
        }

        internal static bool TryParse(ReadOnlySpan<byte> block, int pid, out HookFrameCounts counts)
        {
            counts = default;
            if (block.Length < Size ||
                BinaryPrimitives.ReadInt32LittleEndian(block) != Magic ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(4)) != Version ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(8)) != pid ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(12)) != Size)
                return false;
            counts = new HookFrameCounts(
                BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(TickOffset)),
                Count(block, 0), Count(block, 1), Count(block, 2), Count(block, 3),
                Count(block, 4), Count(block, 5));
            return true;
        }

        private static ulong Count(ReadOnlySpan<byte> block, int index)
            => BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(CountsOffset + index * sizeof(ulong)));
    }
}
