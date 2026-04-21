using Nox.CCK.Mods.Events;
using Nox.Users;

namespace Nox.Worlds.Runtime.Editor {
	public static class UserConnectedNotification {
		private const string NotConnectedUid = "not_connected";
		private const string ConnectedUid = "connected";

		public static void OnUserUpdated(EventData context)
			=> OnUserUpdated(context.TryGet(0, out ICurrentUser u) ? u : null);

		public static void OnUserUpdated(ICurrentUser user) {
			if (user == null)
				UpdateNotConnected();
			else UpdateConnected(user);
		}

		private static void UpdateNotConnected() {
			WorldNotificationHelper.Remove(ConnectedUid);
			if (WorldNotificationHelper.Has(NotConnectedUid)) return;
			var notification = new WorldNotification(
				NotConnectedUid,
				NotificationType.Warning,
				new[] { "world.editor.notification.user_not_connected" }
			);
			WorldNotificationHelper.Add(notification);
		}

		private static void UpdateConnected(ICurrentUser user) {
			WorldNotificationHelper.Remove(NotConnectedUid);
			if (WorldNotificationHelper.Has(ConnectedUid)) return;
			var notification = new WorldNotification(
				ConnectedUid,
				NotificationType.Info,
				new[] {
					"world.editor.notification.user_connected",
					user.Display,
					user.Identifier.ToString()
				}
			);
			WorldNotificationHelper.Add(notification);
		}
	}
}
