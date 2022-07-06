using System;

namespace CapFrameX.PMD
{
    public interface IPmdService
    {
        IObservable<PmdChannel[]> PmdChannelStream { get; }

        bool StartDriver();

        bool ShutDownDriver();
    }
}
