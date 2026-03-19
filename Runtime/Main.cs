using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Network;
using Nox.Search;
using Nox.Sessions;
using Nox.Tables;
using Nox.Users;
using Nox.Worlds.Runtime.Caching;
using Nox.Worlds.Runtime.Network;
using Nox.Worlds.Runtime.SceneGroups;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Nox.Worlds.Runtime {
	public class Main : IMainModInitializer, IWorldAPI {
		#region Variables

		public static Main Instance;
		public IMainModCoreAPI CoreAPI;
		public Network.Network Network;
		internal CacheManager Cache;
		private Search.Search _search;
		private LanguagePack _lang;
		internal SceneGroupManager Manager;

		public readonly UnityEvent<IWorldDescriptor, Scene> OnWorldLoaded = new();

		public static IUserAPI UserAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("users")
				?.GetInstance<IUserAPI>();

		static internal ISearchAPI SearchAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("search")
				?.GetInstance<ISearchAPI>();

		public static INetworkAPI NetworkAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("network")
				?.GetInstance<INetworkAPI>();

		static internal ITableAPI TableAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("table")
				?.GetInstance<ITableAPI>();

		static internal ISessionAPI SessionAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("session")
				?.GetInstance<ISessionAPI>();

		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		#endregion

		#region initialization

		public void OnInitializeMain(IMainModCoreAPI api) {
			Instance = this;
			CoreAPI  = api;

			api.LoggerAPI.LogDebug("Initialized");
			_lang = CoreAPI.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);

			WorldSetup.OnCheckRequest = OnCheckRequest;

			Manager = new SceneGroupManager();
			Network = new Network.Network();
			Cache   = new CacheManager();
			_search = new Search.Search();

			USceneManager.sceneLoaded   += OnSceneLoaded;
			USceneManager.sceneUnloaded += OnSceneUnloaded;

			_events = new[] {
				api.EventAPI.Subscribe("user_update", OnUserUpdate),
				api.EventAPI.Subscribe("user_logout", OnUserLogout),
			};

			var user = api.ModAPI.GetMod("users")
				?.GetInstance<IUserAPI>()?.GetCurrent();
			
			if (user != null)
				PredownloadHomeWorldAsync(user).Forget();
		}

		private bool OnCheckRequest(IWorldDescriptor descriptor) {
			var valid = true;
			CoreAPI.EventAPI.Emit("world_check_request", descriptor, new Action<object[]>(OnCallback));
			return valid;

			void OnCallback(object[] args) {
				if (args.Length > 0 && args[0] is false)
					valid = false;
			}
		}

		public async UniTask OnDisposeMainAsync() {
			WorldSetup.OnCheckRequest   =  null;
			USceneManager.sceneLoaded   -= OnSceneLoaded;
			USceneManager.sceneUnloaded -= OnSceneUnloaded;
			LanguageManager.RemovePack(_lang);

			foreach (var ev in _events)
				CoreAPI.EventAPI.Unsubscribe(ev);
			_events = Array.Empty<EventSubscription>();

			if (Manager != null)
				await Manager.Dispose();
			Manager = null;
			Cache?.Dispose();
			_search?.Dispose();
			Cache    = null;
			_search  = null;
			Network  = null;
			CoreAPI  = null;
			Instance = null;

		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
			if (!scene.TryGetComponentInChildren<IWorldDescriptor>(out var descriptor))
				return;

			OnWorldLoaded.Invoke(descriptor, scene);
		}

		private void OnSceneUnloaded(Scene scene) { }

		#endregion

		#region Favorites

		public async UniTask<IWorldIdentifier[]> AddFavorite(IWorldIdentifier identifier, string from = null)
			=> (await Network.AddFavorite(WorldIdentifier.From(identifier), from))?.Cast<IWorldIdentifier>().ToArray();

		public async UniTask<IWorldIdentifier[]> RemoveFavorite(IWorldIdentifier identifier, string from = null)
			=> (await Network.RemoveFavorite(WorldIdentifier.From(identifier), from))?.Cast<IWorldIdentifier>().ToArray();

		public async UniTask<IWorldIdentifier[]> GetFavorites(string from = null)
			=> (await Network.FetchFavorites(from))?.Cast<IWorldIdentifier>().ToArray();

		#endregion

		#region Scene Groups

		public IRuntimeWorld GetCurrent()
			=> Manager.GetCurrent();

		public bool SetCurrent(string id)
			=> Manager.SetCurrent(id);

		#endregion

		#region Loading

		public async UniTask<IRuntimeWorld> LoadFromPath(string path, Action<float> progress = null, CancellationToken token = default)
			=> await Manager.LoadWorldFromPath(path, progress, token);

		public async UniTask<IRuntimeWorld> LoadFromAssets(ResourceIdentifier path, Action<float> progress = null, CancellationToken token = default)
			=> await Manager.LoadWorldFromAssets(path, progress, token);

		public async UniTask<IRuntimeWorld> LoadFromCache(string hash, Action<float> progress = null, CancellationToken token = default)
			=> await Manager.LoadWorldFromCache(hash, progress, token);

		#endregion

		#region Networking

		public async UniTask<IWorld> Fetch(IWorldIdentifier identifier, string from = null)
			=> await Network.Fetch(WorldIdentifier.From(identifier), from);

		public async UniTask<ISearchResponse> Search(ISearchRequest data, string from = null)
			=> await Network.Search(SearchRequest.From(data), from);

		public async UniTask<IWorld> Create(ICreateWorldRequest data, string server)
			=> await Network.Create(CreateWorldRequest.From(data), server);

		public async UniTask<IWorld> Update(IWorldIdentifier identifier, IUpdateWorldRequest form, string from = null)
			=> await Network.Update(WorldIdentifier.From(identifier), UpdateWorldRequest.From(form), from);

		public async UniTask<bool> Delete(IWorldIdentifier identifier, string from = null)
			=> await Network.Delete(WorldIdentifier.From(identifier), from);

		public async UniTask<IAssetSearchResponse> SearchAssets(IWorldIdentifier identifier, IAssetSearchRequest data, string from = null)
			=> await Network.SearchAssets(WorldIdentifier.From(identifier), AssetSearchRequest.From(data), from);

		public async UniTask<bool> UploadThumbnail(IWorldIdentifier identifier, Texture2D texture, string from = null, Action<float> onProgress = null)
			=> await Network.UploadThumbnail(WorldIdentifier.From(identifier), texture, from, onProgress);

		public async UniTask<IUploadAssetResponse> UploadAssetFile(IWorldIdentifier identifier, uint assetId, string fileName, string fileHash = null, string from = null, Action<float> onProgress = null)
			=> await Network.UploadAssetFile(WorldIdentifier.From(identifier), assetId, fileName, fileHash, from, onProgress);

		public async UniTask<IWorldAsset> CreateAsset(IWorldIdentifier identifier, ICreateAssetRequest data, string from = null)
			=> await Network.CreateAsset(WorldIdentifier.From(identifier), CreateAssetRequest.From(data), from);

		#endregion

		#region Caching

		public ICaching DownloadToCache(string url, string hash = null, string from = null, UnityAction<float> progress = null, CancellationToken token = default) {
			var caching = Cache.AddDownload(url, hash, token);
			if (progress != null)
				caching.OnProgressChanged.AddListener(progress);
			return caching;
		}

		public ICaching GetDownload(string url, string hash)
			=> Cache.GetDownload(url, hash);

		public void RemoveFromCache(string hash)
			=> Cache.Clear(hash);

		public bool HasInCache(string hash)
			=> Cache.Has(hash);

		#endregion

		#region Home World Pre-download

		private void OnUserUpdate(EventData context) {
			if (!context.TryGet<ICurrentUser>(0, out var user) || user == null) {
				// user_update avec null = déconnexion (InvokeLogout → InvokeUpdate(null))
				ClearWorldConfig();
				return;
			}
			PredownloadHomeWorldAsync(user).Forget();
		}

		private static void OnUserLogout(EventData context) {
			ClearWorldConfig();
		}

		private static void ClearWorldConfig() {
			var config = Config.Load();
			config.Remove("world.hash");
			config.Remove("world.id");
			config.Save();
		}

		private async UniTask PredownloadHomeWorldAsync(ICurrentUser user) {
			var homeId = user.GetHomeId();
			if (string.IsNullOrEmpty(homeId)) {
				// Pas de home world : on efface la config pour que le default soit utilisé
				ClearWorldConfig();
				return;
			}

			var identifier = WorldIdentifier.From(homeId);
			if (!identifier.IsValid) {
				CoreAPI.LoggerAPI.LogWarning($"[World] Home world identifier '{homeId}' is invalid, skipping pre-download.");
				return;
			}

			// Recherche de l'asset compatible avec la plateforme et le moteur courants
			var req = new AssetSearchRequest {
				Engines   = new[] { EngineExtensions.CurrentEngine.GetEngineName() },
				Platforms = new[] { PlatformExtensions.CurrentPlatform.GetPlatformName() },
				Limit     = 1,
			};

			IAssetSearchResponse response;
			try {
				var server = identifier.IsLocal ? null : identifier.Server;
				response = await SearchAssets(identifier, req, server);
			} catch (Exception e) {
				CoreAPI.LoggerAPI.LogWarning($"[World] Failed to search assets for home world '{homeId}': {e.Message}");
				return;
			}

			var asset = response?.Assets?.FirstOrDefault();
			if (asset == null || string.IsNullOrEmpty(asset.Hash) || string.IsNullOrEmpty(asset.Url)) {
				CoreAPI.LoggerAPI.LogWarning($"[World] No compatible asset found for home world '{homeId}' (platform={req.Platforms[0]}, engine={req.Engines[0]}).");
				return;
			}

			// Téléchargement si absent du cache
			if (!HasInCache(asset.Hash)) {
				CoreAPI.LoggerAPI.LogDebug($"[World] Pre-downloading home world '{homeId}' (hash: {asset.Hash})...");
				try {
					var download = DownloadToCache(asset.Url, hash: asset.Hash);
					await download.Start();
				} catch (Exception e) {
					CoreAPI.LoggerAPI.LogWarning($"[World] Pre-download failed for home world '{homeId}': {e.Message}");
					return;
				}

				if (!HasInCache(asset.Hash)) {
					CoreAPI.LoggerAPI.LogWarning($"[World] Pre-download of home world '{homeId}' completed but hash '{asset.Hash}' not found in cache.");
					return;
				}
			} else {
				CoreAPI.LoggerAPI.LogDebug($"[World] Home world '{homeId}' already in cache (hash: {asset.Hash}).");
			}

			// Sauvegarde dans la config pour le chargement offline
			var config = Config.Load();
			config.Set("world.hash", asset.Hash);
			config.Set("world.id", identifier.ToString(identifier.IsLocal ? null : identifier.Server));
			config.Save();

			CoreAPI.LoggerAPI.LogDebug($"[World] Home world '{homeId}' ready. Config updated (hash: {asset.Hash}).");
		}

		#endregion
	}
}