using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CapFrameX.Mcp.Tools
{
    // ─── Capture lifecycle ───────────────────────────────────────────────────

    public class ProcessListResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("processes")] public List<ProcessInfoEntry> Processes { get; set; } = new List<ProcessInfoEntry>();
    }

    public class ProcessInfoEntry
    {
        [JsonProperty("processName")] public string ProcessName { get; set; }
        [JsonProperty("pid")] public int Pid { get; set; }
        [JsonProperty("displayName", NullValueHandling = NullValueHandling.Ignore)] public string DisplayName { get; set; }
        [JsonProperty("isBlacklisted")] public bool IsBlacklisted { get; set; }
        [JsonProperty("lastCaptureTime", NullValueHandling = NullValueHandling.Ignore)] public double? LastCaptureTime { get; set; }
    }

    public class StartCaptureResult
    {
        [JsonProperty("processName")] public string ProcessName { get; set; }
        [JsonProperty("pid")] public int Pid { get; set; }
        [JsonProperty("captureSeconds")] public double CaptureSeconds { get; set; }
        [JsonProperty("delaySeconds")] public double DelaySeconds { get; set; }
        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)] public string Comment { get; set; }
        [JsonProperty("started")] public bool Started { get; set; }
    }

    public class StopCaptureResult
    {
        [JsonProperty("wasCapturing")] public bool WasCapturing { get; set; }
        [JsonProperty("cancelledDelayCountdown")] public bool CancelledDelayCountdown { get; set; }
    }

    public class WaitForCaptureResult
    {
        [JsonProperty("timedOut")] public bool TimedOut { get; set; }
        [JsonProperty("finalState")] public string FinalState { get; set; }
        [JsonProperty("waitedSec")] public double WaitedSec { get; set; }
    }

    // ─── Frametime / stutter ─────────────────────────────────────────────────

    public class FrametimesResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("frameCount")] public int FrameCount { get; set; }
        [JsonProperty("durationSec")] public double DurationSec { get; set; }
        [JsonProperty("sampleHz")] public double SampleHz { get; set; }
        [JsonProperty("points")] public List<FrametimePoint> Points { get; set; } = new List<FrametimePoint>();
    }

    public class FrametimePoint
    {
        [JsonProperty("tSec")] public double TSec { get; set; }
        [JsonProperty("frametimeMs")] public double FrametimeMs { get; set; }
    }

    public class StuttersResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("factorVsMedian")] public double FactorVsMedian { get; set; }
        [JsonProperty("minMs")] public double MinMs { get; set; }
        [JsonProperty("windowFrames")] public int WindowFrames { get; set; }
        [JsonProperty("totalCount")] public int TotalCount { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("stutters")] public List<StutterEvent> Stutters { get; set; } = new List<StutterEvent>();
    }

    public class StutterEvent
    {
        [JsonProperty("tSec")] public double TSec { get; set; }
        [JsonProperty("frametimeMs")] public double FrametimeMs { get; set; }
        [JsonProperty("movingMedianMs")] public double MovingMedianMs { get; set; }
        [JsonProperty("severity")] public double Severity { get; set; }
    }

    public class FreezesResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("minMs")] public double MinMs { get; set; }
        [JsonProperty("totalCount")] public int TotalCount { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("freezes")] public List<FreezeEvent> Freezes { get; set; } = new List<FreezeEvent>();
    }

    public class FreezeEvent
    {
        [JsonProperty("tSec")] public double TSec { get; set; }
        [JsonProperty("frametimeMs")] public double FrametimeMs { get; set; }
    }

    public class LowFpsWindowsResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("fpsThreshold")] public double FpsThreshold { get; set; }
        [JsonProperty("minDurationSec")] public double MinDurationSec { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("windows")] public List<LowFpsWindow> Windows { get; set; } = new List<LowFpsWindow>();
    }

    public class LowFpsWindow
    {
        [JsonProperty("startSec")] public double StartSec { get; set; }
        [JsonProperty("endSec")] public double EndSec { get; set; }
        [JsonProperty("durationSec")] public double DurationSec { get; set; }
        [JsonProperty("avgFps")] public double AvgFps { get; set; }
        [JsonProperty("minFps")] public double MinFps { get; set; }
    }

    public class MetricOverTimeResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("metric")] public string Metric { get; set; }
        [JsonProperty("windowSec")] public double WindowSec { get; set; }
        [JsonProperty("stepSec")] public double StepSec { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("points")] public List<MetricOverTimePoint> Points { get; set; } = new List<MetricOverTimePoint>();
    }

    public class MetricOverTimePoint
    {
        [JsonProperty("tSec")] public double TSec { get; set; }
        [JsonProperty("value")] public double Value { get; set; }
    }

    public class RunsConsistencyResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runCount")] public int RunCount { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("metrics")] public List<RunsConsistencyMetric> Metrics { get; set; } = new List<RunsConsistencyMetric>();
    }

    public class RunsConsistencyMetric
    {
        [JsonProperty("metric")] public string Metric { get; set; }
        [JsonProperty("mean")] public double Mean { get; set; }
        [JsonProperty("stdev")] public double Stdev { get; set; }
        [JsonProperty("cvPct")] public double CoefficientOfVariationPct { get; set; }
        [JsonProperty("perRun")] public List<double> PerRun { get; set; } = new List<double>();
        [JsonProperty("note", NullValueHandling = NullValueHandling.Ignore)] public string Note { get; set; }
    }

    // ─── Sensor analysis ────────────────────────────────────────────────────

    public class SensorTimeSeriesResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("downsampleHz")] public double DownsampleHz { get; set; }
        [JsonProperty("sampleCount")] public int SampleCount { get; set; }
        [JsonProperty("sensors")] public List<SensorTimeSeries> Sensors { get; set; } = new List<SensorTimeSeries>();
    }

    public class SensorTimeSeries
    {
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)] public string Type { get; set; }
        [JsonProperty("points")] public List<SensorTimePoint> Points { get; set; } = new List<SensorTimePoint>();
    }

    public class SensorTimePoint
    {
        [JsonProperty("tSec")] public double TSec { get; set; }
        [JsonProperty("value")] public double Value { get; set; }
    }

    public class CorrelationResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("method")] public string Method { get; set; }
        [JsonProperty("correlations")] public List<SensorCorrelation> Correlations { get; set; } = new List<SensorCorrelation>();
    }

    public class SensorCorrelation
    {
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)] public string Type { get; set; }
        [JsonProperty("pearson")] public double Pearson { get; set; }
        [JsonProperty("sampleCount")] public int SampleCount { get; set; }
    }

    public class LiveSensorSnapshotResult
    {
        [JsonProperty("capturedAt")] public DateTime CapturedAt { get; set; }
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("sensors")] public List<LiveSensorEntry> Sensors { get; set; } = new List<LiveSensorEntry>();
    }

    public class LiveSensorEntry
    {
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("sensorType", NullValueHandling = NullValueHandling.Ignore)] public string SensorType { get; set; }
        [JsonProperty("hardwareType", NullValueHandling = NullValueHandling.Ignore)] public string HardwareType { get; set; }
        [JsonProperty("hardwareName", NullValueHandling = NullValueHandling.Ignore)] public string HardwareName { get; set; }
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)] public double? Value { get; set; }
        [JsonProperty("valueText", NullValueHandling = NullValueHandling.Ignore)] public string ValueText { get; set; }
    }

    public class SensorSourcesResult
    {
        [JsonProperty("totalSensorCount")] public int TotalSensorCount { get; set; }
        [JsonProperty("cpuVendor", NullValueHandling = NullValueHandling.Ignore)] public string CpuVendor { get; set; }
        [JsonProperty("gpuVendor", NullValueHandling = NullValueHandling.Ignore)] public string GpuVendor { get; set; }
        [JsonProperty("cpuName", NullValueHandling = NullValueHandling.Ignore)] public string CpuName { get; set; }
        [JsonProperty("gpuName", NullValueHandling = NullValueHandling.Ignore)] public string GpuName { get; set; }
        [JsonProperty("gpuDriverVersion", NullValueHandling = NullValueHandling.Ignore)] public string GpuDriverVersion { get; set; }
        [JsonProperty("detectedGpus", NullValueHandling = NullValueHandling.Ignore)] public List<string> DetectedGpus { get; set; }
        [JsonProperty("sources")] public List<SensorSource> Sources { get; set; } = new List<SensorSource>();
    }

    public class SensorSource
    {
        [JsonProperty("hardwareType")] public string HardwareType { get; set; }
        [JsonProperty("hardwareCount")] public int HardwareCount { get; set; }
        [JsonProperty("sensorCount")] public int SensorCount { get; set; }
        [JsonProperty("hardwareNames")] public List<string> HardwareNames { get; set; } = new List<string>();
    }

    // ─── PMD power analysis ─────────────────────────────────────────────────

    public class PmdChannelAggregate
    {
        [JsonProperty("channel")] public string Channel { get; set; }
        [JsonProperty("unit")] public string Unit { get; set; }
        [JsonProperty("avg")] public double Average { get; set; }
        [JsonProperty("min")] public double Min { get; set; }
        [JsonProperty("max")] public double Max { get; set; }
        [JsonProperty("sampleCount")] public int SampleCount { get; set; }
    }

    public class PmdSummaryResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("sampleTimeMs")] public int SampleTimeMs { get; set; }
        [JsonProperty("durationSec")] public double DurationSec { get; set; }
        [JsonProperty("hasPmdData")] public bool HasPmdData { get; set; }
        [JsonProperty("channels")] public List<PmdChannelAggregate> Channels { get; set; } = new List<PmdChannelAggregate>();
    }

    public class PmdTimePoint
    {
        [JsonProperty("tSec")] public double TSec { get; set; }
        [JsonProperty("value")] public double Value { get; set; }
    }

    public class PmdChannelSeries
    {
        [JsonProperty("channel")] public string Channel { get; set; }
        [JsonProperty("unit")] public string Unit { get; set; }
        [JsonProperty("sampleCount")] public int SampleCount { get; set; }
        [JsonProperty("points")] public List<PmdTimePoint> Points { get; set; } = new List<PmdTimePoint>();
    }

    public class PmdTimeSeriesResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("runIndex")] public int RunIndex { get; set; }
        [JsonProperty("sampleTimeMs")] public int SampleTimeMs { get; set; }
        [JsonProperty("downsampleHz")] public double DownsampleHz { get; set; }
        [JsonProperty("hasPmdData")] public bool HasPmdData { get; set; }
        [JsonProperty("channels")] public List<PmdChannelSeries> Channels { get; set; } = new List<PmdChannelSeries>();
    }

    // ─── Config writes ──────────────────────────────────────────────────────

    public class ConfigGetResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("properties")] public List<ConfigPropertyInfo> Properties { get; set; } = new List<ConfigPropertyInfo>();
    }

    public class ConfigPropertyInfo
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)] public string Value { get; set; }
        [JsonProperty("settable")] public bool Settable { get; set; }
    }

    public class ConfigSetResult
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("oldValue", NullValueHandling = NullValueHandling.Ignore)] public string OldValue { get; set; }
        [JsonProperty("newValue", NullValueHandling = NullValueHandling.Ignore)] public string NewValue { get; set; }
        [JsonProperty("applied")] public bool Applied { get; set; }
    }

    public class OverlayConfigResult
    {
        [JsonProperty("slot")] public int Slot { get; set; }
        [JsonProperty("path")] public string Path { get; set; }
        [JsonProperty("exists")] public bool Exists { get; set; }
        [JsonProperty("json", NullValueHandling = NullValueHandling.Ignore)] public string Json { get; set; }
    }

    public class ToggleOverlayEntryResult
    {
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("oldShowOnOverlay")] public bool OldShowOnOverlay { get; set; }
        [JsonProperty("newShowOnOverlay")] public bool NewShowOnOverlay { get; set; }
        [JsonProperty("oldIsEntryEnabled")] public bool OldIsEntryEnabled { get; set; }
        [JsonProperty("newIsEntryEnabled")] public bool NewIsEntryEnabled { get; set; }
        [JsonProperty("applied")] public bool Applied { get; set; }
    }

    public class SetLoggedSensorsResult
    {
        [JsonProperty("selected")] public bool Selected { get; set; }
        [JsonProperty("applied")] public List<string> Applied { get; set; } = new List<string>();
        [JsonProperty("missing")] public List<string> Missing { get; set; } = new List<string>();
    }

    public class SetRecordCommentResult
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("oldComment", NullValueHandling = NullValueHandling.Ignore)] public string OldComment { get; set; }
        [JsonProperty("newComment", NullValueHandling = NullValueHandling.Ignore)] public string NewComment { get; set; }
    }

    // ─── ETW buffer status ──────────────────────────────────────────────────

    public class EtwStatusResult
    {
        [JsonProperty("verdict")] public string Verdict { get; set; }
        [JsonProperty("reasoning")] public string Reasoning { get; set; }
        [JsonProperty("sampleSec")] public double SampleSec { get; set; }
        [JsonProperty("frameCount")] public int FrameCount { get; set; }
        [JsonProperty("frameViewServiceRunning", NullValueHandling = NullValueHandling.Ignore)] public bool? FrameViewServiceRunning { get; set; }
        [JsonProperty("bufferFillPctCurrent", NullValueHandling = NullValueHandling.Ignore)] public double? BufferFillPctCurrent { get; set; }
        [JsonProperty("bufferFillPctAvg", NullValueHandling = NullValueHandling.Ignore)] public double? BufferFillPctAvg { get; set; }
        [JsonProperty("bufferFillPctMax", NullValueHandling = NullValueHandling.Ignore)] public double? BufferFillPctMax { get; set; }
        [JsonProperty("buffersInUseLatest", NullValueHandling = NullValueHandling.Ignore)] public int? BuffersInUseLatest { get; set; }
        [JsonProperty("totalBuffersLatest", NullValueHandling = NullValueHandling.Ignore)] public int? TotalBuffersLatest { get; set; }
        [JsonProperty("eventsLostTotal", NullValueHandling = NullValueHandling.Ignore)] public int? EventsLostTotal { get; set; }
        [JsonProperty("eventsLostDelta", NullValueHandling = NullValueHandling.Ignore)] public int? EventsLostDelta { get; set; }
        [JsonProperty("buffersLostTotal", NullValueHandling = NullValueHandling.Ignore)] public int? BuffersLostTotal { get; set; }
        [JsonProperty("buffersLostDelta", NullValueHandling = NullValueHandling.Ignore)] public int? BuffersLostDelta { get; set; }
    }

    // ─── Cross-record analysis ──────────────────────────────────────────────

    public class RegressionsResult
    {
        [JsonProperty("processName")] public string ProcessName { get; set; }
        [JsonProperty("baselineId", NullValueHandling = NullValueHandling.Ignore)] public string BaselineId { get; set; }
        [JsonProperty("baselineRecordedAt", NullValueHandling = NullValueHandling.Ignore)] public DateTime? BaselineRecordedAt { get; set; }
        [JsonProperty("baselineValue", NullValueHandling = NullValueHandling.Ignore)] public double? BaselineValue { get; set; }
        [JsonProperty("metric")] public string Metric { get; set; }
        [JsonProperty("unit", NullValueHandling = NullValueHandling.Ignore)] public string Unit { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("samples")] public List<RegressionSample> Samples { get; set; } = new List<RegressionSample>();
    }

    public class RegressionSample
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("recordedAt")] public DateTime RecordedAt { get; set; }
        [JsonProperty("value")] public double Value { get; set; }
        [JsonProperty("deltaAbs")] public double DeltaAbs { get; set; }
        [JsonProperty("deltaPct")] public double DeltaPct { get; set; }
        [JsonProperty("gpu", NullValueHandling = NullValueHandling.Ignore)] public string Gpu { get; set; }
        [JsonProperty("gpuDriver", NullValueHandling = NullValueHandling.Ignore)] public string GpuDriver { get; set; }
        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)] public string Comment { get; set; }
    }

    public class SystemDriftResult
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("recordedAt", NullValueHandling = NullValueHandling.Ignore)] public DateTime? RecordedAt { get; set; }
        [JsonProperty("anyDifferent")] public bool AnyDifferent { get; set; }
        [JsonProperty("drift")] public List<SystemDriftField> Drift { get; set; } = new List<SystemDriftField>();
    }

    public class SystemDriftField
    {
        [JsonProperty("field")] public string Field { get; set; }
        [JsonProperty("recordValue", NullValueHandling = NullValueHandling.Ignore)] public string RecordValue { get; set; }
        [JsonProperty("currentValue", NullValueHandling = NullValueHandling.Ignore)] public string CurrentValue { get; set; }
        [JsonProperty("different")] public bool Different { get; set; }
    }

    public class OutliersResult
    {
        [JsonProperty("processName")] public string ProcessName { get; set; }
        [JsonProperty("metric")] public string Metric { get; set; }
        [JsonProperty("zThreshold")] public double ZThreshold { get; set; }
        [JsonProperty("totalSamples")] public int TotalSamples { get; set; }
        [JsonProperty("median", NullValueHandling = NullValueHandling.Ignore)] public double? Median { get; set; }
        [JsonProperty("medianAbsDeviation", NullValueHandling = NullValueHandling.Ignore)] public double? MedianAbsDeviation { get; set; }
        [JsonProperty("metricSource", NullValueHandling = NullValueHandling.Ignore)] public string MetricSource { get; set; }
        [JsonProperty("metricSourceWarning", NullValueHandling = NullValueHandling.Ignore)] public string MetricSourceWarning { get; set; }
        [JsonProperty("outliers")] public List<OutlierSample> Outliers { get; set; } = new List<OutlierSample>();
    }

    public class OutlierSample
    {
        [JsonProperty("recordId")] public string RecordId { get; set; }
        [JsonProperty("recordedAt")] public DateTime RecordedAt { get; set; }
        [JsonProperty("value")] public double Value { get; set; }
        [JsonProperty("zScore")] public double ZScore { get; set; }
    }
}
