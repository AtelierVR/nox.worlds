using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using Nox.Worlds.Runtime.Caching;
using UnityEngine.Events;

namespace Nox.Worlds.Runtime.SceneGroups {
	public class SceneGroupManager : INoxObject {
		public readonly List<RuntimeWorldGroup> Groups = new();

		internal readonly UnityEvent<RuntimeWorldGroup> OnGroupAdded = new();
		internal readonly UnityEvent<RuntimeWorldGroup> OnGroupRemoved = new();

		public async UniTask Dispose() {
			foreach (var world in Groups) {
				await world.Dispose();
				OnGroupRemoved.Invoke(world);
			}

			Groups.Clear();
		}

		[NoxPublic(NoxAccess.Method)]
		public RuntimeWorldGroup GetWorld(string id)
			=> Groups.Find(w => w.Id == id);

		[NoxPublic(NoxAccess.Method)]
		public async UniTask<AssetBundleRuntimeWorldGroup> LoadWorldFromCache(string hash, Action<float> progress = null, CancellationToken token = default) {
			var path = WorldCache.GetPath(hash);
			if (!string.IsNullOrEmpty(path))
				return await LoadWorldFromPath(path, progress, token);
			Logger.LogError($"World with hash {hash} not found in cache.");
			return null;
		}

		[NoxPublic(NoxAccess.Method)]
		public async UniTask<AssetBundleRuntimeWorldGroup> LoadWorldFromPath(string path, Action<float> progress = null, CancellationToken token = default) {
			var existingWorld = GetWorld(AssetBundleRuntimeWorldGroup.ParseGroup(path));
			if (existingWorld != null) {
				Logger.LogWarning($"World {path} is already loaded.");
				return existingWorld as AssetBundleRuntimeWorldGroup;
			}

			var world = await AssetBundleRuntimeWorldGroup.Load(path, progress, token);

			if (world == null) {
				Logger.LogError($"Failed to load world from path: {path}");
				return null;
			}

			world.GroupManager = this;
			Groups.Add(world);
			OnGroupAdded.Invoke(world);
			Main.Instance.CoreAPI.EventAPI.Emit("scene_group_added", world);
			return world;
		}

		[NoxPublic(NoxAccess.Method)]
		public async UniTask<AssetRuntimeWorldGroup> LoadWorldFromAssets(ResourceIdentifier path, Action<float> progress = null, CancellationToken token = default) {
			var existingWorld = GetWorld(AssetRuntimeWorldGroup.ParseId(path));
			if (existingWorld != null) {
				Logger.LogWarning($"World {path} is already loaded.");
				return existingWorld as AssetRuntimeWorldGroup;
			}

			var world = await AssetRuntimeWorldGroup.Load(path, progress, token);

			if (world == null) {
				Logger.LogError($"Failed to load world from assets: {path}");
				return null;
			}

			world.GroupManager = this;
			Groups.Add(world);
			OnGroupAdded.Invoke(world);
			Main.Instance.CoreAPI.EventAPI.Emit("scene_group_added", world);
			return world;
		}

		[NoxPublic(NoxAccess.Method)]
		public RuntimeWorldGroup GetCurrent() {
			var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			if (!currentScene.IsValid()) return null;
			return (from world in Groups
				let scenes = world.GetUnityScenes()
				where scenes.Any(scene => scene.name == currentScene.name)
				select world).FirstOrDefault();
		}

		[NoxPublic(NoxAccess.Method)]
		public bool SetCurrent(string id) {
			var world = GetWorld(id);
			if (world == null) {
				Logger.LogError($"World with id {id} not found.");
				return false;
			}

			var old = GetCurrent();
			if (old == world) {
				Logger.LogWarning($"World {id} is already the current world.");
				return true;
			}

			var canReplace = true;
			Main.Instance.CoreAPI.EventAPI.Emit("scene_group_request_change", world, new Action<object[]>(OnRequest));
			if (!canReplace) {
				Logger.LogDebug($"Canceling world change to {id} due to request.");
				return false;
			}

			old?.OnDeselect(world);
			world.OnSelect(old);

			Main.Instance.CoreAPI.EventAPI.Emit("scene_group_changed", world);
			Logger.Log($"Current world set to: {world.Id}");
			return true;

			void OnRequest(object[] args) {
				if (args.Length > 0 && args[0] is false)
					canReplace = false;
			}
		}
	}
}