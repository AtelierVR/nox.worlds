using Nox.CCK.Worlds;
using Nox.Worlds.Pipeline;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.Linq;
using Logger = Nox.CCK.Utils.Logger;
using Object = UnityEngine.Object;

namespace Nox.Worlds.Runtime.Editor {
	public class WorldDescriptorHelper {
		public static WorldDescriptor CurrentWorld;

		public static readonly UnityEvent<WorldDescriptor> OnWorldSelected = new();

		[InitializeOnLoadMethod]
		private static void Initialize() {
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.hierarchyChanged += Find;
			Find();
		}

		private static void OnSelectionChanged() {
			if (!Selection.activeGameObject) return;
			var worldDescriptor = Selection.activeGameObject.GetComponent<WorldDescriptor>();
			if (!worldDescriptor)
				worldDescriptor = Selection.activeGameObject.GetComponentInParent<WorldDescriptor>();
			if (worldDescriptor && !IsWorkingCopy(worldDescriptor) && worldDescriptor != CurrentWorld)
				SetCurrentWorld(worldDescriptor);
		}

	public static void Find() {
		try {
			// Vérifier si CurrentWorld est null, Missing ou valide et actif
			if (CurrentWorld && CurrentWorld && CurrentWorld.gameObject.activeInHierarchy && !IsWorkingCopy(CurrentWorld)) return;
			var activeWorlds = Object.FindObjectsByType<WorldDescriptor>(FindObjectsInactive.Include)
				.Where(world => world.gameObject.activeInHierarchy)
				.Where(world => !IsWorkingCopy(world))
				.ToArray();
			SetCurrentWorld(activeWorlds.Length > 0 ? activeWorlds[0] : null);
		} catch {
			SetCurrentWorld(null);
		}
	}

		/// <summary>
		/// Re-résout le world courant après une opération qui a pu recharger les scènes ou détruire le
		/// descriptor (build, refresh) et prévient les listeners si nécessaire. Sans effet quand la
		/// référence courante est toujours vivante.
		/// </summary>
		public static void Rebind() {
			var previous = CurrentWorld;

			if (previous && previous.gameObject.activeInHierarchy && !IsWorkingCopy(previous))
				return;

			// Résolution complète : la référence courante ne peut plus servir
			CurrentWorld = null;
			Find();

			// Une référence détruite est « égale » à null : SetCurrentWorld n'aurait rien signalé alors
			// que les panels tiennent encore l'ancienne référence.
			if (CurrentWorld == null && !ReferenceEquals(previous, null))
				OnWorldSelected?.Invoke(null);
		}

		/// <summary>
		/// Écrit sur disque la scène du descriptor quand elle a des modifications en attente. Le
		/// Scriptable Build Pipeline refuse de construire un bundle tant qu'une scène chargée est modifiée
		/// (<c>ReturnCode.UnsavedChanges</c>), et les panneaux écrivent dans le descriptor (version à
		/// publier, plateformes ciblées) juste avant de lancer le build.
		/// </summary>
		/// <returns><c>false</c> quand la scène est modifiée et n'a pas pu être écrite.</returns>
		public static bool SaveScene(WorldDescriptor world) {
			if (!world || !world.gameObject)
				return true;

			var scene = world.gameObject.scene;

			if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty)
				return true;

			if (EditorSceneManager.SaveScene(scene))
				return true;

			Logger.LogError($"Could not save the world scene '{scene.path}' before the build.");
			return false;
		}

		/// <summary>
		/// Une scène de travail du build (<see cref="Builder.TempRoot"/>) est une copie jetable : son
		/// descriptor ne doit jamais devenir le world courant, il disparaît à la fin du build.
		/// </summary>
		private static bool IsWorkingCopy(WorldDescriptor world)
			=> world && world.gameObject.scene.path.StartsWith(Builder.TempRoot, StringComparison.OrdinalIgnoreCase);

		/// <summary>
		/// Descriptor vivant à utiliser après une attente : une recharge de scène détruit la référence
		/// capturée avant l'<c>await</c>, et y écrire lève une <c>MissingReferenceException</c>. Renvoie
		/// <c>null</c> quand plus aucun world n'est utilisable.
		/// </summary>
		public static WorldDescriptor Live(WorldDescriptor fallback) {
			if (CurrentWorld && CurrentWorld.gameObject && !IsWorkingCopy(CurrentWorld))
				return CurrentWorld;

			return fallback && fallback.gameObject && !IsWorkingCopy(fallback) ? fallback : null;
		}

		public static void SetCurrentWorld(WorldDescriptor newWorld) {
			if (CurrentWorld == newWorld) return;
			Logger.LogDebug($"Current world changed to {(newWorld ? newWorld.name : "null")}");
			CurrentWorld = newWorld;
			OnWorldSelected?.Invoke(newWorld);
		}
	}
}