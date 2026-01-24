using System;
using Newtonsoft.Json;
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

		[JsonProperty("owner")]
		public string Owner { get; private set; }

		[JsonProperty("server")]
		public string Server { get; private set; }

		[JsonProperty("thumbnail")]
		public string Thumbnail { get; private set; }

		[JsonProperty("contributors")]
		public string[] Contributors { get; private set; }

		public IWorldIdentifier Identifier
			=> new WorldIdentifier(Id, null, Server);

		public override string ToString()
			=> $"{GetType().Name}[id={Id}, title={Title}, description={Description}, capacity={Capacity}, tags=[{(Tags != null ? string.Join(", ", Tags) : "")}], owner={Owner}, server={Server}, thumbnail={Thumbnail}, contributors=[{(Contributors != null ? string.Join(", ", Contributors) : "")}]]";
	}
}