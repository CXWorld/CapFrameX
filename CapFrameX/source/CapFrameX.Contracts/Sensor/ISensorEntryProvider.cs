using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorEntryProvider
    {
        Task<IEnumerable<ISensorEntry>> GetWrappedSensorEntries();

        Task SaveSensorConfig();

        Action ConfigChanged { get; set; }

        bool GetIsDefaultActiveSensor(ISensorEntry sensor);
    }
}
