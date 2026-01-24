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
		public Platform target;
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
			if (target == Platform.None)
				target = PlatformExtensions.CurrentPlatform;
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