using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
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
    ///    capture service's frame-data stream → PushFrame / PushDisplayChange,
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
        private readonly IDisposable _frameSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _fallbackSub;
        private readonly int _ftIndex;
        private readonly int _runtimeIndex;
        private readonly int _displayChangedIndex;
        // StartTimeInMs (CPUStartQPCTimeInMs) sits AFTER the optional PC-latency column, so
        // its index is layout-dependent — resolve it lazily instead of caching a stale int.
        private readonly Func<int> _startTimeIndexProvider;
        private volatile bool _active;
        private volatile bool _enabled;
        private volatile bool _fallbackEnabled;
        private volatile bool _started;

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
            _osd = new OsdHost(OsdAnchor.TopLeft, marginX: 40, marginY: 40);
            _ftIndex = frametimeColumnIndex;
            _runtimeIndex = presentRuntimeColumnIndex;
            _displayChangedIndex = displayChangedColumnIndex;
            _startTimeIndexProvider = startTimeIndexProvider;
            _enabled = appConfiguration.EnableHookFreeOverlay;

            _activeSub = overlayService.IsOverlayActiveStream.Subscribe(OnActiveChanged);
            _entriesSub = overlayService.OnDictionaryUpdated.Subscribe(_ => OnEntries());
            _enabledSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookFreeOverlay))
                .Subscribe(x => OnEnabledChanged((bool)x.value));
            if (hookFreeFallbackStream != null)
                _fallbackSub = hookFreeFallbackStream
                    .DistinctUntilChanged()
                    .Subscribe(OnFallbackChanged);
            if (frameDataStream != null && frametimeColumnIndex >= 0)
                _frameSub = frameDataStream.Subscribe(OnFrameRow);
        }

        /// <summary>Configure the fixed overlay position (call from CapFrameX settings UI).</summary>
        public void SetPosition(OsdAnchor anchor, int monitor, int marginX, int marginY)
            => _osd.SetPosition(anchor, monitor, marginX, marginY);

        private void OnActiveChanged(bool active) { _active = active; UpdateRunState(); }
        private void OnEnabledChanged(bool enabled) { _enabled = enabled; UpdateRunState(); }
        private void OnFallbackChanged(bool enabled) { _fallbackEnabled = enabled; UpdateRunState(); }

        private void UpdateRunState()
        {
            bool run = _active && (_enabled || _fallbackEnabled);
            if (run && !_started) { _osd.Start(); _started = true; }
            else if (!run && _started)
            {
                _osd.Stop();
                _started = false;
                _lastBgOpacity = -1; // Stop destroys the native handle; re-feed on next start
                _curRuntime = null;
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
        }

        private void OnEntries()
        {
            if (!_started) return;
            // Use the processed, CapFrameX-sorted display list (same data + order as RTSS)
            // rather than the unordered raw dictionary values from OnDictionaryUpdated.
            var entries = _overlayService.CurrentOverlayEntries;
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

            // Background opacity (percent -> 0..1); forwarded only on change. Same setting the
            // hook overlay receives via the metrics-SHM flags, so both backends stay in sync.
            int bgOpacity = _appConfiguration.OsdBackgroundOpacity;
            if (bgOpacity != _lastBgOpacity)
            {
                _lastBgOpacity = bgOpacity;
                _osd.SetBackgroundAlpha(Math.Max(0, Math.Min(100, bgOpacity)) / 100.0);
            }

            _osd.UpdateEntries(list);
        }

        private void OnFrameRow(string[] row)
        {
            if (!_started || row == null || _ftIndex < 0 || row.Length <= _ftIndex) return;

            // graphics runtime/API of the presenting app -> label for the <APP> line
            if (_runtimeIndex >= 0 && row.Length > _runtimeIndex)
            {
                var rt = row[_runtimeIndex]?.Trim();
                if (!string.IsNullOrEmpty(rt) && rt != "<error>") _curRuntime = rt;
            }

            if (!double.TryParse(row[_ftIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var ms)
                || ms <= 0 || ms >= 10000) return;

            // per-present sample for the frametime graph, timestamped with StartTimeInMs
            // (QPC ms) so the renderer replays the bursty stream smoothly over the window
            double t = 0;
            int startIdx = _startTimeIndexProvider?.Invoke() ?? -1;
            bool hasTimestamp = startIdx >= 0 && row.Length > startIdx &&
                double.TryParse(row[startIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out t) && t > 0;
            if (hasTimestamp)
                _osd.PushFrame(t, ms);
            else
                _osd.PushFrametime(ms);   // no timestamp available: synthetic fallback

            // Display time (MsBetweenDisplayChange) for the "Displaytime" graph: same
            // buffer + replay path as the frametimes. Dropped frames report 0 and are
            // skipped, so only frames that actually reached the display produce a sample.
            double dc = 0;
            bool hasDisplaySample =
                _displayChangedIndex >= 0 && row.Length > _displayChangedIndex &&
                double.TryParse(row[_displayChangedIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out dc)
                && dc > 0 && dc < 10000;
            if (hasDisplaySample)
            {
                if (hasTimestamp)
                    _osd.PushDisplayChange(t, dc);
                else
                    _osd.PushDisplayTime(dc);
            }

            // Current framerate/frametime for the <APP> entries: mean frametime over a ~1s
            // window; FPS = 1000 * frames / window_ms  (equivalently 1000 / mean_frametime).
            // The Displaytime entry uses the same windowed mean over its own samples.
            lock (_fpsLock)
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

                if (hasDisplaySample)
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
            _frameSub?.Dispose();
            _enabledSub?.Dispose();
            _fallbackSub?.Dispose();
            _osd?.Dispose();
        }
    }
}
