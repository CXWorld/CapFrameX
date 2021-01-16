using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Classes
{
    public class SessionSensorEntry<T>: ISessionSensorEntry<T> {
        public string Name { get; }
        public string Type { get; }
        public LinkedList<T> Values { get; } = new LinkedList<T>();

        public SessionSensorEntry(string name, string type) {
            Name = name;
            Type = type;
        }
    }

    public class SessionSensorData2: Dictionary<string, ISessionSensorEntry<double>>, ISessionSensorData2
    {
        [JsonProperty("MeasureTime")]
        [JsonConverter(typeof(ConcreteTypeConverter<SessionSensorEntry<double>>))]
        public ISessionSensorEntry<double> MeasureTime { get; } = new SessionSensorEntry<double>("MeasureTime", "Time");

        [JsonProperty("BetweenMeasureTime")]
        [JsonConverter(typeof(ConcreteTypeConverter<SessionSensorEntry<double>>))]
        public ISessionSensorEntry<double> BetweenMeasureTime { get; } = new SessionSensorEntry<double>("BetweenMeasureTime", "Time");
    }
}
