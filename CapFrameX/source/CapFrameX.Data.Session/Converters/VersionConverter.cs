using Newtonsoft.Json;
using System;

namespace CapFrameX.Data.Session.Converters
{
    public class VersionConverter : JsonConverter<Version>
	{
		public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString());
		}

		public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer)
			=> new Version((string)reader.Value);
	}
}
