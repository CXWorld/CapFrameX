using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
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

            // Detect hardware/library changes by comparing identifier sets,
            // not just counts. This catches cases where sensor indices shifted
            // between versions while the total number of sensors stayed the same.
            bool needsReset = !_sensorConfig.HasConfigFile;

            if (!needsReset)
            {
                var configCopy = _sensorConfig.GetSensorConfigCopy();
                var currentIdentifiers = new HashSet<string>(sensorEntries.Select(e => e.Identifier));
                needsReset = currentIdentifiers.Any(id => !configCopy.ContainsKey(id));
            }

            if (needsReset)
            {
                var backupSensorConfig = _sensorConfig.GetSensorConfigCopy();
                _sensorConfig.ResetConfig();
                wrappedEntries.ForEach(entry => SetIsActiveDefault(entry, backupSensorConfig));
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
                HardwareName = entry.HardwareName,
                UseForLogging = _sensorConfig.IsSelectedForLogging(entry.Identifier),
                UpdateLogState = UpdateLogState
            };
        }

        private void UpdateLogState(string identifier, string stableIdentifier, bool useForLogging)
        {
            ConfigChanged?.Invoke();
            _sensorConfig.SelectForLogging(identifier, useForLogging);
            _sensorConfig.SelectStableForLogging(stableIdentifier, useForLogging);
        }

        private void SetIsActiveDefault(ISensorEntry sensor, Dictionary<string, bool> configCopy)
        {
            bool oldConfigStatus = false;

            if (configCopy.ContainsKey(sensor.Identifier))
            {
                // Exact identifier match (same library version)
                oldConfigStatus = configCopy[sensor.Identifier];
            }
            else
            {
                // Fallback: try stable identifier match from persisted stable config
                var stableId = SensorIdentifierHelper.BuildStableIdentifier(sensor);
                if (stableId != null)
                {
                    oldConfigStatus = _sensorConfig.IsSelectedForLoggingByStableId(stableId);
                }
            }

            bool isActive = oldConfigStatus || GetIsDefaultActiveSensor(sensor);
            _sensorConfig.SelectForLogging(sensor.Identifier, isActive);

            var stableIdentifier = SensorIdentifierHelper.BuildStableIdentifier(sensor);
            _sensorConfig.SelectStableForLogging(stableIdentifier, isActive);
        }

        public bool GetIsDefaultActiveSensor(ISensorEntry sensor)
        {
            Enum.TryParse(sensor.HardwareType, out HardwareType hardwareType);
            Enum.TryParse(sensor.SensorType, out SensorType sensorType);

            bool isDefault = false;

            switch (sensor.Name)
            {
                case "CPU Total" when hardwareType == HardwareType.Cpu:
                case "CPU Max" when hardwareType == HardwareType.Cpu:
                case "CPU Max Clock" when sensorType == SensorType.Clock:
                case "CPU Package" when sensorType == SensorType.Power:
                case "CPU Package" when sensorType == SensorType.Temperature:
                case "CPU Package (Tctl/Tdie)" when sensorType == SensorType.Temperature:
                case "GPU Core" when sensorType == SensorType.Load:
                case "GPU Core" when sensorType == SensorType.Temperature:
                case "GPU Core" when sensorType == SensorType.Clock:
                case "GPU Power" when hardwareType == HardwareType.GpuNvidia:
                case "GPU Power Limit" when hardwareType == HardwareType.GpuNvidia:
                case "GPU Total" when hardwareType == HardwareType.GpuAmd:
				case "GPU TBP" when hardwareType == HardwareType.GpuAmd:
                case "GPU TBP" when hardwareType == HardwareType.GpuIntel:
                case "GPU TDP" when hardwareType == HardwareType.GpuIntel:
                case "Used Memory Game" when hardwareType == HardwareType.Memory:
                case "GPU Memory Dedicated" when sensorType == SensorType.Data:
                    isDefault = true;
                    break;
            }

            return isDefault;
        }
    }
}
