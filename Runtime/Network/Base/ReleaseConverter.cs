using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nox.Worlds.Runtime.Network {
	/// <summary>
	/// JSON converter for <see cref="Release"/> that handles two shapes:
	/// - Plain number (non-privileged): <c>5</c>
	/// - Object (privileged): <c>{"value": 5, "auto": true}</c> or <c>{"resolved": 5, "auto": false}</c>
	/// </summary>
	public class ReleaseConverter : JsonConverter<Release> {
		public override void WriteJson(JsonWriter writer, Release value, JsonSerializer serializer) {
			var obj = JObject.FromObject(value, serializer);
			obj.WriteTo(writer);
		}

		public override Release ReadJson(JsonReader reader, Type objectType, Release existingValue, bool hasExistingValue, JsonSerializer serializer) {
			switch (reader.TokenType) {
				case JsonToken.Integer:
				case JsonToken.Float:
					if (Convert.ToInt32(reader.Value) == -1)
						return new Release(ushort.MaxValue);
					return new Release(Convert.ToUInt16(reader.Value));
				case JsonToken.StartObject: {
					var obj = JObject.Load(reader);
					var valueToken = obj["value"] ?? obj["resolved"];
					ushort value;
					if (valueToken != null && Convert.ToInt32(valueToken) == -1)
						value = ushort.MaxValue;
					else
						value = valueToken != null 
							? Convert.ToUInt16(valueToken) 
							: ushort.MaxValue;
					bool auto = obj["auto"]?.Value<bool>() ?? false;
					return new Release(value, auto);
				}
				default:
					throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing Release.");
			}
		}
	}
}
