using System;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.Users;

namespace Nox.Worlds.Runtime.Editor {
	public class Editor : IEditorModInitializer {
		static internal IEditorModCoreAPI CoreAPI;

		private LanguagePack _lang;

		public static IUserAPI UserAPI
			=> CoreAPI.ModAPI
				.GetMod("users")
				?.GetInstance<IUserAPI>();

		public void OnInitializeEditor(IEditorModCoreAPI api) {
			CoreAPI = api;
			_lang = api.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);
		}

		public void OnDisposeEditor() {
			LanguageManager.RemovePack(_lang);
			_lang = null;
			CoreAPI = null;
		}
	}
}
