using CapFrameX.Contracts.Overlay;
using CapFrameX.Mcp.Attributes;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class OverlayConfigTools
    {
        private readonly IOverlayService _overlayService;

        public OverlayConfigTools(IOverlayService overlayService)
        {
            _overlayService = overlayService;
        }

        [McpServerTool(Name = "cfx_get_overlay_entries",
            Description = "Returns the active OSD overlay entries currently configured in the running CapFrameX instance: " +
                "identifier, group, type (CPU/GPU/RAM/OnlineMetric/CX), whether the entry is enabled, whether it is shown on the overlay, " +
                "current value, format hints, and color/limit configuration. Use this to answer 'what does the overlay show right now?' or " +
                "'why is metric X not visible on screen?'.")]
        public OverlayEntriesResult GetOverlayEntries()
        {
            var entries = _overlayService.CurrentOverlayEntries;
            var result = new OverlayEntriesResult();
            if (entries == null) return result;

            foreach (var entry in entries)
            {
                if (entry == null) continue;

                result.Entries.Add(new OverlayEntryInfo
                {
                    Identifier = entry.Identifier,
                    StableIdentifier = entry.StableIdentifier,
                    Description = entry.Description,
                    GroupName = entry.GroupName,
                    OverlayEntryType = entry.OverlayEntryType.ToString(),
                    IsEntryEnabled = entry.IsEntryEnabled,
                    ShowOnOverlay = entry.ShowOnOverlay,
                    ShowGraph = entry.ShowGraph,
                    Value = entry.Value?.ToString(),
                    ValueUnitFormat = entry.ValueUnitFormat,
                    ValueAlignmentAndDigits = entry.ValueAlignmentAndDigits,
                    Color = entry.Color,
                    UpperLimitValue = entry.UpperLimitValue,
                    LowerLimitValue = entry.LowerLimitValue,
                    SortKey = entry.SortKey,
                });
            }

            result.Entries = result.Entries.OrderBy(e => e.SortKey ?? string.Empty).ToList();
            result.EntryCount = result.Entries.Count;
            result.ShownCount = result.Entries.Count(e => e.ShowOnOverlay);
            return result;
        }
    }
}
