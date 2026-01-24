using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using System.Threading;

namespace Nox.Worlds.Runtime.Caching {
	public class CacheManager {
		public CacheManager() {
			_watcher = new FileSystemWatcher(CachePath) {
				NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
				EnableRaisingEvents   = true,
				IncludeSubdirectories = false
			};
			_watcher.Changed += OnFile;
			_watcher.Created += OnFile;
			_watcher.Deleted += OnFile;
			_watcher.Renamed += OnRenameFile;
		}

		private void OnFile(object sender, FileSystemEventArgs e)
			=> OnFileAsync(e).Forget();

		private async UniTask OnFileAsync(FileSystemEventArgs e) {
			await UniTask.SwitchToMainThread();
			var path = Path.GetRelativePath(CachePath, e.FullPath);
			if (e.ChangeType == WatcherChangeTypes.Created)
				Main.Instance.CoreAPI.EventAPI.Emit("world_cache_added", path);
			else if (e.ChangeType == WatcherChangeTypes.Deleted)
				Main.Instance.CoreAPI.EventAPI.Emit("world_cache_removed", path);
		}

		private void OnRenameFile(object sender, RenamedEventArgs e)
			=> OnRenameFileAsync(e).Forget();

		private async UniTask OnRenameFileAsync(RenamedEventArgs e) {
			await UniTask.SwitchToMainThread();
			var oldPath = Path.GetRelativePath(CachePath, e.OldFullPath);
			var newPath = Path.GetRelativePath(CachePath, e.FullPath);
			Main.Instance.CoreAPI.EventAPI.Emit("world_cache_removed", oldPath);
			Main.Instance.CoreAPI.EventAPI.Emit("world_cache_added", newPath);
		}

		private readonly  FileSystemWatcher _watcher;
		internal readonly List<Cache>     Caching = new();


		public Cache GetDownload(string url, string hash = null)
			=> Caching.Find(c => hash != null && c.Hash == hash || c.Url == url);

		public Cache AddDownload(string url, string hash = null, CancellationToken token = default) {
			var existing = GetDownload(url, hash);
			return existing ?? new Cache(this, url, hash, token);
		}

		public static string CachePath {
			get {
				var dir = Path.Combine(Constants.CachePath, "worlds");
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				return dir;
			}
		}

		public string Get(string hash)
			=> Path.Combine(CachePath, hash);

		public bool Has(string hash)
			=> File.Exists(Get(hash));

		public void Clear() {
			if (Directory.Exists(CachePath)) Directory.Delete(CachePath, true);
		}

		public void Clear(string hash) {
			if (Has(hash)) File.Delete(Get(hash));
		}

		public async UniTask Save(string hash, byte[] data)
			=> await File.WriteAllBytesAsync(Get(hash), data);

		public void Save(string hash, string path)
			=> File.Copy(path, Get(hash));

		public void Dispose() {
			foreach (var caching in Caching)
				caching.Cancel();
			_watcher.Dispose();
			Caching.Clear();
		}
	}
}