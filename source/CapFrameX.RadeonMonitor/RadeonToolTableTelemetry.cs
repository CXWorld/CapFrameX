namespace CapFrameX.RadeonMonitor
{
    internal sealed record RadeonToolTableTelemetry(
        IReadOnlyList<MetricReading> Readings,
        int InvalidValueCount);
}
