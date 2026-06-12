using CapFrameX.Contracts.Sensor;
using CapFrameX.Mcp.Attributes;
using CapFrameX.Monitoring.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class SensorConfigTools
    {
        private readonly ISensorService _sensorService;
        private readonly ISensorConfig _sensorConfig;

        public SensorConfigTools(ISensorService sensorService, ISensorConfig sensorConfig)
        {
            _sensorService = sensorService;
            _sensorConfig = sensorConfig;
        }

        [McpServerTool(Name = "cfx_get_logged_sensors",
            Description = "Returns the live sensor logging configuration of the running CapFrameX instance: " +
                "the catalog of all detected hardware sensors (CPU/GPU/RAM/HDD channels) and which of them are " +
                "currently flagged to be written into capture records, plus the sensor logging refresh period in ms. " +
                "Use this to answer 'why is metric X missing from my record?' or 'what data will the next capture contain?'.")]
        public async Task<LoggedSensorsResult> GetLoggedSensors()
        {
            var result = new LoggedSensorsResult
            {
                RefreshPeriodMs = SafeInt(() => _sensorConfig.SensorLoggingRefreshPeriod),
                SensorEntryCount = SafeInt(() => _sensorConfig.SensorEntryCount),
            };

            Dictionary<string, bool> selectionMap;
            try { selectionMap = _sensorConfig.GetSensorConfigCopy() ?? new Dictionary<string, bool>(); }
            catch { selectionMap = new Dictionary<string, bool>(); }

            IEnumerable<ISensorEntry> entries;
            try { entries = await _sensorService.GetSensorEntries().ConfigureAwait(false) ?? Enumerable.Empty<ISensorEntry>(); }
            catch { entries = Enumerable.Empty<ISensorEntry>(); }

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Identifier)) continue;

                bool selected = selectionMap.TryGetValue(entry.Identifier, out var s) && s;
                result.Sensors.Add(new LoggedSensorEntry
                {
                    Identifier = entry.Identifier,
                    Name = entry.Name,
                    SensorType = entry.SensorType,
                    HardwareType = entry.HardwareType,
                    HardwareName = entry.HardwareName,
                    IsPresentationDefault = entry.IsPresentationDefault,
                    SelectedForLogging = selected,
                });
            }

            result.Sensors = result.Sensors
                .OrderBy(s => s.HardwareType ?? string.Empty)
                .ThenBy(s => s.HardwareName ?? string.Empty)
                .ThenBy(s => s.Name ?? string.Empty)
                .ToList();
            result.SelectedCount = result.Sensors.Count(s => s.SelectedForLogging);
            return result;
        }

        private static int SafeInt(Func<int> fn)
        {
            try { return fn(); } catch { return 0; }
        }
    }
}
