using System.IO;
using Nox.CCK.Utils;

namespace Nox.Worlds.Runtime.Caching {
	public class WorldCache {
		public static string GetPath(string hash)
			=> Path.Combine(Constants.AppPath, "cache", "worlds", hash);

		public static bool InCache(string hash)
			=> File.Exists(GetPath(hash));

		public static void ClearCache() {
			if (Directory.Exists(Path.Combine(Constants.AppPath, "cache", "worlds")))
				Directory.Delete(Path.Combine(Constants.AppPath, "cache", "worlds"), true);
		}

		public static string GetIfExist(string hash)
			=> InCache(hash)
				? GetPath(hash)
				: null;


		public static void Delete(string hash) {
			if (InCache(hash))
				File.Delete(GetPath(hash));
		}
	}
}