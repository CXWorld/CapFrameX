using System.Collections.Generic;

namespace CapFrameX.Data.Session.Contracts
{
    public interface ISessionSensorData2 : IDictionary<string, ISessionSensorEntry>, ISessionSensorData
    {
        new ISessionSensorEntry MeasureTime { get; }
        ISessionSensorEntry BetweenMeasureTime { get; }
    }
}
