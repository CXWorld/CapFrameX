using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; }
        IObservable<TimeSpan> OsdUpdateStream { get; }
        TaskCompletionSource<bool> SensorServiceCompletionSource { get; }
        Func<bool> IsSensorWebsocketActive { get; set; }

        void StartSensorLogging();
        void StopSensorLogging();
        ISessionSensorData2 GetSensorSessionData();
        void CloseOpenHardwareMonitor();
        string GetGpuDriverVersion();
        string GetCpuName();
        string GetGpuName();
        string GetSensorTypeString(string identifier);
        void SetLoggingInterval(TimeSpan timeSpan);
        void SetOSDInterval(TimeSpan timeSpan);
        Task<IEnumerable<ISensorEntry>> GetSensorEntries();
    }
}
