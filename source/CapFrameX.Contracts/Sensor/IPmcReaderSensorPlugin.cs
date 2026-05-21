using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface IPmcReaderSensorPlugin : IDisposable
    {
        string Name { get; }

        IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; }

        Task InitializeAsync(IObservable<TimeSpan> updateIntervalStream);

        Task<IEnumerable<ISensorEntry>> GetSensorEntriesAsync();
    }
}
