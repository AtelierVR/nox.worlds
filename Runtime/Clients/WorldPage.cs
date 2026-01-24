using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Mods.Events;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Sessions;
using Nox.UI;
using Nox.Users;
using UnityEngine;
using Cache = Nox.Worlds.Runtime.Caching.Cache;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Clients {
	public class WorldPage : IPage {
		internal static string GetStaticKey()
			=> "world";

		public string GetKey()
			=> GetStaticKey();

		internal int MId;
		private object[] _context;
		private GameObject _content;
		private WorldComponent _component;
		private IWorldIdentifier _identifier;
		public IWorld World;
		public IWorldAsset Asset;
		public ushort Version = ushort.MaxValue;
		private bool _isLoading;

		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		public void OnRefresh()
			=> Refresh(false).Forget();

		private static bool T<T>(object[] o, int index, out T value) {
			if (o.Length > index && o[index] is T t) {
				value = t;
				return true;
			}

			value = default;
			return false;
		}

		internal bool IsHome(ICurrentUser current = null)
			=> WorldIdentifier.From((current ?? Main.UserAPI.GetCurrent())?.GetHomeId())
				.Equals(World?.Identifier);

		internal static IPage OnGotoAction(IMenu menu, object[] context) {
			if (!T(context, 0, out string type)) return null;
			switch (type) {
				case "id_server" when T(context, 1, out uint id0) && T(context, 2, out string ser0):
					return OnPageByIdentifier(menu, context, new WorldIdentifier(id0, null, ser0));
				case "identifier" when T(context, 1, out string id2):
					return OnPageByIdentifier(menu, context, WorldIdentifier.From(id2));
				case "identifier" when T(context, 1, out IWorldIdentifier wi0):
					return OnPageByIdentifier(menu, context, WorldIdentifier.From(wi0));
				case "world" when T(context, 1, out IWorld w0):
					var a0 = T(context, 2, out IWorldAsset asset) ? asset : null;
					return OnPageByWorld(menu, context, w0, a0);
			}

			return null;
		}

		private static WorldPage OnPageByIdentifier(IMenu menu, object[] context, WorldIdentifier identifier) {
			var page = new WorldPage {
				MId = menu.GetId(),
				_context = context,
				_identifier = identifier,
				World = null,
				Asset = null,
				Version = identifier.Version
			};
			page.Refresh(true).Forget();
			return page;
		}

		private static WorldPage OnPageByWorld(IMenu menu, object[] context, IWorld world, IWorldAsset asset) {
			var page = new WorldPage {
				MId = menu.GetId(),
				_context = context,
				_identifier = world.Identifier,
				World = world,
				Asset = asset,
				Version = world.Identifier.Version
			};
			if (page.Asset == null)
				page.FetchAsset(true).Forget();
			return page;
		}

		private async UniTask Refresh(bool load) {
			if (_isLoading) return;
			await FetchWorld();
			await FetchAsset();
			if (!load)
				_component.UpdateInstances(World).Forget();
			_component.UpdateContent(World, Asset);
			UpdateLayout.UpdateImmediate(_content);
		}

		private async UniTask FetchWorld(bool update = false) {
			if (_isLoading) return;
			_isLoading = true;
			World = await Main.Instance.Network.Fetch(_identifier.ToString());
			_isLoading = false;
			if (update) _component.UpdateContent(World, Asset);
		}

		private async UniTask FetchAsset(bool update = false) {
			if (_isLoading) return;
			_isLoading = true;
			Asset = (await Main.Instance.Network.SearchAssets(
					_identifier.ToString(),
					new AssetSearchRequest {
						Limit = 1,
						Versions = new[] { Version },
						Engines = new[] { EngineExtensions.CurrentEngine.GetEngineName() },
						Platforms = new[] { PlatformExtensions.CurrentPlatform.GetPlatformName() }
					}
				)).Assets
				.FirstOrDefault();
			_isLoading = false;
			if (update) _component.UpdateContent(World, Asset);
		}

		public void RemoveDownload() {
			if (!InCache() && !IsDownloading().Item1) {
				Logger.LogWarning("Cannot remove download, asset is not in cache.");
				return;
			}

			Main.Instance.RemoveFromCache(Asset.Hash);
			Logger.Log($"Removed asset from cache: {Asset.Hash}");
		}

		public void CancelDownload()
			=> GetDownload()?.Cancel();

		public void DownloadAsset() {
			if (IsDownloading().Item1) {
				Logger.Log("Asset is already downloading, no need to start again.");
				return;
			}

			if (InCache()) {
				Logger.Log("Asset is already in cache, no need to download.");
				return;
			}

			var cache = Main.Instance
				.DownloadToCache(Asset.Url, Asset.Hash);

			cache.Start().Forget();
		}

		public object[] GetContext()
			=> _context;

		public IMenu GetMenu()
			=> Client.UiAPI.Get<IMenu>(MId);

		public GameObject GetContent(RectTransform parent) {
			if (_content) return _content;
			(_content, _component) = WorldComponent.Generate(this, parent);
			_component.UpdateLoading();
			return _content;
		}

		public void OnOpen(IPage lastPage) {
			_events = new[] {
				Main.Instance.CoreAPI.EventAPI.Subscribe("world_cache_added", OnCacheUpdate),
				Main.Instance.CoreAPI.EventAPI.Subscribe("world_cache_download", OnCacheUpdate),
				Main.Instance.CoreAPI.EventAPI.Subscribe("world_cache_removed", OnCacheUpdate),
				Main.Instance.CoreAPI.EventAPI.Subscribe("user_update", OnUserUpdate),
			};
			_component.UpdateInstances(World).Forget();
		}

		private void OnUserUpdate(EventData context)
			=> _component.UpdateHome(IsHome());

		private void OnCacheUpdate(EventData context)
			=> _component.UpdateDownloading(IsDownloading());

		public void OnDisplay(IPage lastPage) {
			if (World != null) _component.UpdateContent(World, Asset);
			else if (_isLoading) _component.UpdateLoading();
			else _component.UpdateError("World not found or loading failed.");
		}

		public void OnRemove() {
			foreach (var ev in _events)
				Main.Instance.CoreAPI.EventAPI.Unsubscribe(ev);
			CancelDownload();
		}

		public bool InCache()
			=> Asset != null && Main.Instance.Cache.Has(Asset.Hash);

		private Cache GetDownload()
			=> Asset != null
				? Main.Instance.Cache.GetDownload(Asset.Url, Asset.Hash)
				: null;

		public (bool, float) IsDownloading() {
			var cache = GetDownload();
			if (cache == null) return (false, 0f);
			return cache.IsRunning
				? (true, cache.Progress)
				: (false, 1f);
		}

		public ISession[] GetMatchSessions()
			=> Main.SessionAPI.GetSessions()
				.Where(e => e.Match(_identifier))
				.ToArray();
	}
}