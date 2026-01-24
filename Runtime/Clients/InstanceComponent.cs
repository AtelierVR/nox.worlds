using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Utils;
using Nox.Instances;
using UnityEngine;
using UnityEngine.UI;
using Logger = Nox.CCK.Utils.Logger;
using Transform = UnityEngine.Transform;

namespace Nox.Worlds.Runtime.Clients {
	public class InstanceComponent : MonoBehaviour {
		public static (GameObject go, InstanceComponent comp) Generate(WorldComponent reference, Transform parent) {
			var instance  = Instantiate(Client.GetAsset<GameObject>("instance:prefabs/instance_item.prefab"), parent);
			var component = instance.AddComponent<InstanceComponent>();
			component.reference = reference;
			component.label     = Reference.GetComponent<TextLanguage>("label", instance);
			component.text      = Reference.GetComponent<TextLanguage>("text", instance);
			component.image     = Reference.GetComponent<Image>("image", instance);
			component.button    = Reference.GetComponent<Button>("button", instance);
			component.button.onClick.AddListener(component.OnClick);
			return (instance, component);
		}

		public  WorldComponent          reference;
		public  TextLanguage            label;
		public  TextLanguage            text;
		public  Button                  button;
		public  Image                   image;
		private CancellationTokenSource _thumbnailTokenSource;
		private IInstance               _instance;

		public void UpdateContent(IInstance instance) {
			_instance = instance;
			label.UpdateText(
				"world.instance.label", new[] {
					instance.GetName()
				}
			);
			text.UpdateText(
				"world.instance.text", new[] {
					instance.GetTitle()
					?? reference.Page.World.Title
					?? instance.ToIdentifier().ToString()
				}
			);
			UpdateThumbnail(instance).Forget();
		}

		private void OnClick() {
			Logger.LogDebug($"{_instance} ({reference.Page.World}) clicked");
			Client.UiAPI?.SendGoto(
				reference.Page.MId,
				"instance",
				"instance",
				_instance,
				reference.Page.World
			);
		}


		private async UniTask UpdateThumbnail(IInstance instance) {
			if (_thumbnailTokenSource != null) {
				_thumbnailTokenSource?.Cancel();
				_thumbnailTokenSource?.Dispose();
			}

			_thumbnailTokenSource = new CancellationTokenSource();
			var url = instance?.GetThumbnailUrl() ?? reference.Page.World.Thumbnail;

			if (!string.IsNullOrEmpty(url)) {
				var texture = await Main.NetworkAPI
					.FetchTexture(url)
					.AttachExternalCancellation(_thumbnailTokenSource.Token);
				image.sprite = texture
					? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero)
					: null;
			} else image.sprite = null;

			_thumbnailTokenSource = null;
		}
	}
}