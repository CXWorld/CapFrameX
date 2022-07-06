using System;
using System.Collections.Generic;
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
        }

        public bool StartDriver() => _pmdDriver.Connect();


        public bool ShutDownDriver() => _pmdDriver.Disconnect();

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
