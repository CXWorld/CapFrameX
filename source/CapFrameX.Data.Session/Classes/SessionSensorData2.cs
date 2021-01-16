using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Classes
{
    public class SessionSensorEntry: ISessionSensorEntry {
        public string Name { get; }
        public string Type { get; }
        public LinkedList<double> Values { get; } = new LinkedList<double>();

        public SessionSensorEntry(string name, string type) {
            Name = name;
            Type = type;
        }
    }

    public class SessionSensorData2: Dictionary<string, ISessionSensorEntry>, ISessionSensorData2
    {
        [JsonIgnore]
        public ISessionSensorEntry MeasureTime => this[nameof(MeasureTime)];
        [JsonIgnore]
        public ISessionSensorEntry BetweenMeasureTime => this[nameof(BetweenMeasureTime)];

        public SessionSensorData2()
        {
            Add("MeasureTime", new SessionSensorEntry("MeasureTime", "Time"));
            Add("BetweenMeasureTime", new SessionSensorEntry("BetweenMeasureTime", "Time"));
        }
    }
}
