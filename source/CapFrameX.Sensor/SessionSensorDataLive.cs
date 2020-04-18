using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

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
                "Used Memory",
                "GPU Core",
                "GPU Total",
                "GPU Power",
                "GPU Memory Used"
            };

        private long _timestampStartLogging;
        private List<double> _measureTime;
        private List<int> _cpuUsage;
        private List<int> _cpuPower;
        private List<int> _cpuTemp;
        private List<int> _cpuMaxThreadUsage;
        private List<int> _gpuUsage;
        private List<double> _ramUsage;
        private List<bool> _isInGpuLimit;
        private List<int> _gpuPower;
        private List<int> _gpuTemp;
        private List<int> _vRamUsage;

        public SessionSensorDataLive()
        {
            _timestampStartLogging = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _measureTime = new List<double>();
            _cpuUsage = new List<int>();
            _cpuMaxThreadUsage = new List<int>();
            _cpuPower = new List<int>();
            _cpuTemp = new List<int>();
            _gpuUsage = new List<int>();
            _ramUsage = new List<double>();
            _isInGpuLimit = new List<bool>();
            _gpuPower = new List<int>();
            _gpuTemp = new List<int>();
            _vRamUsage = new List<int>();
        }

        public void AddMeasureTime(DateTime dateTime)
        {
            var timestampLogging = new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
            long ellapsedMilliseconds = timestampLogging - _timestampStartLogging;
            _measureTime.Add(ellapsedMilliseconds * 1E-03);
        }

        public void AddSensorValue(ISensor sensor, float currentValue)
        {
            var name = sensor.Name;

            if (!_relevantNames.Contains(name))
                return;

            // CPU Loads
            switch (name)
            {
                case "CPU Total" when sensor.Hardware.HardwareType == HardwareType.CPU:
                    _cpuUsage.Add((int)Math.Round(currentValue));
                    break;
                case "CPU Max" when sensor.Hardware.HardwareType == HardwareType.CPU:
                    _cpuMaxThreadUsage.Add((int)Math.Round(currentValue));
                    break;
                case "CPU Package" when sensor.SensorType == SensorType.Power:
                    _cpuPower.Add((int)Math.Round(currentValue));
                    break;
                case "CPU Package" when sensor.SensorType == SensorType.Temperature:
                    _cpuTemp.Add((int)Math.Round(currentValue));
                    break;
                case "GPU Core" when sensor.SensorType == SensorType.Load:
                    _gpuUsage.Add((int)Math.Round(currentValue));
                    break;
                case "Used Memory" when sensor.Hardware.HardwareType == HardwareType.RAM:
                    _ramUsage.Add(Math.Round(currentValue, 2));
                    break;
                case "GPU Power" when sensor.Hardware.HardwareType == HardwareType.GpuNvidia:
                    _gpuPower.Add((int)Math.Round(currentValue));
                    break;
                case "GPU Total" when sensor.Hardware.HardwareType == HardwareType.GpuAti:
                    _gpuPower.Add((int)Math.Round(currentValue));
                    break;
                case "GPU Core" when sensor.SensorType == SensorType.Temperature:
                    _gpuTemp.Add((int)Math.Round(currentValue));
                    break;
                case "GPU Memory Used" when sensor.SensorType == SensorType.SmallData
                && sensor.Hardware.HardwareType == HardwareType.GpuNvidia:
                    _vRamUsage.Add((int)Math.Round(currentValue, 0));
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
                CpuPower = _cpuPower.ToArray(),
                CpuTemp = _cpuTemp.ToArray(),
                GpuUsage = _gpuUsage.ToArray(),
                RamUsage = _ramUsage.ToArray(),
                IsInGpuLimit = _isInGpuLimit.ToArray(),
                GpuPower = _gpuPower.ToArray(),
                GpuTemp = _gpuTemp.ToArray(),
                VRamUsage = _vRamUsage.ToArray()
            };
        }
    }
}
