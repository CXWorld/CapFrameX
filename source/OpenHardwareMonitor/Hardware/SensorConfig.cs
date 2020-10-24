using CapFrameX.Contracts.Sensor;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Hardware
{
    public class SensorConfig : ISensorConfig
    {
        public bool IsInitialized { get; set; }

        private readonly Dictionary<string, bool> _activeSensorsDict 
            = new Dictionary<string, bool>();

        public SensorConfig()
        {
            IsInitialized = false;
        }

        public bool GetSensorIsActive(string identifier)
        {
            if (!IsInitialized)
                return true;

            bool isActive = false;
            if (_activeSensorsDict.ContainsKey(identifier))
                isActive = _activeSensorsDict[identifier];

            return isActive;
        }

        public void SetSensorIsActive(string identifier, bool isActive)
        {
            if (_activeSensorsDict.ContainsKey(identifier))
                _activeSensorsDict[identifier] = isActive;
            else
                _activeSensorsDict.Add(identifier, isActive);
        }
    }
}
