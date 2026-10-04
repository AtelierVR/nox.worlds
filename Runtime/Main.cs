using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.Audio;
using Nox.CCK.Audio;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.CCK.Network.Assets;
using Nox.CCK.Worlds;
using Nox.Network;
using Nox.Network.Assets;
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

		/// <summary>Generic asset pipeline, shared with every other asset type.</summary>
		public static IAssetsAPI AssetsAPI
			=> Instance == null
				? null
				: Instance.CoreAPI.ModAPI
					.GetMod("network")
					?.GetInstance<IAssetsAPI>();

		static internal ITableAPI TableAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("tables")
				?.GetInstance<ITableAPI>();

		static internal ISessionAPI SessionAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("session")
				?.GetInstance<ISessionAPI>();

		private EventSubscription[] _events;
	
		static internal IAudioAPI AudioAPI
			=> Instance.CoreAPI.ModAPI
				.GetMod("audio")
				?.GetInstance<IAudioAPI>();
	
		/// <summary>
		/// World volume channel. Routes and controls the volume of all worlds' audio
		/// via the "world" channel (depends on "general").
		/// </summary>
		static internal ChannelRegister WorldRegister;
	

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

			// Register world volume channel (depends on "general"), protected from removal
			WorldRegister = new ChannelRegister("world", new[] { "general" }, api);

			_events = new EventSubscription[] {
				api.EventAPI.Subscribe("user_logout", OnUserLogout),
			};

			var user = api.ModAPI.GetMod("users")
				?.GetInstance<IUserAPI>()?.Current;

			if (user != null)
				PreDownloadHomeWorldAsync(user).Forget();
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

			// Unregister world volume channel
			WorldRegister?.Dispose();
			WorldRegister = null;

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

		public async UniTask<IFavorites> AddFavorite(Identifier identifier)
			=> (await Network.AddFavorite(identifier));

		public async UniTask<IFavorites> RemoveFavorite(Identifier identifier)
			=> (await Network.RemoveFavorite(identifier));

		public async UniTask<IFavorites> GetFavorites()
			=> (await Network.FetchFavorites());

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

		public async UniTask<IWorld> Fetch(Identifier identifier, CancellationToken token = default)
			=> await Network.Fetch(identifier, token);

		public async UniTask<ISearchResponse> Search(ISearchRequest data)
			=> await Network.Search(SearchRequest.From(data));

		public async UniTask<IWorld> Create(ICreateWorldRequest data, string server)
			=> await Network.Create(WorldCreateRequest.From(data), server);

		public async UniTask<IWorld> Update(Identifier identifier, IUpdateWorldRequest form)
			=> await Network.Update(identifier, WorldUpdateRequest.From(form));

		public async UniTask<bool> Delete(Identifier identifier)
			=> await Network.Delete(identifier);

		public async UniTask<IAssetFile> ResolveBundle(Identifier identifier, CancellationToken token = default)
			=> await Network.ResolveBundle(identifier, token);

		public async UniTask<bool> AddImage(Identifier identifier, Texture2D texture, Action<float> onProgress = null)
			=> await Network.AddImage(identifier, texture, onProgress);

		#endregion

		#region Caching

		public ICaching DownloadToCache(string url, string hash = null, UnityAction<float> progress = null, CancellationToken token = default) {
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
			PreDownloadHomeWorldAsync(user).Forget();
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

		private async UniTask PreDownloadHomeWorldAsync(ICurrentUser user) {
			var identifier = user.Home;
			if (!identifier.IsValid()) {
				// Pas de home world : on efface la config pour que le default soit utilisé
				ClearWorldConfig();
				return;
			}

			// Bundle compatible avec la plateforme et le moteur courants
			IAssetFile asset;
			try {
				asset = await ResolveBundle(identifier);
			} catch (Exception e) {
				CoreAPI.LoggerAPI.LogWarning($"[World] Failed to resolve the bundle of home world '{identifier}': {e.Message}");
				return;
			}

			if (asset == null || string.IsNullOrEmpty(asset.Url)) {
				CoreAPI.LoggerAPI.LogWarning(
					$"[World] No compatible bundle for home world '{identifier}' "
					+ $"(platform={PlatformExtensions.CurrentPlatform.GetPlatformName()}, engine={EngineExtensions.CurrentEngine.GetEngineName()})."
				);
				return;
			}

			var hash = asset.CacheKey();

			if (string.IsNullOrEmpty(hash)) {
				CoreAPI.LoggerAPI.LogWarning($"[World] The bundle of home world '{identifier}' carries no hash.");
				return;
			}

			// Téléchargement si absent du cache
			if (!HasInCache(hash)) {
				CoreAPI.LoggerAPI.LogDebug($"[World] Pre-downloading home world '{identifier}' (hash: {hash})...");
				try {
					var download = DownloadToCache(asset.Url, hash: hash);
					await download.Start();
				} catch (Exception e) {
					CoreAPI.LoggerAPI.LogWarning($"[World] Pre-download failed for home world '{identifier}': {e.Message}");
					return;
				}

				if (!HasInCache(hash)) {
					CoreAPI.LoggerAPI.LogWarning($"[World] Pre-download of home world '{identifier}' completed but hash '{hash}' not found in cache.");
					return;
				}
			} else {
				CoreAPI.LoggerAPI.LogDebug($"[World] Home world '{identifier}' already in cache (hash: {hash}).");
			}

			// Sauvegarde dans la config pour le chargement offline
			var config = Config.Load();
			config.Set("home", identifier.ToString(identifier.IsLocal(user.Server) ? null : identifier.Server));
			config.Save();

			CoreAPI.LoggerAPI.LogDebug($"[World] Home world '{identifier}' ready. Config updated (hash: {hash}).");
		}

		#endregion
	}
}