using MathNet.Numerics.Distributions;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;

namespace CapFrameX.PMD
{
    public class PmdUSBDriver : IPmdDriver
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        private ContinuousUniform _continuousUniformCurrent = new ContinuousUniform(10, 250);
        private ContinuousUniform _continuousUniformVoltage = new ContinuousUniform(0.8, 1.3);

        private IDisposable _pmdDataGenerator;
        private readonly ISubject<PmdChannel[]> _pmdChannelStream = new Subject<PmdChannel[]>();

        public IObservable<PmdChannel[]> PmdChannelStream => _pmdChannelStream.AsObservable();

        private PmdChannelArrayPosition[] ChannelMapping => PmdChannelExtensions.PmdChannelIndexMapping;

        public bool Connect()
        {
            _pmdDataGenerator?.Dispose();
            _pmdDataGenerator = GetPmdDataGenerator();
            return true;
        }

        public bool Disconnect()
        {
            _pmdDataGenerator?.Dispose();
            return true;
        }

        public EPmdDriverStatus GetPmdDriverStatus()
        {
            return EPmdDriverStatus.Ready;
        }

        private IDisposable GetPmdDataGenerator()
        {
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1))
                .ObserveOn(scheduler: Scheduler.Default)
                .Subscribe(x => GeneratePmdChannels());
        }

        private PmdChannel[] GeneratePmdChannels()
        {
            var channelArray = new PmdChannel[ChannelMapping.Length];

            QueryPerformanceCounter(out long timeStamp);

            for (int i = 0; i < ChannelMapping.Length; i++)
            {
                channelArray[i] = new PmdChannel()
                {
                    Name = ChannelMapping[i].Name,
                    Measurand = ChannelMapping[i].Measurand,
                    PmdChannelType = ChannelMapping[i].PmdChannelType,
                    TimeStamp = timeStamp,
                    Value = GetVoltageOrCurrentValue(ChannelMapping[i].Measurand, i, channelArray)
                };
            }

            return channelArray;
        }

        private float GetVoltageOrCurrentValue(PmdMeasurand pmdMeasurand, int index, PmdChannel[] channelArray)
        {
            float value = 0;
            switch (pmdMeasurand)
            {
                case PmdMeasurand.Current:
                    value = (float)_continuousUniformCurrent.RandomSource.NextDouble();
                    break;
                case PmdMeasurand.Voltage:
                    value = (float)_continuousUniformVoltage.RandomSource.NextDouble();
                    break;
                case PmdMeasurand.Power:
                    var indices = PmdChannelExtensions.PowerDependcyIndices[index];
                    value = channelArray[indices[0]].Value * channelArray[indices[1]].Value;
                    break;
            }

            return value;
        }
    }
}
