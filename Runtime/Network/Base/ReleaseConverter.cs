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
					return new Release(Convert.ToUInt16(reader.Value));
				case JsonToken.StartObject: 
					return serializer.Deserialize<Release>(reader);
				default:
					throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing Release.");
			}
		}
	}
}
