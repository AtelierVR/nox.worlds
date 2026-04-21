using System;
using Newtonsoft.Json;
using Nox.CCK.Convertors;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Runtime.Network {
	[Serializable, JsonObject]
	public class World : IWorld, INoxObject {
		[JsonProperty("id")]
		public uint Id { get; private set; }

		[JsonProperty("title")]
		public string Title { get; private set; }

		[JsonProperty("description")]
		public string Description { get; private set; }

		[JsonProperty("capacity")]
		public ushort Capacity { get; private set; }

		[JsonProperty("tags")]
		public string[] Tags { get; private set; }

		[JsonProperty("owner"), JsonConverter(typeof(StringToIdentifierConverter))]
		public Identifier Owner { get; private set; }

		[JsonProperty("server")]
		public string Server { get; private set; }

		[JsonProperty("thumbnail")]
		public string Thumbnail { get; private set; }

		[JsonProperty("contributors"), JsonConverter(typeof(ArrayConverter<StringToIdentifierConverter>))]
		public Identifier[] Contributors { get; private set; }

		public Identifier Identifier
			=> new("w", Id, null, Server);

		public override string ToString()
			=> $"{GetType().Name}[id={Id}, title={Title}, description={Description}, capacity={Capacity}, tags=[{(Tags != null ? string.Join(", ", Tags) : "")}], owner={Owner}, server={Server}, thumbnail={Thumbnail}, contributors=[{(Contributors != null ? string.Join(", ", Contributors) : "")}]]";

		public bool IsContributor(Identifier identifier)
			=> Owner.Equals(identifier)
				|| Array.Exists(Contributors, c => c.Equals(identifier));
	}
}