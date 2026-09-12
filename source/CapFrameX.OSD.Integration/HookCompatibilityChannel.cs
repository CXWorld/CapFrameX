using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Publishes the selected compatibility flags before LoadLibrary injects the native hook and
    /// keeps publishing stage changes afterwards. Version 2 (64 bytes, sequence counter) stays
    /// mapped in the hook and is polled per present; version 1 (16 bytes) is what an older hook
    /// reads once at installation, so it is written alongside whenever the flags are not None.
    /// Both mappings are retained by the manager so the hook's deferred initialization thread
    /// can consume them after DllMain returns.
    /// </summary>
    internal sealed class HookCompatibilityChannel : IDisposable
    {
        internal const int Magic = 0x31434643; // 'C''F''C''1'
        internal const int Version = 1;
        internal const long ChannelSize = 16;
        internal const long MagicOffset = 0;
        internal const long VersionOffset = 4;
        internal const long ProcessIdOffset = 8;
        internal const long FlagsOffset = 12;

        internal const int MagicV2 = 0x32434643; // 'C''F''C''2'
        internal const int Version2 = 2;
        internal const long ChannelSizeV2 = 64;
        internal const long SequenceOffset = 16;
        internal const long StageIdOffset = 20;
        internal const long StageIndexOffset = 24;
        internal const long StageCountOffset = 28;
        internal const long ProbeActiveOffset = 32;
        internal const long HostFlagsOffset = 36;
        internal const long HostPidOffset = 40;

        internal const int HostFlagAutoCompatibility = 1 << 0;
        internal const int HostFlagLiveEscalationAllowed = 1 << 1;

        private readonly object _gate = new object();
        private readonly int _processId;
        private readonly MemoryMappedFile _mappingV2;
        private readonly MemoryMappedViewAccessor _viewV2;
        private MemoryMappedFile _mappingV1;
        private int _sequence;

        private HookCompatibilityChannel(int processId, MemoryMappedFile mappingV2,
            MemoryMappedViewAccessor viewV2)
        {
            _processId = processId;
            _mappingV2 = mappingV2;
            _viewV2 = viewV2;
        }

        internal int Sequence => Volatile.Read(ref _sequence);
        internal NativeHookCompatibilityFlags Flags { get; private set; }

        internal static string GetMappingName(int processId) =>
            $"Local\\CfxOsdHookCompatibilityV1_{processId}";

        internal static string GetMappingNameV2(int processId) =>
            $"Local\\CfxOsdHookCompatibilityV2_{processId}";

        internal static bool TryCreate(int processId, NativeHookCompatibilityFlags flags,
            out HookCompatibilityChannel channel, out string error)
            => TryCreate(processId, flags, stageId: 0, stageIndex: 1, stageCount: 1,
                probeActive: false, hostFlags: 0, out channel, out error);

        internal static bool TryCreate(int processId, NativeHookCompatibilityFlags flags,
            int stageId, int stageIndex, int stageCount, bool probeActive, int hostFlags,
            out HookCompatibilityChannel channel, out string error)
        {
            channel = null;
            error = null;
            if (processId <= 0)
            {
                error = "invalid target PID";
                return false;
            }

            try
            {
                MemoryMappedFile mapping = MemoryMappedFile.CreateOrOpen(
                    GetMappingNameV2(processId), ChannelSizeV2, MemoryMappedFileAccess.ReadWrite);
                MemoryMappedViewAccessor view = null;
                try
                {
                    view = mapping.CreateViewAccessor(0, ChannelSizeV2,
                        MemoryMappedFileAccess.ReadWrite);
                    var created = new HookCompatibilityChannel(processId, mapping, view);
                    // A resident hook keeps the section alive across a CapFrameX restart, so the
                    // mapping may already carry a sequence it consumed; continue from there.
                    if (view.ReadInt32(MagicOffset) == MagicV2 &&
                        view.ReadInt32(VersionOffset) == Version2 &&
                        view.ReadInt32(ProcessIdOffset) == processId)
                    {
                        created._sequence = Math.Max(0, view.ReadInt32(SequenceOffset));
                    }
                    // Header first, then the payload, then the sequence — the hook validates the
                    // header before it trusts any sequence.
                    view.Write(MagicOffset, MagicV2);
                    view.Write(VersionOffset, Version2);
                    view.Write(ProcessIdOffset, processId);
                    view.Write(HostPidOffset, Environment.ProcessId);
                    view.Flush();
                    if (!created.TryPublish(flags, stageId, stageIndex, stageCount, probeActive,
                        hostFlags, out error))
                    {
                        created.Dispose();
                        return false;
                    }
                    channel = created;
                    return true;
                }
                catch
                {
                    view?.Dispose();
                    mapping.Dispose();
                    throw;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                       ex is System.IO.IOException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Rewrites the stage and bumps the sequence. A version-2 hook applies the live bits on
        /// its next poll and reports the rest as pending restart; a version-1 hook never reads
        /// again, which is why its mapping is only ever created, not updated.
        /// </summary>
        internal bool TryPublish(NativeHookCompatibilityFlags flags, int stageId, int stageIndex,
            int stageCount, bool probeActive, int hostFlags, out string error)
        {
            error = null;
            lock (_gate)
            {
                try
                {
                    _viewV2.Write(FlagsOffset, unchecked((int)(uint)flags));
                    _viewV2.Write(StageIdOffset, stageId);
                    _viewV2.Write(StageIndexOffset, stageIndex);
                    _viewV2.Write(StageCountOffset, stageCount);
                    _viewV2.Write(ProbeActiveOffset, probeActive ? 1 : 0);
                    _viewV2.Write(HostFlagsOffset, hostFlags);
                    _viewV2.Flush();
                    Thread.MemoryBarrier();
                    int next = _sequence + 1;
                    _viewV2.Write(SequenceOffset, next);
                    _viewV2.Flush();
                    Volatile.Write(ref _sequence, next);
                    Flags = flags;

                    if (flags != NativeHookCompatibilityFlags.None && _mappingV1 == null)
                        _mappingV1 = CreateLegacyMapping(_processId, flags);
                    return true;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                           ex is System.IO.IOException ||
                                           ex is ArgumentException ||
                                           ex is NotSupportedException)
                {
                    error = $"{ex.GetType().Name}: {ex.Message}";
                    return false;
                }
            }
        }

        private static MemoryMappedFile CreateLegacyMapping(int processId,
            NativeHookCompatibilityFlags flags)
        {
            MemoryMappedFile mapping = MemoryMappedFile.CreateOrOpen(
                GetMappingName(processId), ChannelSize, MemoryMappedFileAccess.ReadWrite);
            try
            {
                using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                    0, ChannelSize, MemoryMappedFileAccess.ReadWrite))
                {
                    view.Write(MagicOffset, Magic);
                    view.Write(VersionOffset, Version);
                    view.Write(ProcessIdOffset, processId);
                    view.Write(FlagsOffset, unchecked((int)(uint)flags));
                    view.Flush();
                }
                return mapping;
            }
            catch
            {
                mapping.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _viewV2.Dispose();
                _mappingV2.Dispose();
                _mappingV1?.Dispose();
                _mappingV1 = null;
            }
        }
    }
}
