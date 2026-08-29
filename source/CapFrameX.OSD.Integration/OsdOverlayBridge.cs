using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Interop;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Drives the hook-free DWM/DirectComposition overlay from CapFrameX's existing data,
    /// as an alternative to RTSS:
    ///  - entries come from <c>IOverlayService.CurrentOverlayEntries</c> — the processed,
    ///    SORTED, value-populated display list (the same one RTSS renders); the raw
    ///    OnDictionaryUpdated payload is only used as the per-tick trigger,
    ///  - per-present frametimes and display times (MsBetweenDisplayChange) from the
    ///    capture service's frame-data stream → batched PushSample,
    ///  - runs when the overlay is active AND either the user enabled the hook-free overlay
    ///    (<c>IAppConfiguration.EnableHookFreeOverlay</c>) or the in-game renderer requested a
    ///    transient fallback for an unsupported runtime; RTSS is gated off in OverlayService
    ///    for both hook modes, so the renderers never overlap.
    /// </summary>
    public sealed class OsdOverlayBridge : IDisposable
    {
        private readonly IOverlayService _overlayService;
        private readonly OsdHost _osd;
        private readonly IDisposable _activeSub;
        private readonly IDisposable _entriesSub;
        private IDisposable _frameSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _fallbackSub;
        private readonly IDisposable _valueSmoothingSub;
        private readonly IDisposable _replayBufferSub;
        private readonly IDisposable _hookFreeRefreshRateSub;
        private readonly IDisposable _displaySub;
        private readonly IDisposable _backgroundOpacitySub;
        private readonly IDisposable _zoomSub;
        private readonly int _ftIndex;
        private readonly int _runtimeIndex;
        private readonly int _displayChangedIndex;
        private readonly IObservable<string[]> _frameDataStream;
        private readonly object _frameSubscriptionLock = new object();
        // StartTimeInMs (CPUStartQPCTimeInMs) sits AFTER the optional PC-latency column, so
        // its index is layout-dependent — resolve it lazily instead of caching a stale int.
        private readonly Func<int> _startTimeIndexProvider;
        private volatile bool _active;
        private volatile bool _enabled;
        private volatile bool _fallbackEnabled;
        private volatile bool _started;

        // The PresentMon row stream can run hundreds or thousands of times per second. Keep the
        // bridge completely out of that path unless the currently visible layout actually needs
        // one of its values. In particular, a profile without charts must not keep filling the
        // native replay queue merely because Frametime is available in the capture schema.
        private const int NeedFramerateValue = 1 << 0;
        private const int NeedFrametimeValue = 1 << 1;
        private const int NeedDisplayTimeValue = 1 << 2;
        private const int NeedFrametimeGraph = 1 << 3;
        private const int NeedDisplayTimeGraph = 1 << 4;
        private const int NeedRuntimeLabel = 1 << 5;
        private const int NeedFrametimeScalar = NeedFramerateValue | NeedFrametimeValue;
        private int _frameFeedRequirements;

        // Current <APP> framerate/frametime derived from the PresentMon frame-data stream
        // (RTSS resolves these in the classic path; hook-free we compute them ourselves).
        private const double FpsWindowMs = 1000.0;   // ~1s sliding window
        private readonly object _fpsLock = new object();
        private readonly Queue<double> _ftWindow = new Queue<double>();
        private double _ftWindowSumMs;
        private double _curFps;
        private double _curFrametimeMs;
        // Current display time (MsBetweenDisplayChange mean over the same ~1s window) for
        // the hook-free-only "Displaytime" entry; only displayed frames contribute.
        private readonly Queue<double> _dtWindow = new Queue<double>();
        private double _dtWindowSumMs;
        private double _curDisplayTimeMs;
        // Presenting app's graphics runtime/API (PresentMon "PresentRuntime", e.g. "DXGI") —
        // used to label the <APP> line; RTSS reads this from the 3D API, we from PresentMon.
        private volatile string _curRuntime;

        // Last background opacity forwarded to the OSD (percent); -1 forces the first push.
        private readonly IAppConfiguration _appConfiguration;
        private int _lastBgOpacity = -1;
        // Last zoom forwarded to the OSD (percent); -1 forces the first push.
        private int _lastZoom = -1;
        // Last placement forwarded; -1 forces the first push after a (re)start.
        private int _lastAnchor = -1, _lastMonitor = -1, _lastMarginX = -1, _lastMarginY = -1;
        private readonly object _positionLock = new object();

        public OsdOverlayBridge(IOverlayService overlayService,
                                IAppConfiguration appConfiguration,
                                IObservable<string[]> frameDataStream = null,
                                int frametimeColumnIndex = -1,
                                int presentRuntimeColumnIndex = -1,
                                int displayChangedColumnIndex = -1,
                                Func<int> startTimeIndexProvider = null,
                                IObservable<bool> hookFreeFallbackStream = null)
        {
            if (overlayService == null) throw new ArgumentNullException(nameof(overlayService));
            if (appConfiguration == null) throw new ArgumentNullException(nameof(appConfiguration));

            _overlayService = overlayService;
            _appConfiguration = appConfiguration;
            // Seeded from the configuration so the very first frame is already placed correctly;
            // OnEntries keeps it in sync from there.
            _osd = new OsdHost((OsdAnchor)appConfiguration.OsdAnchor,
                marginX: appConfiguration.OsdMarginX, marginY: appConfiguration.OsdMarginY,
                monitor: DisplayMonitorResolver.GetMonitorIndex(
                    appConfiguration.HookFreeDisplayDeviceName));
            _ftIndex = frametimeColumnIndex;
            _runtimeIndex = presentRuntimeColumnIndex;
            _displayChangedIndex = displayChangedColumnIndex;
            _frameDataStream = frameDataStream;
            _startTimeIndexProvider = startTimeIndexProvider;
            _enabled = appConfiguration.EnableHookFreeOverlay;
            _osd.SetValueSmoothing(appConfiguration.UseOsdValueSmoothing);
            _osd.SetReplayBuffer(appConfiguration.OsdReplayBufferSize);
            _osd.SetHookFreeRefreshRate(appConfiguration.HookFreeRefreshRate);

            _activeSub = overlayService.IsOverlayActiveStream.Subscribe(OnActiveChanged);
            _entriesSub = overlayService.OnDictionaryUpdated.Subscribe(_ => OnEntries());
            _enabledSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookFreeOverlay))
                .Subscribe(x => OnEnabledChanged((bool)x.value));
            _valueSmoothingSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.UseOsdValueSmoothing))
                .Subscribe(x => _osd.SetValueSmoothing((bool)x.value));
            _replayBufferSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.OsdReplayBufferSize))
                .Subscribe(x => _osd.SetReplayBuffer((int)x.value));
            _hookFreeRefreshRateSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.HookFreeRefreshRate))
                .Subscribe(x => _osd.SetHookFreeRefreshRate((int)x.value));
            _displaySub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.HookFreeDisplayDeviceName))
                .Subscribe(_ => ApplyPosition());
            _backgroundOpacitySub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.OsdBackgroundOpacity))
                .Subscribe(_ => ApplyBackgroundOpacity());
            _zoomSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.OsdZoom))
                .Subscribe(_ => ApplyZoom());
            if (hookFreeFallbackStream != null)
                _fallbackSub = hookFreeFallbackStream
                    .DistinctUntilChanged()
                    .Subscribe(OnFallbackChanged);
        }

        /// <summary>Configure the fixed overlay position (call from CapFrameX settings UI).</summary>
        public void SetPosition(OsdAnchor anchor, int monitor, int marginX, int marginY)
            => _osd.SetPosition(anchor, monitor, marginX, marginY);

        private void OnActiveChanged(bool active) { _active = active; UpdateRunState(); }
        private void OnEnabledChanged(bool enabled) { _enabled = enabled; UpdateRunState(); }
        private void OnFallbackChanged(bool enabled) { _fallbackEnabled = enabled; UpdateRunState(); }

        private void UpdateRunState()
        {
            // Two-level lifecycle: the renderer EXISTS while a hook-free mode is selected,
            // and mere visibility toggles (IsOverlayActive — capture auto-disable, overlay
            // hotkey) are soft-hides via DWM cloaking. Tearing the window down instead
            // stalls the game's presentation path (measured 20-70 ms display hitches from
            // the multi-stage DWM re-evaluation, including a delayed one seconds later) —
            // cloaking is the only hide method that stays completely stall-free.
            bool exist = _enabled || _fallbackEnabled;
            bool visible = _active && exist;

            if (exist && !_started)
            {
                ApplyPosition(force: true);
                UpdateFrameFeedRequirements(_overlayService.CurrentOverlayEntries);
                _osd.Start();
                _started = true;
                UpdateFrameSubscription(visible);
                // Start creates the native handle asynchronously. These calls apply immediately
                // if it is ready; otherwise OnEntries retries without poisoning the caches.
                ApplyBackgroundOpacity();
                ApplyZoom();
            }

            if (!_started) return;

            if (!exist)
            {
                StopOsd();
                return;
            }

            // Old prebuilt core without the hidden API: fall back to the historic full stop.
            UpdateFrameSubscription(visible);
            if (!_osd.SetHidden(!visible) && !visible)
                StopOsd();
        }

        private void StopOsd()
        {
            _osd.Stop();
            _started = false;
            _lastBgOpacity = -1; // Stop destroys the native handle; re-feed on next start
            _lastZoom = -1;
            _lastAnchor = -1; _lastMonitor = -1; _lastMarginX = -1; _lastMarginY = -1;
            _curRuntime = null;
            Interlocked.Exchange(ref _frameFeedRequirements, 0);
            UpdateFrameSubscription(false);
            lock (_fpsLock)
            {
                _ftWindow.Clear();
                _ftWindowSumMs = 0;
                _curFps = 0;
                _curFrametimeMs = 0;
                _dtWindow.Clear();
                _dtWindowSumMs = 0;
                _curDisplayTimeMs = 0;
            }
        }

        private void OnEntries()
        {
            if (!_started) return;
            // Use the processed, CapFrameX-sorted display list (same data + order as RTSS)
            // rather than the unordered raw dictionary values from OnDictionaryUpdated.
            var entries = _overlayService.CurrentOverlayEntries;
            UpdateFrameFeedRequirements(entries);
            if (entries == null || entries.Length == 0) return;

            var list = OverlayEntryAdapter.ToOsdEntries(entries,
                _appConfiguration.UseRunHistory,
                _overlayService.RunHistory,
                _overlayService.RunHistoryOutlierFlags,
                _overlayService.RunHistoryAggregation);

            // The <APP> Framerate/Frametime entries are filled by RTSS in the classic path;
            // hook-free they arrive as 0, so overwrite them with the stream-derived values.
            // The "<APP>" group placeholder (RTSS substitutes the app via the 3D API) is
            // resolved to the PresentMon graphics runtime, falling back to "Performance".
            double fps, ft, dt;
            lock (_fpsLock) { fps = _curFps; ft = _curFrametimeMs; dt = _curDisplayTimeMs; }
            var appLabel = _curRuntime;
            if (string.IsNullOrWhiteSpace(appLabel)) appLabel = "Performance";
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                bool changed = false;
                if (e.Identifier == "Framerate") { e.IsNumeric = true; e.ValueText = null; e.Value = fps; changed = true; }
                else if (e.Identifier == "Frametime") { e.IsNumeric = true; e.ValueText = null; e.Value = ft; changed = true; }
                // hook-free-only entry; nothing else feeds it (RTSS can't resolve display times)
                else if (e.Identifier == "DisplayTime") { e.IsNumeric = true; e.ValueText = null; e.Value = dt; changed = true; }
                if (e.Group != null && e.Group.IndexOf("<APP>", StringComparison.Ordinal) >= 0)
                {
                    e.Group = e.Group.Replace("<APP>", appLabel);
                    changed = true;
                }
                if (changed) list[i] = e;
            }

            ApplyBackgroundOpacity();
            ApplyZoom();

            ApplyPosition();

            _osd.UpdateEntries(list);
        }

        private void UpdateFrameFeedRequirements(IEnumerable<IOverlayEntry> entries)
        {
            int requirements = 0;
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || !entry.IsEntryEnabled || !entry.ShowOnOverlay)
                    {
                        continue;
                    }

                    switch (entry.Identifier)
                    {
                        case "Framerate":
                            requirements |= NeedFramerateValue;
                            break;
                        case "Frametime":
                            requirements |= NeedFrametimeValue;
                            if (entry.ShowGraph)
                            {
                                requirements |= NeedFrametimeGraph;
                            }
                            break;
                        case "DisplayTime":
                            requirements |= NeedDisplayTimeValue;
                            if (entry.ShowGraph)
                            {
                                requirements |= NeedDisplayTimeGraph;
                            }
                            break;
                    }

                    if (entry.GroupName?.IndexOf("<APP>", StringComparison.Ordinal) >= 0)
                    {
                        requirements |= NeedRuntimeLabel;
                    }
                }
            }

            int previous = Interlocked.Exchange(ref _frameFeedRequirements, requirements);
            UpdateFrameSubscription(_active && (_enabled || _fallbackEnabled));
            bool enableFrametimeScalar = (previous & NeedFrametimeScalar) == 0 &&
                (requirements & NeedFrametimeScalar) != 0;
            bool enableDisplayTimeScalar = (previous & NeedDisplayTimeValue) == 0 &&
                (requirements & NeedDisplayTimeValue) != 0;
            if (!enableFrametimeScalar && !enableDisplayTimeScalar)
            {
                return;
            }

            // Do not expose an old window when a profile re-enables a scalar after it has spent
            // time disabled. The next completed frame repopulates it immediately.
            lock (_fpsLock)
            {
                if (enableFrametimeScalar)
                {
                    _ftWindow.Clear();
                    _ftWindowSumMs = 0;
                    _curFps = 0;
                    _curFrametimeMs = 0;
                }

                if (enableDisplayTimeScalar)
                {
                    _dtWindow.Clear();
                    _dtWindowSumMs = 0;
                    _curDisplayTimeMs = 0;
                }
            }
        }

        private void UpdateFrameSubscription(bool visible)
        {
            bool shouldSubscribe = visible && _started && _frameDataStream != null &&
                _ftIndex >= 0 && Volatile.Read(ref _frameFeedRequirements) != 0;
            lock (_frameSubscriptionLock)
            {
                if (shouldSubscribe)
                {
                    if (_frameSub == null)
                    {
                        _frameSub = _frameDataStream.Subscribe(OnFrameRow);
                    }
                }
                else
                {
                    _frameSub?.Dispose();
                    _frameSub = null;
                }
            }
        }

        private void ApplyPosition(bool force = false)
        {
            int anchor = _appConfiguration.OsdAnchor;
            // The native API consumes the raw EnumDisplayMonitors index, whereas the setting
            // stores the stable Windows device name exposed in the UI.
            int monitor = DisplayMonitorResolver.GetMonitorIndex(
                _appConfiguration.HookFreeDisplayDeviceName);
            int marginX = _appConfiguration.OsdMarginX;
            int marginY = _appConfiguration.OsdMarginY;

            lock (_positionLock)
            {
                if (!force && anchor == _lastAnchor && monitor == _lastMonitor &&
                    marginX == _lastMarginX && marginY == _lastMarginY)
                {
                    return;
                }

                _lastAnchor = anchor;
                _lastMonitor = monitor;
                _lastMarginX = marginX;
                _lastMarginY = marginY;
                _osd.SetPosition((OsdAnchor)anchor, monitor, marginX, marginY);
            }
        }

        private void ApplyBackgroundOpacity()
        {
            int bgOpacity = Math.Max(0, Math.Min(100,
                _appConfiguration.OsdBackgroundOpacity));
            if (!_osd.IsRunning || bgOpacity == _lastBgOpacity)
            {
                return;
            }

            _osd.SetBackgroundAlpha(bgOpacity / 100.0);
            // SetBackgroundAlpha is a no-op until the asynchronous native handle exists. Only
            // cache the value after IsRunning confirms that the call could reach that handle.
            _lastBgOpacity = bgOpacity;
        }

        private void ApplyZoom()
        {
            int zoom = Math.Max(50, Math.Min(200, _appConfiguration.OsdZoom));
            if (!_osd.IsRunning || zoom == _lastZoom)
            {
                return;
            }

            _osd.SetZoom(zoom / 100.0);
            // Like the opacity API, SetZoom does not retain values while OsdHost is stopped.
            // Keep retrying on data ticks until the native renderer is actually available.
            _lastZoom = zoom;
        }

        private void OnFrameRow(string[] row)
        {
            if (!_started || row == null || _ftIndex < 0 || row.Length <= _ftIndex) return;

            int requirements = Volatile.Read(ref _frameFeedRequirements);
            if (requirements == 0) return;

            // graphics runtime/API of the presenting app -> label for the <APP> line
            if ((requirements & NeedRuntimeLabel) != 0 &&
                _runtimeIndex >= 0 && row.Length > _runtimeIndex)
            {
                var rt = row[_runtimeIndex]?.Trim();
                if (!string.IsNullOrEmpty(rt) && rt != "<error>") _curRuntime = rt;
            }

            bool needFrametimeSample = (requirements &
                (NeedFrametimeScalar | NeedFrametimeGraph)) != 0;
            bool needDisplayTimeSample = (requirements &
                (NeedDisplayTimeValue | NeedDisplayTimeGraph)) != 0;
            if (!needFrametimeSample && !needDisplayTimeSample) return;

            double ms = 0;
            bool hasFrametimeSample = needFrametimeSample &&
                double.TryParse(row[_ftIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out ms) &&
                ms > 0 && ms < 10000;

            // per-present sample for the frametime graph, timestamped with StartTimeInMs
            // (QPC ms) so the renderer replays the bursty stream smoothly over the window
            double t = 0;
            bool needGraphSample = (requirements &
                (NeedFrametimeGraph | NeedDisplayTimeGraph)) != 0;
            int startIdx = needGraphSample ? _startTimeIndexProvider?.Invoke() ?? -1 : -1;
            bool hasTimestamp = needGraphSample && startIdx >= 0 && row.Length > startIdx &&
                double.TryParse(row[startIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out t) && t > 0;
            // Display time (MsBetweenDisplayChange) for the "Displaytime" graph: same
            // buffer + replay path as the frametimes. Dropped frames report 0 and are
            // skipped, so only frames that actually reached the display produce a sample.
            double dc = 0;
            bool hasDisplaySample =
                needDisplayTimeSample && _displayChangedIndex >= 0 && row.Length > _displayChangedIndex &&
                double.TryParse(row[_displayChangedIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out dc)
                && dc > 0 && dc < 10000;

            bool pushFrametimeGraph = (requirements & NeedFrametimeGraph) != 0 &&
                hasFrametimeSample;
            bool pushDisplayTimeGraph = (requirements & NeedDisplayTimeGraph) != 0 &&
                hasDisplaySample;
            if (hasTimestamp && pushFrametimeGraph && pushDisplayTimeGraph)
            {
                // Queue the complete PresentMon row once. OsdHost drains all rows collected
                // during a render slice through cfx_osd_push_samples, allowing the native replay
                // clock to measure delivery waves and preventing per-row P/Invoke/mutex traffic.
                _osd.PushSample(t, ms, dc);
            }
            else if (hasTimestamp && pushFrametimeGraph)
            {
                _osd.PushFrame(t, ms);
            }
            else if (hasTimestamp && pushDisplayTimeGraph)
            {
                _osd.PushDisplayChange(t, dc);
            }
            else if (!hasTimestamp)
            {
                // No source timestamp is available: retain the legacy synthetic timelines.
                if (pushFrametimeGraph) _osd.PushFrametime(ms);
                if (pushDisplayTimeGraph) _osd.PushDisplayTime(dc);
            }

            // Current framerate/frametime for the <APP> entries: mean frametime over a ~1s
            // window; FPS = 1000 * frames / window_ms  (equivalently 1000 / mean_frametime).
            // The Displaytime entry uses the same windowed mean over its own samples.
            bool updateFrametimeScalar = (requirements & NeedFrametimeScalar) != 0 &&
                hasFrametimeSample;
            bool updateDisplayTimeScalar = (requirements & NeedDisplayTimeValue) != 0 &&
                hasDisplaySample;
            if (!updateFrametimeScalar && !updateDisplayTimeScalar) return;

            lock (_fpsLock)
            {
                if (updateFrametimeScalar)
                {
                    _ftWindow.Enqueue(ms);
                    _ftWindowSumMs += ms;
                    // keep ~1s of history, but hard-cap the sample count so pathologically small
                    // frametimes (very high FPS) can't grow the queue without bound
                    while (_ftWindow.Count > 1 && (_ftWindowSumMs > FpsWindowMs || _ftWindow.Count > 4000))
                        _ftWindowSumMs -= _ftWindow.Dequeue();
                    int n = _ftWindow.Count;
                    if (n > 0 && _ftWindowSumMs > 0)
                    {
                        _curFrametimeMs = _ftWindowSumMs / n;
                        _curFps = 1000.0 * n / _ftWindowSumMs;
                    }
                }

                if (updateDisplayTimeScalar)
                {
                    _dtWindow.Enqueue(dc);
                    _dtWindowSumMs += dc;
                    while (_dtWindow.Count > 1 && (_dtWindowSumMs > FpsWindowMs || _dtWindow.Count > 4000))
                        _dtWindowSumMs -= _dtWindow.Dequeue();
                    if (_dtWindow.Count > 0 && _dtWindowSumMs > 0)
                        _curDisplayTimeMs = _dtWindowSumMs / _dtWindow.Count;
                }
            }
        }

        public void Dispose()
        {
            _activeSub?.Dispose();
            _entriesSub?.Dispose();
            UpdateFrameSubscription(false);
            _enabledSub?.Dispose();
            _fallbackSub?.Dispose();
            _valueSmoothingSub?.Dispose();
            _replayBufferSub?.Dispose();
            _hookFreeRefreshRateSub?.Dispose();
            _displaySub?.Dispose();
            _backgroundOpacitySub?.Dispose();
            _zoomSub?.Dispose();
            _osd?.Dispose();
        }
    }
}
