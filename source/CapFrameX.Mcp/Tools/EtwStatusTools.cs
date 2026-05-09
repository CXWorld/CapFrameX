using CapFrameX.Capture.Contracts;
using CapFrameX.Configuration;
using CapFrameX.Mcp.Attributes;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class EtwStatusTools
    {
        private readonly ICaptureService _captureService;

        public EtwStatusTools(ICaptureService captureService)
        {
            _captureService = captureService;
        }

        [McpServerTool(Name = "cfx_get_etw_status",
            Description = "Reports ETW (Event Tracing for Windows) buffer health from the live PresentMon stream. " +
                "Critical for diagnosing 'why are my captures missing frames?' — eventsLost > 0 means frame data was dropped. " +
                "Subscribes to the live frame stream for `sampleSec` seconds, aggregates per-frame ETW counters " +
                "(EtwBufferFillPct, EtwBuffersInUse/Total, EtwEventsLost, EtwBuffersLost), and also detects " +
                "competing ETW sessions (NVIDIA FrameViewService is the most common culprit). " +
                "Returns 'stream-idle' if PresentMon isn't currently producing frames — start a capture first.")]
        public async Task<EtwStatusResult> GetEtwStatus(
            [Description("Sample window length in seconds. Default 2. Use 5+ for noisier sessions.")] double sampleSec = 2.0,
            [Description("Hard cap on frames inspected within the window. Default 500.")] int maxFrames = 500)
        {
            if (sampleSec <= 0) sampleSec = 2.0;
            if (maxFrames <= 0) maxFrames = 500;

            var result = new EtwStatusResult
            {
                SampleSec = sampleSec,
                FrameViewServiceRunning = SafeCheckFrameView(),
            };

            // Buffer frames over the sample window. .Buffer(TimeSpan) emits one list per window;
            // combined with Take(1) we get exactly one list — possibly empty if no frames arrived.
            IList<string[]> frames = Array.Empty<string[]>();
            try
            {
                frames = await _captureService.FrameDataStream
                    .Take(maxFrames)
                    .Buffer(TimeSpan.FromSeconds(sampleSec))
                    .Take(1);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "MCP cfx_get_etw_status: stream subscription failed");
            }

            result.FrameCount = frames?.Count ?? 0;
            if (result.FrameCount == 0)
            {
                result.Verdict = "stream-idle";
                result.Reasoning = "No frames received within the sample window — PresentMon is not currently streaming. " +
                    "Start a capture (cfx_start_capture) and re-check.";
                if (result.FrameViewServiceRunning == true)
                    result.Reasoning += " Note: FrameViewService is running, which may be blocking PresentMon's ETW session.";
                return result;
            }

            // Resolve column indices once.
            int idxFill = _captureService.EtwBufferFillPct_Index;
            int idxInUse = _captureService.EtwBuffersInUse_Index;
            int idxTotal = _captureService.EtwTotalBuffers_Index;
            int idxEventsLost = _captureService.EtwEventsLost_Index;
            int idxBuffersLost = _captureService.EtwBuffersLost_Index;
            int validLen = _captureService.ValidLineLength;

            double sumFill = 0; double maxFill = 0; int fillN = 0;
            int? firstEvents = null, lastEvents = null;
            int? firstBuffers = null, lastBuffers = null;
            int? lastInUse = null, lastTotal = null;

            foreach (var frame in frames)
            {
                if (frame == null || frame.Length < validLen) continue;

                if (TryDouble(frame[idxFill], out double fill))
                {
                    sumFill += fill;
                    if (fill > maxFill) maxFill = fill;
                    fillN++;
                }
                if (TryInt(frame[idxInUse], out int inUse)) lastInUse = inUse;
                if (TryInt(frame[idxTotal], out int total)) lastTotal = total;
                if (TryInt(frame[idxEventsLost], out int events))
                {
                    if (firstEvents == null) firstEvents = events;
                    lastEvents = events;
                }
                if (TryInt(frame[idxBuffersLost], out int buffers))
                {
                    if (firstBuffers == null) firstBuffers = buffers;
                    lastBuffers = buffers;
                }
            }

            if (fillN > 0)
            {
                result.BufferFillPctAvg = Math.Round(sumFill / fillN, 2);
                result.BufferFillPctMax = Math.Round(maxFill, 2);
                // Use last known fill as 'current' — last frame in the window.
                if (TryDouble(frames[frames.Count - 1]?[idxFill], out double lastFill))
                    result.BufferFillPctCurrent = Math.Round(lastFill, 2);
            }
            result.BuffersInUseLatest = lastInUse;
            result.TotalBuffersLatest = lastTotal;
            result.EventsLostTotal = lastEvents;
            result.EventsLostDelta = (firstEvents.HasValue && lastEvents.HasValue) ? lastEvents - firstEvents : null;
            result.BuffersLostTotal = lastBuffers;
            result.BuffersLostDelta = (firstBuffers.HasValue && lastBuffers.HasValue) ? lastBuffers - firstBuffers : null;

            // ─── verdict ────────────────────────────────────────────────────
            // Priority: critical (active loss) > warning (past loss / pressure / conflict) > healthy.
            var notes = new List<string>();
            string verdict = "healthy";

            if (result.EventsLostDelta.GetValueOrDefault() > 0)
            {
                notes.Add(FormattableString.Invariant(
                    $"ETW dropped {result.EventsLostDelta} event(s) during the sample window — frametime data is incomplete RIGHT NOW."));
                verdict = "critical";
            }
            else if (result.EventsLostTotal.GetValueOrDefault() > 0)
            {
                notes.Add(FormattableString.Invariant(
                    $"ETW has {result.EventsLostTotal} cumulative dropped event(s) from earlier in this PresentMon session."));
                if (verdict == "healthy") verdict = "warning";
            }

            if (result.BuffersLostDelta.GetValueOrDefault() > 0)
            {
                notes.Add(FormattableString.Invariant(
                    $"{result.BuffersLostDelta} buffer(s) lost during the sample window — severe ETW pressure."));
                verdict = "critical";
            }
            else if (result.BuffersLostTotal.GetValueOrDefault() > 0)
            {
                notes.Add(FormattableString.Invariant(
                    $"{result.BuffersLostTotal} cumulative buffer(s) lost earlier in this session."));
                if (verdict == "healthy") verdict = "warning";
            }

            if (result.BufferFillPctMax.GetValueOrDefault() >= 90)
            {
                notes.Add(FormattableString.Invariant(
                    $"Buffer fill peaked at {result.BufferFillPctMax:F1}% — close to overflow. Consider reducing sensor logging rate or stopping concurrent ETW consumers."));
                if (verdict == "healthy") verdict = "warning";
            }

            if (result.FrameViewServiceRunning == true)
            {
                notes.Add("FrameViewService (NVIDIA) is running — it competes for the same ETW providers as PresentMon and is a known cause of dropped events. " +
                    "Stop the FrameViewService or uninstall the FrameView SDK and restart CapFrameX.");
                if (verdict == "healthy") verdict = "warning";
            }

            if (notes.Count == 0)
            {
                notes.Add(FormattableString.Invariant(
                    $"No dropped events; buffer fill avg {result.BufferFillPctAvg:F1}% across {result.FrameCount} frames in the {sampleSec:F1}s window."));
            }

            result.Verdict = verdict;
            result.Reasoning = string.Join(" ", notes);
            return result;
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private static bool? SafeCheckFrameView()
        {
            try { return EtwServiceChecker.IsFrameViewServiceRunning(); }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "MCP cfx_get_etw_status: FrameView check failed");
                return null;
            }
        }

        private static bool TryDouble(string s, out double v) =>
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);

        private static bool TryInt(string s, out int v) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    }
}
