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
        int EtwBufferFillPct_Index { get; }
        int EtwBuffersInUse_Index { get; }
        int EtwTotalBuffers_Index { get; }
        int EtwEventsLost_Index { get; }
        int EtwBuffersLost_Index { get; }
        int ValidLineLength { get; }

        IObservable<string[]> FrameDataStream { get; }

        Subject<bool> IsCaptureModeActiveStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter);
    }
}
