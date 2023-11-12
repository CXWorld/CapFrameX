using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Monitoring.Contracts
{
    public interface ISensorConfig
    {
        bool IsInitialized { get; set; }

        bool IsCapturing { get; set; }

        bool HasConfigFile { get; }

        int SensorEntryCount { get; }

        bool WsSensorsEnabled { get; set; }

        bool WsActiveSensorsEnabled { get; set; }

        int SensorLoggingRefreshPeriod { get; set; }

        bool GetSensorIsActive(string identifier);

        void SetSensorIsActive(string identifier, bool isActive);

        bool GetSensorEvaluate(string identifier);

        void SetSensorEvaluate(string identifier, bool isActive);

        Task Save();

        void ResetConfig();

        void ResetEvaluate();

        Dictionary<string, bool> GetSensorConfigCopy();
    }
}
