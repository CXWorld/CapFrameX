using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Interop;

namespace CapFrameX.OSD.Integration
{
    /// <summary>Projects the current classic profile into the designer without activating an OSD.</summary>
    public sealed class OverlayClassicPreviewFeed
    {
        private readonly IOverlayEntryProvider _entries;
        private readonly IOverlayService _overlay;
        private readonly IAppConfiguration _configuration;

        public OverlayClassicPreviewFeed(IOverlayEntryProvider entries, IOverlayService overlay,
            IAppConfiguration configuration)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<IReadOnlyList<OsdEntry>> ReadAsync(OverlayTelemetrySnapshot snapshot,
            IReadOnlyList<OverlayTelemetrySource> sources)
        {
            // This reads the shared provider's cached sensor data, not the hardware. It also
            // works with the OSD switched off; no RTSS slot or overlay visibility is changed.
            var profile = await _entries.GetOverlayEntries(false).ConfigureAwait(false);
            var copies = (profile ?? Array.Empty<IOverlayEntry>()).Where(entry => entry != null)
                .Select(entry => entry.Clone()).ToArray();
            var byIdentifier = (sources ?? Array.Empty<OverlayTelemetrySource>())
                .GroupBy(source => source.Identifier, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            bool fresh = snapshot != null && DateTime.UtcNow - snapshot.TimestampUtc <= TimeSpan.FromSeconds(3)
                && snapshot.TimestampUtc <= DateTime.UtcNow.AddSeconds(1);
            try
            {
                foreach (var entry in copies)
                {
                    if (!byIdentifier.TryGetValue(entry.Identifier, out var source)) continue;
                    object value = null;
                    if (fresh) snapshot.MetricValues.TryGetValue(source.Id, out value);
                    // In particular, replace RTSS's frame macros on these detached copies.
                    // Missing readings stay visibly unavailable rather than becoming a fake zero.
                    entry.Value = value ?? "—";
                    entry.IsNumeric = value != null && !source.IsText && value is not string;
                }
                return OverlayEntryAdapter.ToOsdEntries(copies, _configuration.UseRunHistory,
                    _overlay.RunHistory, _overlay.RunHistoryOutlierFlags, _overlay.RunHistoryAggregation);
            }
            finally
            {
                foreach (var entry in copies) entry.Dispose();
            }
        }
    }
}
