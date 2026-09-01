namespace CapFrameX.RadeonMonitor
{
    internal sealed record MetricReading(
        string Group,
        string Name,
        string Value,
        string Unit,
        string Raw);
}
