using CapFrameX.Data;
using CapFrameX.Mcp.Attributes;
using System;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class CaptureStatusTools
    {
        private readonly CaptureManager _captureManager;

        public CaptureStatusTools(CaptureManager captureManager)
        {
            _captureManager = captureManager;
        }

        [McpServerTool(Name = "cfx_get_capture_status",
            Description = "Returns the current read-only capture state of the running CapFrameX instance: " +
                "whether a capture is active, whether the capture service is locked, whether a delay countdown is running, " +
                "and the last known status (Started, StartedDelay, StartedTimer, StartedRemote, Processing, Stopped). " +
                "Use this to answer 'is CapFrameX recording right now?' or to confirm a capture finished before analyzing the result.")]
        public CaptureStatusInfo GetCaptureStatus()
        {
            var info = new CaptureStatusInfo
            {
                IsCapturing = _captureManager.IsCapturing,
                IsLocked = _captureManager.LockCaptureService,
                DelayCountdownRunning = _captureManager.DelayCountdownRunning,
                OsdAutoDisabled = _captureManager.OSDAutoDisabled,
                State = "unknown",
            };

            // CaptureStatusChange is backed by a BehaviorSubject — subscribing
            // synchronously delivers the latest value immediately.
            try
            {
                CaptureStatus latest = default;
                bool haveLatest = false;
                using (_captureManager.CaptureStatusChange.Subscribe(s =>
                {
                    latest = s;
                    haveLatest = true;
                })) { /* Subscription disposed immediately after pulling the latest value */ }

                if (haveLatest)
                {
                    info.State = latest.Status?.ToString() ?? "Stopped";
                    info.LastMessage = latest.Message;
                }
            }
            catch (Exception)
            {
                // If we can't read the observable, fall back to inferring from booleans.
                info.State = info.IsCapturing ? "Started" : "Stopped";
            }

            return info;
        }
    }
}
