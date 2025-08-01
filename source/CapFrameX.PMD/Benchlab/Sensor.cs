using System.Collections;
using System.Collections.Generic;

namespace CapFrameX.PMD.Benchlab
{
    public enum SensorType
    {
        Temperature,
        Humidity,
        Duty,
        Revolutions,
        Voltage,
        Current,
        Power,
        Clock,
        Usage,
        Dummy,
        Other
    }

    public class SensorSample
    {
        public long TimeStamp { get; set; }
        public IList<Sensor> Sensors { get; set; }
    }

    public class Sensor
    {
        public Sensor(int id, string shortName, string name, SensorType type)
        {
            Id = id;
            ShortName = shortName;
            Name = name;
            Type = type;
        }

        public Sensor()
        {
            Id = 0;
            ShortName = string.Empty;
            Name = string.Empty;
            Type = SensorType.Other;
        }

        public int Id { get; set; }
        public string ShortName { get; set; }
        public string Name { get; set; }
        public SensorType Type { get; set; }
        public double Value { get; set; }
        public bool IsValid { get; set; }
    }
}
