using System;
using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using UnityEditor;
using UnityEngine;

namespace Nox.Worlds.Runtime.Editor {
	/// <summary>
	/// Avertit l'auteur qu'un monde contient des <see cref="Camera"/>. Ces caméras sont
	/// neutralisées au runtime par le garde-fou <c>CameraGuard</c> : elles ne peuvent pas rendre dans
	/// les yeux XR ni recouvrir l'écran bureau (sauf si elles rendent dans une RenderTexture).
	/// </summary>
	public static class WorldCameraNotification {
		private const string NotificationUid = "camera_components";

		[InitializeOnLoadMethod]
		private static void OnInitialize() {
			WorldDescriptorHelper.OnWorldSelected.AddListener(OnWorldSelected);
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
			OnWorldSelected(WorldDescriptorHelper.CurrentWorld);
		}

		private static void OnHierarchyChanged()
			=> OnWorldSelected(WorldDescriptorHelper.CurrentWorld);

		private static void OnWorldSelected(WorldDescriptor world) {
			WorldNotificationHelper.Remove(NotificationUid);
			if (!world)
				return;

			var cameras = FindCameras(world);
			if (cameras.Length == 0)
				return;

			WorldNotificationHelper.Set(new WorldNotification(
				NotificationUid,
				NotificationType.Warning,
				new[] { "world.editor.notification.cameras", cameras.Length.ToString() },
				new WorldAction[] {
					new(
						new[] { "world.editor.notification.cameras.action.untag" },
						() => {
							var w = WorldDescriptorHelper.CurrentWorld;
							if (!w)
								return;

							foreach (var cam in FindCameras(w)) {
								if (!cam.CompareTag("MainCamera") && cam.stereoTargetEye == StereoTargetEyeMask.None)
									continue;

								Undo.RecordObject(cam.gameObject, "Untag Cameras");
								Undo.RecordObject(cam, "Untag Cameras");
								CameraGuard.Demote(cam);
							}

							OnHierarchyChanged();
						}
					)
				}
			));
		}

		/// <summary>
		/// Caméras du monde : sous-arbre du descriptor <b>et</b> reste de sa scène — une caméra posée à
		/// côté du descriptor compte donc aussi, comme <c>WorldDescriptor.FindModules</c>. On reste
		/// limité à cette scène pour ne pas ramasser les caméras d'une autre scène chargée en additif.
		/// </summary>
		private static Camera[] FindCameras(WorldDescriptor world) {
			var root = world ? world.Anchor : null;
			if (!root)
				return Array.Empty<Camera>();

			var cameras = new HashSet<Camera>(CameraGuard.GetCameras(root));

			var scene = root.scene;
			if (scene.IsValid() && scene.isLoaded)
				cameras.UnionWith(CameraGuard.GetCameras(scene));

			cameras.RemoveWhere(camera => !camera);
			return cameras.ToArray();
		}
	}
}
