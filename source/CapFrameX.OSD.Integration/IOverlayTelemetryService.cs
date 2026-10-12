using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.OSD.Integration
{
    public interface IOverlayTelemetryService : IDisposable
    {
        Task<IReadOnlyList<OverlayTelemetrySource>> GetSourcesAsync();

        IObservable<OverlayTelemetrySnapshot> Snapshots { get; }

        IDisposable AcquirePreview();
    }
}
