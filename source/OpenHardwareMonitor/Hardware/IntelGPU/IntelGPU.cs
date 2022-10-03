using CapFrameX.Monitoring.Contracts;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.IntelGPU
{
    internal class IntelGPU : GPUBase
    {
        private readonly int adapterIndex;
        private readonly int busNumber;
        private readonly int deviceNumber;
        private readonly string driverVersion;
        private readonly ISensorConfig sensorConfig;

        private readonly Sensor temperatureCore;
        private readonly Sensor temperatureMemory;

        private readonly Sensor powerTdp;
        private readonly Sensor powerVram;

        private readonly Sensor clockCore;
        private readonly Sensor clockVram;

        private readonly Sensor voltageCore;
        private readonly Sensor voltageVram;

        private readonly Sensor usageCore;
        private readonly Sensor usageRenderEngine;
        private readonly Sensor usageMediaEngine;

        private readonly Sensor bandwidthReadVram;
        private readonly Sensor bandwidthWriteVram;

        // ToDo: get all fans info
        private readonly Sensor speedFan;

        public IntelGPU(string name, int adapterIndex, int busNumber, int deviceNumber, string driverVersion, 
            ISettings settings, ISensorConfig config, IProcessService processService)
          : base(name, new Identifier("intelgpu",
            adapterIndex.ToString(CultureInfo.InvariantCulture)), settings, processService)
        {
            this.adapterIndex = adapterIndex;
            this.busNumber = busNumber;
            this.deviceNumber = deviceNumber;
            this.driverVersion = driverVersion;
            this.sensorConfig = config;

            // define alle sensors
            this.temperatureCore = new Sensor("GPU Core", 0, SensorType.Temperature, this, settings);
            this.temperatureMemory = new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings);

            this.powerTdp = new Sensor("GPU TDP", 0, SensorType.Power, this, settings);
            this.powerVram = new Sensor("GPU VRAM", 1, SensorType.Power, this, settings);

            this.clockCore = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
            this.clockVram = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);

            this.voltageCore = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings);
            this.voltageVram = new Sensor("GPU Memory", 1, SensorType.Voltage, this, settings);

            this.usageCore = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
            this.usageRenderEngine = new Sensor("GPU Computing", 1, SensorType.Load, this, settings);
            this.usageMediaEngine = new Sensor("GPU Media Engine", 2, SensorType.Load, this, settings);

            this.memoryUsageDedicated = new Sensor("GPU Memory Dedicated", 0, SensorType.Data, this, settings);
            this.memoryUsageShared = new Sensor("GPU Memory Shared", 1, SensorType.Data, this, settings);
            this.processMemoryUsageDedicated = new Sensor("GPU Memory Dedicated Game", 2, SensorType.Data, this, settings);
            this.processMemoryUsageShared = new Sensor("GPU Memory Shared Game", 3, SensorType.Data, this, settings);
            this.bandwidthReadVram = new Sensor("GPU Memory Read", 4, SensorType.Data, this, settings);
            this.bandwidthWriteVram = new Sensor("GPU Memory Write", 5, SensorType.Data, this, settings);

            this.speedFan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings);

            Update();
        }

        public override HardwareType HardwareType => HardwareType.GpuIntel;

        public int DeviceNumber => deviceNumber;
        public int BusNumber => busNumber;

        public override void Update()
        {
            // get telemetry data from IGCL
            var igclTelemetryData = new IgclTelemetryData();
            try
            {
                IGCL.GetIgclTelemetryData((uint)adapterIndex, ref igclTelemetryData);
            }
            catch { return; }

            // GPU Core Temperature
            if (igclTelemetryData.gpuCurrentTemperatureSupported)
            {
                temperatureCore.Value = (float)igclTelemetryData.gpuCurrentTemperatureValue;
                ActivateSensor(temperatureCore);
            }
            else
            {
                temperatureCore.Value = null;
            }

            // VRAM Temperature
            if (igclTelemetryData.vramCurrentTemperatureSupported)
            {
                temperatureMemory.Value = (float)igclTelemetryData.vramCurrentTemperatureValue;
                ActivateSensor(temperatureMemory);
            }
            else
            {
                temperatureMemory.Value = null;
            }

            // GPU Core Temperature
            if (igclTelemetryData.gpuEnergySupported)
            {
                powerTdp.Value = (float)igclTelemetryData.gpuEnergyValue;
                ActivateSensor(powerTdp);
            }
            else
            {
                powerTdp.Value = null;
            }

            // VRAM Temperature
            if (igclTelemetryData.vramEnergySupported)
            {
                powerVram.Value = (float)igclTelemetryData.vramEnergyValue;
                ActivateSensor(powerVram);
            }
            else
            {
                powerVram.Value = null;
            }

            // GPU Core Frequency
            if (igclTelemetryData.gpuCurrentClockFrequencySupported)
            {
                clockCore.Value = (float)igclTelemetryData.gpuCurrentClockFrequencyValue;
                ActivateSensor(clockCore);
            }
            else
            {
                clockCore.Value = null;
            }

            // VRAM Frequency
            if (igclTelemetryData.vramCurrentClockFrequencySupported)
            {
                clockVram.Value = 2f * (float)igclTelemetryData.vramCurrentClockFrequencyValue;
                ActivateSensor(clockVram);
            }
            else
            {
                clockVram.Value = null;
            }

            // GPU Core Frequency
            if (igclTelemetryData.gpuVoltageSupported)
            {
                voltageCore.Value = (float)igclTelemetryData.gpuVoltagValue;
                ActivateSensor(voltageCore);
            }
            else
            {
                voltageCore.Value = null;
            }

            // VRAM Voltage
            if (igclTelemetryData.vramVoltageSupported)
            {
                voltageVram.Value = (float)igclTelemetryData.vramVoltageValue;
                ActivateSensor(voltageVram);
            }
            else
            {
                voltageVram.Value = null;
            }

            // GPU Usage
            if (igclTelemetryData.globalActivitySupported)
            {
                usageCore.Value = (float)igclTelemetryData.globalActivityValue;
                ActivateSensor(usageCore);
            }
            else
            {
                usageCore.Value = null;
            }

            // Render Engine Usage
            if (igclTelemetryData.renderComputeActivitySupported)
            {
                usageRenderEngine.Value = (float)igclTelemetryData.renderComputeActivityValue;
                ActivateSensor(usageRenderEngine);
            }
            else
            {
                usageRenderEngine.Value = null;
            }

            // Media Engine Usage
            if (igclTelemetryData.mediaActivitySupported)
            {
                usageMediaEngine.Value = (float)igclTelemetryData.mediaActivityValue;
                ActivateSensor(usageMediaEngine);
            }
            else
            {
                usageMediaEngine.Value = null;
            }

            // VRAM Read Bandwidth
            if (igclTelemetryData.vramReadBandwidthSupported)
            {
                bandwidthReadVram.Value = (float)igclTelemetryData.vramReadBandwidthValue;
                ActivateSensor(bandwidthReadVram);
            }
            else
            {
                bandwidthReadVram.Value = null;
            }

            // VRAM Write Bandwidth
            if (igclTelemetryData.vramWriteBandwidthSupported)
            {
                bandwidthWriteVram.Value = (float)igclTelemetryData.vramWriteBandwidthValue;
                ActivateSensor(bandwidthWriteVram);
            }
            else
            {
                bandwidthWriteVram.Value = null;
            }

            // ToDo: get all fans info
            // Fanspeed (n Fans)
            if (igclTelemetryData.fanSpeedSupported)
            {
                speedFan.Value = (float)igclTelemetryData.fanSpeedValue;
                ActivateSensor(speedFan);
            }
            else
            {
                speedFan.Value = null;
            }

            // update VRAM usage
            if (dedicatedVramUsagePerformCounter != null)
            {
                try
                {
                    if (sensorConfig.GetSensorEvaluate(memoryUsageDedicated.IdentifierString))
                    {
                        memoryUsageDedicated.Value = dedicatedVramUsagePerformCounter.NextValue() / SCALE;
                        ActivateSensor(memoryUsageDedicated);
                    }
                    else
                        memoryUsageDedicated.Value = null;

                }
                catch { memoryUsageDedicated.Value = null; }
            }

            if (sharedVramUsagePerformCounter != null)
            {
                try
                {
                    if (sensorConfig.GetSensorEvaluate(memoryUsageShared.IdentifierString))
                    {
                        memoryUsageShared.Value = (float)sharedVramUsagePerformCounter.NextValue() / SCALE;
                        ActivateSensor(memoryUsageShared);
                    }
                    else
                        memoryUsageShared.Value = null;
                }
                catch { memoryUsageShared.Value = null; }
            }

            try
            {
                if (sensorConfig.GetSensorEvaluate(processMemoryUsageDedicated.IdentifierString))
                {
                    lock (_performanceCounterLock)
                    {
                        processMemoryUsageDedicated.Value = dedicatedVramUsageProcessPerformCounter == null
                        ? 0f : (float)dedicatedVramUsageProcessPerformCounter.NextValue() / SCALE;
                    }
                    ActivateSensor(processMemoryUsageDedicated);
                }
                else
                    processMemoryUsageDedicated.Value = null;
            }
            catch { processMemoryUsageDedicated.Value = null; }

            try
            {
                if (sensorConfig.GetSensorEvaluate(processMemoryUsageShared.IdentifierString))
                {
                    lock (_performanceCounterLock)
                    {
                        processMemoryUsageShared.Value = sharedVramUsageProcessPerformCounter == null
                        ? 0f : (float)sharedVramUsageProcessPerformCounter.NextValue() / SCALE;
                    }
                    ActivateSensor(processMemoryUsageShared);
                }
                else
                    processMemoryUsageShared.Value = null;
            }
            catch { processMemoryUsageShared.Value = null; }
        }

        public override string GetDriverVersion()
        {
            return !string.IsNullOrWhiteSpace(driverVersion) ? driverVersion.ToString() : base.GetDriverVersion();
        }
    }
}
