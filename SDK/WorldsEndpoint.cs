using Nox.Network.Assets;

namespace Nox.Worlds {
	/// <summary>
	/// The worlds collection: declared server-side with <c>@AssetController('world', 'worlds')</c>,
	/// so every route is the generic asset route applied to <c>worlds</c>.
	/// </summary>
	public static class WorldsEndpoint {
		/// <summary>Logical asset type expected by the server-side validator.</summary>
		public const string Type = "world";

		/// <summary>REST endpoint holding the collection, relative to the node gateway.</summary>
		public const string Route = "worlds";

		public static AssetEndpoint Endpoint
			=> new(Type, Route);
	}
}
