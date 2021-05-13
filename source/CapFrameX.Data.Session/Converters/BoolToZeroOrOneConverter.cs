using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Converters
{
    public class BoolToZeroOrOneConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsArray && objectType.GetElementType() == typeof(bool);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value is bool valueAsBool)
            {
                return valueAsBool ? 1 : 0;
            }
            return (int)reader.Value;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;
    }
}
