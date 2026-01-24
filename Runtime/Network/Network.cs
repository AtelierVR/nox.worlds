using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Network;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Network {
	public class Network {
		/// <summary>
		/// Invoked when a world is fetched from the server
		/// </summary>
		private readonly UnityEvent<World> _fetchEvent = new();

		/// <summary>
		/// Invoked when a world is fetched from the server
		/// </summary>
		/// <param name="world"></param>
		private void InvokeFetch(World world) {
			if (world == null) return;
			_fetchEvent.Invoke(world);
			Main.Instance.CoreAPI.EventAPI.Emit("world_fetch", world);
		}

		/// <summary>
		/// Fetch a world from the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<World> Fetch(WorldIdentifier ide, string from = null, CancellationToken cancellationToken = default) {
			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot fetch world {ide}: no server address provided.");
				return null;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			var request = await RequestNode.To(address, $"/api/worlds/{ide}");
			if (request == null) {
				Logger.LogError($"Failed to find the server for world {ide}");
				return null;
			}

			await request.Send(cancellationToken);
			var response = await request.Node<World>(cancellationToken);
			if (response.HasError()) {
				Logger.LogError(new Exception($"Failed to fetch world {ide} from {address}", new Exception(response.Error.Message)));
				return null;
			}

			var world = response.Data;
			InvokeFetch(world);
			return world;
		}

		/// <summary>
		/// Search for worlds on the specified server
		/// </summary>
		/// <param name="data"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<SearchResponse> Search(SearchRequest data, string from = null, CancellationToken cancellationToken = default) {
			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress();
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError("Cannot search worlds: no server address provided.");
				return null;
			}

			var request = await RequestNode.To(address, $"/api/worlds{data}");
			if (request == null) {
				Logger.LogError("Failed to find the server for world search");
				return null;
			}

			await request.Send(cancellationToken);
			var response = await request.Node<SearchResponse>(cancellationToken);
			if (response.HasError()) {
				Logger.LogError(new Exception($"Failed to search worlds from {address}", new Exception(response.Error.Message)));
				return null;
			}

			var worlds = response.Data;
			worlds.Server = address;
			foreach (var world in worlds.Worlds)
				InvokeFetch(world);

			return worlds;
		}

		/// <summary>
		/// Create a new world on the specified server
		/// </summary>
		/// <param name="data"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<World> Create(CreateWorldRequest data, string from, CancellationToken cancellationToken = default) {
			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress();
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError("Cannot create world: no server address provided.");
				return null;
			}

			var request = await RequestNode.To(address, "/api/worlds");
			if (request == null) {
				Logger.LogError($"Failed to create request for world creation");
				return null;
			}

			request.SetBody(data.ToJson());
			request.method = RequestExtension.Method.PUT;

			await request.Send(cancellationToken);
			var response = await request.Node<World>(cancellationToken);

			if (response.HasError()) {
				Logger.LogError(new Exception($"Failed to create world {address}", new Exception(response.Error.Message)));
				return null;
			}

			var world = response.Data;
			InvokeFetch(world);
			return world;
		}

		/// <summary>
		/// Update an existing world on the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="form"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<World> Update(WorldIdentifier ide, UpdateWorldRequest form, string from = null, CancellationToken cancellationToken = default) {
			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot update world {ide}: no server address provided.");
				return null;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			var request = await RequestNode.To(address, $"/api/worlds/{ide}");
			if (request == null) {
				Logger.LogError($"Failed to create request for world {ide}");
				return null;
			}

			request.SetBody(form.ToJson());
			request.method = RequestExtension.Method.POST;
			await request.Send(cancellationToken);
			var response = await request.Node<World>(cancellationToken);
			if (response.HasError()) {
				Logger.LogError($"Failed to update world {ide} from {address}: {response.Error.Message}");
				return null;
			}

			var world = response.Data;
			InvokeFetch(world);
			return world;
		}

		/// <summary>
		/// Delete a world from the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<bool> Delete(WorldIdentifier ide, string from = null, CancellationToken cancellationToken = default) {
			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot delete world {ide}: no server address provided.");
				return false;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			var request = await RequestNode.To(address, $"/api/worlds/{ide}");
			if (request == null) {
				Logger.LogError($"Failed to find the server for world {ide}");
				return false;
			}

			request.method = RequestExtension.Method.DELETE;
			await request.Send(cancellationToken);
			if (!request.Ok()) {
				Logger.LogError($"Failed to delete world {ide} from {address}");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Search for assets in a world on the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="data"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<AssetSearchResponse> SearchAssets(WorldIdentifier ide, AssetSearchRequest data, string from = null, CancellationToken cancellationToken = default) {
			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot search assets for world {ide}: no server address provided.");
				return null;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			var request = await RequestNode.To(address, $"/api/worlds/{ide.ToString()}/assets{data}");
			if (request == null) {
				Logger.LogError($"Failed to create request for world {ide} assets");
				return null;
			}

			await request.Send(cancellationToken);
			var response = await request.Node<AssetSearchResponse>(cancellationToken);
			if (response.HasError()) {
				Logger.LogError(new Exception($"Failed to search assets for world {ide} from {address}", new Exception(response.Error.Message)));
				return null;
			}

			var assets = response.Data;
			assets.Identifier = ide;
			assets.Server = address;
			assets.Request = data;

			return assets;
		}

		/// <summary>
		/// Create a new asset in a world on the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="data"></param>
		/// <param name="from"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<WorldAsset> CreateAsset(WorldIdentifier ide, CreateAssetRequest data, string from = null, CancellationToken cancellationToken = default) {
			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot create asset for world {ide}: no server address provided.");
				return null;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			var request = await RequestNode.To(address, $"/api/worlds/{ide}/assets");
			if (request == null) {
				Logger.LogError($"Failed to create request for world {ide}");
				return null;
			}

			request.SetBody(data.ToJson());
			request.method = RequestExtension.Method.PUT;
			await request.Send(cancellationToken);
			var response = await request.Node<WorldAsset>(cancellationToken);
			if (response.HasError()) {
				Logger.LogError(new Exception($"Failed to create asset for world {ide} on {address}", new Exception(response.Error.Message)));
				return null;
			}

			return response.Data;
		}

		/// <summary>
		/// Upload a thumbnail for a world on the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="texture"></param>
		/// <param name="from"></param>
		/// <param name="onProgress"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public async UniTask<bool> UploadThumbnail(WorldIdentifier ide, Texture2D texture, string from = null, Action<float> onProgress = null, CancellationToken cancellationToken = default) {
			if (!texture) {
				Logger.LogError(new Exception($"Cannot upload thumbnail for world {ide}: texture is null."));
				return false;
			}

			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot upload thumbnail for world {ide}: no server address provided.");
				return false;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			// Convert texture to PNG byte array
			byte[] imageData;
			string fileHash;

			try {
				imageData = texture.EncodeToPNG();

				if (imageData == null || imageData.Length == 0)
					throw new Exception("Encoded image data is null or empty.");

				fileHash = Hashing.HashBytes(imageData);
			} catch (Exception ex) {
				Logger.LogError(new Exception($"Failed to encode texture for world {ide}", ex));
				return false;
			}

			var request = await RequestNode.To(address, $"/api/worlds/{ide.ToString()}/thumbnail");
			if (request == null) {
				Logger.LogError($"Failed to create request for world {ide}");
				return false;
			}

			request.method = RequestExtension.Method.POST;
			request.SetBody(new List<IMultipartFormSection>() {
				new MultipartFormFileSection(
					"file",
					imageData,
					"thumbnail.png",
					"image/png"
				)
			});

			if (!string.IsNullOrEmpty(fileHash))
				request.SetRequestHeader("x-file-hash", fileHash);

			// Send request with progress monitoring if callback provided
			if (onProgress != null)
				request.HandleUploadProgress((progress, _) => onProgress.Invoke(progress), cancellationToken);

			if (!await request.Send(cancellationToken)) {
				Logger.LogError($"Failed during sending request to upload thumbnail for avatar {ide} on {address}");
				return false;
			}

			if (!request.Ok()) {
				Logger.LogError($"Failed to upload thumbnail for avatar {ide} on {address}");
				return false;
			}

			return true;
		}

		/// <summary>
		/// Upload an asset file for a world on the specified server
		/// </summary>
		/// <param name="ide"></param>
		/// <param name="assetId"></param>
		/// <param name="filePath"></param>
		/// <param name="fileHash"></param>
		/// <param name="from"></param>
		/// <param name="onProgress"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
	public async UniTask<UploadAssetResponse> UploadAssetFile(WorldIdentifier ide, uint assetId, string filePath, string fileHash = null, string from = null, System.Action<float> onProgress = null, CancellationToken cancellationToken = default) {
		if (ide.IsLocal)
			ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

		var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
		if (string.IsNullOrEmpty(address)) {
			Logger.LogError($"Cannot upload asset file for world {ide}: no server address provided.");
			return null;
		}

		if (address == ide.Server)
			ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

		var request = await RequestNode.To(address, $"/api/worlds/{ide}/assets/{assetId}/file");
		if (request == null) {
			Logger.LogError($"Failed to create request for world {ide}");
			return null;
		}

		request.method = RequestExtension.Method.POST;
		if (onProgress != null)
			request.HandleUploadProgress((progress, _) => onProgress?.Invoke(progress), cancellationToken);

		request.SetBody(new List<IMultipartFormSection>() {
			new MultipartFormFileSection(
				"file",
				await File.ReadAllBytesAsync(filePath, cancellationToken),
				Path.GetFileName(filePath),
				"application/octet-stream"
			)
		});

		request.SetRequestHeader("Connection", "keep-alive");
		if (!string.IsNullOrEmpty(fileHash))
			request.SetRequestHeader("X-File-Hash", fileHash);

		if (!await request.Send(cancellationToken)) {
			Logger.LogError($"Failed during sending request to upload asset file for world {ide} on {address}");
			return null;
		}

		if (request.responseCode != 202) {
			Logger.LogError($"Status code {request.responseCode} received when uploading asset file for world {ide} on {address}, expected 202 Accepted.");
			return null;
		}

		var response = await request.Node<UploadAssetResponse>(cancellationToken);
		if (response.HasError()) {
			Logger.LogError($"Failed to upload asset file for world {ide} on {address}: {response.Error.Message}");
			return null;
		}

		return response.Data;
	}

	public async UniTask<AssetStatusResponse> GetAssetStatus(WorldIdentifier ide, uint assetId, string from = null, CancellationToken cancellationToken = default) {
		if (ide.IsLocal)
			ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

		var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
		if (string.IsNullOrEmpty(address)) {
			Logger.LogError($"Cannot get asset status for world {ide}: no server address provided.");
			return null;
		}

		if (address == ide.Server)
			ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

		var request = await RequestNode.To(address, $"/api/worlds/{ide}/assets/{assetId}/status");
		if (request == null) {
			Logger.LogError($"Failed to create request for world {ide}");
			return null;
		}

		await request.Send(cancellationToken);

		if (!request.Ok()) {
			Logger.LogError($"Failed to get asset status for world {ide} on {address}");
			return null;
		}

		var response = await request.Node<AssetStatusResponse>(cancellationToken);
		if (response.HasError()) {
			Logger.LogError($"Failed to get asset status for world {ide} on {address}: {response.Error.Message}");
			return null;
		}

		return response.Data;
	}

	/// <summary>
	/// Download an asset file for a world from the specified server
	/// </summary>
		/// <param name="ide"></param>
		/// <param name="assetId"></param>
		/// <param name="hash"></param>
		/// <param name="from"></param>
		/// <param name="onProgress"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async UniTask<string> DownloadAssetFile(WorldIdentifier ide, uint assetId, string hash = null, string from = null, Action<float> onProgress = null, CancellationToken cancellationToken = default) {
			var output = Path.Join(Application.temporaryCachePath, string.IsNullOrEmpty(hash) ? $"{ide}_{assetId}" : hash);

			if (ide.IsLocal)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, from);

			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress() ?? ide.Server;
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError($"Cannot download asset file for world {ide}: no server address provided.");
				return null;
			}

			if (address == ide.Server)
				ide = new WorldIdentifier(ide.Id, ide.Metadata, WorldIdentifier.LocalServer);

			var request = await RequestNode.To(address, $"/api/worlds/{ide}/assets/{assetId}/file");
			if (request == null) {
				Logger.LogError($"Failed to create request for world {ide}");
				return null;
			}

			var downloadHandler = new DownloadHandlerFile(output) { removeFileOnAbort = true };
			request.downloadHandler = downloadHandler; // Use DownloadHandlerFile to save directly to file

			// Send request with progress monitoring if callback provided
			if (onProgress != null)
				request.HandleDownloadProgress((progress, bytes) => onProgress?.Invoke(progress), cancellationToken);

			if (!await request.Send(cancellationToken) || !request.Ok()) {
				Logger.LogError($"Failed to download asset file for world {ide} from {address}");
				return null;
			}

			if (!File.Exists(output)) {
				Logger.LogError($"Downloaded asset file for world {ide} does not exist at expected path: {output}");
				return null;
			}

			if (!string.IsNullOrEmpty(hash) && Hashing.HashFile(output) != hash) {
				Logger.LogError($"Downloaded asset file for world {ide} does not match expected hash: {hash}");
				File.Delete(output); // Clean up if hash doesn't match
				return null;
			}

			Logger.LogDebug($"Successfully downloaded asset file for world {ide} to {output}");
			return output;
		}

		/// <summary>
		/// Key for the favorites table
		/// </summary>
		public const string FavoritesTableKey = "nox.worlds.favorites";

		/// <summary>
		/// Fetch favorite worlds from the specified server
		/// </summary>
		/// <param name="from"></param>
		/// <returns></returns>
		public async UniTask<WorldIdentifier[]> FetchFavorites(string from = null) {
			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress();
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError("Cannot fetch favorites: no server address provided.");
				return Array.Empty<WorldIdentifier>();
			}

			var entry = await Main.TableAPI.Get(FavoritesTableKey, address);
			if (entry != null)
				return entry
					.GetValue()
					.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => WorldIdentifier.From(s.Trim()))
					.Where(i => i.IsValid)
					.Distinct()
					.ToArray();

			Logger.LogError($"Failed to fetch favorites from {address}: entry not found.");
			return Array.Empty<WorldIdentifier>();
		}

		/// <summary>
		/// Add a world to favorites on the specified server
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="from"></param>
		/// <returns></returns>
		public async UniTask<WorldIdentifier[]> AddFavorite(WorldIdentifier identifier, string from = null)
			=> await AddFavorites(new[] { identifier }, from);

		/// <summary>
		/// Add worlds to favorites on the specified server
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="from"></param>
		/// <returns></returns>
		public async UniTask<WorldIdentifier[]> AddFavorites(WorldIdentifier[] identifier, string from = null) {
			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress();
			if (string.IsNullOrEmpty(address)) {
				Logger.LogError("Cannot add favorites: no server address provided.");
				return Array.Empty<WorldIdentifier>();
			}

			var e = await FetchFavorites(from);
			var newE = identifier
				.Concat(e)
				.Distinct()
				.ToArray();

			var entry = await Main.TableAPI.Set(
				"nox.avatars.favorites",
				string.Join(",", newE.Select(i => i.ToString())),
				address
			);

			if (entry != null)
				return newE;

			Logger.LogError($"Failed to add favorites on {address}: entry not found.");
			return e;
		}

		/// <summary>
		/// Remove a world from favorites on the specified server
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="from"></param>
		/// <returns></returns>
		public async UniTask<WorldIdentifier[]> RemoveFavorite(WorldIdentifier identifier, string from = null)
			=> await RemoveFavorites(new[] { identifier }, from);

		/// <summary>
		/// Remove worlds from favorites on the specified server
		/// </summary>
		/// <param name="identifier"></param>
		/// <param name="from"></param>
		/// <returns></returns>
		public async UniTask<WorldIdentifier[]> RemoveFavorites(WorldIdentifier[] identifier, string from = null) {
			var address = from ?? Main.UserAPI?.GetCurrent()?.GetServerAddress();

			if (string.IsNullOrEmpty(address)) {
				Logger.LogError("Cannot remove favorites: no server address provided.");
				return Array.Empty<WorldIdentifier>();
			}

			var e = await FetchFavorites(from);
			var newE = e
				.Where(i => !identifier.Contains(i))
				.ToArray();

			var entry = await Main.TableAPI.Set(
				FavoritesTableKey,
				string.Join(",", newE.Select(i => i.ToString())),
				address
			);

			if (entry != null)
				return newE;

			Logger.LogError($"Failed to add favorites on {address}: entry not found.");
			return e;
		}
	}
}