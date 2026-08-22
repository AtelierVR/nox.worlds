using System;
using UnityEngine;
using Nox.CCK.Utils;

namespace Nox.Worlds.Runtime.SceneGroups.Scenes {
	public class InstanceScene : IDisposable {
		public GameObject Container;
		public IWorldDescriptor Descriptor;
		public bool Visible = false;

		public InstanceScene(GameObject container, IWorldDescriptor descriptor) {
			Container  = container;
			Descriptor = descriptor;
			InstantiateHelper.OnInstantiate.AddListener(OnInstantiation);
			OnVerify(Container);
		}

		public int GetId()
			=> Descriptor.Anchor.GetId();

		/// <summary>
		/// Called when a GameObject is instantiated globally. Checks if it belongs to this
		/// scene instance (is a child of <see cref="Container"/>) and removes any
		/// <see cref="AudioListener"/> components from it.
		/// </summary>
		private void OnInstantiation(GameObject instance) {
			if (!instance || !Container || !instance.transform.IsChildOf(Container.transform))
				return;
			OnVerify(instance);
		}

		private void OnVerify(GameObject instance) {
			foreach (var listener in instance.GetComponentsInChildren<AudioListener>(true))
				listener.Destroy();

			// Apply the world volume channel to any AudioSource in / under this instance.
			foreach (var source in instance.GetComponentsInChildren<AudioSource>(true)) 
				source.outputAudioMixerGroup = Main.WorldRegister.MixerGroup;
		}

		public void Dispose()
			=> InstantiateHelper.OnInstantiate.RemoveListener(OnInstantiation);
	}
}