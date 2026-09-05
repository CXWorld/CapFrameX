using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    internal enum VulkanLayerPresence
    {
        /// <summary>The target's module list was read and does not contain the layer.</summary>
        Absent,

        /// <summary>The layer is mapped into the target.</summary>
        Loaded,

        /// <summary>
        /// The module list could not be read. Never treat this as "no Vulkan": a snapshot of a
        /// process that is still loading its DLLs fails with ERROR_BAD_LENGTH, which is exactly
        /// the startup window in which a Vulkan title must not receive the DXGI hook.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Answers whether the CapFrameX Vulkan implicit layer is mapped into a target process.
    ///
    /// The renderer-arbiter mapping <see cref="VulkanActivityProbe"/> reads only exists after the
    /// layer's first vkQueuePresentKHR, which makes it an unreliable basis for the one decision
    /// that cannot be taken back: whether to LoadLibrary the DXGI hook into a Vulkan title. The
    /// Vulkan loader maps implicit layers during vkCreateInstance — before any present, and
    /// therefore before PresentMon can report the target's first frame row — so module presence
    /// answers "the layer may still become the renderer" without racing the first present. It
    /// does not prove that Vulkan owns presentation: DXGI titles may initialize Vulkan for an
    /// auxiliary API without ever creating a presenting Vulkan swapchain.
    ///
    /// A positive result is sticky: an implicit layer stays mapped for the process lifetime, and
    /// the answer only matters until injection. <see cref="HookOverlayManager"/> bounds how long
    /// a positive result without any Vulkan present can suppress sustained DXGI presentation. A
    /// negative result is re-checked no more often than <see cref="HookTargetPolicy"/> repeats its
    /// own module scan.
    /// </summary>
    internal static class VulkanLayerModuleProbe
    {
        internal const string LayerModuleName = "cfx_osd_vklayer.dll";

        private const uint Th32csSnapModule = 0x00000008;
        private const uint Th32csSnapModule32 = 0x00000010;
        private const int ErrorNoMoreFiles = 18;
        private const int ErrorBadLength = 24;
        private const int SnapshotRetryDelayMs = 15;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private static readonly long NegativeCacheTicks = Math.Max(1, Stopwatch.Frequency * 2L);

        private static readonly object Gate = new object();
        private static readonly Dictionary<int, CacheEntry> Cache = new Dictionary<int, CacheEntry>();

        private struct CacheEntry
        {
            public bool Loaded;
            public long ValidUntil;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ModuleEntry32
        {
            public uint Size;
            public uint ModuleId;
            public uint ProcessId;
            public uint GlobalUsageCount;
            public uint ProcessUsageCount;
            public IntPtr BaseAddress;
            public uint BaseSize;
            public IntPtr ModuleHandle;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string ModuleName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExePath;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Module32FirstW(IntPtr snapshot, ref ModuleEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Module32NextW(IntPtr snapshot, ref ModuleEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Reports whether the CapFrameX Vulkan layer is mapped into <paramref name="pid"/>.
        /// Only a conclusive answer is cached — a failed scan must be retried rather than
        /// remembered as "no layer" for the rest of its cache lifetime.
        /// </summary>
        /// <param name="forceRescan">
        /// Skips the negative cache. The check immediately before LoadLibrary must see the
        /// process as it is now, not as it was up to a cache lifetime ago — the layer can have
        /// been mapped in between.
        /// </param>
        internal static VulkanLayerPresence GetPresence(int pid, out string error,
            bool forceRescan = false)
        {
            error = null;
            if (pid <= 0) return VulkanLayerPresence.Absent;

            lock (Gate)
            {
                long now = Stopwatch.GetTimestamp();
                if (!forceRescan &&
                    Cache.TryGetValue(pid, out CacheEntry cached) &&
                    (cached.Loaded || now < cached.ValidUntil))
                {
                    return cached.Loaded
                        ? VulkanLayerPresence.Loaded
                        : VulkanLayerPresence.Absent;
                }

                if (!TryScanForLayerModule(pid, out bool loaded, out error))
                    return VulkanLayerPresence.Unknown;

                Cache[pid] = new CacheEntry
                {
                    Loaded = loaded,
                    ValidUntil = now + NegativeCacheTicks
                };
                return loaded ? VulkanLayerPresence.Loaded : VulkanLayerPresence.Absent;
            }
        }

        internal static void Invalidate(int pid)
        {
            if (pid <= 0) return;
            lock (Gate) Cache.Remove(pid);
        }

        internal static void Prune(Func<int, bool> isProcessAlive)
        {
            if (isProcessAlive == null) throw new ArgumentNullException(nameof(isProcessAlive));
            lock (Gate)
            {
                var stalePids = new List<int>();
                foreach (int pid in Cache.Keys)
                    if (!isProcessAlive(pid)) stalePids.Add(pid);
                foreach (int pid in stalePids)
                    Cache.Remove(pid);
            }
        }

        internal static bool IsLayerModule(string moduleName, string path)
        {
            return Matches(moduleName) || Matches(path);

            bool Matches(string candidate)
                => !string.IsNullOrEmpty(candidate) &&
                   candidate.IndexOf(LayerModuleName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Walks the target's module list. Returns false when the list could not be read in
        /// full — the caller must not mistake that for a conclusive "layer absent".
        /// </summary>
        private static bool TryScanForLayerModule(int pid, out bool loaded, out string error)
        {
            loaded = false;
            IntPtr snapshot = InvalidHandleValue;
            int snapshotError = 0;
            // A process that is still mapping its DLLs answers ERROR_BAD_LENGTH; retry briefly
            // rather than concluding anything from it.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                snapshot = CreateToolhelp32Snapshot(Th32csSnapModule | Th32csSnapModule32,
                    unchecked((uint)pid));
                if (snapshot != InvalidHandleValue) break;
                snapshotError = Marshal.GetLastWin32Error();
                if (snapshotError != ErrorBadLength) break;
                Thread.Sleep(SnapshotRetryDelayMs);
            }

            if (snapshot == InvalidHandleValue)
            {
                error = $"module snapshot failed (Win32 error {snapshotError})";
                return false;
            }

            try
            {
                var entry = new ModuleEntry32
                {
                    Size = (uint)Marshal.SizeOf<ModuleEntry32>()
                };
                if (!Module32FirstW(snapshot, ref entry))
                {
                    error = $"module list was empty (Win32 error {Marshal.GetLastWin32Error()})";
                    return false;
                }

                do
                {
                    if (IsLayerModule(entry.ModuleName, entry.ExePath))
                    {
                        loaded = true;
                        error = null;
                        return true;
                    }
                    entry.Size = (uint)Marshal.SizeOf<ModuleEntry32>();
                }
                while (Module32NextW(snapshot, ref entry));

                int enumerationError = Marshal.GetLastWin32Error();
                if (enumerationError != ErrorNoMoreFiles)
                {
                    // Aborted part way through: the layer may sit in the unread remainder.
                    error = $"module enumeration aborted (Win32 error {enumerationError})";
                    return false;
                }

                error = null;
                return true;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }
    }
}
