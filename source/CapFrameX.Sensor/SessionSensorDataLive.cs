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
                "Used Memory",
                "GPU Core",
                "GPU Total",
                "GPU Power",
                "GPU Memory Used"
            };

        private long _timestampStartLogging;
        private List<double> _measureTime;
        private List<int> _cpuUsage;
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
            _gpuUsage = new List<int>();
            _ramUsage = new List<double>();
            _isInGpuLimit = new List<bool>();
            _gpuPower = new List<int>();
            _gpuTemp = new List<int>();
            _vRamUsage = new List<int>();
        }

        public void AddMeasureTime()
        {
            var timestampLogging = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            long ellapsedMilliseconds = timestampLogging - _timestampStartLogging;
            _measureTime.Add(ellapsedMilliseconds * 1E-03);
        }

        public void AddSensorValue(ISensor sensor)
        {
            var name = sensor.Name;

            if (!_relevantNames.Contains(name))
                return;

            // CPU Loads
            if (name == "CPU Total" && sensor.Hardware.HardwareType == HardwareType.CPU)
                _cpuUsage.Add((int)Math.Round(sensor.Value.Value));
            else if (name == "CPU Max" && sensor.Hardware.HardwareType == HardwareType.CPU)
                _cpuMaxThreadUsage.Add((int)Math.Round(sensor.Value.Value));       
            // GPU Load
            else if (name == "GPU Core" && sensor.SensorType == SensorType.Load)
                _gpuUsage.Add((int)Math.Round(sensor.Value.Value));
            // RAM Usage
            else if (name == "Used Memory" && sensor.Hardware.HardwareType == HardwareType.RAM)
                _ramUsage.Add(Math.Round(sensor.Value.Value, 2));
            // ToDo: GPU Limit
            // GPU Power
            else if (name == "GPU Power" && sensor.Hardware.HardwareType == HardwareType.GpuNvidia)
                _gpuPower.Add((int)Math.Round(sensor.Value.Value));
            else if (name == "GPU Total" && sensor.Hardware.HardwareType == HardwareType.GpuAti)
                _gpuPower.Add((int)Math.Round(sensor.Value.Value));
            // GPU Temp
            else if (name == "GPU Core" && sensor.SensorType == SensorType.Temperature)
                _gpuTemp.Add((int)Math.Round(sensor.Value.Value));
            // VRAM Usage
            else if (name == "GPU Memory Used" && sensor.SensorType == SensorType.SmallData
                && sensor.Hardware.HardwareType == HardwareType.GpuNvidia)
                _vRamUsage.Add((int)Math.Round(sensor.Value.Value, 0));

        }

        public ISessionSensorData ToSessionSensorData()
        {
            return new SessionSensorData()
            {
                MeasureTime = _measureTime.ToArray(),
                CpuUsage = _cpuUsage.ToArray(),
                CpuMaxThreadUsage = _cpuMaxThreadUsage.ToArray(),
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
