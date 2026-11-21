using System;
using System.Buffers.Binary;

namespace CapFrameX.OSD.Integration
{
    /// <summary>An upscaler library the in-game hook saw evaluate.</summary>
    internal readonly struct HookUpscalerRuntimeUse
    {
        internal HookUpscalerRuntimeUse(ulong lastUseTickMs, RuntimeFileVersion version)
        {
            LastUseTickMs = lastUseTickMs;
            Version = version;
        }

        /// <summary>GetTickCount64 of the most recent evaluation the hook saw; 0 before the first.</summary>
        internal ulong LastUseTickMs { get; }
        /// <summary>The file version of the library that evaluated; empty when it has none.</summary>
        internal RuntimeFileVersion Version { get; }
    }

    /// <summary>Mirrors the order of <c>cfxhook::UpscalerRuntime</c>.</summary>
    internal readonly struct HookUpscalerUses
    {
        internal HookUpscalerUses(HookUpscalerRuntimeUse dlssSuperResolution,
            HookUpscalerRuntimeUse dlssRayReconstruction, HookUpscalerRuntimeUse xess)
        {
            DlssSuperResolution = dlssSuperResolution;
            DlssRayReconstruction = dlssRayReconstruction;
            Xess = xess;
        }

        /// <summary>nvngx_dlss.dll: DLSS Super Resolution and DLAA.</summary>
        internal HookUpscalerRuntimeUse DlssSuperResolution { get; }
        /// <summary>nvngx_dlssd.dll, which upscales in place of Super Resolution.</summary>
        internal HookUpscalerRuntimeUse DlssRayReconstruction { get; }
        /// <summary>libxess.dll and libxess_dx11.dll.</summary>
        internal HookUpscalerRuntimeUse Xess { get; }
    }

    /// <summary>
    /// Reads which DLSS and XeSS libraries the in-game hook saw the game evaluate
    /// (<c>Local\CfxOsdHookUpscalerUseV1_&lt;pid&gt;</c>, upscaler_runtime_use.cpp). A side channel
    /// beside the hook status block: a hook from before it simply has no such mapping.
    /// </summary>
    internal static class HookUpscalerUseProbe
    {
        internal const int Size = 72;
        internal const int Magic = 0x31555543; // CUU1
        internal const int Version = 1;
        internal const int PublicationOffset = 16;
        internal const int RuntimesOffset = 24;
        internal const int RuntimeSize = 16;
        internal const int RuntimeCount = 3;
        internal const int LastUseOffset = 0;
        internal const int VersionOffset = 8;

        internal static string MappingName(int pid) => $"Local\\CfxOsdHookUpscalerUseV1_{pid}";

        /// <summary>
        /// False without the mapping, which is the normal state for every process without a
        /// current hook, and while the hook is writing.
        /// </summary>
        internal static bool TryRead(int pid, out HookUpscalerUses uses)
        {
            uses = default;
            return pid > 0 &&
                   HookSideChannel.TryCopy(MappingName(pid), Size, PublicationOffset,
                       out byte[] block) &&
                   TryParse(block, pid, out uses);
        }

        internal static bool TryParse(ReadOnlySpan<byte> block, int pid, out HookUpscalerUses uses)
        {
            uses = default;
            if (block.Length < Size ||
                BinaryPrimitives.ReadInt32LittleEndian(block) != Magic ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(4)) != Version ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(8)) != pid ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(12)) != Size)
                return false;
            uses = new HookUpscalerUses(ParseRuntime(block, 0), ParseRuntime(block, 1),
                ParseRuntime(block, 2));
            return true;
        }

        private static HookUpscalerRuntimeUse ParseRuntime(ReadOnlySpan<byte> block, int index)
        {
            ReadOnlySpan<byte> runtime = block.Slice(RuntimesOffset + index * RuntimeSize, RuntimeSize);
            ReadOnlySpan<byte> version = runtime.Slice(VersionOffset);
            return new HookUpscalerRuntimeUse(
                BinaryPrimitives.ReadUInt64LittleEndian(runtime.Slice(LastUseOffset)),
                new RuntimeFileVersion(
                    BinaryPrimitives.ReadUInt16LittleEndian(version),
                    BinaryPrimitives.ReadUInt16LittleEndian(version.Slice(2)),
                    BinaryPrimitives.ReadUInt16LittleEndian(version.Slice(4)),
                    BinaryPrimitives.ReadUInt16LittleEndian(version.Slice(6)),
                    null));
        }
    }
}
