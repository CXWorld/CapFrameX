using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IOverlayEntry GetSensorOverlayEntry(string identifier);
        bool CheckHardwareChanged(IList<IOverlayEntry> overlayEntries);
        void StartSensorLogging();
        void StopSensorLogging();
        ISessionSensorData GetSessionSensorData();
        void CloseOpenHardwareMonitor();
        string GetGpuDriverVersion();
        void SetLoggingInterval(TimeSpan timeSpan);
        void SetOSDInterval(TimeSpan timeSpan);
        ISubject<IOverlayEntry[]> OnDictionaryUpdated { get; }
    }
}
