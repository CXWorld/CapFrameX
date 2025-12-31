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
        int CPUStartQPCTimeInMs_INDEX { get; }
        int CpuBusy_INDEX { get; }
        int GpuBusy_INDEX { get; }
        int EtwBufferFillPct_INDEX { get; }
        int EtwBuffersInUse_INDEX { get; }
        int EtwTotalBuffers_INDEX { get; }
        int EtwEventsLost_INDEX { get; }
        int EtwBuffersLost_INDEX { get; }
        int ValidLineLength { get; }

        IObservable<string[]> FrameDataStream { get; }

        Subject<bool> IsCaptureModeActiveStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter);
    }
}
