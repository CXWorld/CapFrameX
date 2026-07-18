using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Drives the in-game hook overlay from CapFrameX's OWN process detection: it watches
    /// the detected game PID (the same <c>IProcessService.ProcessIdStream</c> the overlay/
    /// capture pipeline uses) and, when the hook overlay is enabled, injects
    /// cfx_osd_hook.dll straight into that process — no manual injector, no proxy DLL.
    ///
    /// Opt-in via <see cref="IAppConfiguration.EnableHookOverlay"/>. Each PID is successfully
    /// injected at most once; failures use a per-PID exponential retry backoff. A PID that has
    /// exited is forgotten so a relaunch re-injects. Injection runs off the caller thread and
    /// never throws into the app.
    /// </summary>
    public sealed class HookOverlayManager : IDisposable
    {
        private const string HookDllName = "cfx_osd_hook.dll";
        private const string InjectHelperName = "cfx_inject.exe"; // x86 bitness helper (WOW64 targets)
        private static readonly long VulkanProbeRetryTicks =
            Math.Max(1, Stopwatch.Frequency / 10L);

        private readonly IAppConfiguration _appConfiguration;
        private readonly IDisposable _pidSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _visibilitySub;
        private readonly IDisposable _runtimeSub;
        private readonly BehaviorSubject<bool> _hookFreeFallbackStream =
            new BehaviorSubject<bool>(false);
        private readonly HookVisibilityChannel _visibility;
        private readonly HookOverlayStatusService _statusService;
        private readonly Timer _statusTimer;
        private readonly string _dllPath;         // x64 hook DLL
        private readonly string _dllPathX86;      // x86 hook DLL (32-bit targets)
        private readonly string _injectHelperX86; // x86 cfx_inject.exe helper
        private readonly object _gate = new object();
        private readonly object _stateGate = new object();
        private readonly object _fallbackGate = new object();
        private readonly int _processIdColumnIndex;
        private readonly int _runtimeColumnIndex;
        private readonly HashSet<int> _injected = new HashSet<int>();
        private readonly InjectionRetryBackoff _injectionRetryBackoff =
            new InjectionRetryBackoff();
        private readonly Dictionary<int, string> _policyBlocks = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _vulkanBlocks = new Dictionary<int, string>();
        private readonly Dictionary<int, long> _nextVulkanProbe = new Dictionary<int, long>();
        private volatile bool _enabled;
        private volatile bool _targetAllowed;
        private volatile int _currentPid;
        private volatile string _currentRuntime;
        private bool _disposed;
        private bool _injectionInProgress;
        private bool _injectionSucceeded;
        private bool _hookFreeFallbackActive;
        private int _injectionStatusPid;
        private string _lastInjectionError;
        private string _targetBlockReason;
        private string _vulkanBlockReason;

        /// <param name="processIdStream">The detected-game PID stream (IProcessService.ProcessIdStream).</param>
        /// <param name="dllPathOverride">Optional explicit path to cfx_osd_hook.dll.</param>
        public HookOverlayManager(IAppConfiguration appConfiguration, IObservable<int> processIdStream,
            IObservable<string[]> frameDataStream, int processIdColumnIndex, int runtimeColumnIndex,
            string dllPathOverride = null, HookOverlayStatusService statusService = null)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            if (processIdStream == null) throw new ArgumentNullException(nameof(processIdStream));
            if (frameDataStream == null) throw new ArgumentNullException(nameof(frameDataStream));
            if (processIdColumnIndex < 0) throw new ArgumentOutOfRangeException(nameof(processIdColumnIndex));
            if (runtimeColumnIndex < 0) throw new ArgumentOutOfRangeException(nameof(runtimeColumnIndex));

            _processIdColumnIndex = processIdColumnIndex;
            _runtimeColumnIndex = runtimeColumnIndex;

            _dllPath = dllPathOverride ?? ResolveHookAsset(HookDllName, "CFX_HOOK_DLL");
            _dllPathX86 = ResolveHookAsset(Path.Combine("x86", HookDllName), "CFX_HOOK_DLL_X86");
            _injectHelperX86 = ResolveHookAsset(Path.Combine("x86", InjectHelperName), "CFX_INJECT_X86");
            _enabled = appConfiguration.EnableHookOverlay;
            _statusService = statusService ?? new HookOverlayStatusService();

            // Mirror the hook overlay's effective visibility to the in-game hook via a named event;
            // the hook reads it each present and skips drawing while it is reset. The hook must draw
            // only while it is BOTH enabled ("In-game hook overlay") AND toggled on (ALT+O =
            // IsOverlayActive), so unchecking the box hides the resident overlay LIVE — otherwise the
            // already-injected hook keeps drawing (it doesn't otherwise learn it was disabled).
            _visibility = HookVisibilityChannel.Create(
                _enabled && appConfiguration.IsOverlayActive && IsDxgiRuntime(_currentRuntime));

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

            _runtimeSub = frameDataStream.Subscribe(OnFrameRow);
            _statusTimer = new Timer(_ => PublishStatus(), null, 1000, 1000);
            PublishStatus();
        }

        /// <summary>
        /// Becomes active while the in-game renderer is selected but the detected presentation
        /// runtime is not supported by either the DXGI hook or the Vulkan layer. Consumers use
        /// this transient signal without changing the user's persisted renderer selection.
        /// </summary>
        public IObservable<bool> HookFreeFallbackStream =>
            _hookFreeFallbackStream.DistinctUntilChanged();

        private void OnEnabledChanged(bool enabled)
        {
            _enabled = enabled;
            UpdateHookVisibility(); // disabling hides the resident hook immediately (no game restart)
            UpdateHookFreeFallback();
            if (_enabled && IsDxgiRuntime(_currentRuntime))
            {
                int pid = _currentPid;
                if (pid > 0) TryInjectAsync(pid);
            }
            PublishStatus();
        }

        // The hook draws only while it is BOTH enabled and toggled on (ALT+O). Push that combined
        // state to the in-game hook through the named visibility event.
        private void UpdateHookVisibility()
        {
            _visibility.SetVisible(_enabled && _appConfiguration.IsOverlayActive &&
                _targetAllowed && IsDxgiRuntime(_currentRuntime));
        }

        private void OnProcessId(int pid)
        {
            int previousPid;
            lock (_stateGate)
            {
                previousPid = _currentPid;
                _currentPid = pid;
                _currentRuntime = null;
                _targetAllowed = false;
                _injectionStatusPid = pid;
                _injectionInProgress = false;
                _injectionSucceeded = false;
                _lastInjectionError = null;
                _targetBlockReason = null;
                _vulkanBlockReason = null;
            }
            if (previousPid > 0 && previousPid != pid)
            {
                HookTargetPolicy.Invalidate(previousPid);
                lock (_gate)
                {
                    _injectionRetryBackoff.Reset(previousPid);
                    _vulkanBlocks.Remove(previousPid);
                    _nextVulkanProbe.Remove(previousPid);
                }
            }
            lock (_gate)
            {
                // A newly selected process must never inherit retry state from a reused PID.
                _injectionRetryBackoff.Reset(pid);
            }
            UpdateHookVisibility();
            UpdateHookFreeFallback();
            if (pid <= 0)
            {
                // process deselected/exited: forget stale PIDs so a relaunch re-injects
                PruneExited();
                PublishStatus();
                return;
            }
            PublishStatus();
        }

        private void OnFrameRow(string[] row)
        {
            if (row == null || _processIdColumnIndex >= row.Length || _runtimeColumnIndex >= row.Length)
                return;
            if (!int.TryParse(row[_processIdColumnIndex], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int pid) || pid <= 0)
                return;

            string runtime = row[_runtimeColumnIndex]?.Trim();
            if (string.IsNullOrEmpty(runtime)) return;

            bool runtimeChanged;
            lock (_stateGate)
            {
                if (pid != _currentPid) return;
                runtimeChanged = !string.Equals(runtime, _currentRuntime,
                    StringComparison.OrdinalIgnoreCase);
                if (runtimeChanged) _currentRuntime = runtime;
            }
            if (runtimeChanged)
            {
                UpdateHookVisibility();
                UpdateHookFreeFallback();
                RefreshTargetPolicy(pid);
            }
            if (IsDxgiRuntime(runtime))
            {
                if (runtimeChanged)
                    Log.Information("HookOverlay: target PID {pid} runtime {runtime} -> DXGI hook", pid, runtime);
                if (_enabled) TryInjectAsync(pid);
            }
            else if (runtimeChanged)
            {
                Log.Information("HookOverlay: target PID {pid} runtime {runtime} -> no DXGI injection", pid, runtime);
            }
            if (runtimeChanged) PublishStatus();
        }

        internal static bool IsDxgiRuntime(string runtime)
            => string.Equals(runtime, "DXGI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(runtime, "D3D11", StringComparison.OrdinalIgnoreCase)
            || string.Equals(runtime, "D3D12", StringComparison.OrdinalIgnoreCase);

        internal static bool IsVulkanRuntime(string runtime)
            => string.Equals(runtime, "Vulkan", StringComparison.OrdinalIgnoreCase);

        internal static bool ShouldUseHookFreeFallback(bool hookEnabled, int processId,
            string runtime)
        {
            if (!hookEnabled || processId <= 0 || string.IsNullOrWhiteSpace(runtime) ||
                string.Equals(runtime, "<error>", StringComparison.OrdinalIgnoreCase))
                return false;

            return !IsDxgiRuntime(runtime) && !IsVulkanRuntime(runtime);
        }

        internal static HookOverlayStatus CreateHookFreeFallbackStatus(int processId,
            string runtime, bool visible)
        {
            return new HookOverlayStatus(
                visible ? EHookOverlayStatus.Fallback : EHookOverlayStatus.Hidden,
                processId, runtime,
                $"PID {processId}, {runtime}: unsupported by the in-game renderer; " +
                $"hook-free fallback is {(visible ? "active" : "hidden")}.");
        }

        private void UpdateHookFreeFallback()
        {
            lock (_fallbackGate)
            {
                if (_disposed) return;

                bool active;
                int pid;
                string runtime;
                lock (_stateGate)
                {
                    pid = _currentPid;
                    runtime = _currentRuntime;
                    active = ShouldUseHookFreeFallback(_enabled, pid, runtime);
                    if (active == _hookFreeFallbackActive) return;
                    _hookFreeFallbackActive = active;
                }

                Log.Information(
                    "HookOverlay: hook-free fallback {state} for pid {pid} (runtime {runtime})",
                    active ? "enabled" : "disabled", pid,
                    string.IsNullOrWhiteSpace(runtime) ? "unknown" : runtime);
                _hookFreeFallbackStream.OnNext(active);
            }
        }

        internal static bool ShouldUseVulkanStatus(string runtime, bool hasVulkanStatus,
            long vulkanHeartbeatAgeMs, bool hasDxgiStatus, bool dxgiTransitionStarted)
        {
            if (IsVulkanRuntime(runtime)) return true;
            if (!IsDxgiRuntime(runtime) || !hasVulkanStatus) return false;

            return vulkanHeartbeatAgeMs >= 0 &&
                ((ulong)vulkanHeartbeatAgeMs <= HookStatusProbe.HeartbeatStaleAfterMs ||
                 (!hasDxgiStatus && !dxgiTransitionStarted));
        }

        private void TryInjectAsync(int pid)
        {
            // Frame rows can arrive many times per second. Suppress all of the comparatively
            // expensive policy/probe work while a failed injection is in its retry window.
            if (IsInjectionRetryBlocked(pid)) return;
            if (!RefreshVulkanInjectionGate(pid)) return;
            if (!RefreshTargetPolicy(pid)) return;
            if (!TryReserveInjection(pid)) return;
            SetInjectionStatus(pid, inProgress: true, succeeded: false, error: null);

            Task.Run(() =>
            {
                try
                {
                    if (!_enabled || pid != _currentPid || !IsDxgiRuntime(_currentRuntime))
                    {
                        ReleaseInjectionReservation(pid);
                        return;
                    }
                    // The detection can briefly hold a stale PID; skip if the process is gone.
                    if (!IsProcessAlive(pid))
                    {
                        ReleaseInjectionReservation(pid);
                        return;
                    }

                    // Pick the path by the TARGET's bitness: x64 games get the x64 hook injected
                    // directly; 32-bit (WOW64) games get the x86 hook via the bitness-matched helper.
                    if (!HookInjector.TryGetIsWow64(pid, out bool isWow64, out string bitError))
                    {
                        TimeSpan retryDelay = RegisterInjectionFailure(pid, bitError);
                        Log.Warning(
                            "HookOverlay: cannot determine bitness of pid {pid} — {error}; retry in {retrySeconds:0.#} s",
                            pid, bitError, retryDelay.TotalSeconds);
                        return;
                    }

                    string arch = isWow64 ? "x86" : "x64";
                    string sourceDll = isWow64 ? _dllPathX86 : _dllPath;
                    if (string.IsNullOrEmpty(sourceDll) || !File.Exists(sourceDll))
                    {
                        string failure = $"{arch} hook DLL not found at '{sourceDll}'";
                        TimeSpan retryDelay = RegisterInjectionFailure(pid, failure);
                        Log.Warning(
                            "HookOverlay: cannot inject into {arch} pid {pid} — {dll} not found (looked at '{path}'); retry in {retrySeconds:0.#} s",
                            arch, pid, HookDllName, sourceDll, retryDelay.TotalSeconds);
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
                        TimeSpan retryDelay = RegisterInjectionFailure(pid,
                            $"could not prepare an injectable {arch} hook DLL");
                        Log.Warning(
                            "HookOverlay: could not prepare an injectable {arch} copy of {dll}; retry in {retrySeconds:0.#} s",
                            arch, HookDllName, retryDelay.TotalSeconds);
                        return;
                    }

                    string error;
                    if (!RefreshTargetPolicy(pid))
                    {
                        ReleaseInjectionReservation(pid);
                        return;
                    }
                    // Re-read immediately before LoadLibrary. The Vulkan mapping may have appeared
                    // after process/runtime detection but while the injectable copy was prepared.
                    if (!RefreshVulkanInjectionGate(pid, forceProbe: true))
                    {
                        ReleaseInjectionReservation(pid);
                        return;
                    }
                    bool ok = isWow64
                        ? HookInjector.TryInjectViaHelper(pid, injectable, _injectHelperX86, out error)
                        : HookInjector.TryInject(pid, injectable, out error);

                    if (ok)
                    {
                        RegisterInjectionSuccess(pid);
                        Log.Information("HookOverlay: injected {dll} ({arch}) into pid {pid}", HookDllName, arch, pid);
                    }
                    else
                    {
                        TimeSpan retryDelay = RegisterInjectionFailure(pid, error);
                        Log.Warning(
                            "HookOverlay: injection into {arch} pid {pid} failed — {error}; retry in {retrySeconds:0.#} s",
                            arch, pid, error, retryDelay.TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    TimeSpan retryDelay = RegisterInjectionFailure(pid, ex.Message);
                    Log.Error(ex,
                        "HookOverlay: unexpected error injecting into pid {pid}; retry in {retrySeconds:0.#} s",
                        pid, retryDelay.TotalSeconds);
                }
            });
        }

        private bool IsInjectionRetryBlocked(int pid)
        {
            lock (_gate)
                return _injectionRetryBackoff.IsBlocked(pid);
        }

        private bool TryReserveInjection(int pid)
        {
            lock (_gate)
            {
                // Re-check the backoff inside the reservation lock. An earlier attempt can
                // fail between the fast check above and this point.
                if (_injected.Contains(pid) || _injectionRetryBackoff.IsBlocked(pid))
                    return false;

                _injected.Add(pid);
                return true;
            }
        }

        private void ReleaseInjectionReservation(int pid)
        {
            lock (_gate)
                _injected.Remove(pid);
            SetInjectionStatus(pid, inProgress: false, succeeded: false, error: null);
        }

        private TimeSpan RegisterInjectionFailure(int pid, string error)
        {
            TimeSpan retryDelay;
            lock (_gate)
            {
                retryDelay = _injectionRetryBackoff.RecordFailure(pid);
                // Publish the retry deadline before releasing the reservation so a frame
                // cannot start another injection attempt in between.
                _injected.Remove(pid);
            }
            SetInjectionStatus(pid, inProgress: false, succeeded: false,
                error: string.IsNullOrWhiteSpace(error) ? "injection failed" : error);
            return retryDelay;
        }

        private void RegisterInjectionSuccess(int pid)
        {
            lock (_gate)
                _injectionRetryBackoff.Reset(pid);
            SetInjectionStatus(pid, inProgress: false, succeeded: true, error: null);
        }

        private void SetInjectionStatus(int pid, bool inProgress, bool succeeded, string error)
        {
            lock (_stateGate)
            {
                if (pid != _currentPid) return;
                _injectionStatusPid = pid;
                _injectionInProgress = inProgress;
                _injectionSucceeded = succeeded;
                _lastInjectionError = error;
            }
            PublishStatus();
        }

        private bool RefreshVulkanInjectionGate(int pid, bool forceProbe = false)
        {
            long now = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                if (!forceProbe && _vulkanBlocks.ContainsKey(pid) &&
                    _nextVulkanProbe.TryGetValue(pid, out long retryAt) && now < retryAt)
                    return false;
            }

            bool probeOk = VulkanActivityProbe.TryHasRecentPresent(
                pid, out bool recent, out string probeError);
            bool allowed = probeOk && !recent;
            string reason = !probeOk
                ? $"Vulkan activity probe failed ({probeError ?? "unknown error"})"
                : recent ? "recent Vulkan presents" : null;

            lock (_gate)
            {
                if (!allowed)
                {
                    bool changed = !_vulkanBlocks.TryGetValue(pid, out string previousReason) ||
                        !string.Equals(previousReason, reason, StringComparison.Ordinal);
                    _vulkanBlocks[pid] = reason;
                    _nextVulkanProbe[pid] = now + VulkanProbeRetryTicks;
                    if (changed)
                    {
                        Log.Information(
                            "HookOverlay: DXGI injection into pid {pid} suppressed ({reason})",
                            pid, reason);
                    }
                }
                else
                {
                    if (_vulkanBlocks.Remove(pid))
                    {
                        Log.Information(
                            "HookOverlay: Vulkan activity grace elapsed for pid {pid}; DXGI injection allowed",
                            pid);
                    }
                    _nextVulkanProbe.Remove(pid);
                }
            }

            bool reasonChanged = false;
            lock (_stateGate)
            {
                if (pid == _currentPid && !string.Equals(_vulkanBlockReason, reason,
                    StringComparison.Ordinal))
                {
                    _vulkanBlockReason = reason;
                    reasonChanged = true;
                }
            }
            if (reasonChanged) PublishStatus();

            return allowed;
        }

        private bool RefreshTargetPolicy(int pid)
        {
            string reason = null;
            bool allowed = pid == _currentPid && HookTargetPolicy.IsAllowed(pid, out reason);
            bool visibilityChanged = allowed != _targetAllowed;
            _targetAllowed = allowed;
            bool reasonChanged;
            lock (_stateGate)
            {
                string currentReason = allowed ? null : reason;
                reasonChanged = !string.Equals(_targetBlockReason, currentReason,
                    StringComparison.Ordinal);
                _targetBlockReason = currentReason;
            }

            lock (_gate)
            {
                if (!allowed)
                {
                    if (!_policyBlocks.TryGetValue(pid, out string previousReason) ||
                        !string.Equals(previousReason, reason, StringComparison.Ordinal))
                    {
                        _policyBlocks[pid] = reason;
                        Log.Warning("HookOverlay: target PID {pid} blocked by target policy ({reason})",
                            pid, reason ?? "unknown reason");
                    }
                }
                else if (_policyBlocks.Remove(pid))
                {
                    Log.Information("HookOverlay: target PID {pid} now passes the target policy", pid);
                }
            }

            if (visibilityChanged) UpdateHookVisibility();
            if (reasonChanged) PublishStatus();
            return allowed;
        }

        private void PublishStatus()
        {
            if (_disposed) return;
            try
            {
                int pid;
                int injectionPid;
                string runtime;
                string injectionError;
                string targetBlockReason;
                string vulkanBlockReason;
                bool targetAllowed;
                bool injectionInProgress;
                bool injectionSucceeded;
                bool hookFreeFallbackActive;
                lock (_stateGate)
                {
                    pid = _currentPid;
                    runtime = _currentRuntime;
                    targetAllowed = _targetAllowed;
                    injectionPid = _injectionStatusPid;
                    injectionInProgress = _injectionInProgress;
                    injectionSucceeded = _injectionSucceeded;
                    hookFreeFallbackActive = _hookFreeFallbackActive;
                    injectionError = _lastInjectionError;
                    targetBlockReason = _targetBlockReason;
                    vulkanBlockReason = _vulkanBlockReason;
                }

                HookOverlayStatus status;
                if (!_enabled)
                {
                    status = new HookOverlayStatus(EHookOverlayStatus.Disabled,
                        detail: "The in-game hook overlay is disabled.");
                }
                else if (pid <= 0)
                {
                    status = new HookOverlayStatus(EHookOverlayStatus.Waiting,
                        detail: "Waiting for a game process.");
                }
                else if (string.IsNullOrWhiteSpace(runtime))
                {
                    status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid,
                        detail: $"PID {pid}: waiting for the presentation runtime.");
                }
                else
                {
                    ulong nowTickMs = HookStatusProbe.CurrentTickCount;
                    bool hasDxgiStatus = HookStatusProbe.TryRead(pid,
                        out NativeHookStatusSnapshot native, out string dxgiProbeError);
                    bool vulkanProbeOk = VulkanActivityProbe.TryRead(pid,
                        out VulkanActivitySnapshot vulkan, out string vulkanProbeError);
                    bool hasVulkanStatus = vulkanProbeOk && vulkan.IsLayerLoaded &&
                        vulkan.LastVulkanPresentTickMs > 0;
                    long vulkanHeartbeatAge = hasVulkanStatus
                        ? HookStatusProbe.GetHeartbeatAgeMilliseconds(
                            vulkan.LastVulkanPresentTickMs, nowTickMs)
                        : -1;
                    bool dxgiTransitionStarted = injectionPid == pid &&
                        (injectionInProgress || injectionSucceeded ||
                         !string.IsNullOrEmpty(injectionError));
                    bool useVulkanStatus = ShouldUseVulkanStatus(runtime, hasVulkanStatus,
                        vulkanHeartbeatAge, hasDxgiStatus, dxgiTransitionStarted);

                    if (useVulkanStatus)
                    {
                        if (!targetAllowed)
                        {
                            string reason = string.IsNullOrEmpty(targetBlockReason)
                                ? "checking the target policy" : targetBlockReason;
                            status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid,
                                "Vulkan", $"PID {pid}, Vulkan: {reason}.");
                        }
                        else if (!vulkanProbeOk)
                        {
                            status = new HookOverlayStatus(EHookOverlayStatus.Error, pid,
                                "Vulkan", $"PID {pid}, Vulkan: layer status unavailable " +
                                $"({vulkanProbeError}).");
                        }
                        else
                        {
                            status = HookOverlayStatusEvaluator.EvaluateVulkan(pid, "Vulkan",
                                vulkan, nowTickMs, _appConfiguration.IsOverlayActive);
                        }
                    }
                    else if (hookFreeFallbackActive)
                    {
                        bool visible = _appConfiguration.IsOverlayActive;
                        status = CreateHookFreeFallbackStatus(pid, runtime, visible);
                    }
                    else if (!IsDxgiRuntime(runtime))
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, runtime,
                            $"PID {pid}, {runtime}: unsupported presentation runtime.");
                    }
                    else if (!string.IsNullOrEmpty(vulkanBlockReason))
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, runtime,
                            $"PID {pid}, {runtime}: DXGI injection suppressed ({vulkanBlockReason}).");
                    }
                    else if (!targetAllowed)
                    {
                        string reason = string.IsNullOrEmpty(targetBlockReason)
                            ? "checking the target policy" : targetBlockReason;
                        status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, runtime,
                            $"PID {pid}, {runtime}: {reason}.");
                    }
                    else if (hasDxgiStatus)
                    {
                        status = HookOverlayStatusEvaluator.EvaluateNative(pid, runtime, native,
                            nowTickMs);
                    }
                    else if (injectionPid == pid && injectionInProgress)
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Injecting, pid, runtime,
                            $"PID {pid}, {runtime}: injecting the in-game hook.");
                    }
                    else if (injectionPid == pid && !string.IsNullOrEmpty(injectionError))
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Error, pid, runtime,
                            $"PID {pid}, {runtime}: {injectionError}");
                    }
                    else if (injectionPid == pid && injectionSucceeded)
                    {
                        string detail = string.IsNullOrEmpty(dxgiProbeError)
                            ? $"PID {pid}, {runtime}: hook injected; waiting for native status and the first Present."
                            : $"PID {pid}, {runtime}: hook injected; native status unavailable ({dxgiProbeError}).";
                        status = new HookOverlayStatus(EHookOverlayStatus.Injected, pid, runtime,
                            detail);
                    }
                    else
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, runtime,
                            $"PID {pid}, {runtime}: waiting to inject the hook.");
                    }
                }

                _statusService.Publish(status);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "HookOverlay: failed to refresh the hook status");
            }
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
                _injectionRetryBackoff.Prune(IsProcessAlive);
                var stalePolicyPids = new List<int>();
                foreach (int pid in _policyBlocks.Keys)
                    if (!IsProcessAlive(pid)) stalePolicyPids.Add(pid);
                foreach (int pid in stalePolicyPids)
                    _policyBlocks.Remove(pid);
                var staleVulkanPids = new List<int>();
                foreach (int pid in _vulkanBlocks.Keys)
                    if (!IsProcessAlive(pid)) staleVulkanPids.Add(pid);
                foreach (int pid in staleVulkanPids)
                {
                    _vulkanBlocks.Remove(pid);
                    _nextVulkanProbe.Remove(pid);
                }
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
            lock (_fallbackGate)
            {
                if (_disposed) return;
                _disposed = true;
                _hookFreeFallbackStream.OnNext(false);
                _hookFreeFallbackStream.OnCompleted();
                _hookFreeFallbackStream.Dispose();
            }
            _statusTimer?.Dispose();
            _pidSub?.Dispose();
            _enabledSub?.Dispose();
            _visibilitySub?.Dispose();
            _runtimeSub?.Dispose();
            _visibility?.Dispose();
            _statusService.Publish(new HookOverlayStatus(EHookOverlayStatus.Disabled,
                detail: "The in-game hook overlay is disabled."));
            // The injected hook disables itself when it observes CapFrameX exiting (it polls
            // a SYNCHRONIZE handle to this process), so no explicit teardown signal is needed.
        }
    }
}
