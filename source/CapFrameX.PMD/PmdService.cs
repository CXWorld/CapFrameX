using System;
using System.IO.Ports;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;

namespace CapFrameX.PMD
{
    public class PmdService : IPmdService
    {
        private readonly IPmdDriver _pmdDriver;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdService> _logger;

        public IObservable<PmdChannel[]> PmdChannelStream { get; }

        public IObservable<EPmdDriverStatus> PmdstatusStream { get; }

        public string PortName { get; set; }

        public int DownSamplingSize
        {
            get => _appConfiguration.DownSamplingSize;
            set
            {
                _appConfiguration.DownSamplingSize = value;
                OnDownSamplingSizeChanged();
            }
        }

        public EDownSamplingMode DownSamplingMode
        {
            get => _appConfiguration.DownSamplingMode;
            set
            {
                _appConfiguration.DownSamplingMode = value;
                OnDownSamplingModeChanged();
            }
        }

        public PmdService(IPmdDriver pmdDriver, IAppConfiguration appConfiguration,
            ILogger<PmdService> logger)
        {
            _pmdDriver = pmdDriver;
            _appConfiguration = appConfiguration;
            _logger = logger;

            PmdstatusStream = _pmdDriver.PmdstatusStream;
            PmdChannelStream = _pmdDriver.PmdChannelStream;
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

        private void OnDownSamplingSizeChanged()
        {
            throw new NotImplementedException();
        }

        private void OnDownSamplingModeChanged()
        {
            throw new NotImplementedException();
        }
    }
}
