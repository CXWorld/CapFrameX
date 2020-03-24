using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IOverlayEntry[] GetSensorOverlayEntries();
        IOverlayEntry GetSensorOverlayEntry(string identifier);
        bool CheckHardwareChanged(List<IOverlayEntry> overlayEntries);
        void StartSensorLogging();
        void StopSensorLogging();
        ISessionSensorData GetSessionSensorData();
        void CloseOpenHardwareMonitor();
        string GetGpuDriverVersion();
        void SetUpdateInterval(TimeSpan timeSpan);
    }
}
