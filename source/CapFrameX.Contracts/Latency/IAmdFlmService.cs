using System;

namespace CapFrameX.Contracts.Latency
{
    public interface IAmdFlmService : IDisposable
    {
        IObservable<AmdFlmSample> SampleStream { get; }

        bool IsRunning { get; }

        string LastError { get; }
    }
}
