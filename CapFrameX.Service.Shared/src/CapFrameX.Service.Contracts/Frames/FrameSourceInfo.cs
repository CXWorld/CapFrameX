namespace CapFrameX.Service.Contracts.Frames;

/// <summary>
/// Identity and capability of a frame source. <see cref="Name"/> and <see cref="Version"/> are
/// written into every record so analysis can explain why a metric is missing.
/// </summary>
/// <param name="Name">Short source name, for example <c>PresentMon</c> or <c>CXVulkanLayer</c>.</param>
/// <param name="Version">Version of the underlying tool or layer.</param>
/// <param name="AvailableMetrics">Optional metrics this source can deliver.</param>
public sealed record FrameSourceInfo(string Name, string Version, FrameMetrics AvailableMetrics);
