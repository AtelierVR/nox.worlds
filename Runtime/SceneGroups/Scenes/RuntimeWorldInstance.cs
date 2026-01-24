using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using UnityEngine.SceneManagement;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.SceneGroups.Scenes {
	public class RuntimeWorldInstance : IRuntimeWorldDimension, INoxObject {
		internal RuntimeWorldGroup   RuntimeWorldGroup;
		internal Scene               Scene;
		internal GameObject          Anchor;
		internal GameObject          Prefab;
		internal List<InstanceScene> Instances = new();

		public RuntimeWorldInstance(RuntimeWorldGroup init, Scene scene, GameObject prefab) {
			RuntimeWorldGroup = init;
			Scene             = scene;
			Prefab            = prefab;
		}

		[NoxPublic(NoxAccess.Method)]
		public Scene GetScene()
			=> Scene;

		public async UniTask<int> MakeInstance() {
			if (!Prefab)
				return -1;

			Prefab.SetActive(false);
			var container = await Object.InstantiateAsync(Prefab);
			if (container.Length != 1) {
				foreach (var go in container)
					go.Destroy();
				return -1;
			}

			SceneManager.MoveGameObjectToScene(container[0], Scene);

			var instance = new InstanceScene {
				Container  = container[0],
				Descriptor = container[0].GetComponentInChildren<IWorldDescriptor>(false)
			};

			container[0].name = $"{GetType().Name}_{instance.GetId()}]";

			if (instance.Descriptor == null) {
				container[0].Destroy();
				return -1;
			}

			instance.Descriptor.FindModules();

			Instances.Add(instance);
			return instance.GetId();
		}

		private InstanceScene GetInstance(int id)
			=> Instances.FirstOrDefault(x => x.GetId() == id);

		public IWorldDescriptor GetDescriptor(int id)
			=> GetInstance(id)?.Descriptor;

		public GameObject GetAnchor(int id)
			=> GetInstance(id)?.Container;

		public void SetVisibleInstance(int id, bool active, bool save) {
			var instance = GetInstance(id);
			if (instance == null) {
				Logger.LogWarning($"BaseScene: Instance with ID {id} not found.");
				return;
			}

			instance.Container.SetActive(active);
			instance.Visible = active;
		}

		public int[] GetInstanceIds()
			=> Instances.Select(x => x.GetId()).ToArray();

		public void RemoveInstance(int id) {
			var instance = GetInstance(id);
			if (instance == null) {
				Logger.LogWarning($"BaseScene: Instance with ID {id} not found.");
				return;
			}

			Instances.Remove(instance);
			Object.Destroy(instance.Container);
		}

		public bool IsVisibleInstance(int id)
			=> GetInstance(id)?.Container.activeSelf ?? false;

		[NoxPublic(NoxAccess.Method)]
		public void Dispose() {
			foreach (var instance in Instances)
				RemoveInstance(instance.GetId());
			Instances.Clear();
			Scene             = default;
			RuntimeWorldGroup = null;
		}

		public override string ToString()
			=> $"{GetType().Name}[Scene={Scene.name}, World={RuntimeWorldGroup}]";
	}
}