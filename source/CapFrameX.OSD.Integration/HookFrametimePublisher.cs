using System;
using System.Globalization;
using System.Reactive.Linq;
using CapFrameX.Contracts.Configuration;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Streams CapFrameX's live PresentMon per-frame frametime + display-time samples to the in-game
    /// hook via <see cref="HookFrametimeChannel"/>, so the hook can replay the SAME data its hook-free
    /// sibling shows — but timestamped and buffered, kept strictly separate from the hook's own local
    /// present ring. Active only while BOTH the in-game hook overlay
    /// (<see cref="IAppConfiguration.EnableHookOverlay"/>) AND the PresentMon graph source
    /// (<see cref="IAppConfiguration.HookOverlayUsePresentMonFrametimes"/>) are enabled.
    ///
    /// Mirrors the per-frame parsing of <see cref="OsdOverlayBridge.OnFrameRow"/> (same frame stream +
    /// column indices), but writes to shared memory instead of the in-process renderer.
    /// </summary>
    public sealed class HookFrametimePublisher : IDisposable
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly int _ftIndex;
        private readonly int _displayChangedIndex;
        private readonly Func<int> _startTimeIndexProvider;
        private readonly IDisposable _frameSub;
        private readonly IDisposable _configSub;
        private readonly object _gate = new object();
        private volatile HookFrametimeChannel _channel;
        private volatile bool _enabled;

        public HookFrametimePublisher(IAppConfiguration appConfiguration,
                                      IObservable<string[]> frameDataStream,
                                      int frametimeColumnIndex,
                                      int displayChangedColumnIndex,
                                      Func<int> startTimeIndexProvider)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _ftIndex = frametimeColumnIndex;
            _displayChangedIndex = displayChangedColumnIndex;
            _startTimeIndexProvider = startTimeIndexProvider;

            RecomputeEnabled();

            _configSub = appConfiguration.OnValueChanged
                .Where(x => x.key == nameof(IAppConfiguration.EnableHookOverlay)
                         || x.key == nameof(IAppConfiguration.HookOverlayUsePresentMonFrametimes))
                .Subscribe(_ => RecomputeEnabled());

            if (frameDataStream != null && frametimeColumnIndex >= 0)
                _frameSub = frameDataStream.Subscribe(OnFrameRow);
        }

        private void RecomputeEnabled()
        {
            lock (_gate)
            {
                bool run = _appConfiguration.EnableHookOverlay
                        && _appConfiguration.HookOverlayUsePresentMonFrametimes;
                if (run == _enabled && (_channel != null) == run) return;
                _enabled = run;
                if (run)
                {
                    if (_channel == null) _channel = HookFrametimeChannel.Create();
                }
                else
                {
                    _channel?.Dispose();
                    _channel = null;
                }
            }
        }

        private void OnFrameRow(string[] row)
        {
            HookFrametimeChannel channel = _channel; // capture; PushSample no-ops if disposed concurrently
            if (channel == null || row == null || _ftIndex < 0 || row.Length <= _ftIndex) return;

            if (!double.TryParse(row[_ftIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var ms)
                || ms <= 0 || ms >= 10000) return;

            // The ring is timestamp-based (the renderer's replay clock windows on StartTimeInMs), so a
            // sample without a valid timestamp is skipped rather than synthesized.
            int startIdx = _startTimeIndexProvider?.Invoke() ?? -1;
            if (startIdx < 0 || row.Length <= startIdx
                || !double.TryParse(row[startIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var t)
                || t <= 0) return;

            // Display time (MsBetweenDisplayChange): 0 for dropped frames -> no display-time sample.
            double dc = 0;
            if (_displayChangedIndex >= 0 && row.Length > _displayChangedIndex
                && double.TryParse(row[_displayChangedIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                && d > 0 && d < 10000)
                dc = d;

            try
            {
                channel.PushSample(t, ms, dc);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: failed to publish a frametime sample to the hook");
            }
        }

        public void Dispose()
        {
            _frameSub?.Dispose();
            _configSub?.Dispose();
            lock (_gate) { _channel?.Dispose(); _channel = null; }
        }
    }
}
