namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorConfig
    {
        bool IsInitialized { get; set; }

        bool GetSensorIsActive(string identifier);

        void SetSensorIsActive(string identifier, bool isActive);
    }
}
