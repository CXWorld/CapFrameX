using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Shared renderer eligibility policy for both DXGI injection and Vulkan TargetPid publication.
    /// A failed or suspicious module scan disables the in-game renderer while leaving the
    /// hook-free overlay available. Clean results are cached briefly so startup races can recover.
    /// Ricochet hides the module list even from an elevated CapFrameX, so it is recognized from
    /// outside (<see cref="GetRicochetReason"/>). A denial that is final for the process (an
    /// anti-cheat module, Ricochet, the injection blacklist) lets the hook-free renderer stand in
    /// for the in-game one (<see cref="RequestsHookFreeFallback"/>).
    /// </summary>
    internal static class HookTargetPolicy
    {
        private const uint Th32csSnapModule = 0x00000008;
        private const uint Th32csSnapModule32 = 0x00000010;
        private const uint ProcessQueryLimitedInformation = 0x00001000;
        private const int ErrorAccessDenied = 5;
        private const int ErrorNoMoreFiles = 18;
        private const int ErrorBadLength = 24;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        // Ricochet, the kernel anti-cheat of Call of Duty, denied the module scan to an elevated
        // CapFrameX on 2026-10-25 (Xbox app: cod.exe, then sp26-cod.exe). Its driver ships with the
        // game (Content\Randgrid.sys beside cod.exe, one folder above sp26\sp26-cod.exe), and the
        // game starts it as a kernel driver service (atvi-randgrid_msstore for the Store build).
        internal const string RicochetReasonPrefix = "Ricochet anti-cheat";
        internal const string RicochetDriverFileName = "Randgrid.sys";
        internal const string RicochetDriverServicePrefix = "atvi-randgrid";
        internal const int RicochetDriverFolderLevels = 2;
        internal const string AntiCheatReasonPrefix = "anti-cheat module ";
        internal const string BlacklistReasonSuffix = " is on the in-game injection blacklist";
        private static readonly string[] AntiCheatMarkers =
        {
            "easyanticheat",
            "beclient",
            "battleye",
            "vac_module",
        };
        private static readonly string[] InjectionBlacklist =
        {
            // AoE II: Definitive Edition terminates during the D3D9-to-DXGI transition when the
            // native in-game hook is present. Keep capture and the hook-free overlay available.
            "AoE2DE_s",

            // Age of Mythology: Retold (DX12)
            "AoMRT_s",

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags,
            StringBuilder name, ref int size);

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

        /// <summary>
        /// True for block reasons that let the hook-free renderer stand in for the selected in-game
        /// renderer: a denial that is final for the process while a desktop window is fine, that is
        /// an anti-cheat module, Ricochet or the injection blacklist. A transient startup failure
        /// (<see cref="IsTransientStartupFailure"/>) does not qualify: it often passes within seconds
        /// of a start, and every switch would create or remove the hook-free window.
        /// </summary>
        internal static bool RequestsHookFreeFallback(string reason)
            => !string.IsNullOrEmpty(reason) &&
               (reason.StartsWith(RicochetReasonPrefix, StringComparison.Ordinal) ||
                reason.StartsWith(AntiCheatReasonPrefix, StringComparison.Ordinal) ||
                reason.EndsWith(BlacklistReasonSuffix, StringComparison.Ordinal));

        internal static string GetAntiCheatReason(string moduleName, string marker)
            => $"{AntiCheatReasonPrefix}'{moduleName}' matched '{marker}'";

        /// <summary>
        /// The block reason of a Ricochet title, or null: its driver ships with the game, or the
        /// game denies the module scan while a Ricochet driver service runs. A denied scan alone
        /// stays a plain failure, other protected processes deny it as well.
        /// </summary>
        internal static string GetRicochetReason(string driverBesideGame, int moduleScanError,
            string runningDriverService)
        {
            if (!string.IsNullOrEmpty(driverBesideGame))
                return $"{RicochetReasonPrefix} ({Path.GetFileName(driverBesideGame)} ships with the game)";
            if (moduleScanError == ErrorAccessDenied && !string.IsNullOrEmpty(runningDriverService))
                return $"{RicochetReasonPrefix} (driver service '{runningDriverService}' runs and " +
                    "the game denied the module scan)";
            return null;
        }

        /// <summary>
        /// Ricochet's driver in the executable's folder or up to
        /// <see cref="RicochetDriverFolderLevels"/> folders above it, else null. COD HQ starts each
        /// title from a subfolder of the install that holds the driver.
        /// </summary>
        internal static string FindRicochetDriver(string executablePath, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || fileExists == null) return null;
            string folder = Path.GetDirectoryName(executablePath);
            for (int level = 0; level <= RicochetDriverFolderLevels && !string.IsNullOrEmpty(folder); level++)
            {
                string candidate = Path.Combine(folder, RicochetDriverFileName);
                if (fileExists(candidate)) return candidate;
                folder = Path.GetDirectoryName(folder);
            }
            return null;
        }

        internal static bool IsRicochetDriverService(string serviceName)
            => !string.IsNullOrEmpty(serviceName) &&
               serviceName.StartsWith(RicochetDriverServicePrefix, StringComparison.OrdinalIgnoreCase);

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
                    reason = $"process '{normalized}'{BlacklistReasonSuffix}";
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

            // Ahead of the scan, which Ricochet denies: the reason has to name it.
            string driverBesideGame = TryReadImagePath(pid, out string imagePath)
                ? FindRicochetDriver(imagePath, File.Exists)
                : null;
            if (driverBesideGame != null)
            {
                reason = GetRicochetReason(driverBesideGame, 0, null);
                return false;
            }

            if (ScanModules(pid, out reason, out int scanError)) return true;
            if (scanError == ErrorAccessDenied)
                reason = GetRicochetReason(null, scanError, FindRunningRicochetDriverService()) ?? reason;
            return false;
        }

        // Process.MainModule needs the module access Ricochet denies; the image name only needs
        // PROCESS_QUERY_LIMITED_INFORMATION, which it grants.
        internal static bool TryReadImagePath(int pid, out string imagePath)
        {
            imagePath = null;
            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero) return false;
            try
            {
                var buffer = new StringBuilder(1024);
                int length = buffer.Capacity;
                if (!QueryFullProcessImageName(process, 0, buffer, ref length)) return false;
                imagePath = buffer.ToString(0, length);
                return true;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        // Only asked after a game denied the module scan. The driver list is one SCM call that
        // needs no administrator rights.
        private static string FindRunningRicochetDriverService()
        {
            ServiceController[] drivers;
            try
            {
                drivers = ServiceController.GetDevices();
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception)
            {
                return null;
            }

            try
            {
                foreach (ServiceController driver in drivers)
                {
                    if (!IsRicochetDriverService(driver.ServiceName)) continue;
                    try
                    {
                        if (driver.Status == ServiceControllerStatus.Running) return driver.ServiceName;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
                return null;
            }
            finally
            {
                foreach (ServiceController driver in drivers) driver.Dispose();
            }
        }

        private static bool ScanModules(int pid, out string reason, out int snapshotError)
        {
            IntPtr snapshot = InvalidHandleValue;
            snapshotError = 0;
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
                reason = $"module scan failed ({snapshotError})";
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
                        reason = GetAntiCheatReason(entry.ModuleName, marker);
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
