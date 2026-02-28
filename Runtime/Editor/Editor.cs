using System;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.Users;

namespace Nox.Worlds.Runtime.Editor {
	public class Editor : IEditorModInitializer {
		internal static IEditorModCoreAPI CoreAPI;

		private LanguagePack _lang;
		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		public static IUserAPI UserAPI
			=> CoreAPI.ModAPI
				.GetMod("users")
				?.GetInstance<IUserAPI>();

		public void OnInitializeEditor(IEditorModCoreAPI api) {
			CoreAPI = api;
			_lang = api.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);
			_events = new[] {
				api.EventAPI.Subscribe("user_updated", UserConnectedNotification.OnUserUpdated),
			};
			UserConnectedNotification.OnUserUpdated(UserAPI.GetCurrent());
		}

		public void OnDisposeEditor() {
			LanguageManager.RemovePack(_lang);
			foreach (var e in _events)
				CoreAPI.EventAPI.Unsubscribe(e);
			_events = Array.Empty<EventSubscription>();
			_lang = null;
			CoreAPI = null;
		}
	}
}
