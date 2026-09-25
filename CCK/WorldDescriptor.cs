using System;
using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Build;
using Nox.CCK.Utils;
using Nox.Worlds;
using UnityEngine;
using UnityEngine.Serialization;

namespace Nox.CCK.Worlds {
	public class WorldDescriptor : MonoBehaviour, IWorldDescriptor, ICompilable {
		public GameObject Anchor
			=> gameObject;

		#region Publisher

		#if UNITY_EDITOR
		/// <summary>
		/// Clé (<see cref="Platform.Key"/>) de la plateforme cible, sérialisée. Les scènes/prefabs
		/// antérieurs, qui stockaient l'ancien <c>enum Platform</c> (<c>target: 1</c>), sont migrés
		/// à la lecture — voir <see cref="PlatformExtensions.GetPlatformFromName(string)"/>.
		/// </summary>
		[FormerlySerializedAs("target")]
		public string targetPlatform;

		/// <summary>Cible du build. <see cref="Platform.None"/> = plateforme courante (voir <see cref="Compile"/>).</summary>
		public Platform Target {
			get => targetPlatform.GetPlatformFromName();
			set => targetPlatform = value.Key;
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
			if (Target == Platform.None)
				Target = PlatformExtensions.CurrentPlatform;
			Modules    = FindModules(this);
			isCompiled = true;
		}
		#endif

		#endregion Build

		#region Modules

		public IWorldModule[] Modules = Array.Empty<IWorldModule>();

		public T[] GetModules<T>() where T : IWorldModule
			=> Modules.OfType<T>().ToArray();

		public IWorldModule[] GetModules()
			=> Modules;

		// ReSharper disable Unity.PerformanceAnalysis
		public static IWorldModule[] FindModules(IWorldDescriptor descriptor) {
			var modules = new HashSet<IWorldModule>(descriptor.GetModules());
			var root    = descriptor.Anchor;
			modules.UnionWith(root.GetComponents<IWorldModule>());
			modules.UnionWith(root.GetComponentsInChildren<IWorldModule>(true));
			return modules.ToArray();
		}

		// ReSharper disable Unity.PerformanceAnalysis
		public IWorldModule[] FindModules()
			=> Modules = FindModules(this);

		#endregion Modules
	}
}