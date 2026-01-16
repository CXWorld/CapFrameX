using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapFrameX.Overlay
{
    public class OverlayTemplateService : IOverlayTemplateService
    {
        // Vendor Group Colors (ARGB hex)
        private const string NVIDIA_GREEN = "FF76B900";
        private const string AMD_RED = "FFED1C24";
        private const string INTEL_BLUE = "FF0071C5";
        private const string INTEL_ARC = "FF9865EB";

        // Value Colors (ARGB hex)
        private const string VALUE_YELLOW = "FFFFD700";
        private const string VALUE_WHITE = "FFFFFFFF";

        // RAM Colors (ARGB hex)
        private const string RAM_GROUP_COLOR = "FF87CEEB";  // Sky Blue
        private const string RAM_VALUE_COLOR = "FFFFFFFF";

        // Metric Colors (ARGB hex - Lime Green)
        private const string METRIC_COLOR = "FF9CD200";

        // Framerate Colors (ARGB hex - Yellow-Green)
        private const string FRAMERATE_COLOR = "FFAEEA00";

        // Latency Colors (ARGB hex)
        private const string LATENCY_GROUP_COLOR = "FF2297F3";
        private const string LATENCY_VALUE_COLOR = "FFFFD700";

        private readonly ISensorService _sensorService;
        private Dictionary<string, StoredEntryState> _storedState;

        public EGpuVendor DetectedGpuVendor { get; private set; }
        public ECpuVendor DetectedCpuVendor { get; private set; }
        public string ShortGpuName { get; private set; }
        public bool HasStoredState => _storedState != null && _storedState.Count > 0;

        public OverlayTemplateService(ISensorService sensorService)
        {
            _sensorService = sensorService;
            DetectHardware();
        }

        private void DetectHardware()
        {
            var gpuName = _sensorService.GetGpuName() ?? string.Empty;
            DetectedGpuVendor = _sensorService.GetGpuVendor();

            // Create short GPU name (e.g., "RTX 4090", "RX 7900 XTX", "Arc A770")
            ShortGpuName = CreateShortGpuName(gpuName);

            DetectedCpuVendor = _sensorService.GetCpuVendor();
        }

        private string CreateShortGpuName(string fullGpuName)
        {
            if (string.IsNullOrWhiteSpace(fullGpuName))
                return "GPU";

            // Try to extract model name patterns
            var name = RemoveParenthesizedText(fullGpuName).Trim();

            // NVIDIA: "NVIDIA GeForce RTX 4090" -> "RTX 4090"
            var rtxIndex = name.IndexOf("RTX", StringComparison.OrdinalIgnoreCase);
            if (rtxIndex >= 0)
                return name.Substring(rtxIndex);

            var gtxIndex = name.IndexOf("GTX", StringComparison.OrdinalIgnoreCase);
            if (gtxIndex >= 0)
                return name.Substring(gtxIndex);

            // AMD: "AMD Radeon RX 7900 XTX" -> "RX 7900 XTX"
            var rxIndex = name.IndexOf("RX ", StringComparison.OrdinalIgnoreCase);
            if (rxIndex >= 0)
                return name.Substring(rxIndex);

            // Intel: "Intel Arc A770" -> "Arc A770"
            var arcIndex = name.IndexOf("Arc", StringComparison.OrdinalIgnoreCase);
            if (arcIndex >= 0)
                return name.Substring(arcIndex);

            // Fallback: return last part or truncate
            var parts = name.Split(' ');
            if (parts.Length > 2)
                return string.Join(" ", parts.Skip(parts.Length - 2));

            return name.Length > 20 ? name.Substring(0, 20) : name;
        }

        private static string RemoveParenthesizedText(string gpuName)
        {
            if (string.IsNullOrWhiteSpace(gpuName))
                return gpuName;

            var cleaned = Regex.Replace(gpuName, "\\s*\\([^)]*\\)", string.Empty);
            cleaned = Regex.Replace(cleaned, "\\bLaptop\\s+GPU\\b", "Mobile", RegexOptions.IgnoreCase);
            return Regex.Replace(cleaned, "\\s{2,}", " ").Trim();
        }

        public void ApplyTemplate(EOverlayTemplate template, IEnumerable<IOverlayEntry> entries)
        {
            var entryList = entries.ToList();

            // First, disable all entries and reset separators
            foreach (var entry in entryList)
            {
                entry.ShowOnOverlay = false;
                entry.GroupSeparators = 0;
            }

            switch (template)
            {
                case EOverlayTemplate.Basic:
                    ApplyBasicTemplate(entryList);
                    break;
                case EOverlayTemplate.Detailed:
                    ApplyDetailedTemplate(entryList);
                    break;
                case EOverlayTemplate.Enthusiast:
                    ApplyEnthusiastTemplate(entryList);
                    break;
            }
        }

        private void ApplyBasicTemplate(List<IOverlayEntry> entries)
        {
            var gpuGroupColor = GetGpuGroupColor();
            var cpuGroupColor = GetCpuGroupColor();

            // 1. GPU Section - clocks with short GPU name, load, temp
            EnableByDescription(entries, "GPU Core (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName, TemplateSortKey(10, 1));
            EnableByDescription(entries, "GPU Memory (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName, TemplateSortKey(10, 2));
            EnableByDescription(entries, "GPU Core (%)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Load", TemplateSortKey(10, 3));
            EnableByDescription(entries, "GPU Core (°C)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Temp", TemplateSortKey(10, 4));

            // 2. CPU Section - Clock, Total Load, Package Power+Temp
            EnableByGroupName(entries, "CPU Clock", cpuGroupColor, VALUE_YELLOW, 1, TemplateSortKey(20, 1));
            EnableByDescription(entries, "CPU Max (MHz)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Max", TemplateSortKey(20, 2));
            EnableByDescriptionContains(entries, "CPU Total (%)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Total", TemplateSortKey(20, 3));
            EnableByDescriptionContains(entries, "CPU Package (W)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Package", TemplateSortKey(20, 4));
            EnableByDescriptionContains(entries, "CPU Package", "°C", cpuGroupColor, VALUE_YELLOW, 0, "CPU Package", TemplateSortKey(20, 5));

            // 3. Metrics Section (with blank line)
            EnableByIdentifier(entries, "OnlineAverage", METRIC_COLOR, METRIC_COLOR, 1, null, null, TemplateSortKey(30, 1));
            EnableByIdentifier(entries, "Online1PercentLow", METRIC_COLOR, METRIC_COLOR, 0, null, null, TemplateSortKey(30, 2));

            // 4. Framerate Section (at the end, with blank line, no graph)
            EnableByIdentifier(entries, "Framerate", FRAMERATE_COLOR, FRAMERATE_COLOR, 1, "<APP>", false, TemplateSortKey(40, 1));
            EnableByIdentifier(entries, "Frametime", FRAMERATE_COLOR, FRAMERATE_COLOR, 0, "<APP>", null, TemplateSortKey(40, 2));

            EnsureCpuSectionSeparator(entries);
        }

        private void ApplyDetailedTemplate(List<IOverlayEntry> entries)
        {
            var gpuGroupColor = GetGpuGroupColor();
            var cpuGroupColor = GetCpuGroupColor();

            // 1. GPU Section - clocks with short GPU name, load, temp, power, VRAM
            EnableByDescription(entries, "GPU Core (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName, TemplateSortKey(10, 1));
            EnableByDescription(entries, "GPU Memory (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName, TemplateSortKey(10, 2));
            EnableByDescription(entries, "GPU Core (%)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Load", TemplateSortKey(10, 3));
            EnableByDescription(entries, "GPU Core (°C)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Temp", TemplateSortKey(10, 4));
            EnableByDescription(entries, "GPU Power (W)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Power", TemplateSortKey(10, 5));
            EnableByDescription(entries, "GPU Memory Dedicated (GB)", gpuGroupColor, VALUE_YELLOW, 0, "VRAM Used", TemplateSortKey(10, 6));
            EnableByDescription(entries, "GPU Memory Shared (GB)", gpuGroupColor, VALUE_YELLOW, 0, "VRAM Used", TemplateSortKey(10, 7));

            // 2. CPU Info (with blank line)
            EnableByIdentifier(entries, "CustomCPU", cpuGroupColor, VALUE_WHITE, 1, "CPU Model", null, TemplateSortKey(20, 1));

            // 3. CPU entries - Clock, Total, Package
            EnableByGroupName(entries, "CPU Clock", cpuGroupColor, VALUE_YELLOW, 0, TemplateSortKey(30, 1));
            EnableByDescription(entries, "CPU Max (MHz)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Max", TemplateSortKey(30, 2));
            EnableByDescriptionContains(entries, "CPU Total (%)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Total", TemplateSortKey(30, 3));
            EnableByDescriptionContains(entries, "CPU Package (W)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Package", TemplateSortKey(30, 4));
            EnableByDescriptionContains(entries, "CPU Package", "°C", cpuGroupColor, VALUE_YELLOW, 0, "CPU Package", TemplateSortKey(30, 5));

            // 4. RAM Section (with blank line)
            EnableByIdentifier(entries, "CustomRAM", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 1, null, null, TemplateSortKey(40, 1));

            // 5. Metrics Section (with blank line)
            EnableByIdentifier(entries, "OnlineAverage", METRIC_COLOR, METRIC_COLOR, 1, null, null, TemplateSortKey(50, 1));
            EnableByIdentifier(entries, "Online1PercentLow", METRIC_COLOR, METRIC_COLOR, 0, null, null, TemplateSortKey(50, 2));

            // 6. Framerate Section (at the end, with blank line, no graph)
            EnableByIdentifier(entries, "Framerate", FRAMERATE_COLOR, FRAMERATE_COLOR, 1, "<APP>", false, TemplateSortKey(60, 1));
            EnableByIdentifier(entries, "Frametime", FRAMERATE_COLOR, FRAMERATE_COLOR, 0, "<APP>", null, TemplateSortKey(60, 2));

            EnsureCpuSectionSeparator(entries);
        }

        private void ApplyEnthusiastTemplate(List<IOverlayEntry> entries)
        {
            var gpuGroupColor = GetGpuGroupColor();
            var cpuGroupColor = GetCpuGroupColor();

            // 1. GPU Section - clocks with short GPU name, load, temp, hot spot, power, VRAM
            EnableByDescription(entries, "GPU Core (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName, TemplateSortKey(10, 1));
            EnableByDescription(entries, "GPU Memory (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName, TemplateSortKey(10, 2));
            EnableByDescription(entries, "GPU Core (%)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Load", TemplateSortKey(10, 3));
            EnableByDescription(entries, "GPU Core (°C)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Temp", TemplateSortKey(10, 4));
            EnableByDescription(entries, "GPU Memory Junction (°C)", gpuGroupColor, VALUE_YELLOW, 0, "VRAM Hot Spot", TemplateSortKey(10, 5));
            EnableByDescription(entries, "GPU Power (W)", gpuGroupColor, VALUE_YELLOW, 0, "GPU Power", TemplateSortKey(10, 6));
            EnableByDescription(entries, "GPU Memory Dedicated (GB)", gpuGroupColor, VALUE_YELLOW, 0, "VRAM Used", TemplateSortKey(10, 7));
            EnableByDescription(entries, "GPU Memory Shared (GB)", gpuGroupColor, VALUE_YELLOW, 0, "VRAM Used", TemplateSortKey(10, 8));

            // 2. CPU Info (with blank line)
            EnableByIdentifier(entries, "CustomCPU", cpuGroupColor, VALUE_WHITE, 1, "CPU Model", null, TemplateSortKey(20, 1));

            // 3. CPU entries - all core clocks and loads, Package
            var coreGroups = GetCoreGroups(entries);
            if (coreGroups.Count > 0)
            {
                for (int i = 0; i < coreGroups.Count; i++)
                {
                    var coreGroup = coreGroups[i];
                    var sortIndex = i + 1;
                    EnableCoreGroupClock(entries, coreGroup.GroupName, cpuGroupColor, VALUE_YELLOW, 0, TemplateSortKey(30, sortIndex, 1));
                    EnableCoreGroupLoad(entries, coreGroup.GroupName, cpuGroupColor, VALUE_YELLOW, 0, TemplateSortKey(30, sortIndex, 2));
                    EnableByGroupNameAndDescriptionContains(entries, coreGroup.GroupName, "Thread #1", cpuGroupColor, VALUE_YELLOW, 0, TemplateSortKey(30, sortIndex, 3));
                    EnableByGroupNameAndDescriptionContains(entries, coreGroup.GroupName, "Thread #2", cpuGroupColor, VALUE_YELLOW, 0, TemplateSortKey(30, sortIndex, 4));
                }
            }
            else
            {
                for (int i = 1; i <= 16; i++)
                {
                    EnableByDescription(entries, $"Core #{i} (MHz)", cpuGroupColor, VALUE_YELLOW, 0, $"Core #{i}", TemplateSortKey(30, i, 1));
                    EnableCpuCoreLoad(entries, i, cpuGroupColor, VALUE_YELLOW, 0, TemplateSortKey(30, i, 2));
                    EnableByDescriptionContains(entries, $"Core #{i}", "Thread #1 (%)", cpuGroupColor, VALUE_YELLOW, 0, $"Core #{i}", TemplateSortKey(30, i, 3));
                    EnableByDescriptionContains(entries, $"Core #{i}", "Thread #2 (%)", cpuGroupColor, VALUE_YELLOW, 0, $"Core #{i}", TemplateSortKey(30, i, 4));
                }
            }

            EnableByDescription(entries, "CPU Max (MHz)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Max", TemplateSortKey(30, 98));
            EnableByDescriptionContains(entries, "CPU Package (W)", cpuGroupColor, VALUE_YELLOW, 0, "CPU Package", TemplateSortKey(30, 99, 1));
            EnableByDescriptionContains(entries, "CPU Package", "°C", cpuGroupColor, VALUE_YELLOW, 0, "CPU Package", TemplateSortKey(30, 99, 2));

            // 4. RAM Section (with blank line) - DDR info, DIMM temps, RAM used
            EnableByIdentifier(entries, "CustomRAM", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 1, null, null, TemplateSortKey(40, 1));
            EnableByDescription(entries, "DIMM #0 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0, "DIMM #0", TemplateSortKey(50, 1));
            EnableByDescription(entries, "DIMM #1 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0, "DIMM #1", TemplateSortKey(50, 2));
            EnableByDescription(entries, "DIMM #2 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0, "DIMM #2", TemplateSortKey(50, 3));
            EnableByDescription(entries, "DIMM #3 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0, "DIMM #3", TemplateSortKey(50, 4));
            EnableByDescription(entries, "DIMM #4 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0, "DIMM #4", TemplateSortKey(50, 5));
            EnableByDescription(entries, "RAM Used (GB)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0, "RAM Used", TemplateSortKey(50, 99));

            // 5. Latency Section (with blank line)
            var pcLatencyEntry = entries.FirstOrDefault(e => e.Identifier == "OnlinePcLatency");
            var hasPcLatency = pcLatencyEntry != null && pcLatencyEntry.IsEntryEnabled;
            if (hasPcLatency)
                EnableByIdentifier(entries, "OnlinePcLatency", LATENCY_GROUP_COLOR, LATENCY_VALUE_COLOR, 1, null, null, TemplateSortKey(60, 1));

            var animationErrorSeparators = hasPcLatency ? 0 : 1;
            EnableByIdentifier(entries, "OnlineAnimationError", LATENCY_GROUP_COLOR, LATENCY_VALUE_COLOR, animationErrorSeparators, null, null, TemplateSortKey(60, 2));

            // 6. Metrics Section (with blank line)
            EnableByIdentifier(entries, "OnlineAverage", METRIC_COLOR, METRIC_COLOR, 1, null, null, TemplateSortKey(70, 1));
            EnableByIdentifier(entries, "Online1PercentLow", METRIC_COLOR, METRIC_COLOR, 0, null, null, TemplateSortKey(70, 2));

            // 7. Framerate Section (at the end, with blank line, no graph)
            EnableByIdentifier(entries, "Framerate", FRAMERATE_COLOR, FRAMERATE_COLOR, 1, "<APP>", false, TemplateSortKey(80, 1));
            EnableByIdentifier(entries, "Frametime", FRAMERATE_COLOR, FRAMERATE_COLOR, 0, "<APP>", null, TemplateSortKey(80, 2));

            EnsureCpuSectionSeparator(entries);
        }

        private string GetGpuGroupColor()
        {
            switch (DetectedGpuVendor)
            {
                case EGpuVendor.Nvidia:
                    return NVIDIA_GREEN;
                case EGpuVendor.Amd:
                    return AMD_RED;
                case EGpuVendor.Intel:
                    return INTEL_ARC;
                default:
                    return VALUE_WHITE;
            }
        }

        private string GetCpuGroupColor()
        {
            switch (DetectedCpuVendor)
            {
                case ECpuVendor.Intel:
                    return INTEL_BLUE;
                case ECpuVendor.Amd:
                    return AMD_RED;
                default:
                    return VALUE_WHITE;
            }
        }

        private bool EnableByIdentifier(List<IOverlayEntry> entries, string identifier, string groupColor, string valueColor, int separators, string groupName = null, bool? showGraph = null, string sortKey = null)
        {
            var entry = entries.FirstOrDefault(e => e.Identifier == identifier);
            if (entry != null)
            {
                entry.ShowOnOverlay = true;
                entry.GroupColor = groupColor;
                entry.Color = valueColor;
                if (groupName != null)
                    entry.GroupName = groupName;
                entry.GroupSeparators = separators;
                if (showGraph.HasValue)
                    entry.ShowGraph = showGraph.Value;
                if (sortKey != null)
                    entry.SortKey = sortKey;
                return true;
            }

            return false;
        }

        private void EnableByDescription(List<IOverlayEntry> entries, string description, string groupColor, string valueColor, int separators, string groupName = null, string sortKey = null)
        {
            var entry = entries.FirstOrDefault(e =>
                e.Description != null &&
                e.Description.Equals(description, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                entry.ShowOnOverlay = true;
                entry.GroupColor = groupColor;
                entry.Color = valueColor;
                if (groupName != null)
                    entry.GroupName = groupName;
                entry.GroupSeparators = separators;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }
        }

        private void EnableByDescriptionContains(List<IOverlayEntry> entries, string contains, string groupColor, string valueColor, int separators, string groupName = null, string sortKey = null)
        {
            var entry = entries.FirstOrDefault(e =>
                e.Description != null &&
                e.Description.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);

            if (entry != null)
            {
                entry.ShowOnOverlay = true;
                entry.GroupColor = groupColor;
                entry.Color = valueColor;
                if (groupName != null)
                    entry.GroupName = groupName;
                entry.GroupSeparators = separators;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }
        }

        private void EnableByDescriptionContains(List<IOverlayEntry> entries, string contains1, string contains2, string groupColor, string valueColor, int separators, string groupName = null, string sortKey = null)
        {
            var entry = entries.FirstOrDefault(e =>
                e.Description != null &&
                e.Description.IndexOf(contains1, StringComparison.OrdinalIgnoreCase) >= 0 &&
                e.Description.IndexOf(contains2, StringComparison.OrdinalIgnoreCase) >= 0);

            if (entry != null)
            {
                entry.ShowOnOverlay = true;
                entry.GroupColor = groupColor;
                entry.Color = valueColor;
                entry.GroupSeparators = separators;
                if (groupName != null)
                    entry.GroupName = groupName;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }
        }

        private void EnableByGroupName(List<IOverlayEntry> entries, string groupName, string groupColor, string valueColor, int separators, string sortKey = null)
        {
            var matchingEntries = entries.Where(e =>
                e.GroupName != null &&
                e.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase)).ToList();

            // Set separator on ALL entries in this group for consistency with Separators list
            foreach (var entry in matchingEntries)
            {
                entry.GroupSeparators = separators;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }

            // Enable only the first entry
            var firstEntry = matchingEntries.FirstOrDefault();
            if (firstEntry != null)
            {
                firstEntry.ShowOnOverlay = true;
                firstEntry.GroupColor = groupColor;
                firstEntry.Color = valueColor;
            }
        }

        private bool EnableCpuCoreLoad(List<IOverlayEntry> entries, int coreIndex, string groupColor, string valueColor, int separators, string sortKey)
        {
            var entry = entries.FirstOrDefault(e =>
                e.Description != null &&
                e.Description.IndexOf($"Core #{coreIndex}", StringComparison.OrdinalIgnoreCase) >= 0 &&
                e.Description.IndexOf("(%)", StringComparison.OrdinalIgnoreCase) >= 0 &&
                e.Description.IndexOf("Thread", StringComparison.OrdinalIgnoreCase) < 0);

            if (entry == null)
                return false;

            entry.ShowOnOverlay = true;
            entry.GroupColor = groupColor;
            entry.Color = valueColor;
            entry.GroupName = $"Core #{coreIndex}";
            entry.GroupSeparators = separators;
            entry.SortKey = sortKey;
            return true;
        }

        private bool EnableCoreGroupClock(List<IOverlayEntry> entries, string groupName, string groupColor, string valueColor, int separators, string sortKey)
        {
            var entry = entries.FirstOrDefault(e =>
                e.GroupName != null &&
                e.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) &&
                e.Description != null &&
                e.Description.IndexOf("(MHz)", StringComparison.OrdinalIgnoreCase) >= 0);

            if (entry == null)
                return false;

            entry.ShowOnOverlay = true;
            entry.GroupColor = groupColor;
            entry.Color = valueColor;
            entry.GroupSeparators = separators;
            entry.SortKey = sortKey;
            return true;
        }

        private bool EnableCoreGroupLoad(List<IOverlayEntry> entries, string groupName, string groupColor, string valueColor, int separators, string sortKey)
        {
            var entry = entries.FirstOrDefault(e =>
                e.GroupName != null &&
                e.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) &&
                e.Description != null &&
                e.Description.IndexOf("(%)", StringComparison.OrdinalIgnoreCase) >= 0 &&
                e.Description.IndexOf("Thread", StringComparison.OrdinalIgnoreCase) < 0);

            if (entry == null)
                return false;

            entry.ShowOnOverlay = true;
            entry.GroupColor = groupColor;
            entry.Color = valueColor;
            entry.GroupSeparators = separators;
            entry.SortKey = sortKey;
            return true;
        }

        private bool EnableByGroupNameAndDescriptionContains(List<IOverlayEntry> entries, string groupName, string descriptionContains, string groupColor, string valueColor, int separators, string sortKey)
        {
            var entry = entries.FirstOrDefault(e =>
                e.GroupName != null &&
                e.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) &&
                e.Description != null &&
                e.Description.IndexOf(descriptionContains, StringComparison.OrdinalIgnoreCase) >= 0);

            if (entry == null)
                return false;

            entry.ShowOnOverlay = true;
            entry.GroupColor = groupColor;
            entry.Color = valueColor;
            entry.GroupSeparators = separators;
            entry.SortKey = sortKey;
            return true;
        }

        private List<CoreGroupInfo> GetCoreGroups(List<IOverlayEntry> entries)
        {
            return entries
                .Where(e => !string.IsNullOrWhiteSpace(e.GroupName))
                .Where(e => !IsEffectiveCoreGroupName(e.GroupName))
                .Select(e => e.GroupName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(groupName =>
                {
                    if (!TryParseCoreGroupName(groupName, out var coreIndex, out var coreSuffix))
                        return null;

                    return new CoreGroupInfo
                    {
                        GroupName = groupName,
                        CoreIndex = coreIndex,
                        CoreSuffix = coreSuffix,
                        SuffixOrder = GetCoreSuffixSortOrder(coreSuffix)
                    };
                })
                .Where(coreGroup => coreGroup != null)
                .OrderBy(coreGroup => coreGroup.CoreIndex)
                .ThenBy(coreGroup => coreGroup.SuffixOrder)
                .ThenBy(coreGroup => coreGroup.GroupName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsEffectiveCoreGroupName(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return false;

            var trimmed = groupName.Trim();
            if (!trimmed.StartsWith("Core #", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("CPU #", StringComparison.OrdinalIgnoreCase))
                return false;

            return trimmed.IndexOf("(Effective)", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryParseCoreGroupName(string groupName, out int coreIndex, out string coreSuffix)
        {
            coreIndex = 0;
            coreSuffix = string.Empty;

            if (string.IsNullOrWhiteSpace(groupName))
                return false;

            var trimmed = groupName.Trim();
            if (!trimmed.StartsWith("Core #", StringComparison.OrdinalIgnoreCase))
                return false;

            var remainder = trimmed.Substring("Core #".Length).Trim();
            if (string.IsNullOrWhiteSpace(remainder))
                return false;

            var parts = remainder.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !int.TryParse(parts[0], out coreIndex))
                return false;

            coreSuffix = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
            return true;
        }

        private static int GetCoreSuffixSortOrder(string coreSuffix)
        {
            if (string.IsNullOrWhiteSpace(coreSuffix))
                return 0;

            switch (coreSuffix.Trim().ToUpperInvariant())
            {
                case "P":
                    return 1;
                case "E":
                    return 2;
                case "LPE":
                    return 3;
                case "D":
                    return 4;
                default:
                    return 5;
            }
        }

        private void EnsureCpuSectionSeparator(List<IOverlayEntry> entries)
        {
            var customCpu = entries.FirstOrDefault(e => e.Identifier == "CustomCPU" && e.ShowOnOverlay);
            if (customCpu != null)
                return;

            var cpuEntries = entries
                .Where(e => e.ShowOnOverlay && e.OverlayEntryType == EOverlayEntryType.CPU)
                .ToList();

            if (!cpuEntries.Any() || cpuEntries.Any(e => e.GroupSeparators > 0))
                return;

            var firstCpuEntry = cpuEntries
                .OrderBy(e => e.SortKey, AlphanumericComparer.Instance)
                .FirstOrDefault();

            if (firstCpuEntry != null)
                firstCpuEntry.GroupSeparators = 1;
        }

        public void StoreCurrentState(IEnumerable<IOverlayEntry> entries)
        {
            _storedState = new Dictionary<string, StoredEntryState>();
            foreach (var entry in entries)
            {
                _storedState[entry.Identifier] = new StoredEntryState
                {
                    ShowOnOverlay = entry.ShowOnOverlay,
                    GroupColor = entry.GroupColor,
                    Color = entry.Color,
                    GroupSeparators = entry.GroupSeparators,
                    GroupName = entry.GroupName,
                    ShowGraph = entry.ShowGraph,
                    SortKey = entry.SortKey
                };
            }
        }

        public bool RevertToStoredState(IEnumerable<IOverlayEntry> entries)
        {
            if (!HasStoredState)
                return false;

            foreach (var entry in entries)
            {
                if (_storedState.TryGetValue(entry.Identifier, out var storedState))
                {
                    entry.ShowOnOverlay = storedState.ShowOnOverlay;
                    entry.GroupColor = storedState.GroupColor;
                    entry.Color = storedState.Color;
                    entry.GroupSeparators = storedState.GroupSeparators;
                    entry.GroupName = storedState.GroupName;
                    entry.ShowGraph = storedState.ShowGraph;
                    entry.SortKey = storedState.SortKey;
                }
            }

            return true;
        }

        private static string TemplateSortKey(int section, int index, int subIndex = 0)
        {
            if (subIndex > 0)
                return $"{section}_{index:D2}_{subIndex:D2}";

            return $"{section}_{index:D2}";
        }

        private class CoreGroupInfo
        {
            public string GroupName { get; set; }
            public int CoreIndex { get; set; }
            public string CoreSuffix { get; set; }
            public int SuffixOrder { get; set; }
        }

        private class StoredEntryState
        {
            public bool ShowOnOverlay { get; set; }
            public string GroupColor { get; set; }
            public string Color { get; set; }
            public int GroupSeparators { get; set; }
            public string GroupName { get; set; }
            public bool ShowGraph { get; set; }
            public string SortKey { get; set; }
        }
    }
}
