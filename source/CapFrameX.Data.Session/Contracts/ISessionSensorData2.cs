using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Contracts
{
    public interface ISessionSensorData2: IDictionary<string, ISessionSensorEntry<double>>
    {
        ISessionSensorEntry<double> MeasureTime { get; }
        ISessionSensorEntry<double> BetweenMeasureTime { get; }
    }
}
