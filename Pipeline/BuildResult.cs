using System;

namespace Nox.Worlds.Pipeline {
	public class BuildResult {
		public BuildResultType Type;
		public string          Message;

		/// <summary>Bundles produits, un par plateforme construite.</summary>
		public BuildOutput[] Outputs = Array.Empty<BuildOutput>();

		/// <summary>Premier bundle produit, ou <c>null</c> quand le build a échoué.</summary>
		public string Output
			=> Outputs is { Length: > 0 } ? Outputs[0].Path : null;

		public bool IsFailed
			=> Type.HasFlag(BuildResultType.Failed);
	}
}