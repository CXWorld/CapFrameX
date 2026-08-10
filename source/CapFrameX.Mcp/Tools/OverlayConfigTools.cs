using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Mcp.Attributes;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class OverlayConfigTools
    {
        private readonly IOverlayService _overlayService;
        private readonly IOverlayEntryProvider _overlayEntryProvider;
        private readonly IAppConfiguration _config;

        public OverlayConfigTools(IOverlayService overlayService,
            IOverlayEntryProvider overlayEntryProvider,
            IAppConfiguration config)
        {
            _overlayService = overlayService;
            _overlayEntryProvider = overlayEntryProvider;
            _config = config;
        }

        [McpServerTool(Name = "cfx_get_overlay_entries",
            Description = "Returns the active OSD overlay entries currently configured in the running CapFrameX instance: " +
                "identifier, group, type (CPU/GPU/RAM/OnlineMetric/CX), whether the entry is enabled, whether it is shown on the overlay, " +
                "current and formatted values, UI capabilities, ordering, fonts, formats, colors, limits, and limit state. Use this to answer " +
                "'what does the overlay show right now?' or 'why is metric X not visible on screen?'.")]
        public OverlayEntriesResult GetOverlayEntries()
        {
            var entries = _overlayService.CurrentOverlayEntries;
            var result = new OverlayEntriesResult();
            if (entries == null) return result;

            for (int index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry == null) continue;
                result.Entries.Add(CreateEntryInfo(entry, index));
            }

            result.EntryCount = result.Entries.Count;
            result.ShownCount = result.Entries.Count(e => e.ShowOnOverlay);
            return result;
        }

        [McpServerTool(Name = "cfx_set_overlay_entry",
            Description = "Updates all user-editable options of one live OSD entry. Omitted arguments stay unchanged. By default the active " +
                "overlay slot is saved after a successful change; pass persist=false for an in-memory-only preview. Use " +
                "cfx_get_overlay_entries first to obtain identifiers, capabilities, and current values.")]
        public async Task<SetOverlayEntryResult> SetOverlayEntry(
            [Description("Entry identifier returned by cfx_get_overlay_entries.")] string identifier,
            [Description("Whether the entry is available to the overlay pipeline.")] bool? isEntryEnabled = null,
            [Description("Whether the entry is visible on the OSD.")] bool? showOnOverlay = null,
            [Description("Editable group label. Empty string removes the label.")] string groupName = null,
            [Description("Whether to show a graph for this entry.")] bool? showGraph = null,
            [Description("Value color as 6- or 8-digit RGB/ARGB hex; empty string resets the default.")] string color = null,
            [Description("Group color as 6- or 8-digit RGB/ARGB hex; empty string resets the default.")] string groupColor = null,
            [Description("Upper numeric limit using invariant decimal notation; empty string disables it.")] string upperLimitValue = null,
            [Description("Lower numeric limit using invariant decimal notation; empty string disables it.")] string lowerLimitValue = null,
            [Description("Upper-limit color as 6- or 8-digit RGB/ARGB hex; empty string resets the default.")] string upperLimitColor = null,
            [Description("Lower-limit color as 6- or 8-digit RGB/ARGB hex; empty string resets the default.")] string lowerLimitColor = null,
            [Description("Value font size/offset percentage used by the OSD formatter.")] int? valueFontSize = null,
            [Description("Group font size/offset percentage used by the OSD formatter.")] int? groupFontSize = null,
            [Description("Number of blank separator rows above this group; must be zero or greater.")] int? groupSeparators = null,
            [Description("Zero-based position in the active overlay entry order.")] int? orderIndex = null,
            [Description("Save the changed live collection to the active overlay configuration slot.")] bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("identifier is required", nameof(identifier));

            if (!HasAnyEntryUpdate(isEntryEnabled, showOnOverlay, groupName, showGraph, color,
                groupColor, upperLimitValue, lowerLimitValue, upperLimitColor, lowerLimitColor,
                valueFontSize, groupFontSize, groupSeparators, orderIndex))
            {
                throw new ArgumentException("Provide at least one overlay entry option to update.");
            }

            var entries = _overlayService.CurrentOverlayEntries ?? Array.Empty<IOverlayEntry>();
            var entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate?.Identifier, identifier, StringComparison.OrdinalIgnoreCase))
                ?? _overlayEntryProvider.GetOverlayEntry(identifier);
            if (entry == null)
                throw new InvalidOperationException($"Overlay entry '{identifier}' not found.");

            if (showOnOverlay == true && !entry.ShowOnOverlayIsEnabled)
                throw new InvalidOperationException($"Overlay entry '{identifier}' cannot be shown in the current configuration.");
            if (showGraph == true && !entry.ShowGraphIsEnabled)
                throw new InvalidOperationException($"Overlay entry '{identifier}' does not support a graph.");
            if (groupSeparators < 0)
                throw new ArgumentOutOfRangeException(nameof(groupSeparators), groupSeparators,
                    "Group separators must be zero or greater.");

            int currentOrderIndex = Array.FindIndex(entries, candidate => ReferenceEquals(candidate, entry));
            if (orderIndex.HasValue)
            {
                if (currentOrderIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Overlay entry '{identifier}' is not part of the live ordered collection.");
                }
                if (orderIndex.Value < 0 || orderIndex.Value >= entries.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(orderIndex), orderIndex.Value,
                        $"Order index must be between 0 and {entries.Length - 1}.");
                }
            }

            ValidateColor(color, nameof(color));
            ValidateColor(groupColor, nameof(groupColor));
            ValidateColor(upperLimitColor, nameof(upperLimitColor));
            ValidateColor(lowerLimitColor, nameof(lowerLimitColor));
            ValidateLimit(upperLimitValue, entry.IsNumeric, nameof(upperLimitValue));
            ValidateLimit(lowerLimitValue, entry.IsNumeric, nameof(lowerLimitValue));

            var changed = new List<string>();
            ApplyEntryValue(nameof(IOverlayEntry.IsEntryEnabled), isEntryEnabled,
                () => entry.IsEntryEnabled, value => entry.IsEntryEnabled = value, changed);
            ApplyEntryValue(nameof(IOverlayEntry.ShowOnOverlay), showOnOverlay,
                () => entry.ShowOnOverlay, value => entry.ShowOnOverlay = value, changed);
            ApplyEntryValue(nameof(IOverlayEntry.GroupName), groupName,
                () => entry.GroupName, value => entry.GroupName = value, changed);
            ApplyEntryValue(nameof(IOverlayEntry.ShowGraph), showGraph,
                () => entry.ShowGraph, value => entry.ShowGraph = value, changed);

            bool formatChanged = false;
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.Color), color,
                () => entry.Color, value => entry.Color = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.GroupColor), groupColor,
                () => entry.GroupColor, value => entry.GroupColor = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.UpperLimitValue), upperLimitValue,
                () => entry.UpperLimitValue, value => entry.UpperLimitValue = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.LowerLimitValue), lowerLimitValue,
                () => entry.LowerLimitValue, value => entry.LowerLimitValue = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.UpperLimitColor), upperLimitColor,
                () => entry.UpperLimitColor, value => entry.UpperLimitColor = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.LowerLimitColor), lowerLimitColor,
                () => entry.LowerLimitColor, value => entry.LowerLimitColor = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.ValueFontSize), valueFontSize,
                () => entry.ValueFontSize, value => entry.ValueFontSize = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.GroupFontSize), groupFontSize,
                () => entry.GroupFontSize, value => entry.GroupFontSize = value, changed);
            formatChanged |= ApplyEntryValue(nameof(IOverlayEntry.GroupSeparators), groupSeparators,
                () => entry.GroupSeparators, value => entry.GroupSeparators = value, changed);
            if (formatChanged)
                entry.FormatChanged = true;

            int resultingOrderIndex = currentOrderIndex;
            if (orderIndex.HasValue && orderIndex.Value != currentOrderIndex)
            {
                _overlayEntryProvider.MoveEntry(currentOrderIndex, orderIndex.Value);
                resultingOrderIndex = orderIndex.Value;
                changed.Add(nameof(OverlayEntryInfo.OrderIndex));
            }

            bool persisted = persist && changed.Count > 0;
            if (persisted)
                await _overlayEntryProvider.SaveOverlayEntriesToJson(_config.OverlayEntryConfigurationFile)
                    .ConfigureAwait(false);

            Log.Logger.Information(
                "MCP cfx_set_overlay_entry {identifier}: changed={changed}, persisted={persisted}, slot={slot}",
                identifier, string.Join(", ", changed), persisted, _config.OverlayEntryConfigurationFile);

            return new SetOverlayEntryResult
            {
                Applied = true,
                Persisted = persisted,
                ChangedCount = changed.Count,
                ChangedProperties = changed,
                Entry = CreateEntryInfo(entry, resultingOrderIndex),
            };
        }

        private static OverlayEntryInfo CreateEntryInfo(IOverlayEntry entry, int orderIndex)
        {
            return new OverlayEntryInfo
            {
                Identifier = entry.Identifier,
                StableIdentifier = entry.StableIdentifier,
                Description = entry.Description,
                GroupName = entry.GroupName,
                OverlayEntryType = entry.OverlayEntryType.ToString(),
                IsEntryEnabled = entry.IsEntryEnabled,
                ShowOnOverlay = entry.ShowOnOverlay,
                ShowOnOverlayIsEnabled = entry.ShowOnOverlayIsEnabled,
                ShowGraph = entry.ShowGraph,
                ShowGraphIsEnabled = entry.ShowGraphIsEnabled,
                Value = entry.Value?.ToString(),
                FormattedValue = SafeGet(() => entry.FormattedValue),
                ValueFormat = entry.ValueFormat,
                ValueUnitFormat = entry.ValueUnitFormat,
                ValueAlignmentAndDigits = entry.ValueAlignmentAndDigits,
                ValueFontSize = entry.ValueFontSize,
                Color = entry.Color,
                FormattedGroupName = SafeGet(() => entry.FormattedGroupName),
                GroupNameFormat = entry.GroupNameFormat,
                GroupColor = entry.GroupColor,
                GroupFontSize = entry.GroupFontSize,
                GroupSeparators = entry.GroupSeparators,
                UpperLimitValue = entry.UpperLimitValue,
                LowerLimitValue = entry.LowerLimitValue,
                UpperLimitColor = entry.UpperLimitColor,
                LowerLimitColor = entry.LowerLimitColor,
                IsNumeric = entry.IsNumeric,
                LastLimitState = entry.LastLimitState.ToString(),
                FormatChanged = entry.FormatChanged,
                OrderIndex = orderIndex,
                SortKey = entry.SortKey,
            };
        }

        private static string SafeGet(Func<string> getter)
        {
            try { return getter(); }
            catch { return null; }
        }

        private static bool HasAnyEntryUpdate(params object[] values)
        {
            return values.Any(value => value != null);
        }

        private static bool ApplyEntryValue<T>(string propertyName, T? requestedValue,
            Func<T> getter, Action<T> setter, List<string> changed)
            where T : struct
        {
            if (!requestedValue.HasValue || EqualityComparer<T>.Default.Equals(getter(), requestedValue.Value))
                return false;

            setter(requestedValue.Value);
            changed.Add(propertyName);
            return true;
        }

        private static bool ApplyEntryValue(string propertyName, string requestedValue,
            Func<string> getter, Action<string> setter, List<string> changed)
        {
            if (requestedValue == null || string.Equals(getter(), requestedValue, StringComparison.Ordinal))
                return false;

            setter(requestedValue);
            changed.Add(propertyName);
            return true;
        }

        private static void ValidateColor(string color, string parameterName)
        {
            if (color == null || color.Length == 0)
                return;
            if ((color.Length != 6 && color.Length != 8) || !color.All(Uri.IsHexDigit))
            {
                throw new ArgumentException(
                    "Color must be empty or contain exactly 6 or 8 hexadecimal RGB/ARGB digits without '#'.",
                    parameterName);
            }
        }

        private static void ValidateLimit(string limit, bool isNumeric, string parameterName)
        {
            if (limit == null || limit.Length == 0)
                return;
            if (!isNumeric)
                throw new InvalidOperationException("Numeric limits can only be assigned to numeric overlay entries.");
            if (!double.TryParse(limit, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException(
                    "Limit must use invariant numeric notation, for example -4, 12.5, or 1e3.",
                    parameterName);
            }
        }
    }
}
