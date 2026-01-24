using Nox.CCK.Worlds;
using UnityEngine.Events;
using UnityEditor;
using UnityEngine;
using System.Linq;
using Logger = Nox.CCK.Utils.Logger;

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
			if (worldDescriptor && worldDescriptor != CurrentWorld)
				SetCurrentWorld(worldDescriptor);
		}

		public static void Find() {
			try {
				if (CurrentWorld?.gameObject.activeInHierarchy ?? false) return;
				var activeWorlds = Object.FindObjectsByType<WorldDescriptor>(FindObjectsSortMode.None)
					.Where(world => world.gameObject.activeInHierarchy)
					.ToArray();
				SetCurrentWorld(activeWorlds.Length > 0 ? activeWorlds[0] : null);
			} catch {
				SetCurrentWorld(null);
			}
		}

		public static void SetCurrentWorld(WorldDescriptor newWorld) {
			if (CurrentWorld == newWorld) return;
			Logger.LogDebug($"Current world changed to {(newWorld ? newWorld.name : "null")}");
			CurrentWorld = newWorld;
			OnWorldSelected?.Invoke(newWorld);
		}
	}
}