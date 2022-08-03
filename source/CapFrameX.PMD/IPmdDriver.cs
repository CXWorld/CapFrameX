using System;

namespace CapFrameX.PMD
{
    public interface IPmdDriver
    {
        IObservable<PmdChannel[]> PmdChannelStream { get; }

        IObservable<EPmdDriverStatus> PmdstatusStream { get; }

        bool Connect(string comPort, bool calibrationMode);

        bool Disconnect();
    }
}
