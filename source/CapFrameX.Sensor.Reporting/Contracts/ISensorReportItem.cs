namespace CapFrameX.Sensor.Reporting.Contracts
{
    public interface ISensorReportItem
    {
        string Name { get; set; }
        double MinValue { get; set; }
        double AverageValue { get; set; }
        double MaxValue { get; set; }
    }
}
