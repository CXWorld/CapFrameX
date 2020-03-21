using CapFrameX.Contracts.Sensor;

namespace CapFrameX.Sensor
{
    public class SensorReportItem : ISensorReportItem
    {
        public string Name { get; set; }

        public double MinValue { get; set; }

        public double AverageValue { get; set; }

        public double MaxValue { get; set; }
    }
}
