using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.Instances;
using Nox.UI;
using Nox.UI.Widgets;
using Nox.Worlds.Runtime.Clients.Widgets;
using UnityEngine;

namespace Nox.Worlds.Runtime.Clients {
	public class Client : IClientModInitializer {
		static internal IUiAPI UiAPI
			=> Main.Instance?.CoreAPI?.ModAPI?
				.GetMod("ui")
				?
				.GetInstance<IUiAPI>();

		static internal IInstanceAPI InstanceAPI
			=> Main.Instance?.CoreAPI?.ModAPI?
				.GetMod("instance")
				?
				.GetInstance<IInstanceAPI>();

		public static T GetAsset<T>(ResourceIdentifier path) where T : UnityEngine.Object
			=> Main.Instance?.CoreAPI?.AssetAPI != null
				? Main.Instance.CoreAPI.AssetAPI.GetAsset<T>(path)
				: null;


		public static UniTask<T> GetAssetAsync<T>(ResourceIdentifier path) where T : UnityEngine.Object
			=> Main.Instance?.CoreAPI?.AssetAPI != null
				? Main.Instance.CoreAPI.AssetAPI.GetAssetAsync<T>(path)
				: UniTask.FromResult<T>(null);

		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		internal static Client            Instance;
		internal        IClientModCoreAPI CoreAPI;

		public void OnInitializeClient(IClientModCoreAPI api) {
			Instance = this;
			CoreAPI  = api;
			_events = new[] {
				CoreAPI.EventAPI.Subscribe("menu_goto", OnGoto),
				CoreAPI.EventAPI.Subscribe("widget_request", OnWidgetRequest)
			};
		}

		private void OnGoto(EventData context) {
			if (!context.TryGet(0, out int mid)) return;
			if (!context.TryGet(1, out string key)) return;
			var menu = UiAPI?.Get<IMenu>(mid);
			if (menu == null) return;
			IPage page = null;
			if (WorldPage.GetStaticKey() == key)
				page = WorldPage.OnGotoAction(menu, context.Data[2..]);
			if (page == null) return;
			Main.Instance.CoreAPI.EventAPI.Emit("menu_display", menu.Id, page);
		}

		private void OnWidgetRequest(EventData context) {
			if (!context.TryGet(0, out int mid)) return;
			if (!context.TryGet(1, out RectTransform tr)) return;
			var menu = UiAPI?.Get<IMenu>(mid);
			if (menu == null) return;
			List<(GameObject, IWidget)> widgets = new();
			if (HomeWidget.TryMake(menu, tr, out var widget))
				widgets.Add(widget);
			foreach (var value in widgets)
				context.Callback(value.Item2, value.Item1);
		}

		public void OnDisposeClient() {
			foreach (var e in _events)
				CoreAPI.EventAPI.Unsubscribe(e);
			_events  = Array.Empty<EventSubscription>();
			CoreAPI  = null;
			Instance = null;
		}
	}
}