using System;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;

namespace CapFrameX.PMD
{
    public class PmdService : IPmdService
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdService> _logger;

        public IObservable<PmdChannel[]> PmdChannelStream { get; }

        public bool UseVirtualMode
        {
            get => _appConfiguration.UseVirtualMode;
            set 
            {
                _appConfiguration.UseVirtualMode = value;
                OnDataSourceModeChanged();
            }
        }

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

        public PmdService(IAppConfiguration appConfiguration,
            ILogger<PmdService> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;
        }

        public bool Connect()
        {
            throw new NotImplementedException();
        }

        public bool Disconnect()
        {
            throw new NotImplementedException();
        }

        private void OnDataSourceModeChanged()
        {
            throw new NotImplementedException();
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
