using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>The editor's view of the telemetry catalog; the designer and automation must agree on it.</summary>
    public static class OverlayDesignSources
    {
        public static OsdTelemetrySource[] ToEditorSources(IEnumerable<OverlayTelemetrySource> sources) =>
            (sources ?? Enumerable.Empty<OverlayTelemetrySource>()).Where(source => source != null).Select(source => new OsdTelemetrySource
            {
                Key = source.Id,
                Name = source.Name,
                Category = source.Category,
                Subcategory = source.Subcategory,
                DeviceName = source.HardwareName,
                Unit = source.Unit,
                SemanticKey = source.SemanticKey,
                DeviceKey = source.DeviceKey,
                GroupKey = source.GroupKey,
                GroupName = source.GroupName,
                GroupOrder = source.GroupOrder,
                CoreType = source.CoreType,
                CoreIndex = source.CoreIndex,
                ThreadIndex = source.ThreadIndex,
                IsAvailable = source.IsAvailable,
                IsText = source.IsText,
                SuggestedValueLines = source.Identifier == "RunHistory" ? 8 : 1
            }).ToArray();
    }
}
