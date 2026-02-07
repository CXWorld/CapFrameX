using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Monitoring.Contracts
{
    public interface ISensorConfig
    {
        bool IsCapturing { get; set; }

        bool HasConfigFile { get; }

        int SensorEntryCount { get; }

        bool WsSensorsEnabled { get; set; }

        bool WsActiveSensorsEnabled { get; set; }

        int SensorLoggingRefreshPeriod { get; set; }

        bool IsSelectedForLogging(string identifier);

        void SelectForLogging(string identifier, bool isActive);

        bool IsSelectedForOverlay(string identifier);

        void SelectForOverlay(string identifier, bool isActive);

        bool GetSensorEvaluate(string identifier);

        Task Save();

        void ResetConfig();

        void ResetEvaluate();

        Dictionary<string, bool> GetSensorConfigCopy();
    }
}
