using System;

namespace CapFrameX.PMD
{
    public interface IPmdService
    {
        IObservable<PmdChannel[]> PmdChannelStream { get; }

        IObservable<EPmdDriverStatus> PmdstatusStream { get; }

        string PortName { get; set; }

        bool StartDriver();

        bool ShutDownDriver();

        string[] GetPortNames();
    }
}
