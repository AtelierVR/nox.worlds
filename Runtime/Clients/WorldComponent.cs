using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nox.CCK.Language;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Instances;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Nox.CCK.Utils.Logger;
using Transform = UnityEngine.Transform;

namespace Nox.Worlds.Runtime.Clients {
	public class WorldComponent : MonoBehaviour {
		public  GameObject              withThumbnail;
		public  GameObject              withoutThumbnail;
		public  Image                   thumbnail;
		public  TextLanguage            title;
		public  TextLanguage            identifier;
		public  TextLanguage            label;
		public  Image                   labelIcon;
		public  RectTransform           content;
		public  WorldPage               Page;
		private CancellationTokenSource _thumbnailTokenSource;
		private CancellationTokenSource _instanceTokenSource;
		public  RectTransform           instanceList;
		public  GameObject              instanceInfobox;
		public  GameObject              instanceListContainer;
		public  GameObject              descriptionContainer;
		public  TextLanguage            descriptionText;
		public  RectTransform           actions;
		public  Button                  offlineButton;

		public void UpdateAssetAvailability(bool hasAsset) {
			if (cacheButton)   cacheButton.interactable   = hasAsset;
			if (offlineButton) offlineButton.interactable = hasAsset;
		}

		public void UpdateError(string error) {
			title.UpdateText("world.error");
			identifier.UpdateText("world.error");
			label.UpdateText("world.error");
			thumbnail.sprite = null;
			thumbnail.sprite = null;
			withThumbnail.SetActive(false);
			withoutThumbnail.SetActive(true);
			descriptionContainer.SetActive(false);
			UpdateAssetAvailability(false);
		}

		public void UpdateLoading() {
			title.UpdateText("world.loading");
			identifier.UpdateText("world.loading");
			label.UpdateText("world.loading");
			thumbnail.sprite = null;
			thumbnail.sprite = null;
			withThumbnail.SetActive(false);
			withoutThumbnail.SetActive(true);
			descriptionContainer.SetActive(false);
			UpdateAssetAvailability(false);
		}

		public void UpdateContent(IWorld world, IWorldAsset asset) {
			if (world == null) return;

			title.UpdateText("world.title", new[] { world.Title });
			label.UpdateText("world.about.title", new[] { world.Title ?? world.Identifier.ToString() });
			identifier.UpdateText(
				"world.identifier", new[] {
					world.Identifier.ToString(),
					world.Id.ToString(),
					world.Server
				}
			);

			if (!string.IsNullOrEmpty(world.Description)) {
				descriptionText.UpdateText("world.description", new[] { world.Description });
				descriptionContainer.SetActive(true);
			} else descriptionContainer.SetActive(false);


			UpdateThumbnail(world).Forget();
			UpdateInstances(world).Forget();

			UpdateAssetAvailability(asset != null);
			HoverCache(_isCachedHover);
			_isHome = Page.IsHome();
			HoverHome(_isHomeHover);
		}

		private async UniTask UpdateThumbnail(IWorld world) {
			if (_thumbnailTokenSource != null) {
				_thumbnailTokenSource?.Cancel();
				_thumbnailTokenSource?.Dispose();
			}

			_thumbnailTokenSource = new CancellationTokenSource();
			if (!string.IsNullOrEmpty(world?.Thumbnail)) {
				var texture = await Main.NetworkAPI
					.FetchTexture(world.Thumbnail)
					.AttachExternalCancellation(_thumbnailTokenSource.Token);
				if (texture) {
					thumbnail.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
					withThumbnail.SetActive(true);
					withoutThumbnail.SetActive(false);
				} else {
					thumbnail.sprite = null;
					withThumbnail.SetActive(false);
					withoutThumbnail.SetActive(true);
				}
			} else {
				thumbnail.sprite = null;
				withThumbnail.SetActive(false);
				withoutThumbnail.SetActive(true);
			}

			_thumbnailTokenSource = null;
		}

