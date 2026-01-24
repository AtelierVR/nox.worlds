using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Nox.CCK.Build;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Logger = Nox.CCK.Utils.Logger;
using Object = UnityEngine.Object;
using Random = System.Random;
using Transform = UnityEngine.Transform;

namespace Nox.Worlds.Pipeline {
	public static class Builder {
		public static bool IsBuilding;

		public static readonly UnityEvent<float, string> OnBuildProgress = new();
		public static readonly UnityEvent<BuildResult> OnBuildFinished = new();
		public static readonly UnityEvent<BuildData> OnBuildStarted = new();


		[MenuItem("Nox/Worlds/Build World")]
		public static void BuildMenu()
			=> BuildMenuAsync().Forget();

		public async static UniTask BuildMenuAsync() {
			if (!SceneManager.GetActiveScene().TryGetComponentInChildren<WorldDescriptor>(out var descriptor)) {
				Logger.OpenDialog("Build Failed", "No valid world descriptor found in the current scene.", "OK");
				return;
			}

			const string path = "Assets/Builds/";
			if (!Directory.Exists(path)) {
				try {
					Directory.CreateDirectory(path);
				} catch (Exception e) {
					Logger.OpenDialog("Build Failed", $"Failed to create output directory: {e.Message}", "OK");
					return;
				}
			}

			var data = new BuildData {
				Descriptor = descriptor,
				ShowDialog = true,
				OutputPath = path
			};

			BuildResult result;
			try {
				result = await Build(data);
			} catch (Exception e) {
				result = new BuildResult {
					Type = BuildResultType.Failed,
					Message = $"Build failed with exception: {e.Message}"
				};
				Logger.LogError(result.Message);
			}
			finally {
				IsBuilding = false;
			}

			if (result.Type == BuildResultType.Success) {
				Logger.OpenDialog("Build Success", "The world has been built successfully!", "OK");
			}
			else Logger.OpenDialog("Build Failed", result.Message, "OK");
		}

