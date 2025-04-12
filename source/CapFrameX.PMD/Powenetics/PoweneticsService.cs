using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Contracts.PMD;

namespace CapFrameX.PMD
{
    public class PoweneticsService : IPoweneticsService
    {
        private readonly IPoweneticsDriver _pmdDriver;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PoweneticsService> _logger;
        private readonly ISubject<PoweneticsChannel[]> _pmdChannelStream = new Subject<PoweneticsChannel[]>();

        private IDisposable _pmdChannelStreamDisposable;
        private IObservable<int> _pmdThroughput;
        private LinkedList<PoweneticsChannel[]> _channelsBuffer = new LinkedList<PoweneticsChannel[]>();

        public IObservable<PoweneticsChannel[]> PmdChannelStream => _pmdChannelStream.AsObservable();

        public IObservable<EPmdDriverStatus> PmdstatusStream => _pmdDriver.PmdstatusStream;

        public IObservable<int> PmdThroughput => _pmdThroughput;

        public IObservable<int> LostPacketsCounterStream => _pmdDriver.LostPacketsCounterStream;

        public string PortName { get; set; }

        public int DownSamplingSize
        {
            get => _appConfiguration.DownSamplingSize;
            set
            {
                _appConfiguration.DownSamplingSize = value;
                SetDownsampledStream();
            }
        }

        public PmdSampleFilterMode DownSamplingMode
        {
            get => _appConfiguration.DownSamplingMode.ConvertToEnum<PmdSampleFilterMode>();
            set
            {
                _appConfiguration.DownSamplingMode = value.ConvertToString();
            }
        }

        public PoweneticsService(IPoweneticsDriver pmdDriver, IAppConfiguration appConfiguration,
            ILogger<PoweneticsService> logger)
        {
            _pmdDriver = pmdDriver;
            _appConfiguration = appConfiguration;
            _logger = logger;
        }

        public bool StartDriver()
        {
            if (PortName == null) return false;

            SetDownsampledStream();
                
            // Troughput after downsampling
            _pmdThroughput =
                _pmdChannelStream
                .Select(channels => 1)
                .Buffer(TimeSpan.FromSeconds(2))
                .Select(buffer => buffer.Count());

            // ToDo: manage calibration mode
            return _pmdDriver.Connect(PortName, false);
        }

        private void SetDownsampledStream()
        {
            _pmdChannelStreamDisposable?.Dispose();
            _pmdChannelStreamDisposable = _pmdDriver
                .PmdChannelStream
                .ObserveOn(new EventLoopScheduler())
                .Buffer(DownSamplingSize)
                .Subscribe(channels => FilterChannelsBuffer(channels));
        }

        public bool ShutDownDriver()
        {
            _pmdChannelStreamDisposable?.Dispose();
            _channelsBuffer = new LinkedList<PoweneticsChannel[]>();

            return _pmdDriver.Disconnect();
        }

        public string[] GetPortNames()
        {
            var comPorts = SerialPort.GetPortNames();
            Array.Sort(comPorts);

            return comPorts;
        }

        public IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<PoweneticsChannel[]> channelData)
        {
            var minTimeStamp = channelData.First()[0].TimeStamp;
            foreach (var channel in channelData)
            {
                var sumPower = PoweneticsChannelExtensions.EPSPowerIndexGroup.Sum(index => channel[index].Value);
                yield return new Point((channel[0].TimeStamp - minTimeStamp) * 1E-03, sumPower);
            }
        }

        public IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<PoweneticsChannel[]> channelData)
        {
            var minTimeStamp = channelData.First()[0].TimeStamp;
            foreach (var channel in channelData)
            {
                var sumPower = PoweneticsChannelExtensions.GPUPowerIndexGroup.Sum(index => channel[index].Value);
                yield return new Point((channel[0].TimeStamp - minTimeStamp) * 1E-03, sumPower);
            }
        }

        private void FilterChannelsBuffer(IList<PoweneticsChannel[]> buffer)
        {
            PoweneticsChannel[] channels;

            switch (DownSamplingMode)
            {
                case PmdSampleFilterMode.Single:
                    channels = buffer.Last();
                    break;
                case PmdSampleFilterMode.Average:
                    channels = GetAveragePmdChannel(buffer);
                    break;

                default:
                    channels = buffer.Last();
                    break;
            }

            _pmdChannelStream.OnNext(channels);
        }

        private PoweneticsChannel[] GetAveragePmdChannel(IList<PoweneticsChannel[]> buffer)
        {
            var avergeSample = buffer.First();

            for (int i = 0; i < avergeSample.Length; i++)
            {
                foreach (var sample in buffer.Skip(1))
                {
                    avergeSample[i].Value += sample[i].Value;
                }

                avergeSample[i].Value /= buffer.Count;
            }

            return avergeSample;
        }
    }
}
