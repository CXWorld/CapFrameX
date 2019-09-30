using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.PresentMonInterface
{
    public interface ICaptureService
    {
        IObservable<string> RedirectedOutputDataStream { get; }

        IObservable<string> RedirectedOutputErrorStream { get; }

        Subject<bool> IsCaptureModeActiveStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter);
    }
}