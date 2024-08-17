using CapFrameX.Contracts.Sensor;

namespace CapFrameX.Sensor
{
    public class SensorEntry : ISensorEntry
    {
        public string Identifier { get; set; }

        public object Value { get; set; }

        public string Name { get; set; }

        public string HardwareType { get; set; }

        public string SensorType { get; set; }
    }
}