		public static async UniTask<BuildResult> Build(BuildData data) {
			// Wrap user progress callback to also emit UnityEvent
			var userProgress = data.ProgressCallback;
			data.ProgressCallback = (p, m) =>
			{
				try {
					OnBuildProgress.Invoke(p, m);
				} catch {
					/* ignore listener errors */
				}

				try {
					userProgress?.Invoke(p, m);
				} catch {
					/* ignore user callback errors */
				}
			};

			// Notify build start
			try {
				OnBuildStarted.Invoke(data);
			} catch {
				/* ignore listener errors */
			}

			// Helper to ensure we always raise finished event
			BuildResult Finish(BuildResult r) {
				try {
					OnBuildFinished.Invoke(r);
				} catch {
					/* ignore listener errors */
				}

				return r;
			}

			try {
				if (data.Target == Platform.None)
					data.Target = PlatformExtensions.CurrentPlatform; // Set default filename if not provided
				if (string.IsNullOrEmpty(data.Filename))
					data.Filename = GenerateDefaultFilename(data.Descriptor.gameObject.scene.name, data.Target); // Set randomized temp path if not provided
				if (string.IsNullOrEmpty(data.TempPath))
					data.TempPath = $"Assets/Temp/{GenerateRandomHash()}/"; // Report progress: Validation
				data.ProgressCallback?.Invoke(0.05f, "Validating build prerequisites...");
				await UniTask.Yield();

				// Validation des prérequis
				var validation = ValidateBuildPrerequisites(data);
				if (validation.Type != BuildResultType.Success)
					return Finish(validation);

				IsBuilding = true;
				var rollback = EditorSceneManager.GetSceneManagerSetup();

				try {
					// Report progress: Preparation
					data.ProgressCallback?.Invoke(0.10f, "Preparing temporary directories...");
					await UniTask.Yield();

					// Préparation des répertoires temporaires
					var preparation = PrepareTemporaryDirectories(data);
					if (preparation.Type != BuildResultType.Success) {
						EditorSceneManager.RestoreSceneManagerSetup(rollback);
						return Finish(preparation);
					}

					// Sauvegarde initiale des scènes
					if (!EditorSceneManager.SaveOpenScenes()) {
						EditorSceneManager.RestoreSceneManagerSetup(rollback);
						return Finish(
							new BuildResult {
								Type = BuildResultType.Failed,
								Message = "Failed to save open scenes. Please ensure all scenes are saved before building."
							}
						);
					}

					AssetDatabase.Refresh();

					// Report progress: Compiling scripts
					data.ProgressCallback?.Invoke(0.40f, "Compiling scripts...");
					await UniTask.Yield();

					// Compilation des scripts
					var compilation = await CompileScripts(data.Descriptor.gameObject);
					if (compilation.Type != BuildResultType.Success) {
						EditorSceneManager.RestoreSceneManagerSetup(rollback);
						return Finish(compilation);
					}

					// Report progress: Processing scenes
					data.ProgressCallback?.Invoke(0.60f, "Processing scenes and dependencies...");
					await UniTask.Yield();

					var processing = await ProcessScenesAndDependencies(data);
					if (processing.Type != BuildResultType.Success) {
						EditorSceneManager.RestoreSceneManagerSetup(rollback);
						return Finish(processing);
					}

					// Report progress: Building AssetBundle
					data.ProgressCallback?.Invoke(0.80f, "Building AssetBundle...");
					await UniTask.NextFrame();

					// Création de l'AssetBundle des scènes
					var assetBundleResult = await BuildScenesAssetBundle(data);
					if (assetBundleResult.Type != BuildResultType.Success) {
						EditorSceneManager.RestoreSceneManagerSetup(rollback);
						return Finish(assetBundleResult);
					}

					// Report progress: Cleanup
					data.ProgressCallback?.Invoke(0.95f, "Cleaning up...");
					await UniTask.Yield();

					// TODO: Cleanup temporary files

					// Report progress: Complete
					data.ProgressCallback?.Invoke(1.0f, "Build completed successfully!");
					await UniTask.Yield();

					return Finish(
						new BuildResult {
							Type = BuildResultType.Success,
							Output = assetBundleResult.Output
						}
					);
				} catch (Exception e) {
					// Restore scene on error
					EditorSceneManager.RestoreSceneManagerSetup(rollback);
					Logger.LogError(new Exception("Build failed with exception", e));
					return Finish(
						new BuildResult {
							Type = BuildResultType.Failed,
							Message = e.Message + "\nSee console for details."
						}
					);
				}
				finally {
					// Cleanup state
					IsBuilding = false;
				}
			} catch (Exception e) {
				Logger.LogError(new Exception("Build failed with exception", e));
				return Finish(
					new BuildResult {
						Type = BuildResultType.Failed,
						Message = e.Message + "\nSee console for details."
					}
				);
			}
		}

		/// <summary>
		/// Validates build prerequisites and parameters
		/// </summary>
		private static BuildResult ValidateBuildPrerequisites(BuildData data) {
			if (IsBuilding)
				return new BuildResult {
					Type = BuildResultType.AlreadyBuilding,
					Message = "A build is already in progress."
				};

			if (EditorApplication.isCompiling)
				return new BuildResult {
					Type = BuildResultType.EditorCompiling,
					Message = "Unity is currently compiling scripts. Please wait until the compilation is complete."
				};

			if (EditorApplication.isPlaying)
				return new BuildResult {
					Type = BuildResultType.EditorPlaying,
					Message = "Unity is currently in play mode. Please stop playing before building."
				};

			if (data.Target == Platform.None)
				return new BuildResult {
					Type = BuildResultType.InvalidTarget,
					Message = "No build target specified. Please select a valid target platform."
				};

			if (!data.Target.IsSupported())
				return new BuildResult {
					Type = BuildResultType.UnsupportedTarget,
					Message = $"The build target {data.Target} is not supported."
				};

			return new BuildResult {
				Type = BuildResultType.Success,
				Output = data.OutputPath
			};
		}

		/// <summary>
		/// Prepares temporary directories and validates scene
		/// </summary>
		private static BuildResult PrepareTemporaryDirectories(BuildData data) {
			if (!data.Descriptor || !data.Descriptor.gameObject)
				return new BuildResult {
					Type = BuildResultType.InvalidScenes,
					Message = "The WorldDescriptor is not set or the game object is invalid."
				};

			var mainScene = data.Descriptor.gameObject.scene;
			if (!mainScene.IsValid() || !mainScene.isLoaded)
				return new BuildResult {
					Type = BuildResultType.InvalidScenes,
					Message = "The scene is not valid. Please ensure the scene is properly set up."
				};

			var tempPath = data.TempPath;
			if (Directory.Exists(tempPath))
				try {
					Directory.Delete(tempPath, true);
				} catch (Exception e) {
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = $"Failed to delete temporary directory: {e.Message}"
					};
				}

