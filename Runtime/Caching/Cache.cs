using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Caching {
	public class Cache : ICaching {
		private float _progress;
		public readonly string Url;
		public readonly string Hash;
		private CancellationTokenSource _cts;
		private readonly CacheManager _cache;
		private readonly CancellationToken _externalToken;

		public Cache(CacheManager cache, string url, string hash = null) {
			Url = url;
			_cache = cache;
			Hash = hash;
			_progress = 0f;
			_externalToken = CancellationToken.None;
		}

		public Cache(CacheManager cache, string url, string hash = null, CancellationToken token = default) {
			Url = url;
			_cache = cache;
			Hash = hash;
			_progress = 0f;
			_externalToken = token;
		}

		public UnityEvent<float> OnProgressChanged { get; } = new();

		private void SetProgress(float value, ulong size = 0) {
			_progress = value;
			SendEvent();
			OnProgressChanged.Invoke(_progress);
		}

		private void SendEvent()
			=> Main.Instance.CoreAPI
				.EventAPI
				.Emit("world_cache_download", Url, Hash, IsRunning, Progress);

		public bool IsRunning
			=> _cts is { IsCancellationRequested: false };

		public void Cancel() {
			if (_cts is not { IsCancellationRequested: false }) {
				Logger.Log("Cancellation requested but not running.");
				return;
			}

			_cts.Cancel();
			_cts.Dispose();
			_cts = null;
			SendEvent();
		}

		public async UniTask Wait() {
			if (!IsRunning) return;
			Logger.Log($"Waiting for download to complete: {Url} (Hash: {Hash})");
			await UniTask.WaitUntil(() => !IsRunning, cancellationToken: _externalToken);
		}

		public float Progress
			=> _progress;

		public async UniTask Start() {
			if (IsRunning) {
				Logger.Log("Already running.");
				return;
			}

			_cts = new CancellationTokenSource();

			// Combine internal and external tokens
			CancellationToken combinedToken = _cts.Token;
			if (_externalToken != CancellationToken.None) {
				var combined = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _externalToken);
				combinedToken = combined.Token;
			}

			_cache.Caching.Add(this);
			Logger.Log($"Starting download for {Url} (Hash: {Hash})");

			try {
				SetProgress(0f);

				// Download
				var path = await Main.NetworkAPI
					.DownloadFile(Url, hash: Hash, progress: SetProgress, token: combinedToken);

				if (combinedToken.IsCancellationRequested) {
					SetProgress(0f);
					throw new OperationCanceledException("Download was cancelled.");
				}

				if (string.IsNullOrEmpty(path)) {
					SetProgress(0f);
					throw new Exception("Download failed: No data received.");
				}

				// Save to cache
				Logger.Log($"Download completed for {Url} (Hash: {Hash}). Saving to cache...");
				Main.Instance.Cache.Save(Hash, path);

				SetProgress(1f);
			} catch (Exception e) {
				// Handle other exceptions
				Logger.LogError(e);
				SetProgress(0f);
			}

			_cache.Caching.Remove(this);
			_cts.Dispose();
			_cts = null;

			SendEvent();
		}
	}
}