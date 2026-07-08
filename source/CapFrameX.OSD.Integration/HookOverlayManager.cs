using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Drives the in-game hook overlay from CapFrameX's OWN process detection: it watches
    /// the detected game PID (the same <c>IProcessService.ProcessIdStream</c> the overlay/
    /// capture pipeline uses) and, when the hook overlay is enabled, injects
    /// cfx_osd_hook.dll straight into that process — no manual injector, no proxy DLL.
    ///
    /// Opt-in via <see cref="IAppConfiguration.EnableHookOverlay"/>. Each PID is injected at
    /// most once; a PID that has exited is forgotten so a relaunch re-injects. Injection runs
    /// off the caller thread and never throws into the app.
    /// </summary>
    public sealed class HookOverlayManager : IDisposable
    {
        private const string HookDllName = "cfx_osd_hook.dll";
        private const string InjectHelperName = "cfx_inject.exe"; // x86 bitness helper (WOW64 targets)

        private readonly IAppConfiguration _appConfiguration;
        private readonly IDisposable _pidSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _visibilitySub;
        private readonly HookVisibilityChannel _visibility;
        private readonly string _dllPath;         // x64 hook DLL
        private readonly string _dllPathX86;      // x86 hook DLL (32-bit targets)
        private readonly string _injectHelperX86; // x86 cfx_inject.exe helper
        private readonly object _gate = new object();
        private readonly HashSet<int> _injected = new HashSet<int>();
        private volatile bool _enabled;
        private volatile int _currentPid;

        /// <param name="processIdStream">The detected-game PID stream (IProcessService.ProcessIdStream).</param>
        /// <param name="dllPathOverride">Optional explicit path to cfx_osd_hook.dll.</param>
        public HookOverlayManager(IAppConfiguration appConfiguration, IObservable<int> processIdStream,
            string dllPathOverride = null)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            if (processIdStream == null) throw new ArgumentNullException(nameof(processIdStream));

            _dllPath = dllPathOverride ?? ResolveHookAsset(HookDllName, "CFX_HOOK_DLL");
            _dllPathX86 = ResolveHookAsset(Path.Combine("x86", HookDllName), "CFX_HOOK_DLL_X86");
            _injectHelperX86 = ResolveHookAsset(Path.Combine("x86", InjectHelperName), "CFX_INJECT_X86");
            _enabled = appConfiguration.EnableHookOverlay;

            // Mirror the hook overlay's effective visibility to the in-game hook via a named event;
            // the hook reads it each present and skips drawing while it is reset. The hook must draw
            // only while it is BOTH enabled ("In-game hook overlay") AND toggled on (ALT+O =
            // IsOverlayActive), so unchecking the box hides the resident overlay LIVE — otherwise the
            // already-injected hook keeps drawing (it doesn't otherwise learn it was disabled).
            _visibility = HookVisibilityChannel.Create(
                appConfiguration.EnableHookOverlay && appConfiguration.IsOverlayActive);

            _enabledSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookOverlay))
                .Subscribe(x => OnEnabledChanged((bool)x.value));

            _visibilitySub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.IsOverlayActive))
                .Subscribe(_ => UpdateHookVisibility());

            // Only act on a genuinely new PID; ignore repeats and the 0 placeholder.
            _pidSub = processIdStream
                .DistinctUntilChanged()
                .Subscribe(OnProcessId);
        }

        private void OnEnabledChanged(bool enabled)
        {
            _enabled = enabled;
            UpdateHookVisibility(); // disabling hides the resident hook immediately (no game restart)
            if (enabled)
            {
                int pid = _currentPid;
                if (pid > 0) TryInjectAsync(pid);
            }
        }

        // The hook draws only while it is BOTH enabled and toggled on (ALT+O). Push that combined
        // state to the in-game hook through the named visibility event.
        private void UpdateHookVisibility()
        {
            _visibility.SetVisible(_appConfiguration.EnableHookOverlay && _appConfiguration.IsOverlayActive);
        }

        private void OnProcessId(int pid)
        {
            _currentPid = pid;
            if (pid <= 0)
            {
                // process deselected/exited: forget stale PIDs so a relaunch re-injects
                PruneExited();
                return;
            }
            if (_enabled) TryInjectAsync(pid);
        }

        private void TryInjectAsync(int pid)
        {
            lock (_gate)
            {
                if (_injected.Contains(pid)) return; // already injected this process
                _injected.Add(pid);                  // reserve now to avoid a double-inject race
            }

            Task.Run(() =>
            {
                try
                {
                    // The detection can briefly hold a stale PID; skip if the process is gone.
                    if (!IsProcessAlive(pid))
                    {
                        lock (_gate) { _injected.Remove(pid); }
                        return;
                    }

                    // Pick the path by the TARGET's bitness: x64 games get the x64 hook injected
                    // directly; 32-bit (WOW64) games get the x86 hook via the bitness-matched helper.
                    if (!HookInjector.TryGetIsWow64(pid, out bool isWow64, out string bitError))
                    {
                        Log.Warning("HookOverlay: cannot determine bitness of pid {pid} — {error}", pid, bitError);
                        lock (_gate) { _injected.Remove(pid); }
                        return;
                    }

                    string arch = isWow64 ? "x86" : "x64";
                    string sourceDll = isWow64 ? _dllPathX86 : _dllPath;
                    if (string.IsNullOrEmpty(sourceDll) || !File.Exists(sourceDll))
                    {
                        Log.Warning("HookOverlay: cannot inject into {arch} pid {pid} — {dll} not found (looked at '{path}')",
                            arch, pid, HookDllName, sourceDll);
                        lock (_gate) { _injected.Remove(pid); }
                        return;
                    }

                    // Inject a per-version COPY, never the staged DLL itself: a game LoadLibrary's (and
                    // locks) whatever file it loads, so loading the staged DLL directly would lock it and
                    // block CapFrameX builds/updates while an injected game runs. The copy lives under
                    // LocalAppData\hook\<arch>\, named by content hash, so a new build gets a new file and
                    // never collides with a copy a running game still holds.
                    string injectable = PrepareInjectableCopy(sourceDll, arch);
                    if (injectable == null)
                    {
                        Log.Warning("HookOverlay: could not prepare an injectable {arch} copy of {dll}", arch, HookDllName);
                        lock (_gate) { _injected.Remove(pid); }
                        return;
                    }

                    string error;
                    bool ok = isWow64
                        ? HookInjector.TryInjectViaHelper(pid, injectable, _injectHelperX86, out error)
                        : HookInjector.TryInject(pid, injectable, out error);

                    if (ok)
                        Log.Information("HookOverlay: injected {dll} ({arch}) into pid {pid}", HookDllName, arch, pid);
                    else
                    {
                        Log.Warning("HookOverlay: injection into {arch} pid {pid} failed — {error}", arch, pid, error);
                        lock (_gate) { _injected.Remove(pid); } // allow a retry on next detection
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "HookOverlay: unexpected error injecting into pid {pid}", pid);
                    lock (_gate) { _injected.Remove(pid); }
                }
            });
        }

        // Copy the source DLL to LocalAppData\CapFrameX\hook\cfx_osd_hook_<hash8>.dll and
        // return that path. The copy is what games load and lock, keeping the app-bin DLL
        // free so CapFrameX can be rebuilt/updated while injected games are running.
        private string PrepareInjectableCopy(string sourceDll, string arch)
        {
            try
            {
                string tag;
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(sourceDll))
                    tag = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").Substring(0, 8).ToLowerInvariant();

                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CapFrameX", "hook", arch);
                Directory.CreateDirectory(dir);
                var target = Path.Combine(dir, $"cfx_osd_hook_{tag}.dll");

                if (!File.Exists(target))
                {
                    try { File.Copy(sourceDll, target, overwrite: false); }
                    catch (IOException) { /* created concurrently, or same-content file already present */ }
                }
                TryCleanupOldCopies(dir, target);
                return File.Exists(target) ? target : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: PrepareInjectableCopy failed");
                return null;
            }
        }

        // Best-effort removal of stale copies from earlier versions. Files a running game
        // still holds are locked and simply left in place.
        private static void TryCleanupOldCopies(string dir, string keep)
        {
            try
            {
                foreach (var f in Directory.GetFiles(dir, "cfx_osd_hook_*.dll"))
                {
                    if (string.Equals(f, keep, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(f); } catch { /* locked by a running game */ }
                }
            }
            catch { }
        }

        private void PruneExited()
        {
            lock (_gate)
            {
                _injected.RemoveWhere(p => !IsProcessAlive(p));
            }
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                    return !p.HasExited;
            }
            catch (ArgumentException) { return false; } // no such process
            catch (InvalidOperationException) { return false; }
        }

        // Resolve a staged hook asset relative to the app-output 'hook' folder: the x64 DLL
        // ("cfx_osd_hook.dll"), the x86 DLL ("x86\cfx_osd_hook.dll") or the x86 helper
        // ("x86\cfx_inject.exe"). The build stages these into 'hook' (not next to the exe) so a game
        // locking the injected COPY never touches the app tree. envVar gives a dev/testing override.
        private static string ResolveHookAsset(string relative, string envVar)
        {
            var envOverride = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(envOverride) && File.Exists(envOverride)) return envOverride;

            foreach (var dir in new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.GetDirectoryName(typeof(HookOverlayManager).Assembly.Location),
            })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var candidate = Path.Combine(dir, "hook", relative);
                if (File.Exists(candidate)) return candidate;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "hook", relative);
        }

        public void Dispose()
        {
            _pidSub?.Dispose();
            _enabledSub?.Dispose();
            _visibilitySub?.Dispose();
            _visibility?.Dispose();
            // The injected hook disables itself when it observes CapFrameX exiting (it polls
            // a SYNCHRONIZE handle to this process), so no explicit teardown signal is needed.
        }
    }
}
