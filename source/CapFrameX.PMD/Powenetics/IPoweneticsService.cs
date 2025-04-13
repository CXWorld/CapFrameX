using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PMD;
using System;
using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.PMD.Powenetics
{
    public interface IPoweneticsService
    {
        IObservable<PoweneticsChannel[]> PmdChannelStream { get; }

        IObservable<EPmdDriverStatus> PmdstatusStream { get; }
        
        IObservable<int> PmdThroughput { get; }

        IObservable<int> LostPacketsCounterStream { get; }

        string PortName { get; set; }

        bool StartDriver();

        bool ShutDownDriver();

        string[] GetPortNames();

        int DownSamplingSize { get; set; }

        PmdSampleFilterMode DownSamplingMode { get; set; }

        bool IsServiceRunning { get; }

        IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<PoweneticsChannel[]> channelData);

        IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<PoweneticsChannel[]> channelData);
    }
}
