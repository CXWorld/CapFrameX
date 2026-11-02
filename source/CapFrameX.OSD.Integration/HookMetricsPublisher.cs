using System;
using System.Reactive.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Feeds the in-game hook with CapFrameX's authoritative metrics. While the in-game hook overlay
    /// is enabled, it forwards the processed overlay entries (<c>CurrentOverlayEntries</c> — the same
    /// fps/lows/sensors/static rows RTSS and the hook-free OSD render) to shared memory
    /// (<see cref="HookMetricsChannel"/>) on every OSD tick, so the injected hook shows the same
    /// values. The hook keeps its own local frame ring only for the smooth per-present graph line.
    /// Mirrors <see cref="OsdOverlayBridge"/> but writes to SHM instead of the in-process renderer.
    /// </summary>
    public sealed class HookMetricsPublisher : IDisposable
    {
        // Decides whether the hook may draw for a PID; HookTargetPolicy.IsAllowed in the app.
        internal delegate bool TargetPolicyCheck(int pid, out string reason);

        private readonly IOverlayService _overlayService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly OverlayPixelOrbitResolver _pixelOrbit;
        private readonly TargetPolicyCheck _targetPolicy;
        private readonly string _metricsMapName;
        private readonly string _placementMapName;
        private readonly IDisposable _entriesSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _activeSub;
        private readonly IDisposable _pidSub;
        private readonly object _gate = new object();
        private volatile HookMetricsChannel _channel;
        // Anchor + margins ride their own channel: the metrics flags DWORD has no room for two
        // pixel margins, and growing the metrics header would move the record area under readers
        // that already accept the next version number.
        private volatile HookPlacementChannel _placement;
        private volatile bool _enabled;
        private int _selectedPid;
        private bool _overlayActive;
        private bool _targetAllowed;
        private string _lastPolicyReason;

        public HookMetricsPublisher(IOverlayService overlayService, IAppConfiguration appConfiguration,
            IObservable<int> processIdStream, OverlayPixelOrbitResolver pixelOrbit = null)
            : this(overlayService, appConfiguration, processIdStream, pixelOrbit,
                HookTargetPolicy.IsAllowed, HookMetricsChannel.MapName, HookPlacementChannel.MapName)
        {
        }

        // Tests replace the policy, which scans the target's modules, and use session-local
        // mappings instead of the live Global channels.
        internal HookMetricsPublisher(IOverlayService overlayService, IAppConfiguration appConfiguration,
            IObservable<int> processIdStream, OverlayPixelOrbitResolver pixelOrbit,
            TargetPolicyCheck targetPolicy, string metricsMapName, string placementMapName)
        {
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            if (processIdStream == null) throw new ArgumentNullException(nameof(processIdStream));
            _pixelOrbit = pixelOrbit;
            _targetPolicy = targetPolicy ?? throw new ArgumentNullException(nameof(targetPolicy));
            _metricsMapName = metricsMapName;
            _placementMapName = placementMapName;

            _enabled = appConfiguration.EnableHookOverlay;
            _overlayActive = appConfiguration.IsOverlayActive;
            if (_enabled)
            {
                _channel = HookMetricsChannel.Create(EffectiveTargetPidLocked(), _metricsMapName);
                _placement = HookPlacementChannel.Create(_placementMapName);
                PublishPlacement();
            }

            _pidSub = processIdStream
                .DistinctUntilChanged()
                .Subscribe(OnTargetPidChanged);

            _enabledSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookOverlay))
                .Subscribe(x => OnEnabledChanged((bool)x.value));

            _activeSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.IsOverlayActive))
                .Subscribe(x => OnOverlayActiveChanged((bool)x.value));

            // The callback also ticks while hidden when a remote API client reads the entries;
            // OnEntries ignores those ticks. The explicit active-state gate also clears TargetPid
            // immediately on hide instead of waiting for SHM staleness.
            _entriesSub = overlayService.OnDictionaryUpdated.Subscribe(_ => OnEntries());
        }

        private void OnEnabledChanged(bool enabled)
        {
            lock (_gate)
            {
                _enabled = enabled;
                if (enabled)
                {
                    RefreshTargetPolicyLocked();
                    if (_channel == null)
                        _channel = HookMetricsChannel.Create(EffectiveTargetPidLocked(),
                            _metricsMapName);
                    if (_placement == null)
                        _placement = HookPlacementChannel.Create(_placementMapName);
                    PublishPlacement();
                }
                else
                {
                    _channel?.Dispose();
                    _channel = null;
                    _placement?.Dispose();
                    _placement = null;
                }
            }
        }

        private void OnTargetPidChanged(int targetPid)
        {
            lock (_gate)
            {
                int previousPid = _selectedPid;
                _selectedPid = targetPid > 0 ? targetPid : 0;
                if (previousPid > 0 && previousPid != _selectedPid)
                    HookTargetPolicy.Invalidate(previousPid);
                RefreshTargetPolicyLocked();
                _channel?.SetTargetPid(EffectiveTargetPidLocked());
            }
        }

        private void OnOverlayActiveChanged(bool active)
        {
            lock (_gate)
            {
                _overlayActive = active;
                if (active) RefreshTargetPolicyLocked();
                _channel?.SetTargetPid(EffectiveTargetPidLocked());
                if (active)
                {
                    // A long hide tears down the native OSD instance, whose replacement starts
                    // at top-left (a short one keeps it). Advance the sequence even when the
                    // configured placement itself did not change so a replacement consumes it.
                    PublishPlacement(force: true);
                }
            }
        }

        private void PublishPlacement(bool force = false)
        {
            int marginX = _appConfiguration.OsdMarginX;
            int marginY = _appConfiguration.OsdMarginY;
            // Every OSD tick republishes, so an orbit step reaches the hook within one period.
            if (_pixelOrbit != null)
                (marginX, marginY) = _pixelOrbit.Apply(marginX, marginY);
            _placement?.Publish(_appConfiguration.OsdAnchor, marginX, marginY, force);
        }

        private int EffectiveTargetPidLocked()
            => _enabled && _overlayActive && _targetAllowed && _selectedPid > 0 ? _selectedPid : 0;

        private void RefreshTargetPolicyLocked()
        {
            // The policy only decides whether the hook may draw for the PID. With another
            // renderer selected or the overlay hidden nothing is published, and the check would
            // just scan the game's modules every 2 s and warn about anti-cheat titles the hook
            // never serves. Turning the hook on or showing the overlay refreshes it first.
            if (!_enabled || !_overlayActive) return;

            string reason = null;
            bool allowed = _selectedPid > 0 &&
                _targetPolicy(_selectedPid, out reason);
            if (allowed == _targetAllowed && string.Equals(reason, _lastPolicyReason,
                StringComparison.Ordinal))
                return;

            bool wasAllowed = _targetAllowed;
            _targetAllowed = allowed;
            _lastPolicyReason = reason;
            if (_selectedPid <= 0) return;

            if (!allowed)
                Log.Warning("HookOverlay: target PID {pid} blocked by target policy ({reason})",
                    _selectedPid, reason ?? "unknown reason");
            else if (!wasAllowed)
                Log.Information("HookOverlay: target PID {pid} passed the target policy", _selectedPid);
        }

        /// <summary>
        /// The metrics header flags for the current settings. Published with every snapshot, so a
        /// changed option reaches the DXGI hook and the Vulkan layer within one publish period.
        /// </summary>
        internal static uint BuildHeaderFlags(IAppConfiguration configuration)
        {
            uint flags = configuration.HookOverlayUsePresentMonFrametimes
                ? HookMetricsChannel.FlagPresentMonGraph : 0u;
            if (!configuration.UseOsdValueSmoothing)
                flags |= HookMetricsChannel.FlagDisableValueSmoothing;
            // OSD background opacity (percent -> byte in bits 8..15).
            uint alphaByte = (uint)Math.Round(
                Math.Max(0, Math.Min(100, configuration.OsdBackgroundOpacity)) * 2.55);
            flags |= HookMetricsChannel.FlagBackgroundAlpha
                   | (alphaByte << HookMetricsChannel.BackgroundAlphaShift);
            // Overlay zoom (percent -> bits 16..23).
            uint zoomPercent = (uint)Math.Max(50, Math.Min(200, configuration.OsdZoom));
            flags |= HookMetricsChannel.FlagZoom
                   | (zoomPercent << HookMetricsChannel.ZoomShift);
            // Resolution-relative sizing carries no value: the hook knows its back buffer.
            if (configuration.OsdScaleWithResolution)
                flags |= HookMetricsChannel.FlagResolutionScale;
            // PresentMon replay baseline (500..10000 ms -> one byte in 50 ms units), carried in
            // the shared metrics header without changing its wire layout.
            int replayBufferMs = Math.Max(500,
                Math.Min(10000, configuration.OsdReplayBufferSize));
            uint replayBufferUnits = (uint)((replayBufferMs +
                HookMetricsChannel.ReplayBufferUnitMs / 2) /
                HookMetricsChannel.ReplayBufferUnitMs);
            flags |= HookMetricsChannel.FlagReplayBuffer
                   | (replayBufferUnits << HookMetricsChannel.ReplayBufferShift);
            return flags;
        }

        private void OnEntries()
        {
            try
            {
                lock (_gate)
                {
                    // Entries keep flowing with the overlay switched off while a remote API client
                    // reads them, and with RTSS or the hook-free overlay as the renderer; neither
                    // the hidden nor the unselected hook needs them or the policy re-check.
                    if (!_enabled || !_overlayActive) return;

                    bool wasAllowed = _targetAllowed;
                    RefreshTargetPolicyLocked();
                    if (wasAllowed != _targetAllowed)
                        _channel?.SetTargetPid(EffectiveTargetPidLocked());
                    int targetPid = EffectiveTargetPidLocked();
                    if (_channel == null || targetPid == 0) return;

                    var list = OverlayEntryAdapter.ToOsdEntries(
                        _overlayService.CurrentOverlayEntries,
                        _appConfiguration.UseRunHistory,
                        _overlayService.RunHistory,
                        _overlayService.RunHistoryOutlierFlags,
                        _overlayService.RunHistoryAggregation);
                    _channel.Publish(list, BuildHeaderFlags(_appConfiguration), targetPid,
                        _appConfiguration.OsdFontFamily);
                    // Publishes only on change; the renderers re-place the panel on every write.
                    PublishPlacement();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: failed to publish metrics to the hook");
            }
        }

        public void Dispose()
        {
            _entriesSub?.Dispose();
            _enabledSub?.Dispose();
            _activeSub?.Dispose();
            _pidSub?.Dispose();
            lock (_gate)
            {
                _channel?.Dispose(); _channel = null;
                _placement?.Dispose(); _placement = null;
            }
        }
    }
}
