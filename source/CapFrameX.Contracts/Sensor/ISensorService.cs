using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; }
        void StartSensorLogging();
        void StopSensorLogging();
        ISessionSensorData GetSessionSensorData();
        void CloseOpenHardwareMonitor();
        string GetGpuDriverVersion();
        string GetCpuName();
        string GetGpuName();
        string GetSensorTypeString(string identifier);
        void SetLoggingInterval(TimeSpan timeSpan);
        void SetOSDInterval(TimeSpan timeSpan);
        IEnumerable<ISensorEntry> GetSensorEntries();
    }
}
