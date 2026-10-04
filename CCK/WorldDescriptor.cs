using System;
using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Build;
using Nox.CCK.Utils;
using Nox.Worlds;
using UnityEngine;

namespace Nox.CCK.Worlds {
	public class WorldDescriptor : MonoBehaviour, IWorldDescriptor, ICompilable {
		public GameObject Anchor
			=> gameObject;

		#region Publisher

		#if UNITY_EDITOR
		/// <summary>
		/// Clés (<see cref="Platform.Key"/>) des plateformes ciblées, sérialisées : une par variant
		/// construit et publié.
		/// </summary>
		public string[] targetPlatforms = Array.Empty<string>();

		/// <summary>
		/// Plateformes que le world publie : un variant (build + fichier) par entrée, dans l'ordre de
		/// <see cref="PlatformExtensions.All"/>. Vide = plateforme courante, voir <see cref="Compile"/>.
		/// </summary>
		public Platform[] Targets {
			get => (targetPlatforms ?? Array.Empty<string>())
				.Select(key => key.GetPlatformFromName())
				.Where(platform => platform != Platform.None)
				.Distinct()
				.OrderBy(platform => Array.IndexOf(PlatformExtensions.All, platform))
				.ToArray();
			set => targetPlatforms = (value ?? Array.Empty<Platform>())
				.Where(platform => platform != Platform.None)
				.Distinct()
				.OrderBy(platform => Array.IndexOf(PlatformExtensions.All, platform))
				.Select(platform => platform.Key)
				.ToArray();
		}

		public uint     publishId;
		public string   publishServer;
		public uint     publishVersion;
		#endif

		#endregion

		#region Build

		#if UNITY_EDITOR
		public bool isCompiled;

		public int CompileOrder
			=> 9999;

		// ReSharper disable Unity.PerformanceAnalysis
		public void Compile() {
			if (Targets.Length == 0)
				Targets = new[] { PlatformExtensions.CurrentPlatform };
			Modules    = FindModules(this);
			isCompiled = true;
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

		public IWorldModule[] Modules = Array.Empty<IWorldModule>();

		public T[] GetModules<T>() where T : IWorldModule
			=> Modules.OfType<T>().ToArray();

		public IWorldModule[] GetModules()
			=> Modules;

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

				var scene = root.scene;
				if (scene.IsValid() && scene.isLoaded)
					foreach (var sceneRoot in scene.GetRootGameObjects())
						modules.UnionWith(sceneRoot.GetComponentsInChildren<IWorldModule>(true));
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