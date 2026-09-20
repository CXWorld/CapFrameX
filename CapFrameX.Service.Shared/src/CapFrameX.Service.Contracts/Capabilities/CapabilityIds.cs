namespace CapFrameX.Service.Contracts.Capabilities;

/// <summary>
/// Capability identifiers both services report through <c>/api/capabilities</c>. The frontend
/// switches on these instead of on the operating system, so a feature the running service cannot
/// offer is shown with the reason the service gave rather than silently missing.
/// </summary>
public static class CapabilityIds
{
    /// <summary>Frame capture is available at all.</summary>
    public const string Capture = "capture";

    /// <summary>Sensor telemetry is available at all.</summary>
    public const string Telemetry = "telemetry";

    /// <summary>Telemetry that needs privileged hardware access, such as PawnIO or RAPL.</summary>
    public const string TelemetryLowLevel = "telemetry.lowlevel";

    /// <summary>Global hotkeys can be registered.</summary>
    public const string Hotkeys = "hotkeys";

    /// <summary>In-game overlay is available at all.</summary>
    public const string Overlay = "overlay";

    /// <summary>The service can render an overlay preview without a running game.</summary>
    public const string OverlayPreview = "overlay.preview";

    /// <summary>RTSS integration; Windows only.</summary>
    public const string Rtss = "overlay.rtss";

    /// <summary>Power measurement device integration; Windows only.</summary>
    public const string Pmd = "pmd";

    /// <summary>Prefix for per-frame-metric capabilities, for example <c>frames.gpuBusy</c>.</summary>
    public const string FrameMetricPrefix = "frames.";

    /// <summary>Capability id for one optional frame metric.</summary>
    /// <param name="metric">Metric name in camel case, for example <c>gpuBusy</c>.</param>
    public static string FrameMetric(string metric) => FrameMetricPrefix + metric;
}
