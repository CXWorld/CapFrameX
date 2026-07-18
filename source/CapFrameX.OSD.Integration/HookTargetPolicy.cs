using System;
using System.Diagnostics;
using System.IO;
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
