using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.Worlds.Runtime.Network;
using Nox.Worlds.Pipeline;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using UnityEditor;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Editor {
	// Actions partial class - handles attach, publish, and upload operations
	public partial class PublisherInstance {
		private async UniTask CheckLoginStatus() {
			var user = Main.UserAPI.GetCurrent();
			var isLoggedIn = user != null && !string.IsNullOrEmpty(user.GetServerAddress());

			if (!isLoggedIn) {
				UpdateDisplayState(DisplayState.NotLogged);
				return;
			}

			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) {
				UpdateDisplayState(DisplayState.NoDescriptor);
				return;
			}

			if (_attachServerField != null)
				_attachServerField.SetValueWithoutNotify(user.GetServerAddress());

			if (descriptor.publishId > 0 && !string.IsNullOrEmpty(descriptor.publishServer)) {
				await AttachWorldAsync(descriptor.publishServer, descriptor.publishId, false);
			}
			else {
				UpdateDisplayState(DisplayState.NotAttached);
			}
		}

		private async UniTask OnAttachAsync() {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) {
				UpdateDisplayState(DisplayState.NoDescriptor);
				return;
			}

			if (!uint.TryParse(_attachIdField?.value ?? "", out var id))
				id = 0;

			var server = _attachServerField?.value;
			if (string.IsNullOrEmpty(server)) {
				var user = Main.UserAPI.GetCurrent();
				server = user?.GetServerAddress();
			}

			if (string.IsNullOrEmpty(server)) {
				Logger.OpenDialog("Error", "No server address available.", "Ok");
				return;
			}

			await AttachWorldAsync(server, id, true);
		}

		private async UniTask<Network.World> AttachWorldAsync(string server, uint id, bool createIfNotFound) {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) {
				UpdateDisplayState(DisplayState.NoDescriptor);
				return null;
			}

			UpdateDisplayState(DisplayState.Loading);

			Network.World world = null;
			if (id > 0) {
				Logger.LogDebug($"Attempting to attach world {id}");
				world = await Main.Instance.Network.Fetch(new WorldIdentifier(id, null, server), server);
			}

			if (world == null && createIfNotFound) {
				Logger.LogDebug($"World {id} not found, attempting to create new world.");
				world = await Main.Instance.Network.Create(new CreateWorldRequest { Id = id }, server);
			}

			if (world != null) {
				Logger.LogDebug($"Attaching world {world}");
				await UniTask.Delay(1000); // Small delay to improve UX

				var user = Main.UserAPI.GetCurrent();
				var isContributor = user != null && user.ToIdentifier().Equals(world.Owner);

				if (!isContributor) {
					Logger.OpenDialog("Error", "You are not a contributor of this world.", "Ok");
					Logger.LogError("You are not a contributor of this world.");
					UpdateDisplayState(DisplayState.NotAttached);
					return null;
				}
			}

			if (world == null) {
				if (createIfNotFound) {
					Logger.OpenDialog("Error", "Failed to create or find world.", "Ok");
					Logger.LogError("Failed to create or find world.");
				}

				UpdateDisplayState(DisplayState.NotAttached);
				return null;
			}

			descriptor.publishId = world.Id;
			descriptor.publishServer = world.Server;
			EditorUtility.SetDirty(descriptor);
			_world = world;
			UpdateWorldUI();
			UpdateDisplayState(DisplayState.Attached);
			return world;
		}

		private async UniTask OnRefreshInfoAsync() {
			if (_world == null) return;
			await AttachWorldAsync(_world.Server, _world.Id, false);
		}

		private async UniTask OnUpdateInfoAsync() {
			if (_world == null) {
				Logger.OpenDialog("Error", "No world attached.", "Ok");
				return;
			}

			var name = _infoNameField?.value ?? "";
			var description = _infoDescriptionField?.value ?? "";

			var success = await Main.Instance.Network.Update(
				_world.Id.ToString(),
				new UpdateWorldRequest {
					Title = name,
					Description = description
				},
				_world.Server
			);

			if (success != null) {
				_world = success;
				UpdateWorldUI();
			}
			else {
				Logger.OpenDialog("Error", "Failed to update world information.", "Ok");
			}
		}

		private async UniTask OnPublishAsync() {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) {
				Logger.OpenDialog("Error", "No descriptor found.", "Ok");
				return;
			}

			if (_world == null) {
				Logger.OpenDialog("Error", "No world attached. Please attach a world before publishing.", "Ok");
				return;
			}

			var target = descriptor.target;
			if (target == Platform.None)
				target = PlatformExtensions.CurrentPlatform;

			if (!target.IsSupported()) {
				Logger.OpenDialog("Error", $"{target.GetPlatformName()} is not supported.", "Ok");
				return;
			}

			var version = (ushort)descriptor.publishVersion;
			if (version == 0) {
				Logger.OpenDialog("Error", "Asset version cannot be 0.", "Ok");
				return;
			}

			ShowBuildProgress(0f, "Verifying world...");
			_world = await Main.Instance.Network.Fetch(_world.Id.ToString(), _world.Server);
			if (_world == null) {
				HideBuildProgress();
				Logger.OpenDialog("Error", "Failed to verify world.", "Ok");
				return;
			}

			var tempBuildPath = CreateTempBuildPath();
			var config = Config.Load();
			try {
				// Check if asset already exists BEFORE building
				ShowBuildProgress(0.1f, "Checking existing assets...");

				var search = await Main.Instance.Network.SearchAssets(
					_world.Id.ToString(),
					new AssetSearchRequest {
						Versions = new[] { version },
						Platforms = new[] { target.GetPlatformName() },
						Engines = new[] { Constants.CurrentEngine.GetEngineName() },
						ShowEmpty = true,
						Limit = 1,
						Offset = 0
					},
					_world.Server
				);

				var existingAsset = search?.Assets?.FirstOrDefault();
				var assetAlreadyExists = existingAsset != null && !existingAsset.IsEmpty;
				var strictVersionChecking = config.Get("sdk.strict_version", true);
				var autoVersion = config.Get("sdk.auto_version", true);

				if (assetAlreadyExists) {
					// Auto-increment has priority: if enabled, increment instead of blocking or overwriting
					if (autoVersion) {
						// Auto-increment: use version+1 instead of overwriting
						version = (ushort)(version + 1);
						descriptor.publishVersion = version;
						EditorUtility.SetDirty(descriptor);
						if (_assetVersionField != null)
							_assetVersionField.SetValueWithoutNotify(version);

						Logger.Log($"Asset version {version - 1} already exists. Auto-incremented to version {version}");
					}
					else if (strictVersionChecking) {
						// Strict mode without auto-increment: block the upload
						HideBuildProgress();
						ShowResultDialog(false, $"Asset version {version} already exists for {target.GetPlatformName()}.\n\nPlease increment the version number, enable 'Auto increment version', or disable 'Strict version checking' to overwrite.");
						Logger.LogError($"Asset version {version} already exists. Strict version checking is enabled.");
						return;
					}
					// else: overwrite existing asset (strict is off, auto is off)
				}

				descriptor.publishVersion = version;
				EditorUtility.SetDirty(descriptor);
				Logger.Log($"Saved publish version {version} to descriptor before build");

				// Build the world
				ShowBuildProgress(0.2f, "Building world...");
				var buildData = new BuildData {
					Descriptor = descriptor,
					Target = target,
					OutputPath = tempBuildPath,
					ShowDialog = false,
					ProgressCallback = (progress, status) => ShowBuildProgress(0.2f + (progress * 0.5f), status)
				};

				var buildResult = await Builder.Build(buildData);

				if (buildResult.IsFailed) {
					HideBuildProgress();
					ShowResultDialog(false, $"Build failed: {buildResult.Message}");
					return;
				}

				var filePath = buildResult.Output;
				if (!File.Exists(filePath)) {
					HideBuildProgress();
					ShowResultDialog(false, "Built file not found: " + filePath);
					return;
				}

				ShowBuildProgress(0.75f, "Preparing file for upload...");
				var sizeMb = new FileInfo(filePath).Length / (1024f * 1024f);

				ShowBuildProgress(0.77f, $"Calculating file hash for {sizeMb:F1} MB file...");

				// Calculate file hash for validation
				var fileHash = Hashing.HashFile(filePath);

				Logger.Log($"File hash: {fileHash}");
				ShowBuildProgress(0.78f, $"Preparing asset entry...");

				// Search for asset again with the potentially updated version
				search = await Main.Instance.Network.SearchAssets(
					_world.Id.ToString(),
					new AssetSearchRequest {
						Versions = new[] { version },
						Platforms = new[] { target.GetPlatformName() },
						Engines = new[] { Constants.CurrentEngine.GetEngineName() },
						ShowEmpty = true,
						Limit = 1,
						Offset = 0
					},
					_world.Server
				);

				var asset = search?.Assets?.FirstOrDefault();

				if (asset == null) {
					ShowBuildProgress(0.54f, "Creating asset entry...");
					asset = await Main.Instance.Network.CreateAsset(
						new WorldIdentifier(_world.Id, null, _world.Server),
						new CreateAssetRequest {
							Version = version,
							Engine = Constants.CurrentEngine.GetEngineName(),
							Platform = target.GetPlatformName()
						},
						_world.Server
					);
				}

				if (asset == null) {
					HideBuildProgress();
					ShowResultDialog(false, "Failed to create or find asset entry.");
					return;
				}

				// Upload the asset
				ShowBuildProgress(0.8f, $"Uploading {sizeMb:F1} MB file...");
				var uploadResponse = await Main.Instance.Network.UploadAssetFile(
					new WorldIdentifier(_world.Id, null, _world.Server),
					asset.Id,
					filePath,
					fileHash,
					_world.Server,
					onProgress: progress =>
					{
						var sizeUploaded = progress * sizeMb;
						ShowBuildProgress(0.8f + progress * 0.1f, $"Uploading... {sizeUploaded:F2} MB / {sizeMb:F2} MB - {progress * 100:F0}%");
					}
				);

				if (uploadResponse == null) {
					HideBuildProgress();
					ShowResultDialog(false, "Failed to upload world file.");
					return;
				}

				Logger.Log($"Upload queued: {uploadResponse.Message} (Status: {uploadResponse.Status}, Queue position: {uploadResponse.QueuePosition})");

				// Poll asset status until processing is complete
				ShowBuildProgress(0.9f, $"Processing asset... (Queue position: {uploadResponse.QueuePosition})");

				const int maxAttempts = 300; // 5 minutes max with 1 second interval
				var attempt = 0;
				var isProcessing = true;
				var nextTryAt = uploadResponse.NextTryAt;

				while (isProcessing && attempt < maxAttempts) {
					// Calculate delay based on NextTryAt if available
					var delayMs = 1000; // Default 1 second
					if (nextTryAt > DateTime.UtcNow) {
						var timeUntilNextTry = (nextTryAt - DateTime.UtcNow).TotalMilliseconds;
						delayMs = (int)Math.Min(Math.Max(timeUntilNextTry, 100), 30000); // Between 100ms and 30s
						Logger.LogDebug($"Waiting {delayMs}ms until next status check (NextTryAt: {nextTryAt:u})");
					}

					await UniTask.Delay(delayMs);
					attempt++;

					var status = await Main.Instance.Network.GetAssetStatus(
						new WorldIdentifier(_world.Id, null, _world.Server),
						asset.Id,
						_world.Server
					);

					if (status == null) {
						Logger.LogWarning($"Failed to get asset status (attempt {attempt})");
						continue;
					}

					// Update nextTryAt from the status response
					if (status.NextTryAt > DateTime.UtcNow)
						nextTryAt = status.NextTryAt;

					Logger.LogDebug($"Asset status: {status.Status}, progress: {status.Progress}%, queue: {status.QueuePosition}");
					var processingProgress = 0.9f + (status.Progress / 100f) * 0.1f;

					switch (status.Status) {
						case AssetStatusType.PENDING:
							ShowBuildProgress(processingProgress, $"Waiting in queue... (Position: {status.QueuePosition})");
							break;
						case AssetStatusType.PROCESSING:
							ShowBuildProgress(processingProgress, $"Processing asset... {status.Progress}%");
							break;
						case AssetStatusType.COMPLETED:
							isProcessing = false;
							Logger.Log($"Asset processing completed. Hash: {status.Hash}, Size: {(status.Size >= 0 ? $"{status.Size} bytes" : "unknown")}");
							break;
						case AssetStatusType.FAILED:
							HideBuildProgress();
							ShowResultDialog(false, $"Asset processing failed: {status.Error ?? "Unknown error"}");
							return;
						default:
							Logger.LogWarning($"Unknown asset status: {status.Status}");
							break;
					}
				}

				if (attempt >= maxAttempts) {
					HideBuildProgress();
					ShowResultDialog(false, "Asset processing timed out. Please check the server status.");
					return;
				}

				// NOTE: No need to save descriptor.publishVersion here anymore!
				// It was already saved BEFORE the build (which destroys and recreates the descriptor)
				// This avoids the "destroyed object" error that occurred here

				HideBuildProgress();
				ShowResultDialog(true, $"World published successfully!\nVersion: {version}\nPlatform: {target.GetPlatformName()}");
			} catch (Exception ex) {
				Logger.LogError($"Publish failed: {ex.Message}");
				HideBuildProgress();
				ShowResultDialog(false, $"Publish failed: {ex.Message}");
			}
			finally {
				// Clean up temp build
				if (Directory.Exists(tempBuildPath)) {
					try {
						Directory.Delete(tempBuildPath, true);
					} catch (Exception ex) {
						Logger.LogWarning($"Failed to clean up temp build directory: {ex.Message}");
					}
				}
			}
		}

		private async UniTask OnDetectVersionAsync() {
			if (_world == null) {
				Logger.OpenDialog("Error", "No world attached.", "Ok");
				return;
			}

			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) return;

			var target = descriptor.target;
			if (target == Platform.None)
				target = PlatformExtensions.CurrentPlatform;

			ShowBuildProgress(0f, "Detecting latest version...");
			var search = await Main.Instance.Network.SearchAssets(
				_world.Id.ToString(),
				new AssetSearchRequest {
					Platforms = new[] { target.GetPlatformName() },
					Engines = new[] { Constants.CurrentEngine.GetEngineName() },
					ShowEmpty = false,
					Limit = 1,
					Offset = 0
				},
				_world.Server
			);
			HideBuildProgress();

			if (search?.Assets != null && search.Assets.Length > 0) {
				var latestVersion = search.Assets[0].Version;
				descriptor.publishVersion = (ushort)(latestVersion + 1);
				EditorUtility.SetDirty(descriptor);
				if (_assetVersionField != null)
					_assetVersionField.SetValueWithoutNotify(descriptor.publishVersion);
				Logger.Log($"Detected latest version: {latestVersion}. Set to {descriptor.publishVersion}.");
			}
			else {
				descriptor.publishVersion = 1;
				EditorUtility.SetDirty(descriptor);
				if (_assetVersionField != null)
					_assetVersionField.SetValueWithoutNotify(1);
				Logger.Log("No existing versions found. Set to 1.");
			}
		}

		private string CreateTempBuildPath() {
			var tempPath = Path.Combine(Path.GetTempPath(), "NoxWorldBuilds", Guid.NewGuid().ToString());
			if (!Directory.Exists(tempPath))
				Directory.CreateDirectory(tempPath);
			return tempPath;
		}
	}
}