using System;
using System.Buffers.Binary;
using System.Text;

namespace CapFrameX.OSD.Integration
{
    /// <summary>Mirrors <c>cfxhook::FidelityFxVersionSource</c>.</summary>
    internal enum HookFidelityFxVersionSource
    {
        None = 0,
        /// <summary>The provider of the context the game dispatched.</summary>
        Context = 1,
        /// <summary>
        /// The default version of the game's library. Libraries from before the provider query
        /// offer nothing better; an override by the game or a provider of the driver is not seen.
        /// </summary>
        LibraryDefault = 2
    }

    internal readonly struct HookFidelityFxProviderVersion
    {
        internal HookFidelityFxProviderVersion(HookFidelityFxVersionSource source, string name,
            ulong versionId, ulong lastUseTickMs)
        {
            Source = source;
            Name = name;
            VersionId = versionId;
            LastUseTickMs = lastUseTickMs;
        }

        internal HookFidelityFxVersionSource Source { get; }
        /// <summary>The version as the library names it ("3.1.5"); null while unknown.</summary>
        internal string Name { get; }
        internal ulong VersionId { get; }
        /// <summary>GetTickCount64 of the most recent dispatch the hook saw; 0 before the first.</summary>
        internal ulong LastUseTickMs { get; }
    }

    internal readonly struct HookFidelityFxVersions
    {
        internal HookFidelityFxVersions(HookFidelityFxProviderVersion upscaler,
            HookFidelityFxProviderVersion frameGeneration)
        {
            Upscaler = upscaler;
            FrameGeneration = frameGeneration;
        }

        internal HookFidelityFxProviderVersion Upscaler { get; }
        internal HookFidelityFxProviderVersion FrameGeneration { get; }
    }

    /// <summary>
    /// Reads which FSR versions the in-game hook saw the game dispatch
    /// (<c>Local\CfxOsdHookFidelityFxV1_&lt;pid&gt;</c>, fidelityfx_provider_version.cpp). A side
    /// channel beside the hook status block: a hook from before it simply has no such mapping.
    /// </summary>
    internal static class HookFidelityFxVersionProbe
    {
        internal const int Size = 136;
        internal const int Magic = 0x31564643; // CFV1
        internal const int Version = 1;
        internal const int NameSize = 32;
        internal const int PublicationOffset = 16;
        internal const int EffectsOffset = 24;
        internal const int EffectSize = 56;
        internal const int LastUseOffset = 0;
        internal const int VersionIdOffset = 8;
        internal const int SourceOffset = 16;
        internal const int NameOffset = 24;

        internal static string MappingName(int pid) => $"Local\\CfxOsdHookFidelityFxV1_{pid}";

        /// <summary>
        /// False without the mapping, which is the normal state for every process without a
        /// current hook, and while the hook is writing.
        /// </summary>
        internal static bool TryRead(int pid, out HookFidelityFxVersions versions)
        {
            versions = default;
            return pid > 0 &&
                   HookSideChannel.TryCopy(MappingName(pid), Size, PublicationOffset,
                       out byte[] block) &&
                   TryParse(block, pid, out versions);
        }

        internal static bool TryParse(ReadOnlySpan<byte> block, int pid,
            out HookFidelityFxVersions versions)
        {
            versions = default;
            if (block.Length < Size ||
                BinaryPrimitives.ReadInt32LittleEndian(block) != Magic ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(4)) != Version ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(8)) != pid ||
                BinaryPrimitives.ReadInt32LittleEndian(block.Slice(12)) != Size)
                return false;
            versions = new HookFidelityFxVersions(
                ParseEffect(block.Slice(EffectsOffset, EffectSize)),
                ParseEffect(block.Slice(EffectsOffset + EffectSize, EffectSize)));
            return true;
        }

        private static HookFidelityFxProviderVersion ParseEffect(ReadOnlySpan<byte> effect)
        {
            ulong lastUse = BinaryPrimitives.ReadUInt64LittleEndian(effect.Slice(LastUseOffset));
            ulong versionId = BinaryPrimitives.ReadUInt64LittleEndian(effect.Slice(VersionIdOffset));
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(effect.Slice(SourceOffset));
            string name = ReadName(effect.Slice(NameOffset, NameSize));
            // The game's process writes this block. A source this reader has no name for, or a
            // source without a usable name, is no version; the time of use still stands.
            var known = name == null ? HookFidelityFxVersionSource.None
                : source == (uint)HookFidelityFxVersionSource.Context
                    ? HookFidelityFxVersionSource.Context
                    : source == (uint)HookFidelityFxVersionSource.LibraryDefault
                        ? HookFidelityFxVersionSource.LibraryDefault
                        : HookFidelityFxVersionSource.None;
            return new HookFidelityFxProviderVersion(known,
                known == HookFidelityFxVersionSource.None ? null : name, versionId, lastUse);
        }

        /// <summary>Printable ASCII up to the terminator; null for anything else or nothing.</summary>
        internal static string ReadName(ReadOnlySpan<byte> name)
        {
            int length = 0;
            // The last byte is the terminator of a full name.
            for (; length < name.Length - 1 && name[length] != 0; length++)
                if (name[length] < 0x20 || name[length] > 0x7e) return null;
            return length == 0 ? null : Encoding.ASCII.GetString(name.Slice(0, length));
        }
    }
}
