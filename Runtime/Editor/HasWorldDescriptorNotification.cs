using Nox.CCK.Worlds;
using UnityEditor;
using UnityEngine;

namespace Nox.Worlds.Runtime.Editor {
	public static class HasWorldDescriptorNotification {
		private const string NotificationUid = "has_world_descriptor";

		[InitializeOnLoadMethod]
		private static void OnInitialize() {
			WorldDescriptorHelper.OnWorldSelected.AddListener(OnWorldSelected);
			OnWorldSelected(WorldDescriptorHelper.CurrentWorld);
		}

		private static void OnWorldSelected(WorldDescriptor world) {
			if (world) {
				WorldNotificationHelper.Remove(NotificationUid);
				return;
			}

			var worlds = Object.FindObjectsByType<WorldDescriptor>(FindObjectsSortMode.None);

			WorldNotificationHelper.Set(
				new WorldNotification(
					NotificationUid,
					NotificationType.Warning,
					worlds.Length > 0
						? new[] { "world.editor.notification.no_world_descriptor.selected" }
						: new[] { "world.editor.notification.no_world_descriptor.found" },
					worlds.Length > 0
						? new WorldAction[] {
							new(
								new[] { "world.editor.notification.no_world_descriptor.action.select_first" },
								() => Selection.activeGameObject = worlds[0].gameObject
							),
							new(
								new[] { "world.editor.notification.no_world_descriptor.action.create_new" },
								() => Selection.activeGameObject = new GameObject("WorldDescriptor", typeof(WorldDescriptor))
							)
						}
						: new WorldAction[] {
							new(
								new[] { "world.editor.notification.no_world_descriptor.action.create" },
								() => Selection.activeGameObject = new GameObject("WorldDescriptor", typeof(WorldDescriptor))
							)
						}
				)
			);
		}
	}
}
