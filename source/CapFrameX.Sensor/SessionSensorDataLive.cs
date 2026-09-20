using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using System;
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
            var latestMeasureTime = _data.MeasureTime.Values.LastOrDefault();
            _data.MeasureTime.Values.AddLast(measureTimeToAdd);
            _data.BetweenMeasureTime.Values.AddLast(measureTimeToAdd - latestMeasureTime);
        }

        public void AddSensorValue(ISensorEntry sensor, float currentValue)
        {
            if (!_data.TryGetValue(sensor.Identifier, out var collection))
            {
                // Do not create an all-NaN sensor when an asynchronous source has not produced
                // its first value yet. Once it does, preceding measure slots are backfilled below.
                if (float.IsNaN(currentValue) || float.IsInfinity(currentValue))
                    return;

                var entry = new SessionSensorEntry(sensor.Name, sensor.SensorType);
                entry.StableIdentifier = SensorIdentifierHelper.BuildStableIdentifier(sensor);
                collection = entry;
                _data.Add(sensor.Identifier, collection);

                int precedingMeasureCount = Math.Max(0, _data.MeasureTime.Values.Count - 1);
                for (int i = 0; i < precedingMeasureCount; i++)
                    collection.Values.AddLast(double.NaN);
            }
            collection.Values.AddLast(currentValue);
        }

        public void CompleteMeasure()
        {
            int measureCount = _data.MeasureTime.Values.Count;
            foreach (var pair in _data.Where(pair => pair.Key != nameof(_data.MeasureTime)
                && pair.Key != nameof(_data.BetweenMeasureTime)))
            {
                while (pair.Value.Values.Count < measureCount)
                    pair.Value.Values.AddLast(double.NaN);
            }
        }

        public ISessionSensorData2 ToSessionSensorData()
        {
            return _data;
        }
    }
}
