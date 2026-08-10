using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CapFrameX.Mcp.Tools
{
    public class RecordSummary
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("game")]
        public string Game { get; set; }

        [JsonProperty("processName")]
        public string ProcessName { get; set; }

        [JsonProperty("recordedAt")]
        public DateTime RecordedAt { get; set; }

        [JsonProperty("durationSec")]
        public double DurationSec { get; set; }

        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public string Comment { get; set; }

        [JsonProperty("hash", NullValueHandling = NullValueHandling.Ignore)]
        public string Hash { get; set; }
    }

    public class RecordDetail : RecordSummary
    {
        [JsonProperty("system")]
        public RecordSystemInfo System { get; set; }

        [JsonProperty("runs")]
        public int Runs { get; set; }

        [JsonProperty("apiInfo", NullValueHandling = NullValueHandling.Ignore)]
        public string ApiInfo { get; set; }

        [JsonProperty("resolution", NullValueHandling = NullValueHandling.Ignore)]
        public string Resolution { get; set; }

        [JsonProperty("presentationMode", NullValueHandling = NullValueHandling.Ignore)]
        public string PresentationMode { get; set; }

        [JsonProperty("resizableBar", NullValueHandling = NullValueHandling.Ignore)]
        public string ResizableBar { get; set; }

        [JsonProperty("hags", NullValueHandling = NullValueHandling.Ignore)]
        public string Hags { get; set; }

        [JsonProperty("winGameMode", NullValueHandling = NullValueHandling.Ignore)]
        public string WinGameMode { get; set; }
    }

    public class RecordSystemInfo
    {
        [JsonProperty("cpu")] public string Cpu { get; set; }
        [JsonProperty("gpu")] public string Gpu { get; set; }
        [JsonProperty("ram")] public string Ram { get; set; }
        [JsonProperty("motherboard")] public string Motherboard { get; set; }
        [JsonProperty("os")] public string Os { get; set; }
        [JsonProperty("gpuDriver")] public string GpuDriver { get; set; }
    }

    public class MetricResult
    {
        [JsonProperty("metric")] public string Metric { get; set; }
        [JsonProperty("value")] public double Value { get; set; }
        [JsonProperty("unit")] public string Unit { get; set; }
    }

    public class RecordMetricsResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("game")] public string Game { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("runs")] public List<RunMetrics> Runs { get; set; } = new List<RunMetrics>();
    }

    public class RunMetrics
    {
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("metrics")] public List<MetricResult> Metrics { get; set; } = new List<MetricResult>();
    }

    public class SensorAggregate
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("avg")] public double Average { get; set; }
        [JsonProperty("min")] public double Min { get; set; }
        [JsonProperty("max")] public double Max { get; set; }
        [JsonProperty("sampleCount")] public int SampleCount { get; set; }
    }

    public class SensorSummaryResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("sensors")] public List<SensorAggregate> Sensors { get; set; } = new List<SensorAggregate>();
    }

    public class ComparisonResult
    {
        [JsonProperty("baseline")] public string BaselineId { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("rows")] public List<ComparisonRow> Rows { get; set; } = new List<ComparisonRow>();
    }

    public class ComparisonRow
    {
        [JsonProperty("metric")] public string Metric { get; set; }
        [JsonProperty("unit")] public string Unit { get; set; }
        [JsonProperty("values")] public List<ComparisonCell> Values { get; set; } = new List<ComparisonCell>();
    }

    public class ComparisonCell
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("value")] public double? Value { get; set; }
        [JsonProperty("deltaAbs", NullValueHandling = NullValueHandling.Ignore)]
        public double? DeltaAbs { get; set; }
        [JsonProperty("deltaPct", NullValueHandling = NullValueHandling.Ignore)]
        public double? DeltaPct { get; set; }
    }

    public class BottleneckResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("verdict")] public string Verdict { get; set; }
        [JsonProperty("confidence")] public string Confidence { get; set; }
        [JsonProperty("reasoning")] public string Reasoning { get; set; }
        [JsonProperty("signals")] public BottleneckSignals Signals { get; set; }
    }

    public class DiagnosticsResult
    {
        [JsonProperty("logPath")] public string LogPath { get; set; }
        [JsonProperty("lookbackMinutes")] public int LookbackMinutes { get; set; }
        [JsonProperty("entriesScanned")] public int EntriesScanned { get; set; }
        [JsonProperty("issuesFound")] public int IssuesFound { get; set; }
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("issues")] public List<DiagnosticIssue> Issues { get; set; } = new List<DiagnosticIssue>();
    }

    public class CaptureTimelineResult
    {
        [JsonProperty("logPath")] public string LogPath { get; set; }
        [JsonProperty("lookbackMinutes")] public int LookbackMinutes { get; set; }
        [JsonProperty("events")] public List<CaptureEvent> Events { get; set; } = new List<CaptureEvent>();
    }

    public class CaptureEvent
    {
        [JsonProperty("when")] public DateTime When { get; set; }
        [JsonProperty("eventType")] public string EventType { get; set; }
        [JsonProperty("level")] public string Level { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("exception", NullValueHandling = NullValueHandling.Ignore)] public string Exception { get; set; }
    }

    public class CaptureStatusInfo
    {
        [JsonProperty("isCapturing")] public bool IsCapturing { get; set; }
        [JsonProperty("isLocked")] public bool IsLocked { get; set; }
        [JsonProperty("delayCountdownRunning")] public bool DelayCountdownRunning { get; set; }
        [JsonProperty("osdAutoDisabled")] public bool OsdAutoDisabled { get; set; }
        [JsonProperty("state")] public string State { get; set; }
        [JsonProperty("lastMessage", NullValueHandling = NullValueHandling.Ignore)] public string LastMessage { get; set; }
    }

    public class CurrentSystemInfo
    {
        [JsonProperty("cpu")] public string Cpu { get; set; }
        [JsonProperty("gpu")] public string Gpu { get; set; }
        [JsonProperty("os")] public string Os { get; set; }
        [JsonProperty("motherboard")] public string Motherboard { get; set; }
        [JsonProperty("ram")] public string Ram { get; set; }
        [JsonProperty("resizableBarHardware")] public string ResizableBarHardware { get; set; }
        [JsonProperty("resizableBarD3D")] public string ResizableBarD3D { get; set; }
        [JsonProperty("resizableBarVulkan")] public string ResizableBarVulkan { get; set; }
        [JsonProperty("hardwareAcceleratedGpuScheduling")] public string HardwareAcceleratedGpuScheduling { get; set; }
        [JsonProperty("windowsGameMode")] public string WindowsGameMode { get; set; }
        [JsonProperty("pciBarSizeHardware", NullValueHandling = NullValueHandling.Ignore)] public ulong? PciBarSizeHardware { get; set; }
        [JsonProperty("pciBarSizeD3D", NullValueHandling = NullValueHandling.Ignore)] public ulong? PciBarSizeD3D { get; set; }
        [JsonProperty("pciBarSizeVulkan", NullValueHandling = NullValueHandling.Ignore)] public ulong? PciBarSizeVulkan { get; set; }
    }

    public class DiagnosticIssue
    {
        [JsonProperty("when")] public DateTime When { get; set; }
        [JsonProperty("severity")] public string Severity { get; set; }
        [JsonProperty("category")] public string Category { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("suggestion", NullValueHandling = NullValueHandling.Ignore)] public string Suggestion { get; set; }
        [JsonProperty("exception", NullValueHandling = NullValueHandling.Ignore)] public string Exception { get; set; }
    }

    public class BottleneckSignals
    {
        [JsonProperty("gpuLoadAvg", NullValueHandling = NullValueHandling.Ignore)]
        public double? GpuLoadAvg { get; set; }
        [JsonProperty("gpuLoadMax", NullValueHandling = NullValueHandling.Ignore)]
        public double? GpuLoadMax { get; set; }
        [JsonProperty("cpuMaxThreadLoadAvg", NullValueHandling = NullValueHandling.Ignore)]
        public double? CpuMaxThreadLoadAvg { get; set; }
        [JsonProperty("cpuTotalLoadAvg", NullValueHandling = NullValueHandling.Ignore)]
        public double? CpuTotalLoadAvg { get; set; }
        [JsonProperty("gpuTempAvg", NullValueHandling = NullValueHandling.Ignore)]
        public double? GpuTempAvg { get; set; }
        [JsonProperty("gpuTempMax", NullValueHandling = NullValueHandling.Ignore)]
        public double? GpuTempMax { get; set; }
        [JsonProperty("cpuTempAvg", NullValueHandling = NullValueHandling.Ignore)]
        public double? CpuTempAvg { get; set; }
        [JsonProperty("cpuTempMax", NullValueHandling = NullValueHandling.Ignore)]
        public double? CpuTempMax { get; set; }
        [JsonProperty("gpuPowerLimitHitsPct", NullValueHandling = NullValueHandling.Ignore)]
        public double? GpuPowerLimitHitsPct { get; set; }
        [JsonProperty("vramUsageMaxGB", NullValueHandling = NullValueHandling.Ignore)]
        public double? VramUsageMaxGB { get; set; }
    }

    public class LoggedSensorsResult
    {
        [JsonProperty("refreshPeriodMs")] public int RefreshPeriodMs { get; set; }
        [JsonProperty("sensorEntryCount")] public int SensorEntryCount { get; set; }
        [JsonProperty("selectedCount")] public int SelectedCount { get; set; }
        [JsonProperty("sensors")] public List<LoggedSensorEntry> Sensors { get; set; } = new List<LoggedSensorEntry>();
    }

    public class LoggedSensorEntry
    {
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("sensorType", NullValueHandling = NullValueHandling.Ignore)] public string SensorType { get; set; }
        [JsonProperty("hardwareType", NullValueHandling = NullValueHandling.Ignore)] public string HardwareType { get; set; }
        [JsonProperty("hardwareName", NullValueHandling = NullValueHandling.Ignore)] public string HardwareName { get; set; }
        [JsonProperty("selectedForLogging")] public bool SelectedForLogging { get; set; }
        [JsonProperty("isPresentationDefault")] public bool IsPresentationDefault { get; set; }
    }

    public class OverlayEntriesResult
    {
        [JsonProperty("entryCount")] public int EntryCount { get; set; }
        [JsonProperty("shownCount")] public int ShownCount { get; set; }
        [JsonProperty("entries")] public List<OverlayEntryInfo> Entries { get; set; } = new List<OverlayEntryInfo>();
    }

    public class OverlayEntryInfo
    {
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("stableIdentifier", NullValueHandling = NullValueHandling.Ignore)] public string StableIdentifier { get; set; }
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)] public string Description { get; set; }
        [JsonProperty("groupName", NullValueHandling = NullValueHandling.Ignore)] public string GroupName { get; set; }
        [JsonProperty("overlayEntryType", NullValueHandling = NullValueHandling.Ignore)] public string OverlayEntryType { get; set; }
        [JsonProperty("isEntryEnabled")] public bool IsEntryEnabled { get; set; }
        [JsonProperty("showOnOverlay")] public bool ShowOnOverlay { get; set; }
        [JsonProperty("showOnOverlayIsEnabled")] public bool ShowOnOverlayIsEnabled { get; set; }
        [JsonProperty("showGraph")] public bool ShowGraph { get; set; }
        [JsonProperty("showGraphIsEnabled")] public bool ShowGraphIsEnabled { get; set; }
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)] public string Value { get; set; }
        [JsonProperty("formattedValue", NullValueHandling = NullValueHandling.Ignore)] public string FormattedValue { get; set; }
        [JsonProperty("valueFormat", NullValueHandling = NullValueHandling.Ignore)] public string ValueFormat { get; set; }
        [JsonProperty("valueUnitFormat", NullValueHandling = NullValueHandling.Ignore)] public string ValueUnitFormat { get; set; }
        [JsonProperty("valueAlignmentAndDigits", NullValueHandling = NullValueHandling.Ignore)] public string ValueAlignmentAndDigits { get; set; }
        [JsonProperty("valueFontSize")] public int ValueFontSize { get; set; }
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)] public string Color { get; set; }
        [JsonProperty("formattedGroupName", NullValueHandling = NullValueHandling.Ignore)] public string FormattedGroupName { get; set; }
        [JsonProperty("groupNameFormat", NullValueHandling = NullValueHandling.Ignore)] public string GroupNameFormat { get; set; }
        [JsonProperty("groupColor", NullValueHandling = NullValueHandling.Ignore)] public string GroupColor { get; set; }
        [JsonProperty("groupFontSize")] public int GroupFontSize { get; set; }
        [JsonProperty("groupSeparators")] public int GroupSeparators { get; set; }
        [JsonProperty("upperLimitValue", NullValueHandling = NullValueHandling.Ignore)] public string UpperLimitValue { get; set; }
        [JsonProperty("lowerLimitValue", NullValueHandling = NullValueHandling.Ignore)] public string LowerLimitValue { get; set; }
        [JsonProperty("upperLimitColor", NullValueHandling = NullValueHandling.Ignore)] public string UpperLimitColor { get; set; }
        [JsonProperty("lowerLimitColor", NullValueHandling = NullValueHandling.Ignore)] public string LowerLimitColor { get; set; }
        [JsonProperty("isNumeric")] public bool IsNumeric { get; set; }
        [JsonProperty("lastLimitState", NullValueHandling = NullValueHandling.Ignore)] public string LastLimitState { get; set; }
        [JsonProperty("formatChanged")] public bool FormatChanged { get; set; }
        [JsonProperty("orderIndex")] public int OrderIndex { get; set; }
        [JsonProperty("sortKey", NullValueHandling = NullValueHandling.Ignore)] public string SortKey { get; set; }
    }

    public class SetOverlayEntryResult
    {
        [JsonProperty("applied")] public bool Applied { get; set; }
        [JsonProperty("persisted")] public bool Persisted { get; set; }
        [JsonProperty("changedCount")] public int ChangedCount { get; set; }
        [JsonProperty("changedProperties")] public List<string> ChangedProperties { get; set; } = new List<string>();
        [JsonProperty("entry")] public OverlayEntryInfo Entry { get; set; }
    }

    public enum OsdRendererMode
    {
        Rtss,
        InGame,
        HookFree
    }

    public enum OsdAnchorPosition
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3,
        TopCenter = 4
    }

    public class OsdOptionsInfo
    {
        [JsonProperty("renderer")] public string Renderer { get; set; }
        [JsonProperty("rendererConfigurationValid")] public bool RendererConfigurationValid { get; set; }
        [JsonProperty("enableHookOverlay")] public bool EnableHookOverlay { get; set; }
        [JsonProperty("enableHookFreeOverlay")] public bool EnableHookFreeOverlay { get; set; }
        [JsonProperty("rtssInstalled")] public bool RtssInstalled { get; set; }
        [JsonProperty("isOverlayActive")] public bool IsOverlayActive { get; set; }
        [JsonProperty("autoDisableOverlay")] public bool AutoDisableOverlay { get; set; }
        [JsonProperty("showSystemTimeSeconds")] public bool ShowSystemTimeSeconds { get; set; }
        [JsonProperty("hideOverlay")] public bool HideOverlay { get; set; }
        [JsonProperty("hookOverlayUsePresentMonFrametimes")] public bool HookOverlayUsePresentMonFrametimes { get; set; }
        [JsonProperty("replayBufferSizeMs")] public int ReplayBufferSizeMs { get; set; }
        [JsonProperty("hookFreeRefreshRate")] public int HookFreeRefreshRate { get; set; }
        [JsonProperty("osdCustomPosition")] public bool OsdCustomPosition { get; set; }
        [JsonProperty("osdPositionX")] public int OsdPositionX { get; set; }
        [JsonProperty("osdPositionY")] public int OsdPositionY { get; set; }
        [JsonProperty("backgroundOpacity")] public int BackgroundOpacity { get; set; }
        [JsonProperty("anchor")] public string Anchor { get; set; }
        [JsonProperty("anchorValue")] public int AnchorValue { get; set; }
        [JsonProperty("marginX")] public int MarginX { get; set; }
        [JsonProperty("marginY")] public int MarginY { get; set; }
        [JsonProperty("zoom")] public int Zoom { get; set; }
        [JsonProperty("useValueSmoothing")] public bool UseValueSmoothing { get; set; }
        [JsonProperty("overlayHotkey")] public string OverlayHotkey { get; set; }
        [JsonProperty("overlayConfigHotkey")] public string OverlayConfigHotkey { get; set; }
        [JsonProperty("resetMetricsHotkey")] public string ResetMetricsHotkey { get; set; }
        [JsonProperty("refreshPeriodMs")] public int RefreshPeriodMs { get; set; }
        [JsonProperty("metricIntervalSeconds")] public int MetricIntervalSeconds { get; set; }
    }

    public class SetOsdOptionsResult
    {
        [JsonProperty("applied")] public bool Applied { get; set; }
        [JsonProperty("changedCount")] public int ChangedCount { get; set; }
        [JsonProperty("changedProperties")] public List<string> ChangedProperties { get; set; } = new List<string>();
        [JsonProperty("options")] public OsdOptionsInfo Options { get; set; }
    }
}
