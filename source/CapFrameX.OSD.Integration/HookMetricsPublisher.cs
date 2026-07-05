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
        private readonly object _gate = new object();
        private volatile HookMetricsChannel _channel;
        private volatile bool _enabled;

        public HookMetricsPublisher(IOverlayService overlayService, IAppConfiguration appConfiguration)
        {
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));

            _enabled = appConfiguration.EnableHookOverlay;
            if (_enabled) _channel = HookMetricsChannel.Create();

            _enabledSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookOverlay))
                .Subscribe(x => OnEnabledChanged((bool)x.value));

            // OnDictionaryUpdated only ticks while the overlay is active, so no extra gating needed.
            _entriesSub = overlayService.OnDictionaryUpdated.Subscribe(_ => OnEntries());
        }

        private void OnEnabledChanged(bool enabled)
        {
            lock (_gate)
            {
                _enabled = enabled;
                if (enabled)
                {
                    if (_channel == null) _channel = HookMetricsChannel.Create();
                }
                else
                {
                    _channel?.Dispose();
                    _channel = null;
                }
            }
        }

        private void OnEntries()
        {
            if (!_enabled) return;
            HookMetricsChannel channel = _channel; // capture; Publish no-ops if disposed concurrently
            if (channel == null) return;
            try
            {
                var list = OverlayEntryAdapter.ToOsdEntries(_overlayService.CurrentOverlayEntries);
                uint flags = _appConfiguration.HookOverlayUsePresentMonFrametimes
                    ? HookMetricsChannel.FlagPresentMonGraph : 0u;
                channel.Publish(list, flags);
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
            lock (_gate) { _channel?.Dispose(); _channel = null; }
        }
    }
}
