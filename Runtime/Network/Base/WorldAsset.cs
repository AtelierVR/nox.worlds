using System;
using Newtonsoft.Json;
using Nox.CCK.Utils;

namespace Nox.Worlds.Runtime.Network {
	[Serializable]
	public class WorldAsset : IWorldAsset, INoxObject {
		[JsonProperty("id")]
		public uint Id { get; private set; }

		[JsonProperty("version")]
		public ushort Version { get; private set; }

		[JsonProperty("engine")]
		public string Engine { get; private set; }

		[JsonProperty("platform")]
		public string Platform { get; private set; }

		[JsonProperty("is_empty")]
		public bool IsEmpty { get; private set; }

		[JsonProperty("url")]
		public string Url { get; private set; }

		[JsonProperty("hash")]
		public string Hash { get; private set; }

		[JsonProperty("size")]
		private uint? _size;

		[JsonProperty("mods")]
		public string[] Mods { get; private set; }

		[JsonProperty("features")]
		public string[] Features { get; private set; }

		public uint Size
			=> _size ?? 0;

	}
}