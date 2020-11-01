using CapFrameX.Contracts.Sensor;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Hardware
{
    /// <summary>
    /// Already implemented: 
    /// GPU Power (Nvidia/AMD)
    /// GPU Memory (Dedicated/Shared) Usage (Nvidia/AMD)
    /// </summary>
    public class SensorConfig : ISensorConfig
    {
        public bool IsInitialized { get; set; } = false;

        public bool GlobalIsActivated { get; set; } = false;

        private readonly Dictionary<string, bool> _activeSensorsDict
            = new Dictionary<string, bool>();

        public bool GetSensorIsActive(string identifier)
        {
            if (!IsInitialized || GlobalIsActivated)
                return true;

            bool isActive = false;
            if (_activeSensorsDict.ContainsKey(identifier))
                isActive = _activeSensorsDict[identifier];

            return isActive;
        }

        public void SetSensorIsActive(string identifier, bool isActive)
        {
            isActive = !IsInitialized || isActive;

            if (_activeSensorsDict.ContainsKey(identifier))
                _activeSensorsDict[identifier] = isActive;
            else
                _activeSensorsDict.Add(identifier, isActive);
        }
    }
}