			Directory.CreateDirectory(tempPath);

			return new BuildResult {
				Type = BuildResultType.Success,
				Output = tempPath
			};
		}

		/// <summary>
		/// Compiles all ICompilable scripts in the loaded scenes
		/// </summary>
		private static async UniTask<BuildResult> CompileScripts(GameObject mainObject) {
			var compilableScripts = mainObject
				.GetComponentsInChildren<ICompilable>(true)
				.OrderBy(s => s.CompileOrder)
				.ToList();

			if (compilableScripts.Count == 0)
				Logger.Log("No compilable scripts found in the loaded scenes.");

			var compiler = new Compiler(compilableScripts);
			if (!await compiler.Compile())
				return new BuildResult {
					Type = BuildResultType.Failed,
					Message = "Script compilation failed. Original scenes have been restored from backup."
				};

			var removeScripts = mainObject
				.GetComponentsInChildren<IRemoveOnBuild>(true)
				.ToArray();

			var exceptions = new List<Exception>();

			foreach (var script in removeScripts)
				try {
					if (script == null) continue;
					Logger.Log($"Removing script: {script.GetType().Name}");
					script.OnRemoveOnBuild();
					if (script is Object scriptObject) // In case the script removed itself
						scriptObject.DestroyImmediate();
				} catch (Exception e) {
					var ex = new Exception($"Failed to remove script {script?.GetType().Name ?? "null"}", e);
					Logger.LogError(ex);
					exceptions.Add(ex);
				}

			if (exceptions.Count > 0)
				return new BuildResult {
					Type = BuildResultType.Failed,
					Message = string.Join(
						"\n",
						new[] { "Script removal failed:" }
							.Concat(exceptions.Select(e => "\t" + e.Message))
							.Concat(new[] { "See console for details." })
					)
				};
			
			// Force asset database refresh to ensure files are written to disk
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// Wait a frame to ensure file operations are complete
			await UniTask.Yield();

			return new BuildResult {
				Type = BuildResultType.Success,
				Output = null
			};
		}

		/// <summary>
		/// Saves compiled scenes and copies dependencies to temporary directory
		/// </summary>
		private static async UniTask<BuildResult> ProcessScenesAndDependencies(BuildData data) {
			var tempPath = data.TempPath;

			try {
				// Process world scene (similar to avatar prefab processing)
				var worldGameObject = data.Descriptor.gameObject;

				// Validate world GameObject before processing
				if (!worldGameObject)
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "World GameObject is null or invalid."
					};

				if (!worldGameObject.activeInHierarchy)
					Logger.LogWarning("World GameObject is not active in hierarchy. This might cause issues.");

				// Nettoyage complet des composants manquants AVANT de vérifier les problèmes
				Logger.Log("Cleaning up missing components from World GameObject and its children...");
				CleanupMissingComponents(worldGameObject);

				var problematicComponents = new List<(GameObject, int)>();
				CheckForProblematicComponents(worldGameObject);

				if (problematicComponents.Count > 0) {
					Logger.LogWarning($"Found {problematicComponents.Count} problematic component(s). Attempting to clean them up...");
					ForceCleanProblematicComponents(problematicComponents);

					// Re-check after cleanup
					problematicComponents.Clear();
					CheckForProblematicComponents(worldGameObject);

					if (problematicComponents.Count > 0) {
						Logger.LogError($"Still found {problematicComponents.Count} problematic component(s) after cleanup attempt:");
						foreach (var (go, index) in problematicComponents)
							Logger.LogError($"  - GameObject '{go?.name}' at component index {index}");

						return new BuildResult {
							Type = BuildResultType.Failed,
							Message = $"World GameObject contains {problematicComponents.Count} problematic component(s) that prevent scene processing. Please fix these issues manually."
						};
					}
				}

				// Ensure the temp directory exists and is writable
				if (!Directory.Exists(tempPath))
					Directory.CreateDirectory(tempPath);

				// Check if we can write to the temp directory
				var testFile = Path.Combine(tempPath, "test.tmp");
				try {
					await File.WriteAllTextAsync(testFile, "test");
					File.Delete(testFile);
				} catch (Exception e) {
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = $"Cannot write to temporary directory '{tempPath}': {e.Message}"
					};
				}

