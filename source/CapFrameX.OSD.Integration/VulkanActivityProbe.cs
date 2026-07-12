using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Reads the per-process renderer arbitration state published by the Vulkan layer.
    /// A recent Vulkan present must suppress DXGI hook injection, not merely DXGI drawing:
    /// installing a dormant Present hook is enough to destabilize some interop swapchains.
    /// </summary>
    internal static class VulkanActivityProbe
    {
        internal const ulong PriorityWindowMs = 2000;

        // Keep these values synchronized with renderer_arbiter.cpp::RendererState.
        private const int StateVersion = 2;
        private const long StateSize = 24;
        private const long VersionOffset = 0;
        private const long LastVulkanPresentOffset = 8;

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        internal static bool TryHasRecentPresent(int pid, out bool recent, out string error)
        {
            recent = false;
            error = null;
            if (pid <= 0)
            {
                error = "invalid target PID";
                return false;
            }

            string mappingName = $"Local\\CfxOsdRendererStateV2_{pid}";
            try
            {
                using (var mapping = MemoryMappedFile.OpenExisting(
                    mappingName, MemoryMappedFileRights.Read))
                using (var view = mapping.CreateViewAccessor(
                    0, StateSize, MemoryMappedFileAccess.Read))
                {
                    int version = view.ReadInt32(VersionOffset);
                    if (version != StateVersion)
                    {
                        error = $"renderer state version {version}, expected {StateVersion}";
                        return false;
                    }

                    long lastPresent = view.ReadInt64(LastVulkanPresentOffset);
                    if (lastPresent <= 0) return true;

                    recent = IsRecent(unchecked((ulong)lastPresent), GetTickCount64(),
                        PriorityWindowMs);
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                // Normal for a process which has not loaded/presented through our Vulkan layer.
                return true;
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

        internal static bool IsRecent(ulong then, ulong now, ulong windowMs)
            => now < then || now - then <= windowMs;
    }
}
