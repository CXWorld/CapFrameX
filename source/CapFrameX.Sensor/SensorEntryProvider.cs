using CapFrameX.Contracts.Sensor;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
    public class SensorEntryProvider : ISensorEntryProvider
    {
        private readonly ISensorService _sensorService;
        private readonly ISensorConfig _sensorConfig;

        public SensorEntryProvider(ISensorService sensorService,
            ISensorConfig sensorConfig)
        {
            _sensorService = sensorService;
            _sensorConfig = sensorConfig;
        }

        public async Task<IEnumerable<ISensorEntry>> GetWrappedSensorEntries()
        {
            var sensorEntries = await _sensorService.GetSensorEntries();
            return sensorEntries.Select(WrapSensorEntry);
        }

        private SensorEntryWrapper WrapSensorEntry(ISensorEntry entry)
        {
            return new SensorEntryWrapper()
            {
                Name = entry.Name,
                SensorType = entry.SensorType,
                UseForLogging = _sensorConfig.GetSensorIsActive(entry.Identifier),
                UpdateLogState = UptdateLogState
            };
        }

        void UptdateLogState(string identifier)
        {
        }
    }
}