				Logger.Log($"Processing world scene from GameObject '{worldGameObject.name}'");

				// Final cleanup of any remaining null components
				CleanupNullComponents(worldGameObject);

				// Rafraîchir pour que Unity reconnaisse les changements
				AssetDatabase.Refresh();
				await UniTask.Yield();

				Logger.Log($"Successfully processed world scene");
				return new BuildResult {
					Type = BuildResultType.Success
				};

				void CheckForProblematicComponents(GameObject go) {
					if (!go) return;

					// Check if the GameObject has any components that might prevent scene processing
					var components = go.GetComponents<Component>();
					for (var i = 0; i < components.Length; i++) {
						var component = components[i];
						if (component) continue;
						problematicComponents.Add((go, i));
					}

					// Recursively check children
					foreach (Transform child in go.transform)
						CheckForProblematicComponents(child.gameObject);
				}
			} catch (Exception e) {
				Logger.LogError(e);
				return new BuildResult {
					Type = BuildResultType.Failed,
					Message = $"Failed to process world scene: {e.Message}"
				};
			}
		}

		/// <summary>
		/// Builds an AssetBundle containing the world scenes
		/// </summary>
		/// <param name="data">Build data containing target platform and descriptor info</param>
		/// <returns>BuildResult indicating success or failure</returns>
		private static async UniTask<BuildResult> BuildScenesAssetBundle(BuildData data) {
			try {
				// Validate input data
				if (data == null)
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "BuildData is null."
					};

				if (string.IsNullOrEmpty(data.TempPath))
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "Temporary path is null or empty."
					};

				if (string.IsNullOrEmpty(data.OutputPath))
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "Output path is null or empty."
					};

				if (string.IsNullOrEmpty(data.Filename))
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "Filename is null or empty."
					};

				var tempPath = data.TempPath;
				Logger.Log("Building AssetBundle for world scenes...");

				// Report progress: Collecting scene files
				data.ProgressCallback?.Invoke(0.82f, "Collecting world scenes...");
				await UniTask.Yield();

				// Collect the main world scene (get from descriptor's scene path)
				var sceneFiles = new List<string> { data.Descriptor.gameObject.scene.path };


				if (sceneFiles.Count == 0 || sceneFiles.Any(string.IsNullOrEmpty))
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "No world scene found to bundle."
					};

				// Report progress: Preparing AssetBundle
				data.ProgressCallback?.Invoke(0.84f, "Preparing AssetBundle build...");
				await UniTask.Yield();

				Logger.Log($"Found {sceneFiles.Count} scene file(s) to bundle:");
				foreach (var sceneFile in sceneFiles)
					Logger.Log($"  - Scene: {sceneFile}");

				// Validate all files exist and convert to relative paths for Unity
				var validAssetFiles = new List<string>();
				foreach (var assetFile in sceneFiles)
					if (File.Exists(assetFile)) {
						// Convert absolute path to relative path for Unity AssetDatabase
						var relativePath = assetFile.Replace('\\', '/');
						if (relativePath.StartsWith(Application.dataPath.Replace('\\', '/')))
							relativePath = "Assets" + relativePath[Application.dataPath.Length..];
						validAssetFiles.Add(relativePath);
						Logger.Log($"  Valid asset: {relativePath}");
					}
					else Logger.LogWarning($"Asset file does not exist: {assetFile}");

				if (validAssetFiles.Count == 0)
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "No valid asset files found for bundling."
					};

				// Create AssetBundleBuild
				var assetBundleBuilds = new AssetBundleBuild[1];
				assetBundleBuilds[0] = new AssetBundleBuild {
					assetBundleName = data.Filename,
					assetNames = validAssetFiles.ToArray(),
					addressableNames = validAssetFiles.Select(Path.GetFileNameWithoutExtension).ToArray()
				};

				Logger.Log($"Created AssetBundle build: {data.Filename} with {assetBundleBuilds[0].assetNames.Length} assets");

				// Validate the AssetBundleBuild
				if (assetBundleBuilds[0].assetNames.Length == 0)
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "No valid assets found for AssetBundle build."
					};

				if (string.IsNullOrEmpty(assetBundleBuilds[0].assetBundleName))
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "AssetBundle name is null or empty."
					};

				// Report progress: Creating output directory
				data.ProgressCallback?.Invoke(0.86f, "Creating output directory...");
				await UniTask.Yield();

				// Create output directory
				var outputPath = data.OutputPath;
				Directory.CreateDirectory(outputPath);

				// Build options optimized for worlds with maximum compression
				var options = BuildAssetBundleOptions.None | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode;

				// Report progress: Building AssetBundle (this is the long operation)
				data.ProgressCallback?.Invoke(0.88f, "Building world AssetBundle (this may take a while)...");
				await UniTask.Yield();

				// Validate build target
				var buildTarget = data.Target.GetBuildTarget();
				Logger.Log($"Building world AssetBundle with target: {buildTarget}");

				// Build the AssetBundle
				var buildSuccess = BuildAssetBundleInternal(
					outputPath,
					assetBundleBuilds,
					options,
					buildTarget
				);

				data.ProgressCallback?.Invoke(0.92f, "Finalizing world AssetBundle...");

				if (!buildSuccess) {
					Logger.LogError("World AssetBundle build failed. Check console for details.");
					return new BuildResult {
						Type = BuildResultType.Failed,
						Message = "Failed to build world AssetBundle. Check console for details."
					};
				}

				Logger.Log($"World AssetBundle '{data.Filename}' built successfully at: {outputPath}");
				Logger.Log($"World scenes built without dependencies");
				return new BuildResult {
					Type = BuildResultType.Success,
					Output = Path.Combine(outputPath, data.Filename)
				};
			} catch (Exception e) {
				Logger.LogError($"World AssetBundle build failed: {e.Message}");
				return new BuildResult {
					Type = BuildResultType.Failed,
					Message = $"World AssetBundle build failed: {e.Message}"
				};
			}
		}

		/// <summary>
		/// Builds an AssetBundle and returns whether the operation was successful
		/// Thanks for "https://light11.hatenadiary.com/entry/2021/03/30/201333" to fix the issue with BuildPipeline.BuildAssetBundles and UniTask
		/// </summary>
		/// <param name="outputPath">The output path for the AssetBundle</param>
		/// <param name="assetBundleBuilds">The AssetBundle builds to create</param>
		/// <param name="options">Build options</param>
		/// <param name="buildTarget">Target platform</param>
		/// <returns>True if the build was successful, false otherwise</returns>
		private static bool BuildAssetBundleInternal(string outputPath, AssetBundleBuild[] assetBundleBuilds, BuildAssetBundleOptions options, BuildTarget buildTarget) {
			try {
				Logger.Log($"Starting AssetBundle build with {assetBundleBuilds.Length} bundles to path: {outputPath}");
				Logger.Log($"Build target: {buildTarget}, Options: {options}");

				// Validate output path exists
				if (!Directory.Exists(outputPath)) {
					Logger.LogError($"Output directory does not exist: {outputPath}");
					return false;
				}

				// Validate each asset bundle
				foreach (var bundle in assetBundleBuilds) {
					Logger.Log($"Validating bundle '{bundle.assetBundleName}' with {bundle.assetNames.Length} assets");

					if (string.IsNullOrEmpty(bundle.assetBundleName)) {
						Logger.LogError("AssetBundle name is null or empty");
						return false;
					}

					if (bundle.assetNames.Length == 0) {
						Logger.LogError($"Bundle '{bundle.assetBundleName}' has no assets");
						return false;
					}

					// Validate each asset exists and is importable by Unity
					foreach (var asset in bundle.assetNames) {
						if (!File.Exists(asset)) {
							Logger.LogError($"Asset file does not exist: {asset}");
							return false;
						}

						// Check if Unity can recognize this asset
						var guid = AssetDatabase.AssetPathToGUID(asset);
						if (string.IsNullOrEmpty(guid)) {
							Logger.LogError($"Unity cannot recognize asset (no GUID): {asset}");
							return false;
						}

						Logger.Log($"  - Valid asset: {asset} (GUID: {guid})");
					}
				}

				// Force a final asset database refresh before building
				AssetDatabase.Refresh();
				AssetDatabase.SaveAssets();

				// If legacy fails, try with CompatibilityBuildPipeline
				var manifest = CompatibilityBuildPipeline.BuildAssetBundles(
					outputPath,
					assetBundleBuilds,
					options,
					buildTarget
				);

				bool success = manifest;

				if (success) {
					Logger.Log("AssetBundle build completed successfully with CompatibilityBuildPipeline.");
				}
				else {
					Logger.LogError("Both BuildPipeline methods failed. No manifest was created.");

					// Additional debugging information
					Logger.LogError($"Output path contents:");
					if (Directory.Exists(outputPath)) {
						var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
						foreach (var file in files) {
							Logger.LogError($"  - {file}");
						}
					}
				}

				return success;
			} catch (Exception e) {
				Logger.LogError($"AssetBundle build failed with exception: {e.Message}");
				Logger.LogError($"Stack trace: {e.StackTrace}");
				return false;
			}
		}

		/// <summary>
		/// Cleans up missing components from GameObject and its children
		/// </summary>
		private static void CleanupMissingComponents(GameObject rootObject) {
			if (!rootObject) return;

			var allGameObjects = new List<GameObject> { rootObject };
			GetAllChildren(rootObject, allGameObjects);

			foreach (var go in allGameObjects) {
				if (!go) continue;

				// Count removed components for logging
				var initialComponentCount = go.GetComponentCount();

				// Remove missing MonoBehaviours
				GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

				var finalComponentCount = go.GetComponentCount();
				var removedCount = initialComponentCount - finalComponentCount;

				if (removedCount > 0) {
					Logger.Log($"Removed {removedCount} missing component(s) from GameObject '{go.name}'");
				}
			}

			static void GetAllChildren(GameObject parent, List<GameObject> list) {
				foreach (Transform child in parent.transform) {
					if (child && child.gameObject) {
						list.Add(child.gameObject);
						GetAllChildren(child.gameObject, list);
					}
				}
			}
		}

		/// <summary>
		/// Force cleanup of remaining problematic components
		/// </summary>
		private static void ForceCleanProblematicComponents(List<(GameObject go, int index)> problematicComponents) {
			foreach (var (go, index) in problematicComponents) {
				if (!go) continue;

				try {
					// Try to get component at index and remove if null
					var components = go.GetComponents<Component>();
					if (index < components.Length && !components[index]) {
						// Component is null, we need to remove it manually
						// Since we can't remove by index directly, we'll use GameObjectUtility again
						GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
						Logger.Log($"Force removed null component at index {index} from GameObject '{go.name}'");
					}
				} catch (Exception e) {
					Logger.LogWarning($"Failed to force clean component at index {index} from GameObject '{go.name}': {e.Message}");
				}
			}
		}

		/// <summary>
		/// Final cleanup of any remaining null components
		/// </summary>
		private static void CleanupNullComponents(GameObject rootObject) {
			if (!rootObject) return;

			var allGameObjects = new List<GameObject> { rootObject };
			GetAllChildren(rootObject, allGameObjects);

			foreach (var go in allGameObjects) {
				if (!go) continue;

				try {
					// Check for any remaining null components
					var components = go.GetComponents<Component>();
					var hasNullComponents = components.Any(c => !c);

					if (hasNullComponents) {
						// Final attempt to clean
						GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
						Logger.Log($"Final cleanup of null components on GameObject '{go.name}'");
					}
				} catch (Exception e) {
					Logger.LogWarning($"Failed to perform final cleanup on GameObject '{go.name}': {e.Message}");
				}
			}

			static void GetAllChildren(GameObject parent, List<GameObject> list) {
				foreach (Transform child in parent.transform) {
					if (child && child.gameObject) {
						list.Add(child.gameObject);
						GetAllChildren(child.gameObject, list);
					}
				}
			}
		}


		/// <summary>
		/// Generates a default filename for the asset bundle based on date, random int, and main scene name
		/// </summary>
		/// <param name="mainSceneName">The name of the main scene</param>
		/// <param name="platform"></param>
		/// <returns>A filename in the format: date-sceneName.noxw</returns>
		private static string GenerateDefaultFilename(string mainSceneName, Platform platform) {
			var date = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
			var sceneName = mainSceneName.ToLowerInvariant();

			// Remove any invalid filename characters from scene name
			sceneName = Regex.Replace(sceneName, @"[^a-z0-9\-_]", "");

			return $"{date}-{sceneName}-{platform.GetPlatformName()}.noxw";
		}

		/// <summary>
		/// Generates a random hash for temporary directory
		/// </summary>
		/// <returns>A random hash string</returns>
		private static string GenerateRandomHash() {
			var random = new Random();
			var bytes = new byte[16];
			random.NextBytes(bytes);
			return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
		}
	}
}