using Nox.CCK.Utils;

namespace Nox.Worlds.Pipeline {
	/// <summary>
	/// Un AssetBundle produit par le build, accompagné de la plateforme dont il est le variant. Les
	/// sorties se lisent donc par plateforme, sans dépendre de l'ordre de <c>BuildResult.Outputs</c>.
	/// </summary>
	public readonly struct BuildOutput {
		public Platform Platform { get; }

		/// <summary>Chemin disque du bundle.</summary>
		public string Path { get; }

		public BuildOutput(Platform platform, string path) {
			Platform = platform;
			Path     = path;
		}

		public override string ToString()
			=> $"{Platform.GetPlatformName()}: {Path}";
	}
}