		internal async UniTask UpdateInstances(IWorld world) {
			if (_instanceTokenSource != null) {
				_instanceTokenSource?.Cancel();
				_instanceTokenSource?.Dispose();
			}

			_instanceTokenSource = new CancellationTokenSource();
			var tasks = new List<UniTask<IInstance[]>>();

			var isEmpty = true;
			var isFirst = true;
			var action = new Action<IInstance[]>(
				instances => {
					Logger.LogDebug($"Found {instances.Length} instances for world {world.Title} ({world.Identifier})");
					if (isFirst)
						foreach (Transform child in instanceList.transform)
							Destroy(child.gameObject);
					isFirst = false;
					foreach (var instance in instances) {
						var (go, comp) = InstanceComponent.Generate(this, instanceList.transform);
						comp.UpdateContent(instance);
					}

					if (instances.Length > 0) {
						isEmpty = false;
						instanceInfobox.SetActive(false);
						instanceListContainer.SetActive(true);
						UpdateLayout.UpdateImmediate(instanceList);
					}
				}
			);


			foreach (var server in GetSearchableServers()) {
				if (_instanceTokenSource.IsCancellationRequested) {
					_instanceTokenSource = null;
					return;
				}

				tasks.Add(SearchInstances(world, server, _instanceTokenSource.Token, action));
			}

			await UniTask.WhenAll(tasks);
			if (isEmpty) {
				instanceInfobox.SetActive(true);
				instanceListContainer.SetActive(false);
			} else UpdateLayout.UpdateImmediate(instanceList);
		}

		private async UniTask<IInstance[]> SearchInstances(IWorld world, string server, CancellationToken token, Action<IInstance[]> callback = null) {
			if (token.IsCancellationRequested)
				return Array.Empty<IInstance>();

			var instanceAPI = Client.InstanceAPI;
			if (instanceAPI == null) {
				Logger.LogError("InstanceAPI is not available", this, tag: "WorldComponent");
				return Array.Empty<IInstance>();
			}

			var request = instanceAPI
				.MakeSearchRequest()
				.SetWorld(world.Identifier);

			var response = await instanceAPI.Search(request, server)
				.AttachExternalCancellation(token);
			if (token.IsCancellationRequested)
				return Array.Empty<IInstance>();
			var res = response == null
				? Array.Empty<IInstance>()
				: response.GetInstances();
			callback?.Invoke(res);
			return res;
		}

		public string[] GetSearchableServers() {
			var x0 = Config.Load().Get("servers");
			if (x0 == null) return Array.Empty<string>();
			var x1 = x0.ToObject<Dictionary<string, JObject>>();
			var x2 = new List<string>();
			foreach (var (address, value) in x1) {
				var features = value["features"]?.Values<string>().ToArray() ?? Array.Empty<string>();
				var search   = value["search"]?.ToObject<bool>()             ?? false;
				if (!(search && features.Contains("instance"))) continue;
				x2.Add(address);
			}

			return x2.ToArray();
		}

		private void OnRefreshInstancesClicked()
			=> UpdateInstances(Page.World).Forget();

		#region Favorite Logic

		private bool         _isFavorite      = false;
		private bool         _isFavoriteHover = false;
		public  Image        favoriteIcon;
		public  Button       favoriteButton;
		public  TextLanguage favoriteLabel;

		private void HoverFavorite(bool isHover) {
			_isFavoriteHover    = isHover;
			favoriteIcon.sprite = Client.GetAsset<Sprite>($"ui:icons/{(isHover ? _isFavorite ? "bookmark_remove" : "bookmark_add" : _isFavorite ? "bookmark_star" : "bookmark")}.png");
			favoriteLabel.UpdateText(
				isHover
					? _isFavorite
						? "world.favorite.remove"
						: "world.favorite.add"
					: _isFavorite
						? "world.favorite.star"
						: "world.favorite.none"
			);
		}

