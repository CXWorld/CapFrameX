using CapFrameX.Contracts.Sensor;

namespace CapFrameX.PmcReader.Plugin
{
    public class PmcReaderSensorEntry : ISensorEntry
    {
        public string Identifier { get; set; }

        public string SortKey { get; set; }

        public string Name { get; set; }

        public object Value { get; set; }

        public string HardwareType { get; set; }

        public string SensorType { get; set; }

        public bool IsPresentationDefault { get; set; }
    }
}
