using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Shared renderer eligibility policy for both DXGI injection and Vulkan TargetPid publication.
    /// A failed or suspicious module scan disables the in-game renderer while leaving the
    /// hook-free overlay available. Clean results are cached briefly so startup races can recover.
    /// </summary>
    internal static class HookTargetPolicy
    {
        private const uint Th32csSnapModule = 0x00000008;
        private const uint Th32csSnapModule32 = 0x00000010;
        private const int ErrorNoMoreFiles = 18;
        private const int ErrorBadLength = 24;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private static readonly string[] AntiCheatMarkers =
        {
            "easyanticheat",
            "beclient",
            "battleye",
            "vac_module",
        };
        private static readonly string[] InjectionBlacklist =
        {
            // CS2's default Trusted Mode rejects/ejects third-party hook DLLs. Keep the game
            // detectable so capture and the hook-free overlay continue to work; only native
            // injection is denied here.
            "cs2",

            // Vanguard is mandatory for VALORANT and operates outside the user-mode module
            // markers scanned below. Do not make an unsigned LoadLibrary attempt there.
            "VALORANT-Win64-Shipping",
        };

        // Overlays that install their own present hook. Their mere presence is NOT a reason to
        // refuse injection — RTSS is loaded in every game while it runs (CapFrameX starts it
        // itself for the RTSS renderer) and the Steam overlay is in every Steam title by default,
        // so treating them as blockers would disable the in-game hook almost everywhere. They only
        // matter in combination with a mid-session injection, see HookOverlayManager.
        private static readonly string[] ForeignOverlayModules =
        {
            "rtsshooks",            // RivaTuner Statistics Server / MSI Afterburner
            "gameoverlayrenderer",  // Steam
            "discordhook",          // Discord
            "eosovh",               // Epic Online Services
            "graphics-hook",        // OBS game capture
            "nvspcap",              // NVIDIA ShadowPlay / GeForce Experience
        };

        private static readonly object Gate = new object();
        private static readonly long CacheTicks = Math.Max(1, Stopwatch.Frequency * 2L);
        private static int _cachedPid;
        private static long _cacheUntil;
        private static bool _cachedAllowed;
        private static string _cachedReason;

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

        internal static bool IsAllowed(int pid, out string reason)
        {
            if (pid <= 0)
            {
                reason = "no target process";
                return false;
            }

            lock (Gate)
            {
                long now = Stopwatch.GetTimestamp();
                if (_cachedPid == pid && now < _cacheUntil)
                {
                    reason = _cachedReason;
                    return _cachedAllowed;
                }

                _cachedAllowed = EvaluateTarget(pid, out _cachedReason);
                _cachedPid = pid;
                _cacheUntil = now + CacheTicks;
                reason = _cachedReason;
                return _cachedAllowed;
            }
        }

        internal static void Invalidate(int pid)
        {
            lock (Gate)
            {
                if (pid > 0 && pid != _cachedPid) return;
                _cachedPid = 0;
                _cacheUntil = 0;
                _cachedAllowed = false;
                _cachedReason = null;
            }
        }

        // A process can become visible a few milliseconds before its loader has exposed a stable
        // module list. Early injection retries these readiness failures immediately; a real policy
        // denial (blacklist or anti-cheat marker) continues through the normal bounded backoff.
        internal static bool IsTransientStartupFailure(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return false;
            return reason.StartsWith("process identity check failed", StringComparison.Ordinal) ||
                reason.StartsWith("module scan failed", StringComparison.Ordinal) ||
                reason.StartsWith("module scan returned no modules", StringComparison.Ordinal) ||
                reason.StartsWith("module enumeration failed", StringComparison.Ordinal);
        }

        internal static bool HasAntiCheatMarker(string moduleName, string path, out string marker)
        {
            foreach (string candidate in AntiCheatMarkers)
            {
                if ((!string.IsNullOrEmpty(moduleName) &&
                     moduleName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(path) &&
                     path.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    marker = candidate;
                    return true;
                }
            }

            marker = null;
            return false;
        }

        internal static bool IsInjectionBlacklisted(string processName, out string reason)
        {
            string normalized = string.IsNullOrWhiteSpace(processName)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(processName.Trim());
            foreach (string candidate in InjectionBlacklist)
            {
                if (string.Equals(normalized, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"process '{normalized}' is on the in-game injection blacklist";
                    return true;
                }
            }

            reason = null;
            return false;
        }

        /// <summary>
        /// Scans a live target without caching and returns the first requested module that is
        /// currently mapped. A successful scan with no match returns true and a null module.
        /// Early injection uses this as a loader milestone so it never runs merely because the
        /// process object became visible while the executable's CRT is still starting.
        /// </summary>
        internal static bool TryFindLoadedModule(int pid,
            IEnumerable<string> requestedModules, out string loadedModule,
            out string error)
        {
            loadedModule = null;
            error = null;
            if (pid <= 0)
            {
                error = "no target process";
                return false;
            }

            var requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (requestedModules != null)
            {
                foreach (string module in requestedModules)
                {
                    if (!string.IsNullOrWhiteSpace(module))
                        requested.Add(Path.GetFileName(module));
                }
            }
            if (requested.Count == 0)
            {
                error = "no module names requested";
                return false;
            }

            IntPtr snapshot = InvalidHandleValue;
            int snapshotError = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                snapshot = CreateToolhelp32Snapshot(Th32csSnapModule | Th32csSnapModule32,
                    unchecked((uint)pid));
                if (snapshot != InvalidHandleValue) break;
                snapshotError = Marshal.GetLastWin32Error();
                if (snapshotError != ErrorBadLength) break;
            }

            if (snapshot == InvalidHandleValue)
            {
                error = $"module scan failed ({snapshotError})";
                return false;
            }

            try
            {
                var entry = new ModuleEntry32 { Size = (uint)Marshal.SizeOf<ModuleEntry32>() };
                if (!Module32FirstW(snapshot, ref entry))
                {
                    error = $"module scan returned no modules ({Marshal.GetLastWin32Error()})";
                    return false;
                }

                do
                {
                    if (!string.IsNullOrWhiteSpace(entry.ModuleName) &&
                        requested.Contains(entry.ModuleName))
                    {
                        loadedModule = entry.ModuleName;
                        return true;
                    }
                    entry.Size = (uint)Marshal.SizeOf<ModuleEntry32>();
                }
                while (Module32NextW(snapshot, ref entry));

                int enumerationError = Marshal.GetLastWin32Error();
                if (enumerationError != ErrorNoMoreFiles)
                {
                    error = $"module enumeration failed ({enumerationError})";
                    return false;
                }

                return true;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        /// <summary>
        /// Names of the foreign present-hook overlays currently loaded in <paramref name="pid"/>,
        /// newest scan (never cached — the caller evaluates this once per process).
        /// </summary>
        /// <returns>
        /// false when the module list could not be read; <paramref name="modules"/> is empty then.
        /// An unreadable list is NOT evidence of absence, but it is also not a reason to hold the
        /// hook back on its own — the caller decides.
        /// </returns>
        internal static bool TryGetForeignOverlayModules(int pid, out string[] modules,
            out string error)
        {
            modules = Array.Empty<string>();
            error = null;
            if (pid <= 0)
            {
                error = "no target process";
                return false;
            }

            var found = new List<string>();
            IntPtr snapshot = InvalidHandleValue;
            int snapshotError = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                snapshot = CreateToolhelp32Snapshot(Th32csSnapModule | Th32csSnapModule32,
                    unchecked((uint)pid));
                if (snapshot != InvalidHandleValue) break;
                snapshotError = Marshal.GetLastWin32Error();
                if (snapshotError != ErrorBadLength) break;
            }

            if (snapshot == InvalidHandleValue)
            {
                error = $"module scan failed ({snapshotError})";
                return false;
            }

            try
            {
                var entry = new ModuleEntry32 { Size = (uint)Marshal.SizeOf<ModuleEntry32>() };
                if (!Module32FirstW(snapshot, ref entry))
                {
                    error = $"module scan returned no modules ({Marshal.GetLastWin32Error()})";
                    return false;
                }

                do
                {
                    string name = entry.ModuleName;
                    foreach (string candidate in ForeignOverlayModules)
                    {
                        if (!string.IsNullOrEmpty(name) &&
                            name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !found.Contains(name, StringComparer.OrdinalIgnoreCase))
                        {
                            found.Add(name);
                            break;
                        }
                    }
                    entry.Size = (uint)Marshal.SizeOf<ModuleEntry32>();
                }
                while (Module32NextW(snapshot, ref entry));

                int enumerationError = Marshal.GetLastWin32Error();
                if (enumerationError != ErrorNoMoreFiles)
                {
                    error = $"module enumeration failed ({enumerationError})";
                    return false;
                }

                modules = found.ToArray();
                return true;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        private static bool EvaluateTarget(int pid, out string reason)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    if (IsInjectionBlacklisted(process.ProcessName, out reason))
                        return false;
                }
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                reason = $"process identity check failed ({ex.Message})";
                return false;
            }

            return ScanModules(pid, out reason);
        }

        private static bool ScanModules(int pid, out string reason)
        {
            IntPtr snapshot = InvalidHandleValue;
            int error = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                snapshot = CreateToolhelp32Snapshot(Th32csSnapModule | Th32csSnapModule32,
                    unchecked((uint)pid));
                if (snapshot != InvalidHandleValue) break;
                error = Marshal.GetLastWin32Error();
                if (error != ErrorBadLength) break;
            }

            if (snapshot == InvalidHandleValue)
            {
                reason = $"module scan failed ({error})";
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
                    reason = $"module scan returned no modules ({Marshal.GetLastWin32Error()})";
                    return false;
                }

                do
                {
                    if (HasAntiCheatMarker(entry.ModuleName, entry.ExePath, out string marker))
                    {
                        reason = $"anti-cheat module '{entry.ModuleName}' matched '{marker}'";
                        return false;
                    }
                    entry.Size = (uint)Marshal.SizeOf<ModuleEntry32>();
                }
                while (Module32NextW(snapshot, ref entry));

                int enumerationError = Marshal.GetLastWin32Error();
                if (enumerationError != ErrorNoMoreFiles)
                {
                    reason = $"module enumeration failed ({enumerationError})";
                    return false;
                }

                reason = null;
                return true;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }
    }
}
