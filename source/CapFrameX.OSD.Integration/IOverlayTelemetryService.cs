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

        /// <summary>
        /// A lease for a consumer that shows only some sources, such as a tile design. Online
        /// metrics and CapFrameX values are computed only for the sources a lease needs;
        /// <see cref="AcquirePreview()"/>, which the editors use, needs all of them. The function
        /// is read for every snapshot and returns the current source ids, or null for all.
        /// </summary>
        IDisposable AcquirePreview(Func<IReadOnlyCollection<string>> requiredSourceIds) => AcquirePreview();
    }
}
