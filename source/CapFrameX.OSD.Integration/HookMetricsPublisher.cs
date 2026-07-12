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
        private readonly IOverlayService _overlayService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDisposable _entriesSub;
        private readonly IDisposable _enabledSub;
        private readonly IDisposable _activeSub;
        private readonly IDisposable _pidSub;
        private readonly object _gate = new object();
        private volatile HookMetricsChannel _channel;
        private volatile bool _enabled;
        private int _selectedPid;
        private bool _overlayActive;
        private bool _targetAllowed;
        private string _lastPolicyReason;

        public HookMetricsPublisher(IOverlayService overlayService, IAppConfiguration appConfiguration,
            IObservable<int> processIdStream)
        {
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            if (processIdStream == null) throw new ArgumentNullException(nameof(processIdStream));

            _enabled = appConfiguration.EnableHookOverlay;
            _overlayActive = appConfiguration.IsOverlayActive;
            if (_enabled) _channel = HookMetricsChannel.Create(EffectiveTargetPidLocked());

            _pidSub = processIdStream
                .DistinctUntilChanged()
                .Subscribe(OnTargetPidChanged);

            _enabledSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookOverlay))
                .Subscribe(x => OnEnabledChanged((bool)x.value));

            _activeSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.IsOverlayActive))
                .Subscribe(x => OnOverlayActiveChanged((bool)x.value));

            // The callback normally ticks only while active; the explicit active-state gate also
            // clears TargetPid immediately on hide instead of waiting for SHM staleness.
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
                        _channel = HookMetricsChannel.Create(EffectiveTargetPidLocked());
                }
                else
                {
                    _channel?.Dispose();
                    _channel = null;
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
            }
        }

        private int EffectiveTargetPidLocked()
            => _enabled && _overlayActive && _targetAllowed && _selectedPid > 0 ? _selectedPid : 0;

        private void RefreshTargetPolicyLocked()
        {
            string reason = null;
            bool allowed = _selectedPid > 0 &&
                HookTargetPolicy.IsAllowed(_selectedPid, out reason);
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

        private void OnEntries()
        {
            try
            {
                lock (_gate)
                {
                    bool wasAllowed = _targetAllowed;
                    RefreshTargetPolicyLocked();
                    if (wasAllowed != _targetAllowed)
                        _channel?.SetTargetPid(EffectiveTargetPidLocked());
                    int targetPid = EffectiveTargetPidLocked();
                    if (_channel == null || targetPid == 0) return;

                    var list = OverlayEntryAdapter.ToOsdEntries(_overlayService.CurrentOverlayEntries);
                    uint flags = _appConfiguration.HookOverlayUsePresentMonFrametimes
                        ? HookMetricsChannel.FlagPresentMonGraph : 0u;
                    // OSD background opacity (percent -> byte in bits 8..15). Published with every
                    // snapshot so slider changes reach the hook within one publish period.
                    uint alphaByte = (uint)Math.Round(
                        Math.Max(0, Math.Min(100, _appConfiguration.OsdBackgroundOpacity)) * 2.55);
                    flags |= HookMetricsChannel.FlagBackgroundAlpha
                           | (alphaByte << HookMetricsChannel.BackgroundAlphaShift);
                    _channel.Publish(list, flags, targetPid);
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
            lock (_gate) { _channel?.Dispose(); _channel = null; }
        }
    }
}
