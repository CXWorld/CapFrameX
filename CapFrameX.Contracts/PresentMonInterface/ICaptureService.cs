using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.PresentMonInterface
{
    public interface ICaptureService
    {
        IObservable<string> RedirectedOutputDataStream { get; }

        IObservable<string> RedirectedOutputErrorStream { get; }

        bool StartCaptureService(IServiceStartInfo startinfo);

        bool StopCaptureService();

        IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter);
    }
}