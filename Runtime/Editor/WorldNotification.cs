using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace Nox.Worlds.Runtime.Editor {
	public static class WorldNotificationHelper {
		public static readonly UnityEvent<WorldNotification[]> OnNotificationsChanged = new();
		public static readonly List<WorldNotification>         Notifications          = new();

		public static bool Allowed
			=> !Notifications.Exists(n => n.Type == NotificationType.Error);

		public static void Add(WorldNotification notification) {
			Notifications.Add(notification);
			OnNotificationsChanged?.Invoke(Notifications.ToArray());
		}

		public static void Remove(WorldNotification notification) {
			Notifications.Remove(notification);
			OnNotificationsChanged?.Invoke(Notifications.ToArray());
		}

		public static void Remove(string uid) {
			foreach (var notification in GetMany(uid))
				Notifications.Remove(notification);
			OnNotificationsChanged?.Invoke(Notifications.ToArray());
		}

		public static WorldNotification Get(string uid)
			=> Notifications.Find(n => n.Uid == uid);

		public static List<WorldNotification> GetMany(string uid)
			=> uid.StartsWith("*")
				? Notifications.FindAll(n => n.Uid.EndsWith(uid[1..]))
				: uid.EndsWith("*")
					? Notifications.FindAll(n => n.Uid.StartsWith(uid[..^1]))
					: Notifications.FindAll(n => n.Uid == uid);

		public static bool Has(string uid)
			=> GetMany(uid).Count > 0;

		public static void Clear() {
			Notifications.Clear();
			OnNotificationsChanged?.Invoke(Notifications.ToArray());
		}

		public static void Set(WorldNotification notification) {
			Notifications.Remove(notification);
			Notifications.Add(notification);
			OnNotificationsChanged?.Invoke(Notifications.ToArray());
		}
	}

	public class WorldNotification {
		public string           Uid;
		public NotificationType Type;
		public string[]         Content;
		public WorldAction[]    Actions;

		public WorldNotification(string uid, NotificationType type, string[] content, WorldAction[] actions = null) {
			Uid     = uid;
			Type    = type;
			Content = content;
			Actions = actions ?? Array.Empty<WorldAction>();
		}

		public override string ToString()
			=> $"{GetType()}[Uid={Uid}, Type={Type}, Content={Content}]";
	}

	public class WorldAction {
		public readonly string[]   Content;
		public readonly UnityEvent Action = new();

		public WorldAction(string[] content, UnityAction callback = null) {
			Content = content;
			if (callback != null)
				Action.AddListener(callback);
		}
	}

	public enum NotificationType {
		Success,
		Warning,
		Error,
		Info
	}
}
