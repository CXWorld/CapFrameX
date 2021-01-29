using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
    public class SensorEntryProvider : ISensorEntryProvider
    {
        private readonly ISensorService _sensorService;
        private readonly ISensorConfig _sensorConfig;

        public Action ConfigChanged { get; set; }

        public SensorEntryProvider(ISensorService sensorService,
            ISensorConfig sensorConfig)
        {
            _sensorService = sensorService;
            _sensorConfig = sensorConfig;
        }

        public async Task<IEnumerable<ISensorEntry>> GetWrappedSensorEntries()
        {
            var sensorEntries = await _sensorService.GetSensorEntries();
            var wrappedEntries = sensorEntries.Select(WrapSensorEntry);

            if (!_sensorConfig.HasConfigFile
              // reset config when hardware has changed
              || _sensorConfig.SensorEntryCount != sensorEntries.Count())
            {
                _sensorConfig.ResetConfig();
                wrappedEntries.ForEach(SetIsActiveDefault);
                await SaveSensorConfig();
            }

            return wrappedEntries;
        }

        public async Task SaveSensorConfig()
        {
            await _sensorConfig.Save();
        }

        private SensorEntryWrapper WrapSensorEntry(ISensorEntry entry)
        {
            return new SensorEntryWrapper()
            {
                Identifier = entry.Identifier,
                Name = entry.Name,
                SensorType = entry.SensorType,
                HardwareType = entry.HardwareType,
                UseForLogging = _sensorConfig.GetSensorIsActive(entry.Identifier),
                UpdateLogState = UpdateLogState
            };
        }

        private void UpdateLogState(string identifier, bool useForLogging)
        {
            ConfigChanged?.Invoke();
            _sensorConfig.SetSensorIsActive(identifier, useForLogging);
        }

        private void SetIsActiveDefault(ISensorEntry sensor)
        {
            _sensorConfig.SetSensorIsActive(sensor.Identifier, GetIsDefaultActiveSensor(sensor));
        }

        public bool GetIsDefaultActiveSensor(ISensorEntry sensor)
        {
            Enum.TryParse(sensor.HardwareType, out HardwareType hardwareType);
            Enum.TryParse(sensor.SensorType, out SensorType sensorType);

            bool isDefault = false;

            switch (sensor.Name)
            {
                case "CPU Total" when hardwareType == HardwareType.CPU:
                case "CPU Max" when hardwareType == HardwareType.CPU:
                case "CPU Max Clock" when sensorType == SensorType.Clock:
                case "CPU Package" when sensorType == SensorType.Power:
                case "CPU Package" when sensorType == SensorType.Temperature:
                case "GPU Core" when sensorType == SensorType.Load:
                case "GPU Core" when sensorType == SensorType.Temperature:
                case "GPU Core" when sensorType == SensorType.Clock:
                case "GPU Power" when hardwareType == HardwareType.GpuNvidia:
                case "GPU Power Limit" when hardwareType == HardwareType.GpuNvidia:
                case "GPU Total" when hardwareType == HardwareType.GpuAti:
                case "Used Memory Game" when hardwareType == HardwareType.RAM:
                case "GPU Memory Dedicated" when sensorType == SensorType.Data:
                    isDefault = true;
                    break;
            }

            return isDefault;
        }
    }
}
