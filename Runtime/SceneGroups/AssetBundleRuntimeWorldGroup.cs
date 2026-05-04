using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.AssetBundles;
using Nox.CCK.AssetBundles;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.SceneGroups {
	public class AssetBundleRuntimeWorldGroup : RuntimeWorldGroup {
		public IAsset Bundle;
		public string Path;
		public string CacheId;

		public static string ParseGroup(string path) {
			if (!string.IsNullOrEmpty(path))
				return $"bundle:{path}";
			Logger.LogError("Path is null or empty.", tag: nameof(AssetBundleRuntimeWorldGroup));
			return null;
		}

		public override async UniTask Dispose() {
			await base.Dispose();

			GlobalAssetBundleManager.DetachFile(Path, CacheId);
			Bundle = null;
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

			var group = new AssetBundleRuntimeWorldGroup {
				Path = path,
				CacheId = nameof(AssetBundleRuntimeWorldGroup) + "_" + Guid.NewGuid(),
			};

			try {
				group.Bundle = await GlobalAssetBundleManager.LoadFileAsync(
					path,
					group.CacheId,
					new Progress<float>(p => progress?.Invoke(p * 0.3f))
				);
			} catch (Exception e) {
				Logger.LogError(new Exception($"Exception while loading AssetBundle from path: {path}", e));
				return null;
			}

			var bundle = group.Bundle?.AssetBundle;

			if (!bundle) {
				Logger.LogError($"Failed to load AssetBundle from path: {path}", tag: nameof(AssetBundleRuntimeWorldGroup));
				GlobalAssetBundleManager.DetachFile(path, group.CacheId);
				return null;
			}

			if (token.IsCancellationRequested) {
				Logger.LogWarning($"Loading AssetBundle {path} was cancelled after loading.", tag: nameof(AssetBundleRuntimeWorldGroup));
				GlobalAssetBundleManager.DetachFile(path, group.CacheId);
				return null;
			}

			progress?.Invoke(0.3f);

			var scenes = bundle.GetAllScenePaths();
			if (scenes.Length == 0 || string.IsNullOrEmpty(scenes[0])) {
				Logger.LogError($"No scenes found in AssetBundle: {path}", tag: nameof(AssetBundleRuntimeWorldGroup));
				GlobalAssetBundleManager.DetachFile(path, group.CacheId);
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
				GlobalAssetBundleManager.DetachFile(path, group.CacheId);
				return null;
			}

			if (token.IsCancellationRequested) {
				Logger.LogWarning($"Loading scene from AssetBundle {path} was cancelled after loading.", tag: nameof(AssetBundleRuntimeWorldGroup));
				await SceneManager.UnloadSceneAsync(scene);
				GlobalAssetBundleManager.DetachFile(path, group.CacheId);
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
				GlobalAssetBundleManager.DetachFile(path, group.CacheId);
				return null;
			}

			res.Runtime.Id = ParseGroup(path);
			res.Runtime.Bundle = group.Bundle;
			res.Runtime.Path = group.Path;
			res.Runtime.CacheId = group.CacheId;

			progress?.Invoke(1f);

			return res.Runtime;
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id} Active={Active} Path={Path}]";
	}
}