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

        IObservable<string[]> FrameDataStream { get; }

        Subject<bool> IsCaptureModeActiveStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter);
    }
}
