using CapFrameX.Contracts.PMD;
using System;

namespace CapFrameX.PMD.Powenetics
{
    public interface IPoweneticsDriver
    {
        IObservable<PoweneticsChannel[]> PmdChannelStream { get; }

        IObservable<EPmdDriverStatus> PmdstatusStream { get; }

        IObservable<int> LostPacketsCounterStream { get; }

        bool Connect(string comPort, bool calibrationMode);

        bool Disconnect();
    }
}
