using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    internal enum VulkanCompositeRoute : uint { Automatic, Graphics, Compute, Suspended }
    internal enum VulkanProbeResult : uint { Pending, Composited, Unsupported, Failed, Dormant, Suspended }

    internal struct VulkanProbeSnapshot
    {
        internal uint AppliedRevision, Capabilities, Bitness;
        internal VulkanCompositeRoute RequestedRoute, ActualRoute;
        internal VulkanProbeResult Result;
        internal ulong Context, Build, Generation, TickMs, Successes;
        internal uint Vendor, Device, Driver, Family, QueueFlags, Format, ColorSpace, ImageUsage;
        internal string Signature => $"vulkan-v1:{Bitness}:{Vendor:X}:{Device:X}:{Driver:X}:" +
            $"{Family}:{QueueFlags:X}:{Format}:{ColorSpace}:{ImageUsage:X}:{Capabilities:X}";
        internal string BuildHash => $"vulkan-v1:{Bitness}:{Build:X16}";
    }

    /// <summary>
    /// Vulkan-only bidirectional channel. Each side owns a separate sequence-locked region;
    /// the native layer acknowledges the request revision, not the heartbeat publication.
    /// Mirrors vk_layer/src/vk_compatibility.cpp. Never changes renderer arbitration.
    /// </summary>
    internal sealed class HookVulkanCompatibilityChannel : IDisposable
    {
        internal const uint Magic = 0x31564C43;
        internal const int Size = 192;
        private readonly MemoryMappedFile _mapping;
        private readonly MemoryMappedViewAccessor _view;
        private readonly int _pid;
        private uint _publication, _revision;

        private HookVulkanCompatibilityChannel(int pid, MemoryMappedFile mapping,
            MemoryMappedViewAccessor view)
        {
            _pid = pid;
            _mapping = mapping;
            _view = view;
            _publication = view.ReadUInt32(16);
            _revision = view.ReadUInt32(52);
        }

        internal static string MappingName(int pid) => $"Local\\CfxOsdVulkanCompatibilityV1_{pid}";

        internal static bool TryOpen(int pid, out HookVulkanCompatibilityChannel channel)
        {
            channel = null;
            MemoryMappedFile mapping = null;
            MemoryMappedViewAccessor view = null;
            try
            {
                mapping = MemoryMappedFile.OpenExisting(MappingName(pid), MemoryMappedFileRights.ReadWrite);
                view = mapping.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
                if (!HeaderMatches(view, pid)) return false;
                channel = new HookVulkanCompatibilityChannel(pid, mapping, view);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is ArgumentException)
            {
                return false;
            }
            finally
            {
                if (channel == null) { view?.Dispose(); mapping?.Dispose(); }
            }
        }

        private static bool HeaderMatches(MemoryMappedViewAccessor view, int pid)
            => view.ReadUInt32(0) == Magic && view.ReadUInt32(4) == 1 &&
               view.ReadUInt32(8) == (uint)pid && view.ReadUInt32(12) == Size;

        internal bool TryRead(out VulkanProbeSnapshot snapshot)
        {
            snapshot = default;
            if (!HeaderMatches(_view, _pid)) return false;
            var words = new uint[32];
            uint before = _view.ReadUInt32(64);
            Thread.MemoryBarrier();
            _view.ReadArray(64, words, 0, words.Length);
            Thread.MemoryBarrier();
            if (before == 0 || words[0] != before || words[1] != before ||
                _view.ReadUInt32(64) != before || _view.ReadUInt32(68) != before ||
                words[3] > 3 || words[4] > 3 || words[5] > 5 ||
                (words[6] & ~3u) != 0 || (words[7] != 32 && words[7] != 64)) return false;
            snapshot = new VulkanProbeSnapshot
            {
                AppliedRevision = words[2], RequestedRoute = (VulkanCompositeRoute)words[3],
                ActualRoute = (VulkanCompositeRoute)words[4], Result = (VulkanProbeResult)words[5],
                Capabilities = words[6], Bitness = words[7], Context = Pair(words, 8),
                Build = Pair(words, 10), Generation = Pair(words, 12), TickMs = Pair(words, 14),
                Successes = Pair(words, 16), Vendor = words[18], Device = words[19],
                Driver = words[20], Family = words[21], QueueFlags = words[22],
                Format = words[23], ColorSpace = words[24], ImageUsage = words[25]
            };
            return snapshot.Context != 0 && snapshot.Generation != 0 && snapshot.Build != 0;
        }

        internal uint Publish(VulkanCompositeRoute route, ulong context, bool enabled,
            ulong nowMs, bool newRequest)
        {
            if (++_publication == 0) ++_publication;
            if (newRequest && ++_revision == 0) ++_revision;
            _view.Write(16, _publication);
            Thread.MemoryBarrier();
            _view.Write(24, (uint)route);
            _view.Write(28, enabled ? 1u : 0u);
            WritePair(32, context);
            WritePair(40, nowMs);
            _view.Write(48, (uint)Environment.ProcessId);
            _view.Write(52, _revision);
            Thread.MemoryBarrier();
            _view.Write(20, _publication);
            return _revision;
        }

        private static ulong Pair(uint[] words, int index)
            => words[index] | ((ulong)words[index + 1] << 32);
        private void WritePair(long offset, ulong value)
        {
            _view.Write(offset, (uint)value);
            _view.Write(offset + 4, (uint)(value >> 32));
        }
        public void Dispose() { _view.Dispose(); _mapping.Dispose(); }
    }
}
