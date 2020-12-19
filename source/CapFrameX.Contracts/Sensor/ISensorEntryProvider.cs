using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorEntryProvider
    {
        Task<IEnumerable<ISensorEntry>> GetWrappedSensorEntries();
    }
}
