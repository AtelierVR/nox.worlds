using Nox.CCK.Language;
using Nox.CCK.Network;
using Nox.CCK.Utils;
using Nox.Instances;
using UnityEngine;
using UnityEngine.UI;
using Logger = Nox.CCK.Utils.Logger;
using Transform = UnityEngine.Transform;

namespace Nox.Worlds.Runtime.Clients {
	public class InstanceComponent : MonoBehaviour {
		public static (GameObject go, InstanceComponent comp) Generate(WorldComponent reference, Transform parent) {
			var instance  = Instantiate(Client.GetAsset<GameObject>("instances:prefabs/instance_item.prefab"), parent);
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
		private NetworkImage            _thumbnailNetworkImage;
		private IInstance               _instance;

		public void UpdateContent(IInstance instance) {
			_instance = instance;
			label.UpdateText(
				"world.instance.label", new[] {
					instance.Name
				}
			);
			text.UpdateText(
				"world.instance.text", new[] {
					instance.Title
					?? reference.Page.World.Title
					?? instance.Identifier.ToString()
				}
			);
			UpdateThumbnail(instance);
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


		private void UpdateThumbnail(IInstance instance) {
			var url = instance?.Thumbnail ?? reference.Page.World.Thumbnail;

			if (string.IsNullOrEmpty(url)) {
				image.sprite = null;
				return;
			}

			_thumbnailNetworkImage = image.GetOrAddComponent<NetworkImage>();
			_thumbnailNetworkImage.Url = url;
		}
	}
}