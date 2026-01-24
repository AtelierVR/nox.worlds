using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using UnityEngine.SceneManagement;
using Nox.Worlds.Runtime.SceneGroups.Scenes;

namespace Nox.Worlds.Runtime.SceneGroups {
	public abstract class RuntimeWorldGroup : IRuntimeWorld, INoxObject {
		internal string Id;
		internal int Active = 0;
		internal RuntimeWorldInstance[] Instances;
		internal SceneGroupManager GroupManager;
		public IWorldIdentifier Identifier { get; set; }

		internal Scene[] GetUnityScenes()
			=> Dimensions
				.Select(e => e.GetScene())
				.Where(s => s.IsValid())
				.ToArray();

		public IEnumerable<IRuntimeWorldDimension> Dimensions
			=> Instances;

		public IRuntimeWorldDimension GetDimension(int index)
			=> index >= 0 && index < Instances.Length
				? Instances[index]
				: null;

		public bool IsCurrent {
			get => GroupManager.GetCurrent() == this;
			set {
				if (!value) return;
				GroupManager.SetCurrent(Id);
			}
		}

		[NoxPublic(NoxAccess.Method)]
		public virtual async UniTask Dispose() {
			await UniTask.Yield();
			foreach (var t in Instances)
				t?.Dispose();
			Instances = Array.Empty<RuntimeWorldInstance>();
		}

		internal void OnSelect(RuntimeWorldGroup old) {
			Logger.LogDebug($"OnSelect: {Id}");

			if (Instances == null || Instances.Length == 0) {
				Logger.LogWarning($"Cannot select world {Id}: no instances available");
				return;
			}

			var active = GetDimension(Active);
			if (active == null) {
				Logger.LogWarning($"Active instance {Active} not found, falling back to first instance");
				active = Dimensions.FirstOrDefault() as RuntimeWorldInstance;
				if (active == null) {
					Logger.LogError($"No valid instances found for world {Id}");
					return;
				}
			}

			foreach (var scene in Dimensions) {
				if (scene == null) {
					Logger.LogWarning($"Skipping null scene in world {Id}");
					continue;
				}

				if (scene == active) {
					Logger.LogDebug($"Showing the active scene {scene} in world {Id}");
					var unityScene = scene.GetScene();
					if (unityScene.IsValid()) {
						SceneManager.SetActiveScene(unityScene);
					}
					else {
						Logger.LogWarning($"Scene {scene} is not valid, cannot set as active");
					}
				}

				var instanceIds = scene.GetInstanceIds();
				if (instanceIds == null) {
					Logger.LogWarning($"GetInstanceIds returned null for scene {scene}");
					continue;
				}

				foreach (var id in instanceIds) {
					var visible = scene.IsVisibleInstance(id);
					Logger.LogDebug($"{(visible ? "Hiding" : "Showing")} the scene {scene} in world {Id}");
					scene.SetVisibleInstance(id, visible, true);
				}
			}
		}

		internal void OnDeselect(RuntimeWorldGroup @rew) {
			Logger.LogDebug($"OnDeselect: {Id}");

			if (Instances == null || Instances.Length == 0) {
				Logger.LogWarning($"Cannot deselect world {Id}: no instances available");
				return;
			}

			foreach (var scene in Dimensions) {
				if (scene == null) {
					Logger.LogWarning($"Skipping null scene in world {Id}");
					continue;
				}

				var instanceIds = scene.GetInstanceIds();
				if (instanceIds == null) {
					Logger.LogWarning($"GetInstanceIds returned null for scene {scene}");
					continue;
				}

				foreach (var id in instanceIds)
					scene.SetVisibleInstance(id, false, false);
			}
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id} Active={Active}]";
	}
}