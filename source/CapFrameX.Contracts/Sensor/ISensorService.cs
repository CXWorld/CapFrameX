using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Contracts;
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
        string GetCpuName();
        string GetGpuName();
        string GetSensorTypeString(IOverlayEntry entry);
        void SetLoggingInterval(TimeSpan timeSpan);
        void SetOSDInterval(TimeSpan timeSpan);
        void ResetSensorOverlayEntries();
        IObservable<IOverlayEntry[]> OnDictionaryUpdated { get; }
    }
}
