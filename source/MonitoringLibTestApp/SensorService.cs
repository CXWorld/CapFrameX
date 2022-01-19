using CapFrameX.Monitoring.Contracts;
using OpenHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

namespace MonitoringLibTestApp
{
    public class ProcessService : IProcessService
    {
        public ISubject<int> ProcessIdStream { get; } = new BehaviorSubject<int>(default);
    }

    public class SensorService
    {
        private SensorConfig _sensorConfig;

        public Computer Computer { get; }

        public SensorService()
        {
            // Set a config path
            _sensorConfig = new SensorConfig(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Computer = new Computer(_sensorConfig, new ProcessService())
            {
                CPUEnabled = true,
            };

            Computer.Open();
        }

        public float CCX1Freq { get; private set; }
        public float CCX1Temp { get; private set; }
        public float CCX2Temp { get; private set; }
        public float Core1F { get; private set; }
        public float CpuFrequency { get; private set; }
        public float CpuPPT { get; private set; }
        public float CpuTemp { get; private set; }
        public float CpuUsage { get; private set; }
        public double VCoreVoltage { get; private set; }
        public float CPUFrequency { get; private set; }

        public void UpdateSensors()
        {
            for (int i = 0; i < Computer.Hardware.Length; i++)
            {
                IHardware hardware = Computer.Hardware[i];
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    // only fire the update when found
                    hardware.Update();

                    // loop through the data
                    foreach (var sensor in hardware.Sensors)
                    {
                        switch (sensor.SensorType)
                        {
                            case SensorType.Temperature when sensor.Name.Contains("CPU Package"):
                                CpuTemp = sensor.Value.GetValueOrDefault();
                                break;

                            case SensorType.Load when sensor.Name.Contains("CPU Total"):
                                CpuUsage = (int)sensor.Value.GetValueOrDefault();
                                break;

                            case SensorType.Power when sensor.Name.Contains("CPU Package"):
                                CpuPPT = (int)sensor.Value.GetValueOrDefault();
                                break;

                            case SensorType.Clock when sensor.Name.Contains("CPU Core #1"):
                                CpuFrequency = (int)sensor.Value.GetValueOrDefault();
                                break;

                            case SensorType.Temperature when sensor.Name.Contains("CCD #1"):
                                CCX1Temp = (int)sensor.Value.GetValueOrDefault();
                                break;

                            case SensorType.Temperature when sensor.Name.Contains("CCD #2"):
                                CCX2Temp = (int)sensor.Value.GetValueOrDefault();
                                break;

                            case SensorType.Clock when sensor.Name.Contains("CPU"):
                                CPUFrequency = sensor.Value.GetValueOrDefault();
                                break;
                        }
                    }

                    // Manage Vcore as special case
                    var voltageSensorValues = new List<float>();

                    // Intel
                    if (hardware.Vendor == Vendor.Intel)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Voltage
                                && sensor.Name.Contains("CPU Core"))
                            {
                                voltageSensorValues.Add(sensor.Value.GetValueOrDefault());
                            }
                        }

                        VCoreVoltage = voltageSensorValues.Max();
                    }

                    // AMD
                    if (hardware.Vendor == Vendor.AMD)
                    {
                        // Approach? 
                        // Core (SVI2 TFN) or Max Core VID
                        // For now CX lib does not support Core VID for AMD CPUs

                        // Core (SVI2 TFN)
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Voltage
                                && sensor.Name.Contains("Core (SVI2 TFN)"))
                            {
                                VCoreVoltage = sensor.Value.GetValueOrDefault();
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void ActivateAllSensors()
        {
            for (int i = 0; i < Computer.Hardware.Length; i++)
            {
                IHardware hardware = Computer.Hardware[i];
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    // only fire the update when found
                    hardware.Update();

                    // loop through the sensors
                    foreach (var sensor in hardware.Sensors)
                    {
                        // activate sensor
                        _sensorConfig.SetSensorIsActive(sensor.IdentifierString, true);
                    }
                }
            }
        }
    }
}
