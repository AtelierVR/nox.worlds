using System;
using System.Collections.Generic;
using Nox.Assets.Editor;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using UnityEditor;
using UnityEngine;

namespace Nox.Worlds.Runtime.Editor {
	/// <summary>
	/// Runs the notifications shared by every asset kind (<see cref="AssetDescriptorNotifications"/>:
	/// cameras, mesh import settings, missing components) on the selected world, and declares the world to
	/// <see cref="AssetPlayModeNotifications"/>.
	/// </summary>
	public static class WorldDescriptorNotifications {
		/// <summary>Panel id of the world publisher/builder (<c>WorldBuilderSource.Panel</c>).</summary>
		private const string Kind = "world";

		[InitializeOnLoadMethod]
		private static void Initialize() {
			AssetPlayModeNotifications.Register(Kind);

			WorldDescriptorHelper.OnWorldSelected.AddListener(OnWorldSelected);
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
			OnWorldSelected(WorldDescriptorHelper.CurrentWorld);
		}

		private static void OnHierarchyChanged()
			=> OnWorldSelected(WorldDescriptorHelper.CurrentWorld);

		private static void OnWorldSelected(WorldDescriptor world) {
			if (!world) {
				AssetDescriptorNotifications.Clear(Kind);
				return;
			}

			// The root is read on every call, actions included: a fix must run on the world selected when
			// the user clicks it, not on the one that was selected when the notification was made.
			AssetDescriptorNotifications.Update(Kind, CurrentRoot, FindCameras);
		}

		private static GameObject CurrentRoot() {
			var world = WorldDescriptorHelper.CurrentWorld;
			return world && world.Anchor ? world.Anchor : null;
		}

		/// <summary>
		/// Cameras of the world: the descriptor subtree <b>and</b> the rest of its scene — a camera placed
		/// next to the descriptor counts too, as in <c>WorldDescriptor.FindModules</c>. Staying inside that
		/// scene avoids picking up the cameras of another scene loaded additively.
		/// </summary>
		private static Camera[] FindCameras(GameObject root) {
			if (!root)
				return Array.Empty<Camera>();

			var cameras = new HashSet<Camera>(CameraGuard.GetCameras(root));

			var scene = root.scene;
			if (scene.IsValid() && scene.isLoaded)
				cameras.UnionWith(CameraGuard.GetCameras(scene));

			cameras.RemoveWhere(camera => !camera);
			return new List<Camera>(cameras).ToArray();
		}
	}
}