		private async UniTask OnFavoriteClickedAsync() {
			if (!favoriteButton.interactable) return;

			_isFavorite                 = !_isFavorite;
			favoriteButton.interactable = false;
			HoverFavorite(_isFavoriteHover);
			var id = WorldIdentifier.From(Page.World.Identifier);

			var favorites = _isFavorite
				? await Main.Instance.Network.AddFavorite(id)
				: await Main.Instance.Network.RemoveFavorite(id);

			_isFavorite = favorites.Any(f => f.Equals(id));
			HoverFavorite(_isFavoriteHover);

			favoriteButton.interactable = true;
		}

		#endregion

		#region Cache Logic

		private bool         _isCachedHover      = false;
		private string       _lastTextureCaching = "icons/0.png";
		public  Image        cacheIcon;
		public  Button       cacheButton;
		public  Slider       cacheProgress;
		public  TextLanguage cacheLabel;

		public void UpdateDownloading((bool, float) download) {
			if (download.Item1) {
				cacheProgress.value = download.Item2;
			} else cacheProgress.value = 0;

			HoverCache(_isCachedHover);
		}

		private void HoverCache(bool isHover) {
			_isCachedHover = isHover;
			var texture = ((Page.InCache() ? 1 : 0)     << 2)
				| ((Page.IsDownloading().Item1 ? 1 : 0) << 1)
				| ((_isCachedHover ? 1 : 0)             << 0);
			if (texture > 5) texture -= 4;
			if (!Page.IsDownloading().Item1)
				cacheProgress.value = 0;

			// 0 - | 0 | 0 | 0 | neutral (not hovered, not downloaded)
			// 1 - | 0 | 0 | 1 | can be downloaded (hovered, not downloaded)
			// 2 - | 0 | 1 | 0 | downloading (not hovered, downloading)
			// 3 - | 0 | 1 | 1 | cancel download (hovered, downloading)
			// 4 - | 1 | 0 | 0 | downloaded (not hovered, downloaded)
			// 5 - | 1 | 0 | 1 | remove from cache (hovered, downloaded)
			// 6 - | 1 | 1 | 0 | re-downloading (not hovered, re-downloading) (set to 2)
			// 7 - | 1 | 1 | 1 | cancel re-download (hovered, re-downloading) (set to 3)

			if (_lastTextureCaching != $"ui:icons/cache{texture}.png")
				cacheIcon.sprite = Client.GetAsset<Sprite>(_lastTextureCaching = $"ui:icons/cache{texture}.png");

			cacheLabel.UpdateText(
				"world.cache."
				+ new[] {
					"none",
					"add",
					"downloading",
					"cancel",
					"downloaded",
					"remove"
				}[texture]
			);
		}

		private void OnCacheClickedAsync() {
			if (Page.IsDownloading().Item1) {
				Page.CancelDownload();
				return;
			}

			if (Page.InCache()) {
				Page.RemoveDownload();
				return;
			}

			Page.DownloadAsset();
			HoverCache(_isCachedHover);
		}

		#endregion

		#region Join Offline Logic

		public Image        offlineIcon;
		public TextLanguage offlineLabel;

		private void OnJoinOffline() {
			var world = Page.World.Identifier;
			
			var meta = world.Metadata.ToDictionary(
				kvp => kvp.Key,
				kvp => kvp.Value.ToArray()
			);
			
			meta.Add(WorldIdentifier.VersionKey, new []{ Page.Version.ToString() });
			
			world = new WorldIdentifier(
				world.Id,
				meta,
				world.Server
			);
			
			Main.SessionAPI.TryMake(
				"offline", new Dictionary<string, object> {
					{ "world", world },
					{ "set_current", true }
				}, out _
			);
		}

		#endregion

		#region Make Instance Logic

		private void OnMakeInstanceClicked() {
			if (Page?.World == null) return;
			Client.UiAPI?.SendGoto(Page.MId, "instance_create", Page.World, Page.Version == ushort.MaxValue ? null : Page.Version);
		}

		#endregion

