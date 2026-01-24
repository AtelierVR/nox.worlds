using System;
using System.Collections.Generic;
using System.Linq;
using Nox.Worlds;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nox.CCK.Worlds {
	public static class WorldDescriptorExtension {

		#if UNITY_EDITOR
		[MenuItem("Nox/Worlds/Make Main Scene Descriptor")]
		public static void MakeMainSceneDescriptor() {
			var selectedObjects = Selection.gameObjects;
			var descriptor = selectedObjects.Length > 0
				? MakeSceneDescriptor<WorldDescriptor>(selectedObjects[0])
				: MakeSceneDescriptor<WorldDescriptor>(SceneManager.GetActiveScene());
			if (descriptor) {
				Selection.activeGameObject = descriptor.gameObject;
				EditorGUIUtility.PingObject(descriptor.gameObject);
			} else
				EditorUtility.DisplayDialog(
					"Error",
					"Failed to create MainSceneDescriptor. Please ensure you have a valid scene selected.",
					"OK"
				);
		}
		#endif

		public static T MakeSceneDescriptor<T>(GameObject selected) where T : WorldDescriptor {
			var descriptor = new GameObject(typeof(T).Name);
			descriptor.transform.SetParent(selected.transform, false);
			var desc = descriptor.AddComponent<T>();
			descriptor.transform.localPosition = Vector3.zero;
			descriptor.transform.localRotation = Quaternion.identity;
			descriptor.transform.localScale    = Vector3.one;
			return desc;
		}

		public static T MakeSceneDescriptor<T>(Scene scene) where T : WorldDescriptor {
			if (!scene.IsValid() || !scene.isLoaded) return null;
			var rootObjects = scene.GetRootGameObjects();
			if (rootObjects.Length == 0) return null;
			var desc = MakeSceneDescriptor<T>(rootObjects[0]);
			SceneManager.MoveGameObjectToScene(desc.gameObject, scene);
			return desc;
		}
	}
}