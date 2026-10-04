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
using UnityEditor.Build.Pipeline.Tasks;
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

		/// <summary>
		/// Racine des dossiers de travail du build. Les scènes qui s'y trouvent sont des copies jetables
		/// et non des worlds : voir <see cref="WorldCopy"/>.
		/// </summary>
		public const string TempRoot = "Assets/Temp/";

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
				OutputPath = path
			};

			BuildResult result;
			try {
				result = await Build(data);
			} catch (Exception e) {
				result = new BuildResult {
					Type    = BuildResultType.Failed,
					Message = $"Build failed with exception: {e.Message}"
				};
				Logger.LogError(result.Message);
			} finally {
				IsBuilding = false;
			}

			if (result.Type == BuildResultType.Success) {
				Logger.OpenDialog("Build Success", "The world has been built successfully!", "OK");
			} else
				Logger.OpenDialog("Build Failed", result.Message, "OK");
		}

		/// <summary>
		/// État interne d'un build. L'appelant ne fournit qu'un <see cref="BuildData"/> : les plateformes,
		/// l'emplacement temporaire, le nom des bundles et la copie de travail sont décidés ici.
		/// </summary>
		private sealed class State {
			public WorldDescriptor Descriptor;
			public string          OutputPath;

			/// <summary>Plateformes à construire : un AssetBundle par entrée.</summary>
			public Platform[] Targets = Array.Empty<Platform>();

			/// <summary>
			/// Nom de la scène d'origine, figé avant toute opération : les références vers ses objets
			/// peuvent être invalidées en cours de route (rechargement de scène, refresh).
			/// </summary>
			public string SceneName;

			public string    Filename;
			public string    TempPath;
			public WorldCopy Copy;

			public Action<float, string> ProgressCallback = (_, _) => { };
		}

		/// <summary>
		/// Construit le world de <paramref name="request"/> : une copie de travail est créée, compilée,
		/// puis bundlée une fois par plateforme cible. La scène d'origine n'est jamais modifiée, et rien
		/// n'est écrit hors de <see cref="BuildData.OutputPath"/> et de <see cref="TempRoot"/>.
		/// </summary>
		public static async UniTask<BuildResult> Build(BuildData request) {
			// Wrap user progress callback to also emit UnityEvent
			var userProgress = request?.ProgressCallback;

			var state = new State {
				Descriptor       = request?.Descriptor,
				OutputPath       = request?.OutputPath,
				Targets          = NormalizeTargets(request?.Descriptor ? request.Descriptor.Targets : null),
				SceneName        = request?.Descriptor ? request.Descriptor.gameObject.scene.name : null,
				TempPath         = $"{TempRoot}{GenerateRandomHash()}/",
				ProgressCallback = (p, m) => {
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
				}
			};

			if (!state.Descriptor || !state.Descriptor.gameObject)
				return Finish(
					new BuildResult {
						Type    = BuildResultType.InvalidScenes,
						Message = "No world descriptor was given to the build."
					}
				);

			if (string.IsNullOrEmpty(state.OutputPath))
				return Finish(
					new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "No output path was given to the build."
					}
				);

			// Notify build start
			try {
				OnBuildStarted.Invoke(request);
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
				state.ProgressCallback?.Invoke(0.05f, "Validating build prerequisites...");
				await UniTask.Yield();

				// Validation des prérequis
				var validation = ValidateBuildPrerequisites(state);
				if (validation.Type != BuildResultType.Success)
					return Finish(validation);

				IsBuilding = true;
				var rollback = EditorSceneManager.GetSceneManagerSetup();

				// La copie de travail est libérée avant d'annoncer la fin du build : les panels
				// re-résolvent le world courant et ne doivent jamais tomber sur le descriptor de la copie.
				// La scène d'origine n'ayant pas été touchée, il n'y a rien à restaurer.
				BuildResult Fail(BuildResult result) {
					state.Copy?.Dispose();
					state.Copy = null;
					return Finish(result);
				}

				try {
					// Report progress: Preparation
					state.ProgressCallback?.Invoke(0.10f, "Preparing temporary directories...");
					await UniTask.Yield();

					// Préparation des répertoires temporaires
					var preparation = PrepareTemporaryDirectories(state);
					if (preparation.Type != BuildResultType.Success)
						return Fail(preparation);

					AssetDatabase.Refresh();

					// Report progress: Copying the world
					state.ProgressCallback?.Invoke(0.20f, "Copying the world...");
					await UniTask.Yield();

					// Le build travaille sur une copie : la scène d'origine n'est ni compilée, ni modifiée,
					// ni réécrite (SaveAsCopy), donc jamais réimportée en cours de route.
					state.Copy = WorldCopy.Create(state.Descriptor, state.TempPath);
					if (state.Copy == null)
						return Fail(
							new BuildResult {
								Type    = BuildResultType.InvalidScenes,
								Message = "Failed to copy the world scene. Make sure the scene is saved and that 'Assets/Temp/' is writable."
							}
						);

					// Report progress: Compiling scripts
					state.ProgressCallback?.Invoke(0.40f, "Compiling scripts...");
					await UniTask.Yield();

					// Compilation des scripts, sur la copie
					var compilation = await CompileScripts(state.Copy.Descriptor.gameObject);
					if (compilation.Type != BuildResultType.Success)
						return Fail(compilation);

					// Persister le résultat de la compilation au plus tôt : c'est le disque qui est bundlé
					if (!state.Copy.Save())
						return Fail(
							new BuildResult {
								Type    = BuildResultType.Failed,
								Message = "Failed to save the compiled world copy."
							}
						);

					// Report progress: Processing scenes
					state.ProgressCallback?.Invoke(0.60f, "Processing scenes and dependencies...");
					await UniTask.Yield();

					var processing = await ProcessScenesAndDependencies(state.Copy.Descriptor.gameObject, state.TempPath);
					if (processing.Type != BuildResultType.Success)
						return Fail(processing);

					// Report progress: Saving the compiled copy
					state.ProgressCallback?.Invoke(0.70f, "Saving the compiled world...");
					await UniTask.Yield();

					// La scène bundlée est celle du disque : elle doit contenir le résultat de la compilation
					if (!state.Copy.Save())
						return Fail(
							new BuildResult {
								Type    = BuildResultType.Failed,
								Message = "Failed to save the compiled world copy."
							}
						);

					// Report progress: Building AssetBundle
					state.ProgressCallback?.Invoke(0.80f, "Building AssetBundle...");
					// Yield et non NextFrame : en Edit mode Time.frameCount n'avance que lorsque l'éditeur
					// reçoit un frame (focus, repaint), un NextFrame gèlerait le build en arrière-plan.
					await UniTask.Yield();

					// Un AssetBundle par plateforme ciblée
					var outputs = new List<BuildOutput>();
					foreach (var platform in state.Targets) {
						state.Filename = GenerateDefaultFilename(state.SceneName, platform);

						state.ProgressCallback?.Invoke(
							0.80f,
							$"Building AssetBundle for {platform.GetPlatformName()}... ({outputs.Count + 1}/{state.Targets.Length})"
						);
						await UniTask.Yield();

						var assetBundleResult = await BuildScenesAssetBundle(state, platform);
						if (assetBundleResult.Type != BuildResultType.Success)
							return Fail(assetBundleResult);

						outputs.Add(new BuildOutput(platform, assetBundleResult.Output));
					}

					// La copie a rempli son rôle : suppression avant de prévenir les listeners
					state.Copy.Dispose();
					state.Copy = null;

					// Report progress: Cleanup
					state.ProgressCallback?.Invoke(0.95f, "Cleaning up...");
					await UniTask.Yield();

					// Report progress: Complete
					state.ProgressCallback?.Invoke(1.0f, "Build completed successfully!");
					await UniTask.Yield();

					return Finish(
						new BuildResult {
							Type    = BuildResultType.Success,
							Outputs = outputs.ToArray()
						}
					);
				} catch (Exception e) {
					// Restore scene on error
					state.Copy?.Dispose();
					state.Copy = null;
					EditorSceneManager.RestoreSceneManagerSetup(rollback);
					Logger.LogError(new Exception("Build failed with exception", e));
					return Finish(
						new BuildResult {
							Type    = BuildResultType.Failed,
							Message = e.Message + "\nSee console for details."
						}
					);
				} finally {
					// Cleanup state
					IsBuilding = false;
				}
			} catch (Exception e) {
				state.Copy?.Dispose();
				state.Copy = null;
				Logger.LogError(new Exception("Build failed with exception", e));
				return Finish(
					new BuildResult {
						Type    = BuildResultType.Failed,
						Message = e.Message + "\nSee console for details."
					}
				);
			}
		}

		/// <summary>
		/// Validates build prerequisites and parameters
		/// </summary>
		private static BuildResult ValidateBuildPrerequisites(State state) {
			if (IsBuilding)
				return new BuildResult {
					Type    = BuildResultType.AlreadyBuilding,
					Message = "A build is already in progress."
				};

			if (EditorApplication.isCompiling)
				return new BuildResult {
					Type    = BuildResultType.EditorCompiling,
					Message = "Unity is currently compiling scripts. Please wait until the compilation is complete."
				};

			if (EditorApplication.isPlaying)
				return new BuildResult {
					Type    = BuildResultType.EditorPlaying,
					Message = "Unity is currently in play mode. Please stop playing before building."
				};

			// Le Scriptable Build Pipeline refuse de construire tant qu'une scène chargée est modifiée :
			// on échoue tout de suite, plutôt qu'après la copie et la compilation du world.
			var dirtyScenes = DirtyScenes();
			if (dirtyScenes.Length > 0)
				return new BuildResult {
					Type    = BuildResultType.Failed,
					Message = $"These loaded scenes have unsaved changes: {string.Join(", ", dirtyScenes)}. Save them and build again."
				};

			if (state.Targets.Length == 0)
				return new BuildResult {
					Type    = BuildResultType.InvalidTarget,
					Message = "No build target specified. Please select a valid target platform."
				};

			foreach (var platform in state.Targets)
				if (!platform.IsSupported())
					return new BuildResult {
						Type    = BuildResultType.UnsupportedTarget,
						Message = $"The build target {platform.GetPlatformName()} is not supported."
					};

			return new BuildResult {
				Type = BuildResultType.Success
			};
		}

		/// <summary>
		/// Scènes chargées avec des modifications non écrites. Le Scriptable Build Pipeline refuse de
		/// construire un bundle tant qu'il en existe une (<c>ReturnCode.UnsavedChanges</c>).
		/// </summary>
		private static string[] DirtyScenes() {
			var scenes = new List<string>();

			for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
				var scene = EditorSceneManager.GetSceneAt(i);

				if (scene.isDirty)
					scenes.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
			}

			return scenes.ToArray();
		}

		/// <summary>
		/// Targets to build: the requested ones, deduplicated and in the
		/// <see cref="PlatformExtensions.All"/> order, falling back on the current platform.
		/// </summary>
		private static Platform[] NormalizeTargets(Platform[] requested) {
			var targets = (requested ?? Array.Empty<Platform>())
				.Where(platform => platform != Platform.None)
				.Distinct()
				.OrderBy(platform => Array.IndexOf(PlatformExtensions.All, platform))
				.ToArray();

			return targets.Length > 0
				? targets
				: new[] { PlatformExtensions.CurrentPlatform };
		}

		/// <summary>
		/// Prepares temporary directories and validates scene
		/// </summary>
		private static BuildResult PrepareTemporaryDirectories(State state) {
			if (!state.Descriptor || !state.Descriptor.gameObject)
				return new BuildResult {
					Type    = BuildResultType.InvalidScenes,
					Message = "The WorldDescriptor is not set or the game object is invalid."
				};

			var mainScene = state.Descriptor.gameObject.scene;
			if (!mainScene.IsValid() || !mainScene.isLoaded)
				return new BuildResult {
					Type    = BuildResultType.InvalidScenes,
					Message = "The scene is not valid. Please ensure the scene is properly set up."
				};

			if (string.IsNullOrEmpty(mainScene.path))
				return new BuildResult {
					Type    = BuildResultType.InvalidScenes,
					Message = "The world scene has never been saved. Save it before building."
				};

			var tempPath = state.TempPath;
			if (Directory.Exists(tempPath))
				try {
					Directory.Delete(tempPath, true);
				} catch (Exception e) {
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = $"Failed to delete temporary directory: {e.Message}"
					};
				}

			Directory.CreateDirectory(tempPath);

			return new BuildResult {
				Type = BuildResultType.Success
			};
		}

		/// <summary>
		/// Compiles all ICompilable scripts of the world copy
		/// </summary>
		/// <param name="root">Racine de la copie de travail, jamais celle de la scène d'origine</param>
		private static async UniTask<BuildResult> CompileScripts(GameObject root) {
			var compilableScripts = root
				.GetComponentsInChildren<ICompilable>(true)
				.OrderBy(s => s.CompileOrder)
				.ToList();

			if (compilableScripts.Count == 0)
				Logger.Log("No compilable scripts found in the loaded scenes.");

			var compiler = new Compiler(compilableScripts);
			if (!await compiler.Compile())
				return new BuildResult {
					Type    = BuildResultType.Failed,
					Message = "Script compilation failed. The world scene has not been modified."
				};

			var removeScripts = root
				.GetComponentsInChildren<IRemoveOnBuild>(true)
				.ToArray();

			var exceptions = new List<Exception>();

			foreach (var script in removeScripts)
				try {
					if (script == null)
						continue;
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
				Type = BuildResultType.Success
			};
		}

		/// <summary>
		/// Saves compiled scenes and copies dependencies to temporary directory
		/// </summary>
		/// <param name="worldGameObject">Racine de la copie de travail</param>
		/// <param name="tempPath">Répertoire temporaire du build</param>
		private static async UniTask<BuildResult> ProcessScenesAndDependencies(GameObject worldGameObject, string tempPath) {
			try {
				// Process world scene (similar to avatar prefab processing)
				// Validate world GameObject before processing
				if (!worldGameObject)
					return new BuildResult {
						Type    = BuildResultType.Failed,
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
							Type    = BuildResultType.Failed,
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
						Type    = BuildResultType.Failed,
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
					if (!go)
						return;

					// Check if the GameObject has any components that might prevent scene processing
					var components = go.GetComponents<Component>();
					for (var i = 0; i < components.Length; i++) {
						var component = components[i];
						if (component)
							continue;
						problematicComponents.Add((go, i));
					}

					// Recursively check children
					foreach (Transform child in go.transform)
						CheckForProblematicComponents(child.gameObject);
				}
			} catch (Exception e) {
				Logger.LogError(e);
				return new BuildResult {
					Type    = BuildResultType.Failed,
					Message = $"Failed to process world scene: {e.Message}"
				};
			}
		}

		/// <summary>
		/// Builds an AssetBundle containing the world scenes
		/// </summary>
		/// <param name="data">Build data containing target platform and descriptor info</param>
		/// <returns>BuildResult indicating success or failure</returns>
		private static async UniTask<BuildResult> BuildScenesAssetBundle(State state, Platform platform) {
			try {
				// Validate input data
				if (state == null)
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "Build state is null."
					};

				if (string.IsNullOrEmpty(state.TempPath))
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "Temporary path is null or empty."
					};

				if (string.IsNullOrEmpty(state.OutputPath))
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "Output path is null or empty."
					};

				if (string.IsNullOrEmpty(state.Filename))
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "Filename is null or empty."
					};

				if (state.Copy == null)
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "The world copy is missing."
					};

				var tempPath = state.TempPath;
				Logger.Log("Building AssetBundle for world scenes...");

				// Report progress: Collecting scene files
				state.ProgressCallback?.Invoke(0.82f, "Collecting world scenes...");
				await UniTask.Yield();

				// Collect the main world scene (the compiled copy, not the authored scene)
				var sceneFiles = new List<string> { state.Copy.ScenePath };


				if (sceneFiles.Count == 0 || sceneFiles.Any(string.IsNullOrEmpty))
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "No world scene found to bundle."
					};

				// Report progress: Preparing AssetBundle
				state.ProgressCallback?.Invoke(0.84f, "Preparing AssetBundle build...");
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
					} else
						Logger.LogWarning($"Asset file does not exist: {assetFile}");

				if (validAssetFiles.Count == 0)
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "No valid asset files found for bundling."
					};

				// Create AssetBundleBuild
				var assetBundleBuilds = new AssetBundleBuild[ 1 ];
				assetBundleBuilds[0] = new AssetBundleBuild {
					assetBundleName  = state.Filename,
					assetNames       = validAssetFiles.ToArray(),
					addressableNames = validAssetFiles.Select(Path.GetFileNameWithoutExtension).ToArray()
				};

				Logger.Log($"Created AssetBundle build: {state.Filename} with {assetBundleBuilds[0].assetNames.Length} assets");

				// Validate the AssetBundleBuild
				if (assetBundleBuilds[0].assetNames.Length == 0)
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "No valid assets found for AssetBundle build."
					};

				if (string.IsNullOrEmpty(assetBundleBuilds[0].assetBundleName))
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "AssetBundle name is null or empty."
					};

				// Report progress: Creating output directory
				state.ProgressCallback?.Invoke(0.86f, "Creating output directory...");
				await UniTask.Yield();

				// Create output directory
				var outputPath = state.OutputPath;
				Directory.CreateDirectory(outputPath);

				// Build options optimized for worlds with maximum compression
				var options = BuildAssetBundleOptions.ForceRebuildAssetBundle;

				// Report progress: Building AssetBundle (this is the long operation)
				state.ProgressCallback?.Invoke(0.88f, "Building world AssetBundle (this may take a while)...");
				await UniTask.Yield();

				// Validate build target
				var buildTarget = platform.GetBuildTarget();
				Logger.Log($"Building world AssetBundle with target: {buildTarget}");

				// Build the AssetBundle
				var buildSuccess = BuildAssetBundleInternal(
					outputPath,
					assetBundleBuilds,
					options,
					buildTarget
				);

				state.ProgressCallback?.Invoke(0.92f, "Finalizing world AssetBundle...");

				if (!buildSuccess) {
					Logger.LogError("World AssetBundle build failed. Check console for details.");
					return new BuildResult {
						Type    = BuildResultType.Failed,
						Message = "Failed to build world AssetBundle. Check console for details."
					};
				}

				Logger.Log($"World AssetBundle '{state.Filename}' built successfully at: {outputPath}");
				Logger.Log($"World scenes built without dependencies");
				return new BuildResult {
					Type    = BuildResultType.Success,
					Outputs = new[] { new BuildOutput(platform, Path.Combine(outputPath, state.Filename)) }
				};
			} catch (Exception e) {
				Logger.LogError($"World AssetBundle build failed: {e.Message}");
				return new BuildResult {
					Type    = BuildResultType.Failed,
					Message = $"World AssetBundle build failed: {e.Message}"
				};
			}
		}

		/// <summary>
		/// Construit l'AssetBundle via le Scriptable Build Pipeline, pour la cible demandée et sans
		/// changer la plateforme active (voir le commentaire dans la méthode). Renvoie <c>false</c> quand
		/// le pipeline échoue ou qu'un bundle attendu n'a pas été écrit.
		/// </summary>
		/// <param name="outputPath">The output path for the AssetBundle</param>
		/// <param name="assetBundleBuilds">The AssetBundle builds to create</param>
		/// <param name="options">Build options, seul <see cref="BuildAssetBundleOptions.ForceRebuildAssetBundle"/> est pris en compte</param>
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

				// Scriptable Build Pipeline, sans la tâche SwitchToBuildPlatform de son preset : les
				// paramètres portent la cible, donc ContentPipeline résout les représentations « player »
				// et les dépendances de scène pour cette cible sans activer la plateforme correspondante.
				// Le pipeline historique (BuildPipeline.BuildAssetBundles avec un target explicite)
				// recompilait les scripts du joueur pour la cible puis rechargeait le domaine au milieu du
				// build : la tâche async en cours était détruite et le build restait figé pour toujours.
				var parameters = new BundleBuildParameters(
					buildTarget,
					BuildPipeline.GetBuildTargetGroup(buildTarget),
					outputPath
				) {
					UseCache = (options & BuildAssetBundleOptions.ForceRebuildAssetBundle) == 0
				};

				var tasks = DefaultBuildTasks
					.Create(DefaultBuildTasks.Preset.AssetBundleCompatible)
					.Where(task => !(task is SwitchToBuildPlatform))
					.ToList();

				ReturnCode code;

				try {
					code = ContentPipeline.BuildAssetBundles(parameters, new BundleBuildContent(assetBundleBuilds), out _, tasks);
				} catch (Exception e) {
					Logger.LogError($"ContentPipeline.BuildAssetBundles failed: {e.Message}");
					return false;
				}

				if (code != ReturnCode.Success) {
					Logger.LogError($"AssetBundle build failed ({code}).");

					if (code == ReturnCode.UnsavedChanges) {
						var dirtyScenes = DirtyScenes();

						Logger.LogError(
							dirtyScenes.Length > 0
								? $"Loaded scenes with unsaved changes: {string.Join(", ", dirtyScenes)}. Save them and build again."
								: "A loaded scene has unsaved changes: save the open scenes and build again."
						);
					}

					if (Directory.Exists(outputPath)) {
						var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
						foreach (var file in files)
							Logger.LogError($"  - {file}");
					}

					return false;
				}

				// Le pipeline ne signale pas toujours un bundle manquant : on vérifie ce qui a été écrit
				foreach (var bundle in assetBundleBuilds) {
					var bundlePath = Path.Combine(outputPath, bundle.assetBundleName);

					if (File.Exists(bundlePath))
						continue;

					Logger.LogError($"AssetBundle '{bundle.assetBundleName}' was not written to '{outputPath}'.");
					return false;
				}

				Logger.Log("AssetBundle build completed successfully.");
				return true;
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
			if (!rootObject)
				return;

			var allGameObjects = new List<GameObject> { rootObject };
			GetAllChildren(rootObject, allGameObjects);

			foreach (var go in allGameObjects) {
				if (!go)
					continue;

				// Count removed components for logging
				var initialComponentCount = go.GetComponentCount();

				// Remove missing MonoBehaviours
				GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

				var finalComponentCount = go.GetComponentCount();
				var removedCount        = initialComponentCount - finalComponentCount;

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
				if (!go)
					continue;

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
			if (!rootObject)
				return;

			var allGameObjects = new List<GameObject> { rootObject };
			GetAllChildren(rootObject, allGameObjects);

			foreach (var go in allGameObjects) {
				if (!go)
					continue;

				try {
					// Check for any remaining null components
					var components        = go.GetComponents<Component>();
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
		/// <returns>A filename in the format: date-sceneName-platform.nw</returns>
		private static string GenerateDefaultFilename(string mainSceneName, Platform platform) {
			var date      = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
			var sceneName = mainSceneName.ToLowerInvariant();

			// Remove any invalid filename characters from scene name
			sceneName = Regex.Replace(sceneName, @"[^a-z0-9\-_]", "");

			return $"{date}-{sceneName}-{platform.GetPlatformName()}.nw";
		}

		/// <summary>
		/// Generates a random hash for temporary directory
		/// </summary>
		/// <returns>A random hash string</returns>
		private static string GenerateRandomHash() {
			var random = new Random();
			var bytes  = new byte[ 16 ];
			random.NextBytes(bytes);
			return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
		}
	}
}