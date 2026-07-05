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

        private readonly IAppConfiguration _appConfiguration;
        private readonly IDisposable _pidSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _visibilitySub;
        private readonly HookVisibilityChannel _visibility;
        private readonly string _dllPath;
        private readonly object _gate = new object();
        private readonly HashSet<int> _injected = new HashSet<int>();
        private volatile bool _enabled;
        private volatile int _currentPid;

        /// <param name="processIdStream">The detected-game PID stream (IProcessService.ProcessIdStream).</param>
        /// <param name="dllPathOverride">Optional explicit path to cfx_osd_hook.dll.</param>
        public HookOverlayManager(IAppConfiguration appConfiguration,
                                  IObservable<int> processIdStream,
                                  string dllPathOverride = null)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            if (processIdStream == null) throw new ArgumentNullException(nameof(processIdStream));

            _dllPath = dllPathOverride ?? ResolveHookDll();
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
            if (string.IsNullOrEmpty(_dllPath) || !File.Exists(_dllPath))
            {
                Log.Warning("HookOverlay: cannot inject — {dll} not found (looked at '{path}')", HookDllName, _dllPath);
                return;
            }

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

                    // Inject a per-version COPY, never the app-bin DLL itself: a game
                    // LoadLibrary's (and locks) whatever file it loads, so loading the
                    // app-bin DLL directly would lock it and block CapFrameX builds/updates
                    // while any injected game runs. The copy lives under LocalAppData and is
                    // named by content hash, so a new build gets a new file and never
                    // collides with a copy a running game still holds.
                    string injectable = PrepareInjectableCopy();
                    if (injectable == null)
                    {
                        Log.Warning("HookOverlay: could not prepare an injectable copy of {dll}", HookDllName);
                        lock (_gate) { _injected.Remove(pid); }
                        return;
                    }

                    if (HookInjector.TryInject(pid, injectable, out string error))
                        Log.Information("HookOverlay: injected {dll} into pid {pid}", HookDllName, pid);
                    else
                    {
                        Log.Warning("HookOverlay: injection into pid {pid} failed — {error}", pid, error);
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
        private string PrepareInjectableCopy()
        {
            try
            {
                string tag;
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(_dllPath))
                    tag = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").Substring(0, 8).ToLowerInvariant();

                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CapFrameX", "hook");
                Directory.CreateDirectory(dir);
                var target = Path.Combine(dir, $"cfx_osd_hook_{tag}.dll");

                if (!File.Exists(target))
                {
                    try { File.Copy(_dllPath, target, overwrite: false); }
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

        private static string ResolveHookDll()
        {
            // The build stages the DLL into a 'hook' subfolder (not next to the exe) so a
            // game locking the injected COPY never touches the app tree. An env override
            // (CFX_HOOK_DLL) helps dev/testing.
            var envOverride = Environment.GetEnvironmentVariable("CFX_HOOK_DLL");
            if (!string.IsNullOrEmpty(envOverride) && File.Exists(envOverride)) return envOverride;

            foreach (var dir in new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.GetDirectoryName(typeof(HookOverlayManager).Assembly.Location),
            })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var candidate = Path.Combine(dir, "hook", HookDllName);
                if (File.Exists(candidate)) return candidate;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "hook", HookDllName);
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
