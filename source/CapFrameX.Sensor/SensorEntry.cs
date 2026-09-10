using CapFrameX.Contracts.Sensor;
using LibreHardwareMonitor.Hardware;

namespace CapFrameX.Sensor
{
    public class SensorEntry : ISensorEntry
    {
        public string Identifier { get; set; }

        public string SortKey { get; set; }

        public object Value { get; set; }

        public string Name { get; set; }

        public string HardwareType { get; set; }

        public string SensorType { get; set; }

        public bool IsPresentationDefault { get; set; }

        public string HardwareName { get; set; }

        /// <summary>
        /// The one mapping from a hardware-monitor sensor onto the entry the sensor service publishes.
        /// </summary>
        public static SensorEntry FromSensor(ISensor sensor)
        {
            return new SensorEntry()
            {
                Identifier = sensor.Identifier.ToString(),
                SortKey = sensor.PresentationSortKey,
                Value = sensor.Value,
                Name = sensor.Name,
                SensorType = sensor.SensorType.ToString(),
                HardwareType = sensor.Hardware.HardwareType.ToString(),
                HardwareName = sensor.Hardware.Name,
                IsPresentationDefault = sensor.IsPresentationDefault
            };
        }
    }
}
