﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PMD;
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

        int DownSamplingSize { get; set; }

        PmdSampleFilterMode DownSamplingMode { get; set; }

        IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<PmdChannel[]> channelDat0, int downSamplingSize);

        IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<PmdChannel[]> channelDat0, int downSamplingSize);
    }
}
