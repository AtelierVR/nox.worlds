using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Nox.CCK.Network.Assets;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Runtime.Network {
	/// <summary>
	/// A world as returned by the worlds endpoint: the generic <see cref="BaseAsset"/> plus the
	/// world-only <c>capacity</c>.
	/// </summary>
	[Serializable, JsonObject]
	public class WorldAsset : BaseAsset, IWorld, INoxObject {
		[JsonProperty("capacity")]
		public ushort Capacity { get; private set; }

		/// <summary>Canonical reference of the world (<c>w:&lt;id&gt;@&lt;server&gt;</c>).</summary>
		[JsonIgnore]
		public override Identifier Identifier
			=> new(WorldIdentifierExtensions.WorldType, Id, null, Server);

		public override string ToString()
			=> $"{GetType().Name}[id={Id}, name={Name ?? "<no-name>"}, capacity={Capacity}, owner={Owner}, server={Server}, images={Images?.Length ?? 0}]";
	}
}
