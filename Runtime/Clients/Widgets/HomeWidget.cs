using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.UI;
using Nox.UI.Widgets;
using Nox.Users;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.Worlds.Runtime.Clients.Widgets {
	public class HomeWidget : MonoBehaviour, IWidget {
		public static string GetDefaultKey()
			=> "home";

		private int               _mid;
		private Image             _image;
		private AspectRatioFitter _ratio;
		private GameObject        _container;
		private GameObject        _content;
		private Image             _icon;
		private TextLanguage      _label;

		private void OnClick()
			=> Client.UiAPI?.SendGoto(_mid, WorldPage.GetStaticKey(), "identifier", GetHomeIdentifier());

		public string GetKey()
			=> GetDefaultKey();

		public Vector2Int GetSize()
			=> new(3, 2);

		public int GetPriority()
			=> 99;

		internal static WorldIdentifier GetHomeIdentifier(ICurrentUser current = null)
			=> WorldIdentifier.From((current ?? Main.UserAPI.GetCurrent())?.GetHomeId());

		public async UniTask UpdateContent() {
			var identifier = GetHomeIdentifier();
			if (!identifier.IsValid) {
				_container.SetActive(false);
				_label.UpdateText("world.no_home");
				return;
			}

			if (!_image.sprite)
				_container.SetActive(false);

			var home = await Main.Instance.Network.Fetch(identifier);
			if (home == null) {
				_container.SetActive(false);
				_label.UpdateText("world.no_home");
				return;
			}

			_label.UpdateText("value", new[] { home.Title ?? identifier.ToString() });

			await UpdateBanner(home);
		}

		private async UniTask UpdateBanner(IWorld home) {
			var url = home.Thumbnail;

			if (string.IsNullOrEmpty(url)) {
				_container.SetActive(false);
				return;
			}

			var banner = await Main.NetworkAPI.FetchTexture(url);
			if (!banner || banner.height == 0) {
				_container.SetActive(false);
				return;
			}

			_image.sprite = Sprite.Create(
				banner,
				new Rect(0, 0, banner.width, banner.height),
				new Vector2(0.5f, 0.5f)
			);
			_ratio.aspectRatio = (float)banner.width / banner.height;
			_container.SetActive(true);
		}

		public static bool TryMake(IMenu menu, RectTransform parent, out (GameObject, IWidget) values) {
			if (!GetHomeIdentifier().IsValid) {
				values = (null, null);
				return false;
			}

			var prefab    = Client.GetAsset<GameObject>("ui:prefabs/grid_item.prefab");
			var instance  = Instantiate(prefab, parent);
			var component = instance.AddComponent<HomeWidget>();
			component._mid = menu.GetId();

			var button = Reference.GetComponent<Button>("button", instance);
			button.onClick.AddListener(component.OnClick);
			instance.name = $"[{component.GetKey()}_{instance.GetInstanceID()}]";
			values        = (instance, component);

			prefab               = Client.GetAsset<GameObject>("ui:prefabs/large_widget.prefab");
			component._content   = Instantiate(prefab, Reference.GetComponent<RectTransform>("content", instance));
			component._image     = Reference.GetComponent<Image>("image", component._content);
			component._ratio     = Reference.GetComponent<AspectRatioFitter>("image_ratio", component._content);
			component._container = Reference.GetReference("image_container", component._content);
			component._icon      = Reference.GetComponent<Image>("icon", component._content);
			component._label     = Reference.GetComponent<TextLanguage>("label", component._content);

			component.UpdateContent().Forget();
			component.UpdateIcon().Forget();

			return true;
		}

		private async UniTask UpdateIcon() {
			_icon.sprite = await Client.GetAssetAsync<Sprite>("ui:icons/home.png");
		}
	}
}