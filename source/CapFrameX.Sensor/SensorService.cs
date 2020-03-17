using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;
        private Computer _computer;

        public bool UseSensorLogging => _appConfiguration.UseSensorLogging;

        public SensorService(IAppConfiguration appConfiguration,
                             ILogger<SensorService> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);

            StartOpenHardwareMonitor();
        }

        private void StartOpenHardwareMonitor()
        {
            try
            {
                _computer = new Computer();
                //_computer.HardwareAdded += new HardwareEventHandler(h => { });
                //_computer.HardwareRemoved += new HardwareEventHandler(h => { });

                _computer.Open();

                _computer.MainboardEnabled = false;
                _computer.FanControllerEnabled = false;
                _computer.GPUEnabled = true;
                _computer.CPUEnabled = true;
                _computer.RAMEnabled = true;
                _computer.HDDEnabled = false;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error when starting OpenHardwareMonitor");
            }
        }

        public bool CheckHardwareChanged(List<IOverlayEntry> overlayEntries)
        {
            throw new NotImplementedException();
        }

        public IOverlayEntry[] GetSensorOverlayEntries()
        {
            throw new NotImplementedException();
        }

        public IOverlayEntry GetSensorOverlayEntry(string identifier)
        {
            throw new NotImplementedException();
        }

        public ISessionSensorData GetSessionSensorData()
        {
            throw new NotImplementedException();
        }

        public void StartSensorLogging()
        {
            throw new NotImplementedException();
        }

        public void StopSensorLogging()
        {
            throw new NotImplementedException();
        }

        public void UpdateSensors()
        {
            throw new NotImplementedException();
        }

        public void CloseOpenHardwareMonitor()
        {
            _computer?.Close();
        }
    }
}
