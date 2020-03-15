using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
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

        public void StartSensorLogging()
        {
            throw new NotImplementedException();
        }

        public ISessionSensorData StopSensorLogging()
        {
            throw new NotImplementedException();
        }

        public void UpdateSensors()
        {
            throw new NotImplementedException();
        }
    }
}
