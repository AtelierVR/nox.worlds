using System;
using System.IO;
using System.Linq;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Pipeline {
	/// <summary>
	/// Copie de travail du world : la scène est dupliquée, les <see cref="Nox.CCK.Build.ICompilable"/>
	/// sont exécutés sur la copie, et c'est la copie qui est bundlée. La scène d'origine et son
	/// descriptor ne sont donc jamais compilés, modifiés ni invalidés par un build.
	/// <para>Interne au pipeline : un appelant passe par <see cref="Builder.Build"/>.</para>
	/// </summary>
	internal sealed class WorldCopy : IDisposable {
		private readonly Scene  _sourceScene;
		private readonly string _folder;
		private          bool   _disposed;

		/// <summary>Descriptor de la copie : c'est lui que la compilation doit utiliser.</summary>
		public WorldDescriptor Descriptor { get; }

		/// <summary>Scène de la copie, ouverte en additif.</summary>
		public Scene Scene { get; }

		/// <summary>Chemin AssetDatabase de la scène copiée, celui qui est bundlé.</summary>
		public string ScenePath { get; }

		private WorldCopy(WorldDescriptor descriptor, Scene scene, string scenePath, string folder, Scene sourceScene) {
			Descriptor   = descriptor;
			Scene        = scene;
			ScenePath    = scenePath;
			_folder      = folder;
			_sourceScene = sourceScene;
		}

		/// <summary>
		/// Duplique la scène de <paramref name="source"/> dans <paramref name="tempPath"/> et ouvre la
		/// copie en additif. Renvoie <c>null</c>, après avoir nettoyé ce qui a été créé, quand la copie
		/// est impossible.
		/// </summary>
		public static WorldCopy Create(WorldDescriptor source, string tempPath) {
			if (!source || !source.gameObject) {
				Logger.LogError("The world descriptor is not set: there is nothing to copy.");
				return null;
			}

			var sourceScene = source.gameObject.scene;

			if (!sourceScene.IsValid() || !sourceScene.isLoaded) {
				Logger.LogError("The world scene is not loaded: there is nothing to copy.");
				return null;
			}

			if (string.IsNullOrEmpty(sourceScene.path)) {
				Logger.LogError("The world scene has never been saved: save it before building.");
				return null;
			}

			var folder = tempPath.Replace('\\', '/').TrimEnd('/');

			// Même nom de fichier que la scène d'origine : le contenu du bundle reste identique
			var path = $"{folder}/{Path.GetFileName(sourceScene.path)}";

			try {
				Directory.CreateDirectory(folder);
				AssetDatabase.Refresh();

				// SaveAsCopy : la scène d'origine garde son fichier, son marqueur « non sauvegardé » et
				// n'est donc pas réimportée — ce qui invaliderait toutes les références vers ses objets.
				if (!EditorSceneManager.SaveScene(sourceScene, path, true)) {
					Logger.LogError($"Could not save a copy of the world scene '{sourceScene.path}' to '{path}'.");
					return null;
				}
			} catch (Exception e) {
				Logger.LogError(new Exception("Could not copy the world scene", e));
				return null;
			}

			Scene scene;

			try {
				scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
			} catch (Exception e) {
				Logger.LogError(new Exception($"Could not open the world copy '{path}'", e));
				AssetDatabase.DeleteAsset(path);
				return null;
			}

			if (!scene.IsValid() || !scene.isLoaded) {
				Logger.LogError($"Could not open the world copy '{path}'.");
				AssetDatabase.DeleteAsset(path);
				return null;
			}

			var descriptor = FindDescriptor(scene, source);

			if (!descriptor) {
				Logger.LogError($"No world descriptor found in the world copy '{path}'.");
				EditorSceneManager.CloseScene(scene, true);
				AssetDatabase.DeleteAsset(path);
				return null;
			}

			return new WorldCopy(descriptor, scene, path, folder, sourceScene);
		}

		/// <summary>
		/// Écrit la copie compilée sur disque : c'est cette version qui est bundlée. Sans effet quand la
		/// copie est déjà à jour.
		/// </summary>
		public bool Save() {
			if (_disposed || !Scene.IsValid() || !Scene.isLoaded)
				return false;

			return !Scene.isDirty || EditorSceneManager.SaveScene(Scene);
		}

		/// <summary>
		/// Ferme la copie et supprime la scène temporaire. Sans effet si la copie est déjà libérée, afin
		/// de pouvoir être appelé depuis n'importe quel chemin de sortie du build.
		/// </summary>
		public void Dispose() {
			if (_disposed)
				return;
			_disposed = true;

			if (Scene.IsValid() && Scene.isLoaded) {
				if (Scene.isDirty)
					EditorSceneManager.SaveScene(Scene);
				EditorSceneManager.CloseScene(Scene, true);

				// Rendre la scène d'origine active : la copie l'a remplacée en s'ouvrant
				if (_sourceScene.IsValid() && _sourceScene.isLoaded)
					SceneManager.SetActiveScene(_sourceScene);
			}

			if (!string.IsNullOrEmpty(ScenePath))
				AssetDatabase.DeleteAsset(ScenePath);

			// Le dossier temporaire n'accueillait que la copie (et son .meta)
			if (!string.IsNullOrEmpty(_folder)
				&& AssetDatabase.IsValidFolder(_folder)
				&& !Directory.EnumerateFileSystemEntries(_folder).Any())
				AssetDatabase.DeleteAsset(_folder);

			AssetDatabase.Refresh();
		}

		/// <summary>
		/// Descriptor de la copie, retrouvé par son chemin dans la hiérarchie pour rester le même objet
		/// logique que celui de la scène d'origine.
		/// </summary>
		private static WorldDescriptor FindDescriptor(Scene scene, WorldDescriptor source) {
			var candidates = scene.GetRootGameObjects()
				.SelectMany(root => root.GetComponentsInChildren<WorldDescriptor>(true))
				.ToArray();

			var path = HierarchyPath(source.transform);

			return candidates.FirstOrDefault(candidate => HierarchyPath(candidate.transform) == path)
				?? candidates.FirstOrDefault();
		}

		private static string HierarchyPath(Transform transform) {
			var path = transform.name;
			for (var parent = transform.parent; parent; parent = parent.parent)
				path = parent.name + "/" + path;
			return path;
		}
	}
}
