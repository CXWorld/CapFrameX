using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapFrameX.Data.Session.Converters
{
    public static class SessionSensorDataConverter
    {
        public static void ConvertToSensorData2(ISessionRun sessionRun)
        {
            sessionRun.SensorData2 = new SessionSensorData2();

            void InsertValues(string key, string sensorName, string sensorType, IEnumerable<object> values)
            {
                if (!values.All(v => v is double || v is int))
                {
                    return;
                }

                if (!sessionRun.SensorData2.TryGetValue(key, out var container))
                {
                    container = new SessionSensorEntry(sensorName, sensorType);
                    sessionRun.SensorData2[key] = container;
                }
                foreach (var value in values)
                {
                    container.Values.Add(Convert.ToDouble(value));
                }
            }

            //Times
            InsertValues("MeasureTime", "MeasureTime", "Time", sessionRun.SensorData.MeasureTime.Cast<object>());
            InsertValues("BetweenMeasureTime", "BetweenMeasureTime", "Time", sessionRun.SensorData.BetweenMeasureTimes.Cast<object>());

            //CPU
            InsertValues("CpuPower", "CpuPower", "Clock", sessionRun.SensorData.CpuPower.Cast<object>());
            InsertValues("CpuTemp", "CpuTemp", "Temperature", sessionRun.SensorData.CpuTemp.Cast<object>());
            InsertValues("CpuLoad", "CpuLoad", "Load", sessionRun.SensorData.CpuUsage.Cast<object>());
            InsertValues("CpuMaxClock", "CpuMaxClock", "Clock", sessionRun.SensorData.CpuMaxClock.Cast<object>());
            InsertValues("CpuMaxThreadLoad", "CpuMaxThreadLoad", "Load", sessionRun.SensorData.CpuMaxThreadUsage.Cast<object>());

            //GPU
            InsertValues("GpuClock", "GPU Core", "Clock", sessionRun.SensorData.GpuClock.Cast<object>());
            InsertValues("GpuPower", "GpuPower", "Power", sessionRun.SensorData.GpuPower.Cast<object>());
            InsertValues("GpuTemp", "GpuTemp", "Temperature", sessionRun.SensorData.GpuTemp.Cast<object>());
            InsertValues("GpuUsage", "GpuUsage", "Load", sessionRun.SensorData.GpuUsage.Cast<object>());

            InsertValues("RamUsage", "Used Memory", "Load", sessionRun.SensorData.RamUsage.Cast<object>());
            InsertValues("VRamUsage", "GPU Memory Dedicated", "Load", sessionRun.SensorData.VRamUsage.Cast<object>());
        }
    }
}
