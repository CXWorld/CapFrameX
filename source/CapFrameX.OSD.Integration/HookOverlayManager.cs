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
        internal const ulong HookHandshakeTimeoutMs = 3000;
        // The injected hook reports Present activity long before it can draw. If it never gets
        // past that stage the overlay would silently stay invisible forever, so bound it and let
        // the hook-free renderer take over instead.
        internal const ulong HookRendererReadyTimeoutMs = 10000;
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
        private readonly InjectionCompatibilityDelay _compatibilityDelay =
            new InjectionCompatibilityDelay();
        private readonly Dictionary<int, HookCompatibilityChannel> _compatibilityChannels =
            new Dictionary<int, HookCompatibilityChannel>();
        private readonly Dictionary<int, string> _policyBlocks = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _vulkanBlocks = new Dictionary<int, string>();
        private readonly Dictionary<int, long> _nextVulkanProbe = new Dictionary<int, long>();
        // Evaluated ONCE per process: neither the foreign overlay modules nor the process start
        // time can change while the process lives, so a re-scan could only produce log noise.
        private readonly Dictionary<int, string> _foreignOverlayBlocks = new Dictionary<int, string>();
        private volatile bool _enabled;
        private volatile bool _targetAllowed;
        private volatile int _currentPid;
        private volatile string _currentProcessName;
        private volatile string _currentRuntime;
        private volatile bool _disposed;
        private bool _injectionInProgress;
        private bool _injectionSucceeded;
        private bool _hookFreeFallbackActive;
        private ulong _injectionSucceededTickMs;
        private ulong _lastNativeStatusTickMs;
        private ulong _rendererInitializingSinceTickMs;
        private bool _rendererInitializationStalled;
        private bool _foreignPresenterDetected;
        private int _injectionStatusPid;
        private string _lastInjectionError;
        private string _nativeFallbackReason;
        private string _hookFreeFallbackReason;
        private string _targetBlockReason;
        private string _vulkanBlockReason;
        private string _foreignOverlayBlockReason;
        // When the USER switched the in-game hook on at runtime. A process that already existed at
        // that moment gets injected MID-SESSION, into a live device other overlays draw into.
        // MinValue while the hook was merely enabled by the stored configuration at startup:
        // CapFrameX starting next to an already running game is the normal case, not a renderer
        // switch, and must never be blocked — otherwise restarting CapFrameX disables the overlay
        // for every game that happens to be open.
        private DateTime _hookEnabledAtUtc = DateTime.MinValue;
        private ulong _injectionDelayUntilTickMs;
        private string _injectionDelayProfile;

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
        /// Becomes active while the in-game renderer is selected but native injection is blocked,
        /// fails, does not complete its status handshake, or the presentation runtime is unsupported.
        /// Consumers use this transient signal without changing the user's persisted selection.
        /// </summary>
        public IObservable<bool> HookFreeFallbackStream =>
            _hookFreeFallbackStream.DistinctUntilChanged();

        private void OnEnabledChanged(bool enabled)
        {
            if (enabled && !_enabled)
            {
                _hookEnabledAtUtc = DateTime.UtcNow;
                // A process blocked under the previous activation must be re-evaluated against
                // the new timestamp instead of inheriting a verdict from a past session.
                lock (_gate) _foreignOverlayBlocks.Clear();
                lock (_stateGate) _foreignOverlayBlockReason = null;
            }
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
            bool targetAllowed;
            bool fallbackActive;
            lock (_stateGate)
            {
                targetAllowed = _targetAllowed;
                fallbackActive = _hookFreeFallbackActive;
            }

            _visibility.SetVisible(_enabled && _appConfiguration.IsOverlayActive &&
                targetAllowed && !fallbackActive && IsDxgiRuntime(_currentRuntime));
        }

        private void OnProcessId(int pid)
        {
            int previousPid;
            string processName = ResolveProcessName(pid);
            lock (_stateGate)
            {
                previousPid = _currentPid;
                _currentPid = pid;
                _currentProcessName = processName;
                _currentRuntime = null;
                _targetAllowed = false;
                _injectionStatusPid = pid;
                _injectionInProgress = false;
                _injectionSucceeded = false;
                _injectionSucceededTickMs = 0;
                _lastNativeStatusTickMs = 0;
                _rendererInitializingSinceTickMs = 0;
                _rendererInitializationStalled = false;
                _foreignPresenterDetected = false;
                _lastInjectionError = null;
                _nativeFallbackReason = null;
                _hookFreeFallbackReason = null;
                _targetBlockReason = null;
                _vulkanBlockReason = null;
                _foreignOverlayBlockReason = null;
                _injectionDelayUntilTickMs = 0;
                _injectionDelayProfile = null;
            }
            if (previousPid > 0 && previousPid != pid)
            {
                HookTargetPolicy.Invalidate(previousPid);
                lock (_gate)
                {
                    _injectionRetryBackoff.Reset(previousPid);
                    _compatibilityDelay.Reset(previousPid);
                    _vulkanBlocks.Remove(previousPid);
                    _nextVulkanProbe.Remove(previousPid);
                    _foreignOverlayBlocks.Remove(previousPid);
                    VulkanLayerModuleProbe.Invalidate(previousPid);
                    DisposeCompatibilityChannelLocked(previousPid);
                }
            }
            // Forget stale PIDs so a relaunch re-injects, and so a reused PID cannot inherit the
            // "already injected" mark of the process that previously held it. Pruning by liveness
            // clears exactly that, and leaves a still running, merely re-selected process alone.
            PruneExited();
            lock (_gate)
            {
                // A newly selected process must never inherit retry or probe state from a
                // previously selected process whose PID has since been reused.
                _injectionRetryBackoff.Reset(pid);
                _compatibilityDelay.Reset(pid);
                _vulkanBlocks.Remove(pid);
                _nextVulkanProbe.Remove(pid);
                VulkanLayerModuleProbe.Invalidate(pid);
                DisposeCompatibilityChannelLocked(pid);
            }
            UpdateHookVisibility();
            UpdateHookFreeFallback();
            if (pid > 0)
                Log.Information("HookOverlay: target PID {pid} is '{process}'", pid, processName);
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
            string runtime, string targetBlockReason = null, string nativeFallbackReason = null,
            bool targetProcessAlive = true)
            => GetHookFreeFallbackReason(hookEnabled, processId, runtime,
                targetBlockReason, nativeFallbackReason, targetProcessAlive) != null;

        internal static string GetHookFreeFallbackReason(bool hookEnabled, int processId,
            string runtime, string targetBlockReason = null, string nativeFallbackReason = null,
            bool targetProcessAlive = true)
        {
            if (!hookEnabled || !targetProcessAlive || processId <= 0 ||
                string.IsNullOrWhiteSpace(runtime) ||
                string.Equals(runtime, "<error>", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!string.IsNullOrWhiteSpace(targetBlockReason))
                return $"in-game injection blocked ({targetBlockReason})";

            if (!string.IsNullOrWhiteSpace(nativeFallbackReason))
                return nativeFallbackReason;

            return !IsDxgiRuntime(runtime) && !IsVulkanRuntime(runtime)
                ? "unsupported by the in-game renderer"
                : null;
        }

        internal static HookOverlayStatus CreateHookFreeFallbackStatus(int processId,
            string runtime, bool visible, string fallbackReason = null)
        {
            string reason = string.IsNullOrWhiteSpace(fallbackReason)
                ? "unsupported by the in-game renderer"
                : fallbackReason;
            return new HookOverlayStatus(
                visible ? EHookOverlayStatus.Fallback : EHookOverlayStatus.Hidden,
                processId, runtime,
                $"PID {processId}, {runtime}: {reason}; " +
                $"hook-free fallback is {(visible ? "active" : "hidden")}.");
        }

        internal static bool HasHookStatusTimedOut(bool injectionSucceeded,
            ulong injectionSucceededTickMs, ulong lastNativeStatusTickMs,
            bool hasNativeStatus, ulong nowTickMs)
        {
            if (!injectionSucceeded || hasNativeStatus) return false;

            ulong referenceTickMs = lastNativeStatusTickMs > 0
                ? lastNativeStatusTickMs
                : injectionSucceededTickMs;
            if (referenceTickMs == 0 || nowTickMs < referenceTickMs) return false;

            return nowTickMs - referenceTickMs >= HookHandshakeTimeoutMs;
        }

        /// <summary>
        /// True once the injected hook has reported <see cref="EHookOverlayStatus.Initializing"/>
        /// uninterruptedly for longer than <see cref="HookRendererReadyTimeoutMs"/>. Present
        /// activity is live at that point, so the renderer is not going to come up — typically
        /// because the hook landed in a process whose swapchain another renderer owns.
        /// </summary>
        internal static bool HasRendererInitializationStalled(ulong initializingSinceTickMs,
            ulong nowTickMs)
        {
            if (initializingSinceTickMs == 0 || nowTickMs < initializingSinceTickMs)
                return false;

            return nowTickMs - initializingSinceTickMs >= HookRendererReadyTimeoutMs;
        }

        private void UpdateHookFreeFallback()
        {
            lock (_fallbackGate)
            {
                if (_disposed) return;

                bool active;
                int pid;
                string runtime;
                string reason;
                bool activeChanged;
                bool reasonChanged;
                lock (_stateGate)
                {
                    pid = _currentPid;
                    runtime = _currentRuntime;
                    reason = GetHookFreeFallbackReason(_enabled, pid, runtime,
                        _targetBlockReason, _nativeFallbackReason,
                        targetProcessAlive: pid > 0 && IsProcessAlive(pid));
                    active = reason != null;
                    activeChanged = active != _hookFreeFallbackActive;
                    reasonChanged = !string.Equals(reason, _hookFreeFallbackReason,
                        StringComparison.Ordinal);
                    if (!activeChanged && !reasonChanged) return;
                    _hookFreeFallbackActive = active;
                    _hookFreeFallbackReason = reason;
                }

                Log.Information(
                    "HookOverlay: hook-free fallback {state} for pid {pid} (runtime {runtime}, reason {reason})",
                    active ? "enabled" : "disabled", pid,
                    string.IsNullOrWhiteSpace(runtime) ? "unknown" : runtime,
                    string.IsNullOrWhiteSpace(reason) ? "none" : reason);
                if (activeChanged) _hookFreeFallbackStream.OnNext(active);
                UpdateHookVisibility();
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
            // The target policy gates the resident hook's visibility as well as injection, so it
            // stays current for the whole session — an anti-cheat module that appears later must
            // still hide the overlay. Its own cache keeps the per-frame cost at a timestamp
            // compare. Everything below only serves injection and is dead once that is settled.
            if (!RefreshTargetPolicy(pid)) return;
            if (IsInjectionAttemptBlocked(pid)) return;
            if (!RefreshVulkanInjectionGate(pid)) return;
            if (!RefreshForeignOverlayGate(pid)) return;
            HookCompatibilityProfileCatalog.TryGetForProcess(pid,
                out HookCompatibilityProfile compatibilityProfile);
            if (!TryReserveInjection(pid)) return;
            TimeSpan compatibilityDelay;
            lock (_gate)
            {
                compatibilityDelay = _compatibilityDelay.GetRemainingDelay(pid,
                    compatibilityProfile?.InjectionDelay ?? TimeSpan.Zero);
            }
            SetInjectionStatus(pid, inProgress: true, succeeded: false, error: null);
            SetInjectionDelayStatus(pid, compatibilityProfile, compatibilityDelay);

            Task.Run(async () =>
            {
                try
                {
                    if (!IsInjectionStillEligible(pid))
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

                    if (compatibilityDelay > TimeSpan.Zero)
                    {
                        Log.Information(
                            "HookOverlay: compatibility profile {profile} delays injection into pid {pid} by {delaySeconds:0.#} s",
                            compatibilityProfile.ExecutableName, pid,
                            compatibilityDelay.TotalSeconds);
                        await Task.Delay(compatibilityDelay).ConfigureAwait(false);
                        ClearInjectionDelayStatus(pid);
                        if (!IsInjectionStillEligible(pid) || !IsProcessAlive(pid))
                        {
                            ReleaseInjectionReservation(pid);
                            return;
                        }
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
                    // Re-read immediately before LoadLibrary. Vulkan may have claimed the process
                    // after the runtime was detected but while the injectable copy was prepared.
                    if (!RefreshVulkanInjectionGate(pid, forceProbe: true))
                    {
                        ReleaseInjectionReservation(pid);
                        return;
                    }
                    if (!IsInjectionStillEligible(pid))
                    {
                        ReleaseInjectionReservation(pid);
                        return;
                    }
                    if (!TryPublishCompatibilityProfile(pid, compatibilityProfile,
                        out string compatibilityError))
                    {
                        TimeSpan retryDelay = RegisterInjectionFailure(pid,
                            compatibilityError);
                        Log.Warning(
                            "HookOverlay: compatibility configuration for pid {pid} failed — {error}; retry in {retrySeconds:0.#} s",
                            pid, compatibilityError, retryDelay.TotalSeconds);
                        return;
                    }
                    if (!IsInjectionStillEligible(pid))
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

        private bool IsInjectionStillEligible(int pid)
        {
            return !_disposed && _enabled && pid == _currentPid &&
                IsDxgiRuntime(_currentRuntime);
        }

        private void SetInjectionDelayStatus(int pid, HookCompatibilityProfile profile,
            TimeSpan delay)
        {
            if (profile == null || delay <= TimeSpan.Zero) return;
            ulong delayMs = (ulong)Math.Ceiling(delay.TotalMilliseconds);
            ulong now = HookStatusProbe.CurrentTickCount;
            lock (_stateGate)
            {
                if (pid != _currentPid) return;
                _injectionDelayUntilTickMs = now > ulong.MaxValue - delayMs
                    ? ulong.MaxValue
                    : now + delayMs;
                _injectionDelayProfile = profile.ExecutableName;
            }
            PublishStatus();
        }

        private void ClearInjectionDelayStatus(int pid)
        {
            lock (_stateGate)
            {
                if (pid != _currentPid) return;
                _injectionDelayUntilTickMs = 0;
                _injectionDelayProfile = null;
            }
            PublishStatus();
        }

        private bool TryPublishCompatibilityProfile(int pid,
            HookCompatibilityProfile profile, out string error)
        {
            error = null;
            if (_disposed)
            {
                error = "hook overlay manager is disposed";
                return false;
            }
            NativeHookCompatibilityFlags flags = profile?.NativeFlags ??
                NativeHookCompatibilityFlags.None;
            if (flags == NativeHookCompatibilityFlags.None) return true;

            lock (_gate)
            {
                if (_disposed)
                {
                    error = "hook overlay manager is disposed";
                    return false;
                }
                if (_compatibilityChannels.ContainsKey(pid)) return true;
                if (!HookCompatibilityChannel.TryCreate(pid, flags,
                    out HookCompatibilityChannel channel, out error))
                {
                    error = $"could not publish native compatibility flags ({error})";
                    return false;
                }

                _compatibilityChannels.Add(pid, channel);
            }

            Log.Information(
                "HookOverlay: compatibility profile {profile} published flags {flags} for pid {pid}",
                profile.ExecutableName, flags, pid);
            return true;
        }

        private void DisposeCompatibilityChannelLocked(int pid)
        {
            if (!_compatibilityChannels.TryGetValue(pid,
                out HookCompatibilityChannel channel))
                return;
            _compatibilityChannels.Remove(pid);
            channel.Dispose();
        }

        private bool IsInjectionAttemptBlocked(int pid)
        {
            lock (_gate)
                return _injected.Contains(pid) || _injectionRetryBackoff.IsBlocked(pid);
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
                if (succeeded)
                {
                    _injectionSucceededTickMs = HookStatusProbe.CurrentTickCount;
                    _lastNativeStatusTickMs = 0;
                }
                else if (!inProgress)
                {
                    _injectionSucceededTickMs = 0;
                }
                if (!inProgress)
                {
                    _injectionDelayUntilTickMs = 0;
                    _injectionDelayProfile = null;
                }

                if (!string.IsNullOrWhiteSpace(error))
                    _nativeFallbackReason = $"in-game hook injection failed ({error})";
            }
            UpdateHookFreeFallback();
            PublishStatus();
        }

        private void UpdateNativeStatusFallback(int pid, bool hasNativeStatus,
            EHookOverlayStatus? nativeState, ulong nowTickMs, bool foreignPresenter)
        {
            bool changed = false;
            bool fallbackEnabled = false;
            string reason = null;
            lock (_stateGate)
            {
                if (pid != _currentPid) return;

                if (hasNativeStatus)
                {
                    _lastNativeStatusTickMs = nowTickMs;
                    // A frame-generation runtime (FSR FG / DLSS-G / XeSS-FG) wraps this game's
                    // swapchain; the hook stands down by design. Latched so an in-game FG toggle
                    // cannot flip the OSD path back and forth mid-session.
                    if (foreignPresenter)
                        _foreignPresenterDetected = true;

                    // The hook reports Present activity long before it can draw. Track how long
                    // it stays in that stage; once it has clearly stalled, latch the verdict so
                    // hiding the hook for the fallback cannot flip the state back and forth.
                    if (!_rendererInitializationStalled && !_foreignPresenterDetected)
                    {
                        if (nativeState == EHookOverlayStatus.Initializing)
                        {
                            if (_rendererInitializingSinceTickMs == 0)
                                _rendererInitializingSinceTickMs = nowTickMs;
                            _rendererInitializationStalled = HasRendererInitializationStalled(
                                _rendererInitializingSinceTickMs, nowTickMs);
                        }
                        else
                        {
                            _rendererInitializingSinceTickMs = 0;
                        }
                    }

                    if (_foreignPresenterDetected)
                    {
                        reason = "a frame-generation runtime (FSR FG / DLSS FG / XeSS FG) is " +
                            "presenting this game; the in-game overlay stands down";
                        if (!string.Equals(reason, _nativeFallbackReason,
                            StringComparison.Ordinal))
                        {
                            _nativeFallbackReason = reason;
                            changed = true;
                            fallbackEnabled = true;
                        }
                    }
                    else if (_rendererInitializationStalled)
                    {
                        reason = "the in-game renderer did not initialize within " +
                            $"{HookRendererReadyTimeoutMs / 1000} seconds";
                        if (!string.Equals(reason, _nativeFallbackReason,
                            StringComparison.Ordinal))
                        {
                            _nativeFallbackReason = reason;
                            changed = true;
                            fallbackEnabled = true;
                        }
                    }
                    else if (!string.IsNullOrEmpty(_nativeFallbackReason))
                    {
                        _nativeFallbackReason = null;
                        changed = true;
                    }
                }
                else if (HasHookStatusTimedOut(_injectionSucceeded,
                    _injectionSucceededTickMs, _lastNativeStatusTickMs,
                    hasNativeStatus: false, nowTickMs))
                {
                    reason = _lastNativeStatusTickMs > 0
                        ? $"native hook status was unavailable for more than {HookHandshakeTimeoutMs / 1000} seconds"
                        : $"native hook did not publish status within {HookHandshakeTimeoutMs / 1000} seconds after injection";
                    if (!string.Equals(reason, _nativeFallbackReason, StringComparison.Ordinal))
                    {
                        _nativeFallbackReason = reason;
                        changed = true;
                        fallbackEnabled = true;
                    }
                }
            }

            if (!changed) return;

            if (fallbackEnabled)
            {
                Log.Warning(
                    "HookOverlay: in-game renderer unusable for pid {pid} ('{process}', {reason}); enabling hook-free fallback",
                    pid, _currentProcessName ?? "unknown", reason);
            }
            else
            {
                Log.Information(
                    "HookOverlay: native status available for pid {pid}; clearing native-failure fallback",
                    pid);
            }
            UpdateHookFreeFallback();
        }

        private bool RefreshVulkanInjectionGate(int pid, bool forceProbe = false)
        {
            long now = Stopwatch.GetTimestamp();
            bool firstEvaluation;
            lock (_gate)
            {
                firstEvaluation = !_nextVulkanProbe.ContainsKey(pid);
                if (!forceProbe &&
                    _nextVulkanProbe.TryGetValue(pid, out long retryAt) && now < retryAt)
                    return !_vulkanBlocks.ContainsKey(pid);
            }

            bool probeOk = VulkanActivityProbe.TryHasRecentPresent(
                pid, out bool recent, out bool yieldedToDxgi, out string probeError);

            // A recent Vulkan present is the strongest signal, but the renderer-state mapping it
            // is read from only exists once the layer has presented — PresentMon reports a Vulkan
            // swapchain as DXGI well before that, so relying on it alone races the very first
            // frames and injects the DXGI hook into a Vulkan title. The loaded layer module
            // settles that deterministically: the Vulkan loader maps it during vkCreateInstance.
            // A layer that has permanently yielded (PreferDxgi) is exempt — that is the
            // documented fail-open path where DXGI is supposed to take over.
            string layerError = null;
            VulkanLayerPresence layerPresence = probeOk && !recent && !yieldedToDxgi
                ? VulkanLayerModuleProbe.GetPresence(pid, out layerError, forceRescan: forceProbe)
                : VulkanLayerPresence.Absent;
            bool layerLoaded = layerPresence == VulkanLayerPresence.Loaded;
            // An unreadable module list is not evidence of absence. Injection is irreversible
            // and a wrong "no Vulkan" costs the overlay entirely, while waiting costs one more
            // probe interval — so an inconclusive scan holds the hook back.
            bool layerUnknown = layerPresence == VulkanLayerPresence.Unknown;

            bool allowed = IsDxgiInjectionAllowed(probeOk, recent, layerLoaded, layerUnknown);
            string reason = !probeOk
                ? $"Vulkan activity probe failed ({probeError ?? "unknown error"})"
                : recent
                    ? "recent Vulkan presents"
                    : layerLoaded
                        ? "the CapFrameX Vulkan layer renders this process"
                        : layerUnknown
                            ? $"Vulkan layer check inconclusive ({layerError ?? "unknown error"})"
                            : null;

            if (firstEvaluation)
            {
                // The decision to permit injection is irreversible and was previously invisible:
                // only blocks were logged, so a wrong "allow" left no trace at all. Report the
                // module scan as skipped rather than as "Absent" when a stronger signal already
                // settled the decision — otherwise the line reads like a missing layer.
                bool layerChecked = probeOk && !recent && !yieldedToDxgi;
                Log.Information(
                    "HookOverlay: Vulkan gate for pid {pid} ('{process}') — probeOk {probeOk}, " +
                    "recentPresent {recent}, yieldedToDxgi {yielded}, layer {layer}; " +
                    "DXGI injection {decision}",
                    pid, _currentProcessName ?? "unknown", probeOk, recent, yieldedToDxgi,
                    layerChecked ? layerPresence.ToString() : "not checked",
                    allowed ? "allowed" : "suppressed");
            }

            lock (_gate)
            {
                // Cache both outcomes briefly. A missing map is the normal DXGI case and frame
                // rows can arrive many times per second. The forceProbe immediately before
                // LoadLibrary still bypasses this cache and closes the Vulkan activation race.
                _nextVulkanProbe[pid] = now + VulkanProbeRetryTicks;
                if (!allowed)
                {
                    bool changed = !_vulkanBlocks.TryGetValue(pid, out string previousReason) ||
                        !string.Equals(previousReason, reason, StringComparison.Ordinal);
                    _vulkanBlocks[pid] = reason;
                    if (changed)
                    {
                        Log.Information(
                            "HookOverlay: DXGI injection into pid {pid} ('{process}') suppressed ({reason})",
                            pid, _currentProcessName ?? "unknown", reason);
                    }
                }
                else
                {
                    if (_vulkanBlocks.Remove(pid))
                    {
                        Log.Information(
                            "HookOverlay: Vulkan no longer renders pid {pid}; DXGI injection allowed",
                            pid);
                    }
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

        internal static bool IsDxgiInjectionAllowed(bool probeSucceeded,
            bool hasRecentVulkanPresent, bool vulkanLayerLoaded,
            bool vulkanLayerCheckInconclusive = false)
            => probeSucceeded && !hasRecentVulkanPresent && !vulkanLayerLoaded &&
               !vulkanLayerCheckInconclusive;

        /// <summary>
        /// Refuses injection into a process that was ALREADY RUNNING when the in-game hook was
        /// switched on AND already carries another overlay's present hook.
        /// </summary>
        /// <remarks>
        /// Injecting into a live D3D12 device that other overlays are drawing into crashed
        /// LEGO Batman (UE 5.6, AMD) with a GPU device removal one second after the first publish,
        /// while the identical switch had succeeded 25 minutes earlier — it is a race, not a fixed
        /// incompatibility. Neither condition alone is a usable signal: RTSS is loaded in every
        /// game while it runs and the Steam overlay in every Steam title, so blocking on the
        /// modules alone would disable the in-game hook nearly everywhere; and a mid-session
        /// injection without foreign hooks has not been observed to fail. Requiring BOTH keeps the
        /// normal case (game started with the hook already enabled) completely untouched.
        /// </remarks>
        private bool RefreshForeignOverlayGate(int pid)
        {
            lock (_gate)
            {
                if (_foreignOverlayBlocks.TryGetValue(pid, out string cached))
                    return cached == null;
            }

            bool startTimeKnown = TryIsMidSessionTarget(pid, out bool midSession);
            string[] modules = Array.Empty<string>();
            bool moduleScanOk = false;
            if (startTimeKnown && midSession)
                moduleScanOk = HookTargetPolicy.TryGetForeignOverlayModules(pid, out modules, out _);

            string reason = IsForeignOverlayInjectionBlocked(startTimeKnown, midSession,
                moduleScanOk, modules.Length)
                ? $"the game was already running when the in-game overlay was enabled and " +
                  $"another overlay is hooked into it ({string.Join(", ", modules)}); " +
                  $"restart the game to use the in-game overlay"
                : null;

            bool firstEvaluation;
            lock (_gate)
            {
                firstEvaluation = !_foreignOverlayBlocks.ContainsKey(pid);
                _foreignOverlayBlocks[pid] = reason;
            }

            if (firstEvaluation)
            {
                if (reason != null)
                    Log.Warning("HookOverlay: injection into pid {pid} ('{process}') blocked — {reason}",
                        pid, _currentProcessName ?? "unknown", reason);
                else
                    Log.Information("HookOverlay: foreign-overlay gate for pid {pid} ('{process}') — passed",
                        pid, _currentProcessName ?? "unknown");
            }

            bool reasonChanged = false;
            lock (_stateGate)
            {
                if (pid == _currentPid &&
                    !string.Equals(_foreignOverlayBlockReason, reason, StringComparison.Ordinal))
                {
                    _foreignOverlayBlockReason = reason;
                    reasonChanged = true;
                }
            }
            if (reasonChanged) PublishStatus();

            return reason == null;
        }

        /// <summary>
        /// The gate's decision, isolated from process and module lookups so it can be tested.
        /// EVERY condition must hold — each unknown falls open, because a wrongly withheld
        /// injection costs the overlay outright while the crash it guards against is a race that
        /// needs the mid-session + foreign-hook combination to occur at all.
        /// </summary>
        internal static bool IsForeignOverlayInjectionBlocked(bool startTimeKnown, bool midSession,
            bool moduleScanOk, int foreignModuleCount)
            => startTimeKnown && midSession && moduleScanOk && foreignModuleCount > 0;

        /// <summary>
        /// Whether injecting into a process started at <paramref name="processStartUtc"/> would be a
        /// MID-SESSION injection. Only a runtime switch counts: while <paramref name="hookEnabledAtUtc"/>
        /// is <see cref="DateTime.MinValue"/> the hook came from the stored configuration at startup,
        /// and a game that was already open then is the normal case — not a renderer switch.
        /// </summary>
        internal static bool IsMidSession(DateTime processStartUtc, DateTime hookEnabledAtUtc)
            => hookEnabledAtUtc != DateTime.MinValue && processStartUtc < hookEnabledAtUtc;

        /// <summary>
        /// True when <paramref name="pid"/> already existed when the hook overlay was enabled.
        /// Returns false when the start time is unreadable — an unknown age must not cost the
        /// overlay, so the gate falls open and behaves exactly as before.
        /// </summary>
        private bool TryIsMidSessionTarget(int pid, out bool midSession)
        {
            midSession = false;
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    midSession = IsMidSession(process.StartTime.ToUniversalTime(), _hookEnabledAtUtc);
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                Log.Debug("HookOverlay: could not read the start time of pid {pid} ({message})",
                    pid, ex.Message);
                return false;
            }
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
            if (visibilityChanged || reasonChanged) UpdateHookFreeFallback();
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
                string hookFreeFallbackReason;
                string targetBlockReason;
                string vulkanBlockReason;
                string foreignOverlayBlockReason;
                string injectionDelayProfile;
                bool targetAllowed;
                bool injectionInProgress;
                bool injectionSucceeded;
                bool hookFreeFallbackActive;
                ulong injectionDelayUntilTickMs;
                lock (_stateGate)
                {
                    pid = _currentPid;
                    runtime = _currentRuntime;
                    targetAllowed = _targetAllowed;
                    injectionPid = _injectionStatusPid;
                    injectionInProgress = _injectionInProgress;
                    injectionSucceeded = _injectionSucceeded;
                    hookFreeFallbackActive = _hookFreeFallbackActive;
                    hookFreeFallbackReason = _hookFreeFallbackReason;
                    injectionError = _lastInjectionError;
                    targetBlockReason = _targetBlockReason;
                    vulkanBlockReason = _vulkanBlockReason;
                    foreignOverlayBlockReason = _foreignOverlayBlockReason;
                    injectionDelayUntilTickMs = _injectionDelayUntilTickMs;
                    injectionDelayProfile = _injectionDelayProfile;
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
                    HookOverlayStatus nativeStatus = hasDxgiStatus
                        ? HookOverlayStatusEvaluator.EvaluateNative(pid, runtime, native,
                            nowTickMs)
                        : null;
                    UpdateNativeStatusFallback(pid, hasDxgiStatus, nativeStatus?.State,
                        nowTickMs, hasDxgiStatus &&
                        (native.Flags & NativeHookStatusFlags.ForeignPresenter) != 0);
                    lock (_stateGate)
                    {
                        hookFreeFallbackActive = _hookFreeFallbackActive;
                        hookFreeFallbackReason = _hookFreeFallbackReason;
                    }
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

                    if (hookFreeFallbackActive)
                    {
                        bool visible = _appConfiguration.IsOverlayActive;
                        status = CreateHookFreeFallbackStatus(pid, runtime, visible,
                            hookFreeFallbackReason);
                    }
                    else if (useVulkanStatus)
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
                    else if (!IsDxgiRuntime(runtime))
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, runtime,
                            $"PID {pid}, {runtime}: unsupported presentation runtime.");
                    }
                    // Ahead of the Vulkan suppression on purpose: that one resolves by itself once
                    // the probe settles, this one never does without a game restart.
                    else if (!string.IsNullOrEmpty(foreignOverlayBlockReason))
                    {
                        status = new HookOverlayStatus(EHookOverlayStatus.Blocked, pid, runtime,
                            $"PID {pid}, {runtime}: injection blocked — {foreignOverlayBlockReason}.");
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
                        status = nativeStatus;
                    }
                    else if (injectionPid == pid && injectionInProgress &&
                        injectionDelayUntilTickMs > nowTickMs)
                    {
                        ulong remainingMs = injectionDelayUntilTickMs - nowTickMs;
                        double remainingSeconds = Math.Ceiling(remainingMs / 1000.0);
                        status = new HookOverlayStatus(EHookOverlayStatus.Waiting, pid, runtime,
                            $"PID {pid}, {runtime}: compatibility profile " +
                            $"{injectionDelayProfile} delays injection for " +
                            $"{remainingSeconds:0} more seconds.");
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
                _compatibilityDelay.Prune(IsProcessAlive);
                VulkanLayerModuleProbe.Prune(IsProcessAlive);
                var stalePolicyPids = new List<int>();
                foreach (int pid in _policyBlocks.Keys)
                    if (!IsProcessAlive(pid)) stalePolicyPids.Add(pid);
                foreach (int pid in stalePolicyPids)
                    _policyBlocks.Remove(pid);
                var staleVulkanPids = new List<int>();
                foreach (int pid in _vulkanBlocks.Keys)
                    if (!IsProcessAlive(pid)) staleVulkanPids.Add(pid);
                foreach (int pid in staleVulkanPids)
                    _vulkanBlocks.Remove(pid);
                var staleVulkanProbePids = new List<int>();
                foreach (int pid in _nextVulkanProbe.Keys)
                    if (!IsProcessAlive(pid)) staleVulkanProbePids.Add(pid);
                foreach (int pid in staleVulkanProbePids)
                    _nextVulkanProbe.Remove(pid);
                var staleCompatibilityPids = new List<int>();
                foreach (int pid in _compatibilityChannels.Keys)
                    if (!IsProcessAlive(pid)) staleCompatibilityPids.Add(pid);
                foreach (int pid in staleCompatibilityPids)
                    DisposeCompatibilityChannelLocked(pid);
            }
        }

        // Every diagnostic here is keyed by PID alone, which cannot answer the first question an
        // unexpected hook target raises: which process is this? Resolved once per selection.
        private static string ResolveProcessName(int pid)
        {
            if (pid <= 0) return "none";
            try
            {
                using (var process = Process.GetProcessById(pid))
                    return process.ProcessName;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                return "unknown";
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
            // A query failure is not proof that the target exited. Preserve the normal
            // fallback behavior unless Windows conclusively reports that the PID is gone.
            catch (System.ComponentModel.Win32Exception) { return true; }
            catch (NotSupportedException) { return true; }
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
            lock (_gate)
            {
                foreach (HookCompatibilityChannel channel in _compatibilityChannels.Values)
                    channel.Dispose();
                _compatibilityChannels.Clear();
            }
            _statusService.Publish(new HookOverlayStatus(EHookOverlayStatus.Disabled,
                detail: "The in-game hook overlay is disabled."));
            // The injected hook disables itself when it observes CapFrameX exiting (it polls
            // a SYNCHRONIZE handle to this process), so no explicit teardown signal is needed.
        }
    }
}
