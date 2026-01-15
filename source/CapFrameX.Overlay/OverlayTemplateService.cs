using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using System;
using System.Collections.Generic;
using System.Linq;

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
            var gpuNameLower = gpuName.ToLowerInvariant();

            if (gpuNameLower.Contains("nvidia") || gpuNameLower.Contains("geforce") || gpuNameLower.Contains("rtx") || gpuNameLower.Contains("gtx"))
                DetectedGpuVendor = EGpuVendor.Nvidia;
            else if (gpuNameLower.Contains("amd") || gpuNameLower.Contains("radeon") || gpuNameLower.Contains("rx "))
                DetectedGpuVendor = EGpuVendor.Amd;
            else if (gpuNameLower.Contains("intel") || gpuNameLower.Contains("arc") || gpuNameLower.Contains("iris") || gpuNameLower.Contains("uhd"))
                DetectedGpuVendor = EGpuVendor.Intel;
            else
                DetectedGpuVendor = EGpuVendor.Unknown;

            // Create short GPU name (e.g., "RTX 4090", "RX 7900 XTX", "Arc A770")
            ShortGpuName = CreateShortGpuName(gpuName);

            var cpuName = _sensorService.GetCpuName()?.ToLowerInvariant() ?? string.Empty;
            if (cpuName.Contains("intel") || cpuName.Contains("core i"))
                DetectedCpuVendor = ECpuVendor.Intel;
            else if (cpuName.Contains("amd") || cpuName.Contains("ryzen") || cpuName.Contains("threadripper"))
                DetectedCpuVendor = ECpuVendor.Amd;
            else
                DetectedCpuVendor = ECpuVendor.Unknown;
        }

        private string CreateShortGpuName(string fullGpuName)
        {
            if (string.IsNullOrWhiteSpace(fullGpuName))
                return "GPU";

            // Try to extract model name patterns
            var name = fullGpuName.Trim();

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
            EnableByDescription(entries, "GPU Core (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName);
            EnableByDescription(entries, "GPU Memory (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName);
            EnableByDescription(entries, "GPU Core (%)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Core (°C)", gpuGroupColor, VALUE_YELLOW, 0);

            // 2. CPU Section - Clock, Total Load, Package Power+Temp
            EnableByGroupName(entries, "CPU Clock", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Total (%)", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Package (W)", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Package", "°C", cpuGroupColor, VALUE_YELLOW, 0);

            // 3. Metrics Section (with blank line)
            EnableByIdentifier(entries, "OnlineAverage", METRIC_COLOR, METRIC_COLOR, 1);
            EnableByIdentifier(entries, "Online1PercentLow", METRIC_COLOR, METRIC_COLOR, 0);

            // 4. Framerate Section (at the end, with blank line, no graph)
            EnableByIdentifier(entries, "Framerate", FRAMERATE_COLOR, FRAMERATE_COLOR, 1, null, false);
            EnableByIdentifier(entries, "Frametime", FRAMERATE_COLOR, FRAMERATE_COLOR, 0);
        }

        private void ApplyDetailedTemplate(List<IOverlayEntry> entries)
        {
            var gpuGroupColor = GetGpuGroupColor();
            var cpuGroupColor = GetCpuGroupColor();

            // 1. GPU Section - clocks with short GPU name, load, temp, power, VRAM
            EnableByDescription(entries, "GPU Core (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName);
            EnableByDescription(entries, "GPU Memory (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName);
            EnableByDescription(entries, "GPU Core (%)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Core (°C)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Power (W)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Memory Dedicated (GB)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Memory Shared (GB)", gpuGroupColor, VALUE_YELLOW, 0);

            // 2. CPU Model (with blank line)
            EnableByIdentifier(entries, "CustomCPU", cpuGroupColor, VALUE_WHITE, 1, "CPU Model");

            // 3. CPU entries - Clock, Total, Package
            EnableByGroupName(entries, "CPU Clock", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Total (%)", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Package (W)", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Package", "°C", cpuGroupColor, VALUE_YELLOW, 0);

            // 4. RAM Section (with blank line)
            EnableByIdentifier(entries, "CustomRAM", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 1);

            // 5. Metrics Section (with blank line)
            EnableByIdentifier(entries, "OnlineAverage", METRIC_COLOR, METRIC_COLOR, 1);
            EnableByIdentifier(entries, "Online1PercentLow", METRIC_COLOR, METRIC_COLOR, 0);

            // 6. Framerate Section (at the end, with blank line, no graph)
            EnableByIdentifier(entries, "Framerate", FRAMERATE_COLOR, FRAMERATE_COLOR, 1, null, false);
            EnableByIdentifier(entries, "Frametime", FRAMERATE_COLOR, FRAMERATE_COLOR, 0);
        }

        private void ApplyEnthusiastTemplate(List<IOverlayEntry> entries)
        {
            var gpuGroupColor = GetGpuGroupColor();
            var cpuGroupColor = GetCpuGroupColor();

            // 1. GPU Section - clocks with short GPU name, load, temp, hot spot, power, VRAM
            EnableByDescription(entries, "GPU Core (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName);
            EnableByDescription(entries, "GPU Memory (MHz)", gpuGroupColor, VALUE_YELLOW, 0, ShortGpuName);
            EnableByDescription(entries, "GPU Core (%)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Core (°C)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Memory Junction (°C)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Power (W)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Memory Dedicated (GB)", gpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescription(entries, "GPU Memory Shared (GB)", gpuGroupColor, VALUE_YELLOW, 0);

            // 2. CPU Model (with blank line)
            EnableByIdentifier(entries, "CustomCPU", cpuGroupColor, VALUE_WHITE, 1, "CPU Model");

            // 3. CPU entries - all core clocks and loads, Package
            for (int i = 1; i <= 16; i++)
            {
                EnableByDescription(entries, $"Core #{i} (MHz)", cpuGroupColor, VALUE_YELLOW, 0);
                EnableByDescriptionContains(entries, $"Core #{i}", "Thread #1 (%)", cpuGroupColor, VALUE_YELLOW, 0);
                EnableByDescriptionContains(entries, $"Core #{i}", "Thread #2 (%)", cpuGroupColor, VALUE_YELLOW, 0);
            }

            EnableByDescriptionContains(entries, "CPU Package (W)", cpuGroupColor, VALUE_YELLOW, 0);
            EnableByDescriptionContains(entries, "CPU Package", "°C", cpuGroupColor, VALUE_YELLOW, 0);

            // 4. RAM Section (with blank line) - DDR info, DIMM temps, RAM used
            EnableByIdentifier(entries, "CustomRAM", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 1);
            EnableByDescription(entries, "DIMM #1 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0);
            EnableByDescription(entries, "DIMM #2 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0);
            EnableByDescription(entries, "DIMM #3 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0);
            EnableByDescription(entries, "DIMM #4 (°C)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0);
            EnableByDescription(entries, "RAM Used (GB)", RAM_GROUP_COLOR, RAM_VALUE_COLOR, 0);

            // 5. Latency Section (with blank line)
            EnableByIdentifier(entries, "OnlinePcLatency", LATENCY_GROUP_COLOR, LATENCY_VALUE_COLOR, 1);
            EnableByIdentifier(entries, "OnlineAnimationError", LATENCY_GROUP_COLOR, LATENCY_VALUE_COLOR, 0);

            // 6. Metrics Section (with blank line)
            EnableByIdentifier(entries, "OnlineAverage", METRIC_COLOR, METRIC_COLOR, 1);
            EnableByIdentifier(entries, "Online1PercentLow", METRIC_COLOR, METRIC_COLOR, 0);

            // 7. Framerate Section (at the end, with blank line, no graph)
            EnableByIdentifier(entries, "Framerate", FRAMERATE_COLOR, FRAMERATE_COLOR, 1, null, false);
            EnableByIdentifier(entries, "Frametime", FRAMERATE_COLOR, FRAMERATE_COLOR, 0);
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

        private void EnableByIdentifier(List<IOverlayEntry> entries, string identifier, string groupColor, string valueColor, int separators, string groupName = null, bool? showGraph = null, string sortKey = null)
        {
            var entry = entries.FirstOrDefault(e => e.Identifier == identifier);
            if (entry != null)
            {
                entry.ShowOnOverlay = true;
                entry.GroupColor = groupColor;
                entry.Color = valueColor;
                entry.GroupSeparators = separators;
                if (groupName != null)
                    entry.GroupName = groupName;
                if (showGraph.HasValue)
                    entry.ShowGraph = showGraph.Value;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }
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
                entry.GroupSeparators = separators;
                if (groupName != null)
                    entry.GroupName = groupName;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }
        }

        private void EnableByDescriptionContains(List<IOverlayEntry> entries, string contains, string groupColor, string valueColor, int separators, string sortKey = null)
        {
            var entry = entries.FirstOrDefault(e =>
                e.Description != null &&
                e.Description.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);

            if (entry != null)
            {
                entry.ShowOnOverlay = true;
                entry.GroupColor = groupColor;
                entry.Color = valueColor;
                entry.GroupSeparators = separators;
                if (sortKey != null)
                    entry.SortKey = sortKey;
            }
        }

        private void EnableByDescriptionContains(List<IOverlayEntry> entries, string contains1, string contains2, string groupColor, string valueColor, int separators, string sortKey = null)
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
