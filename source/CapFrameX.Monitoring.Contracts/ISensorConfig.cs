using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Monitoring.Contracts
{
    public interface ISensorConfig
    {
        bool IsCapturing { get; set; }

        bool IsSensorLoggingActive { get; set; }

        bool HasConfigFile { get; }

        int SensorEntryCount { get; }

        bool WsSensorsEnabled { get; set; }

        bool WsActiveSensorsEnabled { get; set; }

        /// <summary>
        /// Indicates whether the current OSD layout contains at least one hardware-sensor
        /// identifier. Online metrics and other synthetic OSD rows do not count.
        /// </summary>
        bool HasSelectedOverlaySensors { get; }

        /// <summary>
        /// Indicates whether the current OSD layout contains a selected PmcReader sensor.
        /// </summary>
        bool HasSelectedPmcOverlaySensors { get; }

        /// <summary>
        /// Indicates whether the capture configuration contains a selected PmcReader sensor.
        /// </summary>
        bool HasSelectedPmcLoggingSensors { get; }

        /// <summary>
        /// Forces evaluation of every sensor regardless of the logging/overlay selection.
        /// Set while a live-telemetry consumer without its own selection (the Info tab)
        /// is visible; the flag also keeps the sensor snapshot stream running.
        /// </summary>
        bool EvaluateAllSensors { get; set; }

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

        bool IsSelectedForLoggingByStableId(string stableIdentifier);

        void SelectStableForLogging(string stableIdentifier, bool isActive);

        Dictionary<string, bool> GetStableSensorConfigCopy();
    }
}