		#region Home Logic

		private bool         _isHome      = false;
		private bool         _isHomeHover = false;
		public  Image        homeIcon;
		public  TextLanguage homeLabel;
		public  Button       homeButton;

		public void UpdateHome(bool isHome) {
			_isHome = isHome;
			HoverHome(_isHomeHover);
		}

		private void HoverHome(bool isHover) {
			_isHomeHover    = isHover;
			homeIcon.sprite = Client.GetAsset<Sprite>($"ui:icons/{(isHover ? _isHome ? "home_remove" : "home_add" : _isHome ? "home_star" : "home")}.png");
			homeLabel.UpdateText(
				isHover
					? _isHome
						? "world.home.remove"
						: "world.home.add"
					: _isHome
						? "world.home.star"
						: "world.home.none"
			);
		}

		private async UniTask OnHomeClickedAsync() {
			if (!homeButton.interactable) return;
			var hasHome = Page.IsHome();
			_isHome                 = !hasHome;
			homeButton.interactable = false;
			HoverHome(_isHomeHover);
			var id = Page.World.Identifier;

			await Main.UserAPI
				.UpdateCurrent(
					Main.UserAPI.MakeUpdateCurrentRequest()
						.SetHome(hasHome ? null : id.ToString())
				);

			_isHome = Page.IsHome();
			HoverHome(_isHomeHover);

			homeButton.interactable = true;
		}

		#endregion

