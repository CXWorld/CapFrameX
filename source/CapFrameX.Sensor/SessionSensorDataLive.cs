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
        private static HashSet<string> _relevantNames
            = new HashSet<string>()
            {
                "CPU Total",
                "CPU Max",
                "CPU Package",
                "CPU Max Clock",
                "Used Memory",
                "GPU Core",
                "GPU Total",
                "GPU Power",
                "GPU Power Limit",
                "GPU Memory Dedicated"
            };

        private long _timestampStartLogging;
        private List<double> _measureTime;
        private LinkedList<int> _cpuUsage;
        private LinkedList<int> _cpuPower;
        private LinkedList<int> _cpuTemp;
        private LinkedList<int> _cpuMaxThreadUsage;
        private LinkedList<int> _cpuMaxClock;
        private LinkedList<int> _gpuUsage;
        private LinkedList<int> _gpuClock;
        private LinkedList<double> _ramUsage;
        private LinkedList<bool> _isInGpuLimit;
        private LinkedList<int> _gpuPower;
        private LinkedList<int> _gpuTemp;
        private LinkedList<int> _gpuPowerLimit;
        private LinkedList<int> _vRamUsage;

        public SessionSensorDataLive()
        {
            _measureTime = new List<double>();
            _cpuUsage = new LinkedList<int>();
            _cpuMaxThreadUsage = new LinkedList<int>();
            _cpuPower = new LinkedList<int>();
            _cpuTemp = new LinkedList<int>();
            _cpuMaxClock = new LinkedList<int>();
            _gpuUsage = new LinkedList<int>();
            _gpuClock = new LinkedList<int>();
            _ramUsage = new LinkedList<double>();
            _isInGpuLimit = new LinkedList<bool>();
            _gpuPower = new LinkedList<int>();
            _gpuTemp = new LinkedList<int>();
            _gpuPowerLimit = new LinkedList<int>();
            _vRamUsage = new LinkedList<int>();

            _timestampStartLogging = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        }

        public void AddMeasureTime(DateTime dateTime)
        {
            var timestampLogging = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
            long ellapsedMilliseconds = timestampLogging - _timestampStartLogging;
            _measureTime.Add(ellapsedMilliseconds * 1E-03);
        }

        public void AddSensorValue(ISensorEntry sensor, float currentValue)
        {
            var name = sensor.Name;

            if (!_relevantNames.Contains(name))
                return;

            Enum.TryParse(sensor.HardwareType, out HardwareType hardwareType);
            Enum.TryParse(sensor.SensorType, out SensorType sensorType);

            switch (name)
            {
                case "CPU Total" when hardwareType == HardwareType.CPU:
                    _cpuUsage.AddLast((int)Math.Round(currentValue));
                    break;
                case "CPU Max" when hardwareType == HardwareType.CPU:
                    _cpuMaxThreadUsage.AddLast((int)Math.Round(currentValue));
                    break;
                case "CPU Max Clock" when sensorType == SensorType.Clock:
                    _cpuMaxClock.AddLast((int)Math.Round(currentValue));
                    break;
                case "CPU Package" when sensorType == SensorType.Power:
                    _cpuPower.AddLast((int)Math.Round(currentValue));
                    break;
                case "CPU Package" when sensorType == SensorType.Temperature:
                    _cpuTemp.AddLast((int)Math.Round(currentValue));
                    break;
                case "GPU Core" when sensorType == SensorType.Load:
                    _gpuUsage.AddLast((int)Math.Round(currentValue));
                    break;
                case "GPU Core" when sensorType == SensorType.Temperature:
                    _gpuTemp.AddLast((int)Math.Round(currentValue));
                    break;
                case "GPU Core" when sensorType == SensorType.Clock:
                    _gpuClock.AddLast((int)Math.Round(currentValue));
                    break;
                case "GPU Power" when hardwareType == HardwareType.GpuNvidia:
                    _gpuPower.AddLast((int)Math.Round(currentValue));
                    break;
                case "GPU Power Limit" when hardwareType == HardwareType.GpuNvidia:
                    _gpuPowerLimit.AddLast((int)Math.Round(currentValue));
                    break;
                case "GPU Total" when hardwareType == HardwareType.GpuAti:
                    _gpuPower.AddLast((int)Math.Round(currentValue));
                    break;
                case "Used Memory" when hardwareType == HardwareType.RAM:
                    _ramUsage.AddLast(Math.Round(currentValue, 2));
                    break;
                case "GPU Memory Dedicated" when sensorType == SensorType.SmallData:
                    _vRamUsage.AddLast((int)Math.Round(currentValue, 0));
                    break;
            }
        }

        public ISessionSensorData ToSessionSensorData()
        {
            var betweenMeasureTimes = new List<double>();

            for (int i = 0; i < _measureTime.Count; i++)
            {
                var current = _measureTime[i];
                if (i == 0)
                {
                    betweenMeasureTimes.Add(current);
                }
                else
                {
                    var prev = _measureTime[i - 1];
                    double between = current - prev;
                    betweenMeasureTimes.Add(between);
                }
            }

            return new SessionSensorData()
            {
                MeasureTime = _measureTime.ToArray(),
                BetweenMeasureTimes = betweenMeasureTimes.ToArray(),
                CpuUsage = _cpuUsage.ToArray(),
                CpuMaxThreadUsage = _cpuMaxThreadUsage.ToArray(),
                CpuMaxClock = _cpuMaxClock.ToArray(),
                CpuPower = _cpuPower.ToArray(),
                CpuTemp = _cpuTemp.ToArray(),
                GpuUsage = _gpuUsage.ToArray(),
                GpuClock = _gpuClock.ToArray(),
                RamUsage = _ramUsage.ToArray(),
                IsInGpuLimit = _isInGpuLimit.ToArray(),
                GpuPower = _gpuPower.ToArray(),
                GpuPowerLimit = _gpuPowerLimit.Select(state => state == 1).ToArray(),
                GpuTemp = _gpuTemp.ToArray(),
                VRamUsage = _vRamUsage.ToArray()
            };
        }
    }
}
