using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Nox.Worlds {
	/// <summary>
	/// Interface for the World API, providing methods to load worlds from various sources.
	/// </summary>
	public interface IWorldAPI {
		/// <summary>
		/// Loads a world from the given path in the filesystem.
		/// Return null if the world is not found at the specified path.
		/// </summary>
		/// <param name="path">Path to the world file.</param>
		/// <param name="progress">Progress callback to report loading progress.</param>
		/// <param name="token">Cancellation token to cancel the loading operation.</param>
		/// <returns>Returns a <see cref="IRuntimeWorld"/> instance representing the loaded world.</returns>
		public UniTask<IRuntimeWorld> LoadFromPath(string path, Action<float> progress = null, CancellationToken token = default);

		/// <summary>
		/// Loads a world from the given path in the assets.
		/// Return null if the world is not found in the assets.
		/// </summary>
		/// <param name="path">Path to the world asset.</param>
		/// <param name="progress">Progress callback to report loading progress.</param>
		/// <param name="token">Cancellation token to cancel the loading operation.</param>
		/// <returns>Returns a <see cref="IRuntimeWorld"/> instance representing the loaded world.</returns>
		public UniTask<IRuntimeWorld> LoadFromAssets(ResourceIdentifier path, Action<float> progress = null, CancellationToken token = default);

		/// <summary>
		/// Loads a world from the cache using its hash.
		/// If the world is not found in the cache, it will return null.
		/// </summary>
		/// <param name="hash">Hash of the world to load.</param>
		/// <param name="progress">Progress callback to report loading progress.</param>
		/// <param name="token">Cancellation token to cancel the loading operation.</param>
		/// <returns>Returns a <see cref="IRuntimeWorld"/> instance representing the loaded world, or null if not found.</returns>
		public UniTask<IRuntimeWorld> LoadFromCache(string hash, Action<float> progress = null, CancellationToken token = default);

		/// <summary>
		/// Gets the currently active world.
		/// </summary>
		/// <returns></returns>
		public IRuntimeWorld GetCurrent();

		/// <summary>
		/// Sets the current world by its ID.
		/// If the world with the given ID does not exist or cannot be set as current,
		/// If the world is successfully or already set as current, it will return true.
		/// </summary>
		/// <param name="id">Identifier of the world to set as current.</param>
		/// <returns>Returns true if the world was successfully set as current, false otherwise.</returns>
		public bool SetCurrent(string id);

		/// <summary>
		/// Fetches a world by its identifier.
		/// </summary>
		/// <param name="identifier">Identifier of the world to fetch.</param>
		/// <param name="from">Where is the world fetched from, if null it will use the current server.</param>
		/// <returns></returns>
		public UniTask<IWorld> Fetch(Identifier identifier);

		/// <summary>
		/// Searches for worlds based on the provided search request.
		/// </summary>
		/// <param name="data">Search request containing the search parameters.</param>
		/// <param name="from">Server where the search is performed, if null it will use the current server.</param>
		/// <returns></returns>
		public UniTask<ISearchResponse> Search(ISearchRequest data);

		/// <summary>
		/// Creates a new world based on the provided creation request.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="server"></param>
		/// <returns></returns>
		public UniTask<IWorld> Create(ICreateWorldRequest data, string server);

		/// <summary>
		/// Updates an existing world with the provided update request.
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="form"></param>
		/// <returns></returns>
		public UniTask<IWorld> Update(Identifier identifier, IUpdateWorldRequest form);

		/// <summary>
		/// Deletes a world by its identifier.
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="from"></param>
		/// <returns></returns>
		public UniTask<bool> Delete(Identifier identifier);

		/// <summary>
		/// Searches for assets associated with a world.
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public UniTask<IAssetSearchResponse> SearchAssets(Identifier identifier, IAssetSearchRequest data);

		/// <summary>
		/// Uploads a thumbnail for a world asset.
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="texture"></param>
		/// <param name="onProgress"></param>
		/// <returns></returns>
		public UniTask<bool> UploadThumbnail(Identifier identifier, Texture2D texture, Action<float> onProgress = null);

		/// <summary>
		/// Uploads a file for a world asset.
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="assetId"></param>
		/// <param name="fileName"></param>
		/// <param name="fileHash"></param>
		/// <param name="onProgress"></param>
		/// <returns></returns>
		public UniTask<IUploadAssetResponse> UploadAssetFile(Identifier identifier, uint assetId, string fileName, string fileHash = null, Action<float> onProgress = null);

		/// <summary>
		/// Creates a new asset for a world.
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public UniTask<IWorldAsset> CreateAsset(Identifier identifier, ICreateAssetRequest data);

		/// <summary>
		/// Downloads a file for a world asset.
		/// </summary>
		/// <param name="url">URL of the file to download.</param>
		/// <param name="hash">Expected hash of the file, used for integrity verification.</param>
		/// <param name="progress"></param>
		/// <param name="token">Cancellation token to cancel the download operation.</param>
		/// <returns></returns>
		public ICaching DownloadToCache(string url, string hash = null, UnityAction<float> progress = null, CancellationToken token = default);

		/// <summary>
		/// Gets an existing download from the cache.
		/// </summary>
		/// <param name="url">URL of the file being downloaded.</param>
		/// <param name="hash">Hash of the file being downloaded.</param>
		/// <returns>Returns the ICaching instance if a download is in progress, null otherwise.</returns>
		public ICaching GetDownload(string url, string hash);

		/// <summary>
		/// Removes an asset from the cache using its hash.
		/// </summary>
		/// <param name="hash"></param>
		public void RemoveFromCache(string hash);

		/// <summary>
		/// Checks if an asset with the given hash exists in the cache.
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		public bool HasInCache(string hash);

		/// <summary>
		/// Adds a world to the favorites list.
		/// </summary>
		/// <param name="identifier">Identifier of the world to add to favorites.</param>
		/// <returns></returns>
		public UniTask<IFavorites> AddFavorite(Identifier identifier);

		/// <summary>
		/// Removes a world from the favorites list.
		/// </summary>
		/// <param name="identifier">Identifier of the world to remove from favorites.</param>
		/// <returns></returns>
		public UniTask<IFavorites> RemoveFavorite(Identifier identifier);

		/// <summary>
		/// Gets the list of favorite world identifiers.
		/// </summary>
		/// <returns></returns>
		public UniTask<IFavorites> GetFavorites();
	}
}