		public static (GameObject, WorldComponent) Generate(WorldPage worldPage, RectTransform parent) {
			var content              = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/split.prefab"), parent);
			var iconAsset            = Client.GetAsset<GameObject>("ui:prefabs/header_icon.prefab");
			var labelAsset           = Client.GetAsset<GameObject>("ui:prefabs/header_label.prefab");
			var withTitleAsset       = Client.GetAsset<GameObject>("ui:prefabs/with_title.prefab");
			var listAsset            = Client.GetAsset<GameObject>("ui:prefabs/list.prefab");
			var scrollAsset          = Client.GetAsset<GameObject>("ui:prefabs/scroll.prefab");
			var boxAsset             = Client.GetAsset<GameObject>("ui:prefabs/box.prefab");
			var actionButtonAsset    = Client.GetAsset<GameObject>("ui:prefabs/action_button.prefab");
			var actionContainerAsset = Client.GetAsset<GameObject>("ui:prefabs/action_container.prefab");

			var component = content.AddComponent<WorldComponent>();
			component.Page = worldPage;
			content.name   = $"[{worldPage.GetKey()}_{content.GetInstanceID()}]";

			var splitContent   = Reference.GetComponent<RectTransform>("content", content);
			var containerAsset = Client.GetAsset<GameObject>("ui:prefabs/container.prefab");

			// generate profile
			var container = Instantiate(containerAsset, splitContent);
			var profile = Instantiate(
				Client.GetAsset<GameObject>("ui:prefabs/profile.prefab"),
				Reference.GetComponent<RectTransform>("content", container)
			);
			component.identifier       = Reference.GetComponent<TextLanguage>("identifier", profile);
			component.title            = Reference.GetComponent<TextLanguage>("title", profile);
			component.thumbnail        = Reference.GetComponent<Image>("thumbnail", profile);
			component.withThumbnail    = Reference.GetReference("with_thumbnail", profile);
			component.withoutThumbnail = Reference.GetReference("without_thumbnail", profile);

			// generate dashboard
			container = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/container_full.prefab"), splitContent);
			var withTitle = Instantiate(
				withTitleAsset,
				Reference.GetComponent<RectTransform>("content", container)
			);

			var header = Reference.GetReference("header", withTitle);
			var icon   = Instantiate(iconAsset, Reference.GetComponent<RectTransform>("before", header));
			var label  = Instantiate(labelAsset, Reference.GetComponent<RectTransform>("content", header));

			component.labelIcon        = Reference.GetComponent<Image>("image", icon);
			component.label            = Reference.GetComponent<TextLanguage>("text", label);
			component.labelIcon.sprite = Client.GetAsset<Sprite>("ui:icons/globe.png");

			var contentDash = Reference.GetComponent<RectTransform>("content", withTitle);
			// setup scroll + list
			var scroll = Instantiate(scrollAsset, contentDash);
			var list   = Instantiate(listAsset, Reference.GetComponent<RectTransform>("content", scroll));
			component.content = Reference.GetComponent<RectTransform>("content", list);

			// add box actions
			var boxActions = Instantiate(boxAsset, component.content);
			Reference.GetComponent<TextLanguage>("text", boxActions).UpdateText("world.about.actions");
			component.actions = Reference.GetComponent<RectTransform>("content", Instantiate(actionContainerAsset, Reference.GetComponent<RectTransform>("content", boxActions)));

			#region Join Offline Button

			var offline             = Instantiate(actionButtonAsset, component.actions);
			var offlineEventTrigger = Reference.GetComponent<EventTrigger>("button", offline);
			component.offlineButton      = Reference.GetComponent<Button>("button", offline);
			component.offlineIcon        = Reference.GetComponent<Image>("image", offline);
			component.offlineLabel       = Reference.GetComponent<TextLanguage>("text", offline);
			component.offlineIcon.sprite = Client.GetAsset<Sprite>("ui:icons/distance.png");
			component.offlineLabel.UpdateText("world.offline.join");
			SetupEvents(
				offlineEventTrigger,
				() => component.OnJoinOffline(),
				() => { }, // No hover effects for now
				() => { }  // No hover effects for now
			);

			#endregion

			#region Make Instance Button

			var makeInstance             = Instantiate(actionButtonAsset, component.actions);
			var makeInstanceEventTrigger = Reference.GetComponent<EventTrigger>("button", makeInstance);
			var makeInstanceIcon         = Reference.GetComponent<Image>("image", makeInstance);
			var makeInstanceLabel        = Reference.GetComponent<TextLanguage>("text", makeInstance);
			makeInstanceIcon.sprite = Client.GetAsset<Sprite>("ui:icons/edit_location.png");
			makeInstanceLabel.UpdateText("world.instance.make");
			SetupEvents(
				makeInstanceEventTrigger,
				() => component.OnMakeInstanceClicked(),
				() => { }, // No hover effects for now
				() => { }  // No hover effects for now
			);

			#endregion

			#region Cache Button

			var cache             = Instantiate(actionButtonAsset, component.actions);
			var cacheEventTrigger = Reference.GetComponent<EventTrigger>("button", cache);
			component.cacheButton   = Reference.GetComponent<Button>("button", cache);
			component.cacheIcon     = Reference.GetComponent<Image>("image", cache);
			component.cacheLabel    = Reference.GetComponent<TextLanguage>("text", cache);
			component.cacheProgress = Reference.GetComponent<Slider>("progress", cache);
			component.cacheLabel.UpdateText("world.cache.none");
			component.cacheIcon.sprite = Client.GetAsset<Sprite>("ui:icons/cache0.png");
			SetupEvents(
				cacheEventTrigger,
				() => component.OnCacheClickedAsync(),
				() => component.HoverCache(true),
				() => component.HoverCache(false)
			);

			#endregion

			#region Favorite Button

			var favorite             = Instantiate(actionButtonAsset, component.actions);
			var favoriteEventTrigger = Reference.GetComponent<EventTrigger>("button", favorite);
			component.favoriteButton = Reference.GetComponent<Button>("button", favorite);
			component.favoriteIcon   = Reference.GetComponent<Image>("image", favorite);
			component.favoriteLabel  = Reference.GetComponent<TextLanguage>("text", favorite);
			component.favoriteLabel.UpdateText("world.favorite.none");
			component.favoriteIcon.sprite = Client.GetAsset<Sprite>("ui:icons/bookmark.png");
			SetupEvents(
				favoriteEventTrigger,
				() => component.OnFavoriteClickedAsync().Forget(),
				() => component.HoverFavorite(true),
				() => component.HoverFavorite(false)
			);

			#endregion

			#region Home Button

			var homeButton       = Instantiate(actionButtonAsset, component.actions);
			var homeEventTrigger = Reference.GetComponent<EventTrigger>("button", homeButton);
			component.homeButton = Reference.GetComponent<Button>("button", homeButton);
			component.homeIcon   = Reference.GetComponent<Image>("image", homeButton);
			component.homeLabel  = Reference.GetComponent<TextLanguage>("text", homeButton);
			component.homeLabel.UpdateText("world.home.none");
			component.homeIcon.sprite = Client.GetAsset<Sprite>("ui:icons/home.png");
			SetupEvents(
				homeEventTrigger,
				() => component.OnHomeClickedAsync().Forget(),
				() => component.HoverHome(true),
				() => component.HoverHome(false)
			);

			#endregion

			// add box description
			component.descriptionContainer = Instantiate(boxAsset, component.content);
			Reference.GetComponent<TextLanguage>("text", component.descriptionContainer).UpdateText("world.about.description");
			component.descriptionText = Reference.GetComponent<TextLanguage>(
				"text", Instantiate(
					Client.GetAsset<GameObject>("ui:prefabs/text.prefab"),
					Reference.GetComponent<RectTransform>("content", component.descriptionContainer)
				)
			);

			// generate instances
			container = Instantiate(containerAsset, splitContent);
			withTitle = Instantiate(withTitleAsset, Reference.GetComponent<RectTransform>("content", container));

			header = Reference.GetReference("header", withTitle);
			icon   = Instantiate(iconAsset, Reference.GetComponent<RectTransform>("before", header));
			label  = Instantiate(labelAsset, Reference.GetComponent<RectTransform>("content", header));

			Reference.GetComponent<Image>("image", icon).sprite = Client.GetAsset<Sprite>("ui:icons/location.png");
			Reference.GetComponent<TextLanguage>("text", label).UpdateText("world.instances.title");

			var headerButtonAsset = Client.GetAsset<GameObject>("ui:prefabs/header_button.prefab");
			var before            = Reference.GetComponent<RectTransform>("after", header);
			var refreshButton     = Instantiate(headerButtonAsset, before);
			Reference.GetComponent<Button>("button", refreshButton)
				.onClick.AddListener(component.OnRefreshInstancesClicked);

			Reference.GetComponent<Image>("image", refreshButton).sprite = Client.GetAsset<Sprite>("ui:icons/refresh.png");
			var searchButton = Instantiate(headerButtonAsset, before);
			Reference.GetComponent<Image>("image", searchButton).sprite = Client.GetAsset<Sprite>("ui:icons/search.png");

			var contentIn = Reference.GetComponent<RectTransform>("content", withTitle);
			component.instanceInfobox = Instantiate(Client.GetAsset<GameObject>("ui:prefabs/infobox.prefab"), contentIn);
			Reference.GetComponent<TextLanguage>("text", component.instanceInfobox).UpdateText("world.no_instances");
			component.instanceListContainer = Instantiate(scrollAsset, contentIn);
			list                            = Instantiate(listAsset, Reference.GetComponent<RectTransform>("content", component.instanceListContainer));
			component.instanceList          = Reference.GetComponent<RectTransform>("content", list);

			return (content, component);
		}

		// ReSharper disable Unity.PerformanceAnalysis
		private static void SetupEvents(EventTrigger eventTrigger, Action click, Action enter, Action exit) {
			if (!eventTrigger) return;
			var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
			entry.callback.AddListener(_ => click());
			eventTrigger.triggers.Add(entry);
			entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
			entry.callback.AddListener(_ => enter());
			eventTrigger.triggers.Add(entry);
			entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
			entry.callback.AddListener(_ => exit());
			eventTrigger.triggers.Add(entry);
		}
	}
}