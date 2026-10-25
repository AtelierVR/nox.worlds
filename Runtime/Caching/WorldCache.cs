using System.IO;
using Nox.CCK.Utils;
using Nox.Assets;

namespace Nox.Worlds.Runtime.Caching {
	/// <summary>
	/// World-facing view of the shared asset cache. The files themselves live in the
	/// <c>assets</c> mod (<c>cache/assets/&lt;hash&gt;</c>): this helper only resolves their path so the
	/// scene group manager keeps a simple API.
	/// </summary>
	public static class WorldCache {
		/// <summary>Shared asset cache, or <c>null</c> before the mods are initialized.</summary>
		private static IAssetCacheAPI Store
			=> Main.AssetsCache;

		/// <summary>Absolute path the hash is (or would be) stored at.</summary>
		public static string GetPath(string hash)
			=> Store?.PathFor(hash);

		public static bool InCache(string hash)
			=> Store?.Has(hash) == true;

		/// <summary>Path of a stored hash, or <c>null</c> when it is not downloaded yet.</summary>
		public static string GetIfExist(string hash)
			=> InCache(hash) ? GetPath(hash) : null;

		public static void Delete(string hash)
			=> Store?.Clear(hash);

		/// <summary>Removes every cached asset (avatars, worlds…), not only the worlds.</summary>
		public static void ClearCache() {
			var root = Path.Combine(Constants.CachePath, "assets");

			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}
	}
}
