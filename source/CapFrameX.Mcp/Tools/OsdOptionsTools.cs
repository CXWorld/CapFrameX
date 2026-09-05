using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Hotkey;
using CapFrameX.Mcp.Attributes;
using CapFrameX.PresentMonInterface;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class OsdOptionsTools
    {
        private readonly IAppConfiguration _config;
        private readonly IOverlayService _overlayService;
        private readonly IRTSSService _rtssService;
        private readonly ISensorService _sensorService;
        private readonly IOnlineMetricService _onlineMetricService;

        public OsdOptionsTools(
            IAppConfiguration config,
            IOverlayService overlayService,
            IRTSSService rtssService,
            ISensorService sensorService,
            IOnlineMetricService onlineMetricService)
        {
            _config = config;
            _overlayService = overlayService;
            _rtssService = rtssService;
            _sensorService = sensorService;
            _onlineMetricService = onlineMetricService;
        }

        [McpServerTool(Name = "cfx_get_osd_options",
            Description = "Returns every option shown in CapFrameX's OSD options UI as one typed snapshot, including renderer, visibility, " +
                "position, appearance, PresentMon replay buffering, hotkeys, and update intervals. Also reports the underlying renderer flags and RTSS availability so " +
                "an invalid or unavailable renderer selection can be diagnosed without querying generic AppSettings.")]
        public OsdOptionsInfo GetOsdOptions()
        {
            return CreateSnapshot();
        }

        [McpServerTool(Name = "cfx_set_osd_options",
            Description = "Updates one or more global OSD options and returns the complete resulting snapshot. Omitted arguments stay unchanged. " +
                "All arguments are validated before any setting is written. Runtime services are notified so visibility, RTSS position, refresh " +
                "periods, metric intervals, renderer selection, and hotkeys take effect in the running CapFrameX instance.")]
        public SetOsdOptionsResult SetOsdOptions(
            [Description("Renderer: Rtss, InGame, or HookFree.")] OsdRendererMode? renderer = null,
            [Description("Show or hide the selected OSD renderer.")] bool? isOverlayActive = null,
            [Description("Automatically hide the OSD while a capture is running.")] bool? autoDisableOverlay = null,
            [Description("Include seconds in the system-time OSD entry.")] bool? showSystemTimeSeconds = null,
            [Description("Suppress RTSS output while keeping overlay data available to APIs.")] bool? hideOverlay = null,
            [Description("For the in-game renderer, use CapFrameX PresentMon frame/display times instead of the hook-local frame times.")] bool? hookOverlayUsePresentMonFrametimes = null,
            [Description("PresentMon replay buffer in milliseconds for the in-game PresentMon source and hook-free renderer, 500..10000.")] int? replayBufferSizeMs = null,
            [Description("Maximum hook-free chart refresh rate in Hz; one of 1, 2, 5, 10, 20, 30, 60, or 120.")] int? hookFreeRefreshRate = null,
            [Description("Enable RTSS custom coordinates.")] bool? osdCustomPosition = null,
            [Description("RTSS custom X coordinate.")] int? osdPositionX = null,
            [Description("RTSS custom Y coordinate.")] int? osdPositionY = null,
            [Description("CapFrameX renderer background opacity in percent, 0..100.")] int? backgroundOpacity = null,
            [Description("CapFrameX renderer anchor position.")] OsdAnchorPosition? anchor = null,
            [Description("Horizontal distance from the anchor in pixels, 0..2000.")] int? marginX = null,
            [Description("Vertical distance from the anchor in pixels, 0..2000.")] int? marginY = null,
            [Description("CapFrameX renderer size in percent, 50..200.")] int? zoom = null,
            [Description("Smooth numeric values between OSD data updates.")] bool? useValueSmoothing = null,
            [Description("Global overlay toggle hotkey, for example Alt+O.")] string overlayHotkey = null,
            [Description("Global overlay-configuration switch hotkey, for example Alt+C.")] string overlayConfigHotkey = null,
            [Description("Global hotkey that cycles through all CapFrameX overlay anchor positions, for example Alt+P.")] string overlayPositionHotkey = null,
            [Description("Global real-time metrics reset hotkey, for example Alt+M.")] string resetMetricsHotkey = null,
            [Description("OSD sensor refresh period in milliseconds; must be greater than zero.")] int? refreshPeriodMs = null,
            [Description("Real-time metric calculation interval in seconds; must be greater than zero.")] int? metricIntervalSeconds = null)
        {
            if (!HasAnyUpdate(renderer, isOverlayActive, autoDisableOverlay, showSystemTimeSeconds,
                hideOverlay, hookOverlayUsePresentMonFrametimes, replayBufferSizeMs, hookFreeRefreshRate,
                osdCustomPosition, osdPositionX,
                osdPositionY, backgroundOpacity, anchor, marginX, marginY, zoom,
                useValueSmoothing, overlayHotkey, overlayConfigHotkey, overlayPositionHotkey, resetMetricsHotkey,
                refreshPeriodMs, metricIntervalSeconds))
            {
                throw new ArgumentException("Provide at least one OSD option to update.");
            }

            ValidateDefinedEnum(renderer, nameof(renderer));
            ValidateDefinedEnum(anchor, nameof(anchor));
            ValidateRange(replayBufferSizeMs, 500, 10000, nameof(replayBufferSizeMs));
            ValidateHookFreeRefreshRate(hookFreeRefreshRate, nameof(hookFreeRefreshRate));
            ValidateRange(backgroundOpacity, 0, 100, nameof(backgroundOpacity));
            ValidateRange(marginX, 0, 2000, nameof(marginX));
            ValidateRange(marginY, 0, 2000, nameof(marginY));
            ValidateRange(zoom, 50, 200, nameof(zoom));
            ValidatePositive(refreshPeriodMs, nameof(refreshPeriodMs));
            ValidatePositive(metricIntervalSeconds, nameof(metricIntervalSeconds));
            ValidateHotkey(overlayHotkey, nameof(overlayHotkey));
            ValidateHotkey(overlayConfigHotkey, nameof(overlayConfigHotkey));
            ValidateHotkey(overlayPositionHotkey, nameof(overlayPositionHotkey));
            ValidateHotkey(resetMetricsHotkey, nameof(resetMetricsHotkey));

            bool activeAfterUpdate = isOverlayActive ?? _config.IsOverlayActive;
            bool rendererIsBeingActivated = isOverlayActive == true
                || (renderer.HasValue && activeAfterUpdate);
            var rendererAfterUpdate = renderer ?? TryGetCurrentRenderer();
            if (rendererIsBeingActivated)
            {
                if (!rendererAfterUpdate.HasValue)
                {
                    throw new InvalidOperationException(
                        "The current renderer flags are inconsistent. Set renderer to Rtss, InGame, or HookFree before activating the OSD.");
                }
                if (rendererAfterUpdate.Value == OsdRendererMode.Rtss && !_rtssService.IsRTSSInstalled())
                {
                    throw new InvalidOperationException(
                        "RTSS is not installed. Select InGame or HookFree, or install RTSS before activating the RTSS renderer.");
                }
            }

            var changed = new List<string>();

            if (renderer.HasValue)
                ApplyRenderer(renderer.Value, changed);

            ApplyIfChanged(nameof(IAppConfiguration.AutoDisableOverlay), autoDisableOverlay,
                () => _config.AutoDisableOverlay, value => _config.AutoDisableOverlay = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.ShowSystemTimeSeconds), showSystemTimeSeconds,
                () => _config.ShowSystemTimeSeconds, value => _config.ShowSystemTimeSeconds = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.HideOverlay), hideOverlay,
                () => _config.HideOverlay, value => _config.HideOverlay = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.HookOverlayUsePresentMonFrametimes), hookOverlayUsePresentMonFrametimes,
                () => _config.HookOverlayUsePresentMonFrametimes,
                value => _config.HookOverlayUsePresentMonFrametimes = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.OsdReplayBufferSize), replayBufferSizeMs,
                () => _config.OsdReplayBufferSize, value => _config.OsdReplayBufferSize = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.HookFreeRefreshRate), hookFreeRefreshRate,
                () => _config.HookFreeRefreshRate, value => _config.HookFreeRefreshRate = value, changed);

            bool customPositionChanged = ApplyIfChanged(nameof(IAppConfiguration.OSDCustomPosition), osdCustomPosition,
                () => _config.OSDCustomPosition, value => _config.OSDCustomPosition = value, changed);
            bool positionXChanged = ApplyIfChanged(nameof(IAppConfiguration.OSDPositionX), osdPositionX,
                () => _config.OSDPositionX, value => _config.OSDPositionX = value, changed);
            bool positionYChanged = ApplyIfChanged(nameof(IAppConfiguration.OSDPositionY), osdPositionY,
                () => _config.OSDPositionY, value => _config.OSDPositionY = value, changed);
            if (customPositionChanged)
                _rtssService.SetOSDCustomPosition(_config.OSDCustomPosition);
            if (positionXChanged || positionYChanged)
                _rtssService.SetOverlayPosition(_config.OSDPositionX, _config.OSDPositionY);

            ApplyIfChanged(nameof(IAppConfiguration.OsdBackgroundOpacity), backgroundOpacity,
                () => _config.OsdBackgroundOpacity, value => _config.OsdBackgroundOpacity = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.OsdAnchor), anchor.HasValue ? (int?)anchor.Value : null,
                () => _config.OsdAnchor, value => _config.OsdAnchor = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.OsdMarginX), marginX,
                () => _config.OsdMarginX, value => _config.OsdMarginX = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.OsdMarginY), marginY,
                () => _config.OsdMarginY, value => _config.OsdMarginY = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.OsdZoom), zoom,
                () => _config.OsdZoom, value => _config.OsdZoom = value, changed);
            ApplyIfChanged(nameof(IAppConfiguration.UseOsdValueSmoothing), useValueSmoothing,
                () => _config.UseOsdValueSmoothing, value => _config.UseOsdValueSmoothing = value, changed);

            bool hotkeyChanged = false;
            hotkeyChanged |= ApplyIfChanged(nameof(IAppConfiguration.OverlayHotKey), overlayHotkey,
                () => _config.OverlayHotKey, value => _config.OverlayHotKey = value, changed);
            hotkeyChanged |= ApplyIfChanged(nameof(IAppConfiguration.OverlayConfigHotKey), overlayConfigHotkey,
                () => _config.OverlayConfigHotKey, value => _config.OverlayConfigHotKey = value, changed);
            hotkeyChanged |= ApplyIfChanged(nameof(IAppConfiguration.OverlayPositionHotkey), overlayPositionHotkey,
                () => _config.OverlayPositionHotkey, value => _config.OverlayPositionHotkey = value, changed);
            hotkeyChanged |= ApplyIfChanged(nameof(IAppConfiguration.ResetMetricsHotkey), resetMetricsHotkey,
                () => _config.ResetMetricsHotkey, value => _config.ResetMetricsHotkey = value, changed);
            if (hotkeyChanged)
                HotkeyDictionaryBuilder.Refresh(_config);

            bool refreshChanged = ApplyIfChanged(nameof(IAppConfiguration.OSDRefreshPeriod), refreshPeriodMs,
                () => _config.OSDRefreshPeriod, value => _config.OSDRefreshPeriod = value, changed);
            if (refreshChanged)
                _sensorService.SetOSDInterval(TimeSpan.FromMilliseconds(_config.OSDRefreshPeriod));

            bool metricIntervalChanged = ApplyIfChanged(nameof(IAppConfiguration.MetricInterval), metricIntervalSeconds,
                () => _config.MetricInterval, value => _config.MetricInterval = value, changed);
            if (metricIntervalChanged)
                _onlineMetricService.SetMetricInterval();

            bool activeChanged = ApplyIfChanged(nameof(IAppConfiguration.IsOverlayActive), isOverlayActive,
                () => _config.IsOverlayActive, value => _config.IsOverlayActive = value, changed);
            if (activeChanged)
                _overlayService.IsOverlayActiveStream.OnNext(_config.IsOverlayActive);

            Log.Logger.Information("MCP cfx_set_osd_options changed {count} option(s): {options}",
                changed.Count, string.Join(", ", changed));

            return new SetOsdOptionsResult
            {
                Applied = true,
                ChangedCount = changed.Count,
                ChangedProperties = changed,
                Options = CreateSnapshot(),
            };
        }

        private OsdOptionsInfo CreateSnapshot()
        {
            var renderer = TryGetCurrentRenderer();
            int anchorValue = _config.OsdAnchor;
            string anchor = Enum.IsDefined(typeof(OsdAnchorPosition), anchorValue)
                ? ((OsdAnchorPosition)anchorValue).ToString()
                : "Invalid";

            return new OsdOptionsInfo
            {
                Renderer = renderer?.ToString() ?? "Invalid",
                RendererConfigurationValid = renderer.HasValue,
                EnableHookOverlay = _config.EnableHookOverlay,
                EnableHookFreeOverlay = _config.EnableHookFreeOverlay,
                RtssInstalled = _rtssService.IsRTSSInstalled(),
                IsOverlayActive = _config.IsOverlayActive,
                AutoDisableOverlay = _config.AutoDisableOverlay,
                ShowSystemTimeSeconds = _config.ShowSystemTimeSeconds,
                HideOverlay = _config.HideOverlay,
                HookOverlayUsePresentMonFrametimes = _config.HookOverlayUsePresentMonFrametimes,
                ReplayBufferSizeMs = _config.OsdReplayBufferSize,
                HookFreeRefreshRate = _config.HookFreeRefreshRate,
                OsdCustomPosition = _config.OSDCustomPosition,
                OsdPositionX = _config.OSDPositionX,
                OsdPositionY = _config.OSDPositionY,
                BackgroundOpacity = _config.OsdBackgroundOpacity,
                Anchor = anchor,
                AnchorValue = anchorValue,
                MarginX = _config.OsdMarginX,
                MarginY = _config.OsdMarginY,
                Zoom = _config.OsdZoom,
                UseValueSmoothing = _config.UseOsdValueSmoothing,
                OverlayHotkey = _config.OverlayHotKey,
                OverlayConfigHotkey = _config.OverlayConfigHotKey,
                OverlayPositionHotkey = _config.OverlayPositionHotkey,
                ResetMetricsHotkey = _config.ResetMetricsHotkey,
                RefreshPeriodMs = _config.OSDRefreshPeriod,
                MetricIntervalSeconds = _config.MetricInterval,
            };
        }

        private OsdRendererMode? TryGetCurrentRenderer()
        {
            if (_config.EnableHookOverlay && _config.EnableHookFreeOverlay)
                return null;
            if (_config.EnableHookOverlay)
                return OsdRendererMode.InGame;
            if (_config.EnableHookFreeOverlay)
                return OsdRendererMode.HookFree;
            return OsdRendererMode.Rtss;
        }

        private void ApplyRenderer(OsdRendererMode renderer, List<string> changed)
        {
            switch (renderer)
            {
                case OsdRendererMode.InGame:
                    ApplyIfChanged(nameof(IAppConfiguration.EnableHookOverlay), true,
                        () => _config.EnableHookOverlay, value => _config.EnableHookOverlay = value, changed);
                    ApplyIfChanged(nameof(IAppConfiguration.EnableHookFreeOverlay), false,
                        () => _config.EnableHookFreeOverlay, value => _config.EnableHookFreeOverlay = value, changed);
                    break;
                case OsdRendererMode.HookFree:
                    ApplyIfChanged(nameof(IAppConfiguration.EnableHookFreeOverlay), true,
                        () => _config.EnableHookFreeOverlay, value => _config.EnableHookFreeOverlay = value, changed);
                    ApplyIfChanged(nameof(IAppConfiguration.EnableHookOverlay), false,
                        () => _config.EnableHookOverlay, value => _config.EnableHookOverlay = value, changed);
                    break;
                case OsdRendererMode.Rtss:
                    ApplyIfChanged(nameof(IAppConfiguration.EnableHookOverlay), false,
                        () => _config.EnableHookOverlay, value => _config.EnableHookOverlay = value, changed);
                    ApplyIfChanged(nameof(IAppConfiguration.EnableHookFreeOverlay), false,
                        () => _config.EnableHookFreeOverlay, value => _config.EnableHookFreeOverlay = value, changed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(renderer), renderer, "Unknown OSD renderer.");
            }
        }

        private static bool ApplyIfChanged<T>(string propertyName, T? requestedValue,
            Func<T> getter, Action<T> setter, List<string> changed)
            where T : struct
        {
            if (!requestedValue.HasValue || EqualityComparer<T>.Default.Equals(getter(), requestedValue.Value))
                return false;

            setter(requestedValue.Value);
            changed.Add(propertyName);
            return true;
        }

        private static bool ApplyIfChanged(string propertyName, string requestedValue,
            Func<string> getter, Action<string> setter, List<string> changed)
        {
            if (requestedValue == null || string.Equals(getter(), requestedValue, StringComparison.Ordinal))
                return false;

            setter(requestedValue);
            changed.Add(propertyName);
            return true;
        }

        private static bool HasAnyUpdate(params object[] values)
        {
            foreach (var value in values)
            {
                if (value != null)
                    return true;
            }
            return false;
        }

        private static void ValidateRange(int? value, int minimum, int maximum, string parameterName)
        {
            if (value.HasValue && (value.Value < minimum || value.Value > maximum))
                throw new ArgumentOutOfRangeException(parameterName, value.Value,
                    $"Value must be between {minimum} and {maximum}.");
        }

        private static void ValidatePositive(int? value, string parameterName)
        {
            if (value.HasValue && value.Value <= 0)
                throw new ArgumentOutOfRangeException(parameterName, value.Value, "Value must be greater than zero.");
        }

        private static void ValidateHookFreeRefreshRate(int? value, string parameterName)
        {
            if (!value.HasValue)
                return;

            int rate = value.Value;
            if (rate != 1 && rate != 2 && rate != 5 && rate != 10 && rate != 20 &&
                rate != 30 && rate != 60 && rate != 120)
            {
                throw new ArgumentOutOfRangeException(parameterName, rate,
                    "Value must be one of 1, 2, 5, 10, 20, 30, 60, or 120 Hz.");
            }
        }

        private static void ValidateDefinedEnum<T>(T? value, string parameterName)
            where T : struct, Enum
        {
            if (value.HasValue && !Enum.IsDefined(typeof(T), value.Value))
                throw new ArgumentOutOfRangeException(parameterName, value.Value, "Unknown enum value.");
        }

        private static void ValidateHotkey(string value, string parameterName)
        {
            if (value != null && !CXHotkey.IsValidHotkey(value))
                throw new ArgumentException(
                    "Hotkey must contain a trigger key and up to two modifiers (Control, Shift, or Alt), for example Alt+O.",
                    parameterName);
        }
    }
}
