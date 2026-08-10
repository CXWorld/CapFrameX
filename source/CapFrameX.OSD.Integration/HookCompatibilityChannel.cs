using System;
using System.IO.MemoryMappedFiles;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Publishes the selected compatibility flags before LoadLibrary injects the native hook.
    /// The mapping is retained by the manager so the hook's deferred initialization thread can
    /// consume it after DllMain returns.
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

        private readonly MemoryMappedFile _mapping;

        private HookCompatibilityChannel(MemoryMappedFile mapping)
        {
            _mapping = mapping;
        }

        internal static string GetMappingName(int processId) =>
            $"Local\\CfxOsdHookCompatibilityV1_{processId}";

        internal static bool TryCreate(int processId, NativeHookCompatibilityFlags flags,
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
                    channel = new HookCompatibilityChannel(mapping);
                    return true;
                }
                catch
                {
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

        public void Dispose()
        {
            _mapping.Dispose();
        }
    }
}
