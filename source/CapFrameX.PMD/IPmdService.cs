using System;
using System.Collections.Generic;
using System.Windows;

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

        IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<PmdChannel[]> channelDat0, int downSamplingSize);
    }
}
