using CapFrameX.Contracts.Overlay;
using CapFrameX.Data.Session.Contracts;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorService
    {
        IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; }
        IObservable<TimeSpan> OsdUpdateStream { get; }
        TaskCompletionSource<bool> SensorServiceCompletionSource { get; }
        Func<bool> IsSensorWebsocketActive { get; set; }
        Subject<bool> IsLoggingActiveStream { get; }
        void StartSensorLogging();
        Task StopSensorLogging();
        ISessionSensorData2 GetSensorSessionData();
        void ShutdownSensorService();
        string GetGpuDriverVersion();
        string GetCpuName();
        string GetGpuName();
        /// <summary>Cached SPD module manufacturers, retaining one entry per identified DIMM.</summary>
        IReadOnlyList<string> GetMemoryManufacturers();
        ECpuVendor GetCpuVendor();
        EGpuVendor GetGpuVendor();
        string GetSensorTypeString(EOverlayEntryType entryType, string stableIdentifier);
        void SetLoggingInterval(TimeSpan timeSpan);
        void SetOSDInterval(TimeSpan timeSpan);
        Task<IEnumerable<ISensorEntry>> GetSensorEntries();
        IEnumerable<string> GetDetectedGpus();
    }
}
