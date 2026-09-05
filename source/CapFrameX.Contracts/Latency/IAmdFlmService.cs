using System;

namespace CapFrameX.Contracts.Latency
{
    public interface IAmdFlmService : IDisposable
    {
        IObservable<AmdFlmSample> SampleStream { get; }

        IObservable<AmdFlmStatus> StatusStream { get; }

        AmdFlmStatus Status { get; }

        bool IsRunning { get; }

        string LastError { get; }
    }
}
