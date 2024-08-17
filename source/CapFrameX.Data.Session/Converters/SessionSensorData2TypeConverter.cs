using CapFrameX.Data.Session.Classes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Converters
{
    public class SessionSensorData2TypeConverter: JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(SessionSensorData2);

        public override object ReadJson(JsonReader reader,
         Type objectType, object existingValue, JsonSerializer serializer)
        {
            var dictionary = serializer.Deserialize<Dictionary<string, SessionSensorEntry>>(reader);
            if(dictionary is null)
            {
                return null;
            }
            var sessionSensorData = new SessionSensorData2();
            foreach(var entry in dictionary)
            {
                sessionSensorData[entry.Key] = entry.Value;
            }
            return sessionSensorData;
        }

        public override void WriteJson(JsonWriter writer,
            object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
