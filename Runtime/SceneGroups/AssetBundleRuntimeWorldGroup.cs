using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.SceneGroups {
	public class AssetBundleRuntimeWorldGroup : RuntimeWorldGroup {
		private AssetBundle AssetBundle;

		public static string ParseGroup(string path) {
			if (!string.IsNullOrEmpty(path))
				return $"bundle:{path}";
			Logger.LogError("Path is null or empty.", tag: nameof(AssetBundleRuntimeWorldGroup));
			return null;
		}

		public override async UniTask Dispose() {
			await base.Dispose();

			if (AssetBundle) {
				await AssetBundle.UnloadAsync(true);
				AssetBundle = null;
			}
		}

		public static async UniTask<AssetBundleRuntimeWorldGroup> Load(string path, Action<float> progress, CancellationToken token) {
			progress?.Invoke(0f);

			if (string.IsNullOrEmpty(path)) {
				Logger.LogError("Path is null or empty.", tag: nameof(AssetBundleRuntimeWorldGroup));
				return null;
			}

			if (token.IsCancellationRequested) {
				Logger.LogWarning($"Loading AssetBundle {path} was cancelled.", tag: nameof(AssetBundleRuntimeWorldGroup));
				return null;
			}

			var bundle = await AssetBundle.LoadFromFileAsync(path)
				.ToUniTask(
					progress: new Progress<float>(p => progress?.Invoke(p * 0.3f)),
					cancellationToken: token
				);

			if (!bundle) {
				Logger.LogError($"Failed to load AssetBundle from path: {path}", tag: nameof(AssetBundleRuntimeWorldGroup));
				return null;
			}

			if (token.IsCancellationRequested) {
				Logger.LogWarning($"Loading AssetBundle {path} was cancelled after loading.", tag: nameof(AssetBundleRuntimeWorldGroup));
				await bundle.UnloadAsync(true);
				return null;
			}

			progress?.Invoke(0.3f);

			var scenes = bundle.GetAllScenePaths();
			if (scenes.Length == 0 || string.IsNullOrEmpty(scenes[0])) {
				Logger.LogError($"No scenes found in AssetBundle: {path}", tag: nameof(AssetBundleRuntimeWorldGroup));
				await bundle.UnloadAsync(true);
				return null;
			}

			await SceneManager.LoadSceneAsync(scenes[0], LoadSceneMode.Additive)
				.ToUniTask(
					progress: new Progress<float>(p => progress?.Invoke(p * 0.3f + 0.3f)),
					cancellationToken: token
				);

			var scene = SceneManager.GetSceneByPath(scenes[0]);

			if (!scene.IsValid()) {
				Logger.LogError($"Failed to load scene from AssetBundle: {path}", tag: nameof(AssetBundleRuntimeWorldGroup));
				await bundle.UnloadAsync(true);
				return null;
			}

			if (token.IsCancellationRequested) {
				Logger.LogWarning($"Loading scene from AssetBundle {path} was cancelled after loading.", tag: nameof(AssetBundleRuntimeWorldGroup));
				await SceneManager.UnloadSceneAsync(scene);
				await bundle.UnloadAsync(true);
				return null;
			}

			progress?.Invoke(0.6f);

			var res = await WorldSetup.Prepare<AssetBundleRuntimeWorldGroup>(
				scene,
				progress: p => progress?.Invoke(0.6f + p * 0.4f),
				token: token
			);

			if (!res.Success) {
				Logger.LogError($"Failed to prepare world from AssetBundle: {path} ({res.Error})", tag: nameof(AssetBundleRuntimeWorldGroup));
				await SceneManager.UnloadSceneAsync(scene);
				await bundle.UnloadAsync(true);
				return null;
			}

			res.Runtime.Id = ParseGroup(path);
			res.Runtime.AssetBundle = bundle;

			progress?.Invoke(1f);

			return res.Runtime;
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id} Active={Active} AssetBundle={AssetBundle.name}]";
	}
}