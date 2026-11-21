using System;
using System.Collections.Generic;
using System.Linq;
using Nox.Assets;
using Nox.CCK.Build;
using Nox.Worlds;

namespace Nox.CCK.Worlds {
	public class WorldDescriptor : AssetDescriptor<IWorldModule>, IWorldDescriptor, ICompilable {
		#region Build

		#if UNITY_EDITOR
		// ReSharper disable Unity.PerformanceAnalysis
		public override void Compile() {
			base.Compile();
			Modules = FindModules(this);
		}
		#endif

		#endregion Build

		#region Modules

		#if UNITY_EDITOR
		/// <summary>
		/// Miroir sérialisé de <see cref="Modules"/> pour l'inspecteur : Unity ne sérialise pas un tableau
		/// typé par une interface, on expose donc les mêmes objets en <see cref="UnityEngine.Object"/>.
		/// Réécrit depuis la scène par <c>WorldDescriptorEditor</c>, il ne sert qu'à l'affichage.
		/// </summary>
		public UnityEngine.Object[] detected = Array.Empty<UnityEngine.Object>();
		#endif

		public override IWorldModule[] Modules { get; set; } = Array.Empty<IWorldModule>();

		/// <summary>
		/// Modules du world : ceux du descriptor et de son sous-arbre, puis ceux du reste de sa scène — un
		/// module posé à côté du descriptor compte donc aussi. La recherche reste limitée à cette scène,
		/// pour ne pas ramasser les modules d'une autre scène chargée en additif.
		/// </summary>
		// ReSharper disable Unity.PerformanceAnalysis
		public static IWorldModule[] FindModules(IWorldDescriptor descriptor) {
			var modules = new HashSet<IWorldModule>(descriptor.GetModules());
			var root    = descriptor.Anchor;

			if (root) {
				// Inclut le GameObject du descriptor lui-même
				modules.UnionWith(root.GetComponentsInChildren<IWorldModule>(true));

				if (Utils.Anchor.TryFind(root.transform, WorldPath.WORLD_TAG, out var boundary))
					modules.UnionWith(boundary.GetComponentsInChildren<IWorldModule>(true));
				else {
					var scene = root.scene;
					if (scene.IsValid() && scene.isLoaded)
						foreach (var sceneRoot in scene.GetRootGameObjects())
							modules.UnionWith(sceneRoot.GetComponentsInChildren<IWorldModule>(true));
				}
			}

			// La liste sert de graine aux appels suivants : une référence détruite ne doit pas y rester
			modules.RemoveWhere(module => module is UnityEngine.Object element && !element);

			#if UNITY_EDITOR
			// L'inspecteur affiche un tableau sérialisé (Unity ne sérialise pas IWorldModule[]) : on le tient
			// à jour ici, donc à chaque détection. Trié par nom, l'ordre d'un HashSet n'étant pas déterministe.
			if (descriptor is WorldDescriptor world) {
				var elements = modules.OfType<UnityEngine.Object>()
					.OrderBy(element => element.name, StringComparer.Ordinal)
					.ToArray();
				if (!(world.detected ?? Array.Empty<UnityEngine.Object>()).SequenceEqual(elements))
					world.detected = elements;
			}
			#endif

			return modules.ToArray();
		}

		// ReSharper disable Unity.PerformanceAnalysis
		public IWorldModule[] FindModules()
			=> Modules = FindModules(this);

		#endregion Modules
	}
}
