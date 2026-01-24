using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Build;
using Nox.CCK.Utils;
using Nox.Worlds.Runtime.SceneGroups.Scenes;
using Nox.Worlds.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Nox.Worlds.Runtime.SceneGroups {
	public static class WorldSetup {
		public class PrepareResult<T> where T : RuntimeWorldGroup {
			public bool Success;
			public string Error;
			public T Runtime;
		}
		public static Func<IWorldDescriptor, bool> OnCheckRequest;

		public static async UniTask<PrepareResult<T>> Prepare<T>(Scene scene, Action<float> progress = null, CancellationToken token = default) where T : RuntimeWorldGroup, new() {
			var runtime = new T { Active = 0 };
			if (!scene.IsValid())
				return new PrepareResult<T> {
					Success = false,
					Error = "Invalid scene."
				};

			progress?.Invoke(0.0f);

			var prefab = new GameObject($"[Reference] {nameof(T)}");
			SceneManager.MoveGameObjectToScene(prefab, scene);
			foreach (var root in scene.GetRootGameObjects())
				root.transform.SetParent(prefab.transform);
			prefab.SetActive(false);

			if (!prefab.TryGetComponentInChildren<IWorldDescriptor>(out var descriptor))
				return new PrepareResult<T> {
					Success = false,
					Error = "No world descriptor found in scene."
				};

			// Vérifier l'annulation dès le début
			if (token.IsCancellationRequested)
				return new PrepareResult<T> {
					Success = false,
					Error = "Operation cancelled."
				};

			var gameObject = descriptor.Anchor;

			if (!gameObject)
				return new PrepareResult<T> {
					Success = false,
					Error = "World descriptor root GameObject is null."
				};

			descriptor.FindModules();


			if (WorldSetup.OnCheckRequest != null && !WorldSetup.OnCheckRequest(descriptor))
				return new PrepareResult<T> {
					Success = false,
					Error = "A mod asked to cancel the world preparation."
				};

			descriptor.FindModules();

			if (token.IsCancellationRequested)
				return new PrepareResult<T> {
					Success = false,
					Error = "Operation cancelled."
				};

			progress?.Invoke(0.1f);

			// Compilation des éléments avec progression
			var result = await new Compiler(gameObject.GetComponentsInChildren<ICompilable>(true))
				.Compile(cancellationToken: token);

			if (token.IsCancellationRequested)
				return new PrepareResult<T> {
					Success = false,
					Error = "Operation cancelled."
				};

			if (!result)
				return new PrepareResult<T> {
					Success = false,
					Error = "Compilation failed."
				};

			progress?.Invoke(0.8f);

			var modules = descriptor.GetModules();
			var moduleArray = modules.ToArray();

			// Initialisation des modules avec progression
			for (var i = 0; i < moduleArray.Length; i++) {
				if (token.IsCancellationRequested)
					return new PrepareResult<T> {
						Success = false,
						Error = "Operation cancelled."
					};

				if (!await moduleArray[i].Setup(runtime))
					return new PrepareResult<T> {
						Success = false,
						Error = $"Module {moduleArray[i].GetType().Name} failed to initialize."
					};

				// Rapporter la progression (80% à 100% pour les modules)
				var moduleProgress = 0.8f + 0.2f * (i + 1) / moduleArray.Length;
				progress?.Invoke(moduleProgress);
			}

			var scenes = descriptor.GetModules<IScenesModule>().FirstOrDefault();
			if (scenes == null)
				return new PrepareResult<T> {
					Success = false,
					Error = "No scenes module found in world descriptor."
				};

			foreach (var camera in prefab.GetComponentsInChildren<Camera>(true))
				if (camera.CompareTag("MainCamera"))
					camera.tag = "Untagged";

			foreach (var eventSystem in prefab.GetComponentsInChildren<EventSystem>(true))
				eventSystem.enabled = false;

			runtime.Instances = new RuntimeWorldInstance[scenes.GetScenes().Length + 1];
			runtime.Instances[0] = new RuntimeWorldInstance(runtime, scene, prefab);

			progress?.Invoke(1.0f);
			return new PrepareResult<T> {
				Success = true,
				Error = null,
				Runtime = runtime
			};
		}
	}
}