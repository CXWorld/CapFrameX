namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorEntry
    {
        string Identifier { get; set; }

        string Name { get; set; } 

        object Value { get; set; }

        string HardwareType { get; set; }

        string SensorType { get; set; }

        bool IsPresentationDefault { get; set; }
    }
}
