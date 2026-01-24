using UnityEditor;

namespace Nox.Worlds.Runtime.Editor {
	public class PlayModeNotification {
		private const string NotificationUid = "play_mode";

		[InitializeOnLoadMethod]
		private static void Initialize()
			=> EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

		private static void OnPlayModeStateChanged(PlayModeStateChange state) {
			if (state == PlayModeStateChange.ExitingEditMode && !WorldNotificationHelper.Has(NotificationUid))
				WorldNotificationHelper.Add(
					new WorldNotification(
						NotificationUid,
						NotificationType.Warning,
						new[] { "world.editor.notification.play_mode" }
					)
				);
			else if (state == PlayModeStateChange.EnteredEditMode)
				WorldNotificationHelper.Remove(NotificationUid);
		}
	}
}
