using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PMD;
using CapFrameX.Extensions.NetStandard;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;

namespace CapFrameX.PMD.Powenetics
{
    public class PoweneticsService : IPoweneticsService
    {
        private readonly IPoweneticsDriver _pmdDriver;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ISubject<PoweneticsChannel[]> _pmdChannelStream = new Subject<PoweneticsChannel[]>();

        private IDisposable _pmdChannelStreamDisposable;
        private IObservable<int> _pmdThroughput;
        private bool _isServiceRunning;

        public IObservable<PoweneticsChannel[]> PmdChannelStream => _pmdChannelStream.AsObservable();

        public IObservable<EPmdDriverStatus> PmdStatusStream => _pmdDriver.PmdstatusStream;

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

        public bool IsServiceRunning => _isServiceRunning;

        public LinkedList<PoweneticsChannel[]> ChannelsBuffer { get; private set; } = new LinkedList<PoweneticsChannel[]>();

        public PoweneticsService(IPoweneticsDriver pmdDriver, IAppConfiguration appConfiguration)
        {
            _pmdDriver = pmdDriver;
            _appConfiguration = appConfiguration;
        }

        public bool StartDriver()
        {
            if (PortName == null) return false;

            _isServiceRunning = true;
            SetDownsampledStream();

            // Packets per 2 s as the device sends them. After the time-based downsampling the rate
            // is always 1000 / DownSamplingSize and would hide the real device rate.
            _pmdThroughput =
                _pmdDriver.PmdChannelStream
                .Select(channels => 1)
                .Buffer(TimeSpan.FromSeconds(2))
                .Select(buffer => buffer.Count());

            // ToDo: manage calibration mode
            return _pmdDriver.Connect(PortName, false);
        }

        private void SetDownsampledStream()
        {
            // DownSamplingSize is a length in ms; bins follow the packet times because the device
            // does not send exactly one packet per millisecond.
            var downsampler = new PoweneticsDownsampler(DownSamplingSize);

            _pmdChannelStreamDisposable?.Dispose();
            _pmdChannelStreamDisposable = _pmdDriver
                .PmdChannelStream
                .ObserveOn(new EventLoopScheduler())
                .Subscribe(channels =>
                {
                    if (downsampler.TryAdd(channels, out var bin, out var binStart))
                        FilterChannelsBuffer(bin, binStart);
                });
        }

        public bool ShutDownDriver()
        {
            _isServiceRunning = false;
            _pmdChannelStreamDisposable?.Dispose();
            ChannelsBuffer = new LinkedList<PoweneticsChannel[]>();

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

        private void FilterChannelsBuffer(IList<PoweneticsChannel[]> buffer, long binStart)
        {
            PoweneticsChannel[] channels;

            switch (DownSamplingMode)
            {
                case PmdSampleFilterMode.Single:
                    channels = (PoweneticsChannel[])buffer.Last().Clone();
                    break;
                case PmdSampleFilterMode.Average:
                    channels = GetAveragePmdChannel(buffer);
                    break;

                default:
                    channels = (PoweneticsChannel[])buffer.Last().Clone();
                    break;
            }

            // The sample stands for its whole bin, which starts at binStart.
            for (int i = 0; i < channels.Length; i++)
            {
                channels[i].TimeStamp = binStart;
            }

            _pmdChannelStream.OnNext(channels);
        }

        private PoweneticsChannel[] GetAveragePmdChannel(IList<PoweneticsChannel[]> buffer)
        {
            var avergeSample = (PoweneticsChannel[])buffer.First().Clone();

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
