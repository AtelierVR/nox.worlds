using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using UnityEngine.SceneManagement;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.SceneGroups {
	public class AssetRuntimeWorldGroup : RuntimeWorldGroup {
		public static string ParseId(ResourceIdentifier path)
			=> $"asset:{path}";

		public static async UniTask<AssetRuntimeWorldGroup> Load(ResourceIdentifier path, Action<float> progress, CancellationToken token) {
			progress?.Invoke(0f);

			var scene = Main.Instance.CoreAPI.AssetAPI.GetWorld(path);
			if (!scene.IsValid()) {
				var tmp = await Main.Instance.CoreAPI.AssetAPI.LoadWorld(path, LoadSceneMode.Additive)
					.AttachExternalCancellation(token);

				if (token.IsCancellationRequested) {
					Logger.LogWarning($"Loading scene from AssetBundle {path} was cancelled before completion.");
					await Main.Instance.CoreAPI.AssetAPI.UnloadWorld(path);
					return null;
				}

				scene = tmp;
			}

			if (!scene.IsValid()) {
				Logger.LogError($"Failed to load scene from Internal Assets: {path}");
				return null;
			}

			if (token.IsCancellationRequested) {
				Logger.LogWarning($"Loading scene from AssetBundle {path} was cancelled after loading.");
				await Main.Instance.CoreAPI.AssetAPI.UnloadWorld(path);
				return null;
			}

			progress?.Invoke(0.6f);

			var res = await WorldSetup.Prepare<AssetRuntimeWorldGroup>(
				scene,
				progress: p => progress?.Invoke(0.6f + p * 0.4f),
				token: token
			);

			if (!res.Success) {
				Logger.LogError($"Failed to prepare world from AssetBundle: {path} ({res.Error})");
				await Main.Instance.CoreAPI.AssetAPI.UnloadWorld(path);
				return null;
			}

			res.Runtime.Id = ParseId(path);

			progress?.Invoke(1f);

			return res.Runtime;
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id} Active={Active}]";
	}
}