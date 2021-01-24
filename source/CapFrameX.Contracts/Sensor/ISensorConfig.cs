using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorConfig
    {
        bool IsInitialized { get; set; }

        bool IsCapturing { get; set; }

        bool HasConfigFile { get; }

        int SensorEntryCount { get; }

        bool GetSensorIsActive(string identifier);

        void SetSensorIsActive(string identifier, bool isActive);

        bool GetSensorEvaluate(string identifier);

        void SetSensorEvaluate(string identifier, bool isActive);

        Task Save();

        void ResetConfig();
    }
}
