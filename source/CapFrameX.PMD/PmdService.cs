using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;

namespace CapFrameX.PMD
{
    public class PmdService : IPmdService
    {
        public const int MAX_DOWNSAMPLING_SIZE = 100;

        private readonly IPmdDriver _pmdDriver;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdService> _logger;
        private readonly ISubject<PmdChannel[]> _pmdChannelStream = new Subject<PmdChannel[]>();
        private readonly List<PmdChannel[]> _channelsBuffer = new List<PmdChannel[]>(MAX_DOWNSAMPLING_SIZE + 1);

        public IObservable<PmdChannel[]> PmdChannelStream => _pmdChannelStream.AsObservable();

        public IObservable<EPmdDriverStatus> PmdstatusStream { get; }

        public string PortName { get; set; }

        public int DownSamplingSize
        {
            get => _appConfiguration.DownSamplingSize;
            set
            {
                _appConfiguration.DownSamplingSize = value;
            }
        }

        public EDownSamplingMode DownSamplingMode
        {
            get => _appConfiguration.DownSamplingMode;
            set
            {
                _appConfiguration.DownSamplingMode = value;
            }
        }

        public PmdService(IPmdDriver pmdDriver, IAppConfiguration appConfiguration,
            ILogger<PmdService> logger)
        {
            _pmdDriver = pmdDriver;
            _appConfiguration = appConfiguration;
            _logger = logger;

            PmdstatusStream = _pmdDriver.PmdstatusStream;
            _pmdDriver.PmdChannelStream
                .ObserveOn(new EventLoopScheduler())
                .Subscribe(channels => FilterChannels(channels));
        }

        public bool StartDriver()
        {
            if (PortName == null) return false;

            // ToDo: manage calibration mode
            return _pmdDriver.Connect(PortName, false);
        }

        public bool ShutDownDriver() => _pmdDriver.Disconnect();

        public string[] GetPortNames()
        {
            var comPorts = SerialPort.GetPortNames();
            Array.Sort(comPorts);

            return comPorts;
        }

        private void FilterChannels(PmdChannel[] channel)
        {
            if (DownSamplingSize > 1)
            {
                _channelsBuffer.Add(channel);

                if (_channelsBuffer.Count > DownSamplingSize)
                {
                    _channelsBuffer.RemoveRange(0, _channelsBuffer.Count - DownSamplingSize);
                    _pmdChannelStream.OnNext(SetPmdChannelStream(_channelsBuffer));
                }
            }
            else
            {
                if (_channelsBuffer.Any())
                    _channelsBuffer.Clear();

                _pmdChannelStream.OnNext(channel);
            }
        }

        private PmdChannel[] SetPmdChannelStream(List<PmdChannel[]> buffer)
        {
            PmdChannel[] channels;

            switch (DownSamplingMode)
            {
                case EDownSamplingMode.Single:
                    channels = buffer.Last();
                    break;
                case EDownSamplingMode.Average:
                    channels = GetAveragePmdChannel(buffer);
                    break;

                default:
                    channels = buffer.Last();
                    break;
            }

            return channels;
        }

        private PmdChannel[] GetAveragePmdChannel(IList<PmdChannel[]> buffer)
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
