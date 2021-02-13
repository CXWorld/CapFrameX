using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.PresentMonInterface
{
    public interface ICaptureService
    {
        IObservable<string[]> RedirectedOutputDataStream { get; }

        Subject<bool> IsCaptureModeActiveStream { get; }

        Subject<bool> IsLoggingActiveStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter);
    }
}