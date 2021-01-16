using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace CapFrameX.Sensor
{
    public class SessionSensorDataLive
    {
        private long _timestampStartLogging;

        private ISessionSensorData2 _data = new SessionSensorData2();

        public SessionSensorDataLive()
        {
            _timestampStartLogging = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        }

        public void AddMeasureTime(DateTime dateTime)
        {
            var timestampLogging = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
            long ellapsedMilliseconds = timestampLogging - _timestampStartLogging;
            var measureTimeToAdd = ellapsedMilliseconds * 1E-03;
            var latestMeasureTime = _data.MeasureTime.Values.Last?.Value ?? 0;
            _data.MeasureTime.Values.AddLast(measureTimeToAdd);
            _data.BetweenMeasureTime.Values.AddLast(measureTimeToAdd - latestMeasureTime);
        }

        public void AddSensorValue(ISensorEntry sensor, float currentValue)
        { 
            if(!_data.TryGetValue(sensor.Identifier, out var collection)) {
                collection = new SessionSensorEntry<double>(sensor.Name, sensor.SensorType);
                _data.Add(sensor.Identifier, collection);
            }
            collection.Values.AddLast(currentValue);
        }

        public ISessionSensorData2 ToSessionSensorData()
        {
            return _data;
        }
    }
}
