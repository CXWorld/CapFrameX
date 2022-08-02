using System;

namespace CapFrameX.PMD
{
    public interface IPmdDriver
    {
        IObservable<PmdChannel[]> PmdChannelStream { get; }

        EPmdDriverStatus GetPmdDriverStatus();

        bool Connect(string comPort, bool calibrationMode);

        bool Disconnect();
    }
}
