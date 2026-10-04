using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.Worlds.Runtime.Network;
using Nox.Worlds.Pipeline;
using Nox.CCK.Convertors;
using Nox.CCK.Network.Assets;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Network.Assets;
using UnityEditor;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Editor {
	// Actions partial class - handles attach, publish, and upload operations
	public partial class PublisherInstance {
		private async UniTask CheckLoginStatus() {
			var user       = Main.UserAPI.Current;
			var isLoggedIn = user != null && !string.IsNullOrEmpty(user.Server);

			if (!isLoggedIn) {
				UpdateDisplayState(DisplayState.NotLogged);
				return;
			}

			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) {
				UpdateDisplayState(DisplayState.NoDescriptor);
				return;
			}

			_attachServerField?.SetValueWithoutNotify(user.Server);

			if (descriptor.publishId > 0 && !string.IsNullOrEmpty(descriptor.publishServer)) {
				await AttachWorldAsync(descriptor.publishServer, descriptor.publishId, false);
			} else {
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
				var user = Main.UserAPI.Current;
				server = user?.Server;
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
				world = await Main.Instance.Network.Fetch(new Identifier(WorldIdentifierExtensions.WorldType, id, null, server));
			}

			if (world == null && createIfNotFound) {
				Logger.LogDebug($"World {id} not found, attempting to create new world.");
				world = await Main.Instance.Network.Create(new WorldCreateRequest { Id = id }, server);
			}

			if (world != null) {
				Logger.LogDebug($"Attaching world {world}");
				await UniTask.Delay(1000); // Small delay to improve UX

				var user          = Main.UserAPI.Current;
				var isContributor = world.IsContributor(user.Identifier);

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

			// Une recharge de scène pendant les attentes détruit le descriptor capturé au début : on
			// récupère celui qui est vivant avant d'y écrire (sinon MissingReferenceException).
			var target = WorldDescriptorHelper.Live(descriptor);

			if (target) {
				target.publishId     = world.Id;
				target.publishServer = world.Server;
				EditorUtility.SetDirty(target);
			}

			_world = world;
			UpdateWorldUI();
			UpdateDisplayState(DisplayState.Attached);
			return world;
		}

		private async UniTask OnRefreshInfoAsync() {
			if (_world == null)
				return;
			await AttachWorldAsync(_world.Server, _world.Id, false);
		}

		private async UniTask OnUpdateInfoAsync() {
			if (_world == null) {
				Logger.OpenDialog("Error", "No world attached.", "Ok");
				return;
			}

			var name        = _infoNameField?.value ?? "";
			var description = _infoDescriptionField?.value ?? "";

			var success = await Main.Instance.Network.Update(
				_world.Identifier,
				new WorldUpdateRequest {
					Title       = name.ToTranslated(),
					Description = description.ToTranslated()
				}
			);

			if (success != null) {
				_world = success;
				UpdateWorldUI();
			} else {
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

			var targets = descriptor.Targets;
			if (targets.Length == 0)
				targets = new[] { PlatformExtensions.CurrentPlatform };

			foreach (var platform in targets)
				if (!platform.IsSupported()) {
					Logger.OpenDialog("Error", $"{platform.GetPlatformName()} is not supported.", "Ok");
					return;
				}

			var version = (ushort)descriptor.publishVersion;
			if (version == 0) {
				Logger.OpenDialog("Error", "Asset version cannot be 0.", "Ok");
				return;
			}

			ShowBuildProgress(0f, "Verifying world...");
			_world = await Main.Instance.Network.Fetch(_world.Identifier);
			if (_world == null) {
				HideBuildProgress();
				Logger.OpenDialog("Error", "Failed to verify world.", "Ok");
				return;
			}

			var assets = Main.AssetsAPI;
			if (assets == null) {
				HideBuildProgress();
				Logger.OpenDialog("Error", "The asset pipeline is not available.", "Ok");
				return;
			}

			var tempBuildPath = CreateTempBuildPath();
			var config        = Config.Load();
			try {
				// A release is named after the version it publishes.
				ShowBuildProgress(0.1f, "Checking existing releases...");

				var server   = _world.Server;
				var assetRef = _world.Id.ToString();
				var engine   = Constants.CurrentEngine;
				var release  = await FetchRelease(assets, server, assetRef, version);

				var strictVersionChecking = config.Get("sdk.strict_version", true);
				var autoVersion           = config.Get("sdk.auto_version", true);

				if (release != null && autoVersion) {
					// Auto-increment has priority: publish under the next free version instead.
					var previous = version;

					while (release != null) {
						version++;
						release = await FetchRelease(assets, server, assetRef, version);
					}

					Logger.Log($"Asset version {previous} already exists. Auto-incremented to version {version}");
				} else if (release != null && strictVersionChecking) {
					// Strict mode without auto-increment: block the upload
					HideBuildProgress();
					ShowResultDialog(false, $"Asset version {version} already exists.\n\nPlease increment the version number, enable 'Auto increment version', or disable 'Strict version checking' to overwrite.");
					Logger.LogError($"Asset version {version} already exists. Strict version checking is enabled.");
					return;
				}

				// Le descriptor a pu être détruit par une recharge de scène pendant les attentes ci-dessus
				var live = WorldDescriptorHelper.Live(descriptor);

				if (live) {
					live.publishVersion = version;
					EditorUtility.SetDirty(live);

					// Le bundle est construit par le Scriptable Build Pipeline, qui refuse de tourner tant
					// qu'une scène chargée a des modifications non écrites : on écrit la scène maintenant.
					if (!WorldDescriptorHelper.SaveScene(live)) {
						HideBuildProgress();
						ShowResultDialog(false, "The world scene has unsaved changes and could not be saved. Save it and publish again.");
						return;
					}
				}

				Logger.Log($"Saved publish version {version} to descriptor before build");

				// Build the world: one bundle per targeted platform
				ShowBuildProgress(0.2f, $"Building world for {targets.Length} platform(s)...");
				var buildData = new BuildData {
					Descriptor       = descriptor,
					OutputPath       = tempBuildPath,
					ProgressCallback = (progress, status) => ShowBuildProgress(0.2f + (progress * 0.5f), status)
				};

				var buildResult = await Builder.Build(buildData);

				// Le build a pu recharger les scènes : on repart du descriptor vivant avant d'y écrire
				WorldDescriptorHelper.Rebind();
				descriptor = WorldDescriptorHelper.CurrentWorld ?? descriptor;

				if (buildResult.IsFailed) {
					HideBuildProgress();
					ShowResultDialog(false, $"Build failed: {buildResult.Message}");
					return;
				}

				// Le build rapporte chaque variant avec sa plateforme : plus d'indexation par position
				var bundles = buildResult.Outputs.ToDictionary(output => output.Platform, output => output.Path);

				if (bundles.Count != targets.Length) {
					HideBuildProgress();
					ShowResultDialog(false, $"Expected {targets.Length} bundle(s), got {bundles.Count}.");
					return;
				}

				foreach (var (platform, file) in bundles)
					if (!File.Exists(file)) {
						HideBuildProgress();
						ShowResultDialog(false, $"Built file not found for {platform.GetPlatformName()}: {file}");
						return;
					}

				ShowBuildProgress(0.78f, "Preparing release...");

				if (release == null)
					release = await assets.CreateRelease(
						server,
						Endpoint,
						assetRef,
						new AssetReleaseRequest {
							Name    = version.ToString(),
							Channel = AssetChannel.Stable
						}
					);

				if (release == null) {
					HideBuildProgress();
					ShowResultDialog(false, $"Failed to create release {version}.");
					return;
				}

				var span      = 0.15f / targets.Length;
				var published = new string[targets.Length];

				for (var i = 0; i < targets.Length; i++) {
					if (!bundles.TryGetValue(targets[i], out var bundlePath)) {
						HideBuildProgress();
						ShowResultDialog(false, $"No bundle was built for {targets[i].GetPlatformName()}.");
						return;
					}

					var error = await PublishVariant(
						assets,
						server,
						assetRef,
						release,
						targets[i],
						bundlePath,
						engine,
						0.8f + (span * i),
						span
					);

					if (error != null) {
						HideBuildProgress();
						ShowResultDialog(false, error);
						return;
					}

					published[i] = targets[i].GetPlatformName();
				}

				// La version est réappliquée sur le descriptor vivant : si le build a rechargé la scène
				// depuis le disque, la valeur en mémoire peut être celle d'avant le build.
				if (descriptor) {
					descriptor.publishVersion = version;
					EditorUtility.SetDirty(descriptor);
				}

				HideBuildProgress();
				ShowResultDialog(true, $"World published successfully!\nVersion: {version}\nPlatforms: {string.Join(", ", published)}");
			} catch (Exception ex) {
				Logger.LogError($"Publish failed: {ex.Message}");
				HideBuildProgress();
				ShowResultDialog(false, $"Publish failed: {ex.Message}");
			} finally {
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

		/// <summary>
		/// Uploads <paramref name="filePath"/> as the <paramref name="platform"/> variant of the
		/// release, replacing the variant already published for that platform. Returns <c>null</c> on
		/// success, or the error to report.
		/// </summary>
		private async UniTask<string> PublishVariant(
			IAssetsAPI assets,
			string server,
			string assetRef,
			IAssetRelease release,
			Platform platform,
			string filePath,
			Engine engine,
			float progress,
			float span
		) {
			var name   = platform.GetPlatformName();
			var length = new FileInfo(filePath).Length;
			var sizeMb = length / (1024f * 1024f);
			var start  = progress;

			// Découpage du variant : hash, envoi, traitement serveur
			var hashEnd   = start + (span * 0.20f);
			var uploadEnd = start + (span * 0.80f);

			// Files are immutable: re-publishing the same version replaces the variant.
			var previous = release.BestFile(platform, engine);

			if (previous != null) {
				ShowBuildProgress(start, $"Replacing the existing {name} variant...");

				if (string.IsNullOrEmpty(previous.Name)
					|| !await assets.DeleteFile(server, Endpoint, assetRef, release.Name, previous.Name))
					Logger.LogWarning($"Could not remove the previous '{previous.Name}' variant of version {release.Name}.");
			}

			// Le Content-Hash est exigé par le pipeline : le fichier est haché avant l'envoi, et sans
			// retour de progression c'est la phase la plus longue qui resterait invisible.
			ShowBuildProgress(start, $"Hashing the {name} bundle ({sizeMb:F1} MB)...");

			var hash = await Hashing.HashFileAsync(
				AssetHash.Sha256,
				filePath,
				ratio => ShowBuildProgress(
					start + (ratio * (hashEnd - start)),
					$"Hashing the {name} bundle... {ratio * 100:F0}%"
				)
			);

			if (string.IsNullOrEmpty(hash))
				return $"Failed to hash the {name} bundle.";

			ShowBuildProgress(hashEnd, $"Uploading {name} ({sizeMb:F1} MB)...");

			var uploaded = await assets.Upload(
				server,
				Endpoint,
				assetRef,
				release.Name,
				filePath,
				new AssetFileReservation {
					Name = Path.GetFileName(filePath),
					Mime = "application/octet-stream",
					Attributes = new[] {
						new AssetAttribute("platform", name),
						new AssetAttribute("engine", $"{engine.GetEngineName()}:{EngineVersion}")
					}
				},
				new AssetUploadOptions {
					Hash   = AssetHash.Parse(hash),
					Length = length
				},
				(sent, bytes) => {
					// Certains transports ne rapportent que les octets envoyés : le ratio est alors
					// reconstruit pour que la barre avance quand même.
					if (sent <= 0f && bytes > 0 && length > 0)
						sent = (float)((double)bytes / length);

					ShowBuildProgress(
						hashEnd + (sent * (uploadEnd - hashEnd)),
						$"Uploading {name}... {sent * sizeMb:F2} MB / {sizeMb:F2} MB - {sent * 100:F0}%"
					);
				}
			);

			if (uploaded == null)
				return $"Failed to upload the {name} bundle.";

			ShowBuildProgress(uploadEnd, $"Processing the {name} bundle...");

			var processed = await WaitForProcessing(
				assets,
				server,
				assetRef,
				release.Name,
				uploaded,
				uploadEnd
			);

			if (processed == null)
				return $"Processing the {name} bundle timed out. Please check the server status.";

			if (processed.Status?.Status == AssetState.Failed)
				return $"The {name} bundle was rejected: {processed.Status.Message ?? "Unknown error"}";

			return null;
		}

		/// <summary>The worlds collection served by the node.</summary>
		private static AssetEndpoint Endpoint
			=> WorldsEndpoint.Endpoint;

		/// <summary>Release of <paramref name="version"/>, or <c>null</c> when it does not exist yet.</summary>
		private static async UniTask<IAssetRelease> FetchRelease(IAssetsAPI assets, string server, string asset, ushort version)
			=> await assets.FetchRelease(server, Endpoint, asset, version.ToString());

		/// <summary>Major and minor version of the running engine (<c>6000.4</c>), as stored in the
		/// <c>engine</c> file attribute.</summary>
		private static string EngineVersion {
			get {
				var version = EngineExtensions.CurrentVersion;
				return $"{version.Major}.{version.Minor}";
			}
		}

		/// <summary>
		/// Waits for the server to finish analyzing an uploaded file. The pipeline processes
		/// synchronously on small files, so this usually returns the file as-is; a file still
		/// pending is polled until it completes, fails, or the deadline is reached.
		/// </summary>
		private async UniTask<IAssetFile> WaitForProcessing(
			IAssetsAPI assets,
			string server,
			string asset,
			string release,
			IAssetFile file,
			float progress = 0.9f,
			float timeoutSeconds = 300f
		) {
			var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
			var current  = file;

			while (current?.Status is { Status: AssetState.Queued or AssetState.Processing }) {
				if (DateTime.UtcNow >= deadline) {
					Logger.LogError($"Asset processing timed out for {asset}/{release}/{current.Name}.");
					return null;
				}

				var delay = current.RefetchAt > DateTime.UtcNow
					? (current.RefetchAt - DateTime.UtcNow).TotalSeconds
					: 2d;

				ShowBuildProgress(progress, $"Processing asset... {current.Status.Progress}%");
				await UniTask.Delay(TimeSpan.FromSeconds(Math.Clamp(delay, 0.5d, 30d)));

				if (string.IsNullOrEmpty(current.Name))
					return current;

				current = await assets.FetchFile(server, Endpoint, asset, release, current.Name);

				if (current == null) {
					Logger.LogError($"Failed to read the status of {asset}/{release}/{file.Name}.");
					return null;
				}
			}

			Logger.Log($"Asset processing completed: {current?.Status?.Status} ({current?.Size ?? 0} bytes).");
			return current;
		}

		private async UniTask OnDetectVersionAsync() {
			if (_world == null) {
				Logger.OpenDialog("Error", "No world attached.", "Ok");
				return;
			}

			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor)
				return;

			ShowBuildProgress(0f, "Detecting latest version...");

			var assets = Main.AssetsAPI;
			var latest = 0;

			if (assets != null) {
				// The release the world points at is the newest one (`auto`), and it is named after
				// the version it publishes.
				var release = await assets.FetchPreferredRelease(_world.Server, Endpoint, _world.Id.ToString());

				if (release != null) {
					const string prefix = "v";
					var name = release.Name?.TrimStart(prefix.ToCharArray());

					if (!ushort.TryParse(name, out var detected))
						Logger.LogWarning($"Could not read a version out of release '{release.Name}'.");
					else
						latest = detected;
				}
			}

			HideBuildProgress();

			// La détection fait un aller-retour réseau : le descriptor peut avoir été rechargé entre-temps
			var live = WorldDescriptorHelper.Live(descriptor);

			if (live) {
				live.publishVersion = (ushort)(latest + 1);
				EditorUtility.SetDirty(live);
				_assetVersionField?.SetValueWithoutNotify(live.publishVersion);
			}

			if (latest > 0)
				Logger.Log($"Detected latest version: {latest}. Set to {latest + 1}.");
			else
				Logger.Log("No existing version found. Set to 1.");
		}

		private string CreateTempBuildPath() {
			var tempPath = Path.Combine(Path.GetTempPath(), "NoxWorldBuilds", Guid.NewGuid().ToString());
			if (!Directory.Exists(tempPath))
				Directory.CreateDirectory(tempPath);
			return tempPath;
		}
	}
}