using CapFrameX.Sensor.Reporting.Contracts;

namespace CapFrameX.Sensor.Reporting.Data
{
    public class SensorReportItem : ISensorReportItem
    {
        public string Name { get; set; }

        public double MinValue { get; set; }

        public double AverageValue { get; set; }

        public double MaxValue { get; set; }

        public int RoundingDigits { get; set; }
    }
}
