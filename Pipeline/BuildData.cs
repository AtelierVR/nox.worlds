using System;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Pipeline {
	/// <summary>
	/// Ce qu'un appelant demande à <see cref="Builder.Build"/> : un world, une destination. Le nombre de
	/// variants vient des plateformes du descriptor, et la copie de travail est créée par le build.
	/// </summary>
	public class BuildData {
		/// <summary>
		/// World à construire. Ses <see cref="WorldDescriptor.Targets"/> donnent un variant par
		/// plateforme (vide = plateforme courante). Le build ne le modifie jamais.
		/// </summary>
		public WorldDescriptor Descriptor;

		/// <summary>Dossier de destination des bundles.</summary>
		public string OutputPath;

		public Action<float, string> ProgressCallback = (_, _) => { };
	}
}