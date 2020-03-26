using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Contracts;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IOverlayEntry[] GetSensorOverlayEntries();
        IOverlayEntry GetSensorOverlayEntry(string identifier);
        void UpdateSensors();
        bool CheckHardwareChanged(IList<IOverlayEntry> overlayEntries);
        void StartSensorLogging();
        void StopSensorLogging();
        ISessionSensorData GetSessionSensorData();
        void CloseOpenHardwareMonitor();
        string GetGpuDriverVersion();
    }
}
