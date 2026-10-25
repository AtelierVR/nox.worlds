using Nox.Assets.Editor;
using UnityEditor;

namespace Nox.Worlds.Runtime.Editor {
	public class PlayModeNotification {
		private const string NotificationUid = "play_mode";

		[InitializeOnLoadMethod]
		private static void Initialize()
			=> EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

		private static void OnPlayModeStateChanged(PlayModeStateChange state) {
			if (state == PlayModeStateChange.ExitingEditMode && !AssetNotificationHelper.Has(NotificationUid))
				AssetNotificationHelper.Add(
					new AssetNotification(
						NotificationUid,
						AssetNotificationType.Warning,
						new[] { "world.editor.notification.play_mode" }
					)
				);
			else if (state == PlayModeStateChange.EnteredEditMode)
				AssetNotificationHelper.Remove(NotificationUid);
		}
	}
}
