using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Capture.Contracts
{
    public interface ICaptureService
    {
        // Keys:
        // ApplicationName
        // ProcessID
        // Dropped
        // TimeInSeconds
        // MsInPresentAPI
        // MsBetweenPresents
        // UntilDisplayedTimes
        // QPCTime (time stamp)
        Dictionary<string, int> ParameterNameIndexMapping { get; }

        string ColumnHeader { get; }

        // Dynamic column indices based on capture configuration
        int CPUStartQPCTimeInMs_Index { get; }
        int CpuBusy_Index { get; }
        int GpuBusy_Index { get; }
        int AnimationError_Index { get; }
        // -1 when the running session was started without PC latency tracking
        int MsPcLatency_Index { get; }
        int EtwBufferFillPct_Index { get; }
        int EtwBuffersInUse_Index { get; }
        int EtwTotalBuffers_Index { get; }
        int EtwEventsLost_Index { get; }
        int EtwBuffersLost_Index { get; }
        int ValidLineLength { get; }

        IObservable<string[]> FrameDataStream { get; }

        Subject<bool> IsCaptureModeActiveStream { get; }

        /// <summary>
        /// True while the capture service process is up and delivering data. Unlike
        /// <see cref="IsCaptureModeActiveStream"/> this says nothing about a recording being in
        /// progress - it only reports whether the service itself is healthy.
        /// </summary>
        bool IsCaptureServiceRunning { get; }

        /// <summary>
        /// Pushes the current value of <see cref="IsCaptureServiceRunning"/> on subscribe and
        /// every change afterwards.
        /// </summary>
        IObservable<bool> CaptureServiceRunningStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter);
    }
}
