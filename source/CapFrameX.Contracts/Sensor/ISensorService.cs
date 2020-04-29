using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Contracts;
using OpenHardwareMonitor.Hardware;
using System;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IOverlayEntry GetSensorOverlayEntry(string identifier);
        void StartSensorLogging();
        void StopSensorLogging();
        ISessionSensorData GetSessionSensorData();
        void CloseOpenHardwareMonitor();
        string GetGpuDriverVersion();
        void SetLoggingInterval(TimeSpan timeSpan);
        void SetOSDInterval(TimeSpan timeSpan);
        IObservable<IOverlayEntry[]> OnDictionaryUpdated { get; }
    }
}
