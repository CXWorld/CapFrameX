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
}
