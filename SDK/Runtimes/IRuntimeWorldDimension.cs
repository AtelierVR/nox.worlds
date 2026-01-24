using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nox.Worlds {
	public interface IRuntimeWorldDimension {
		/// <summary>
		/// Returns the Unity scene associated with this IScene.
		/// </summary>
		/// <returns></returns>
		public Scene GetScene();

		/// <summary>
		/// Returns the ID of the scene in the world.
		/// </summary>
		/// <returns></returns>
		public UniTask<int> MakeInstance();

		/// <summary>
		/// Returns the scene instance descriptor for the given ID.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public IWorldDescriptor GetDescriptor(int id);

		/// <summary>
		/// Returns the anchor GameObject of the scene instance for the given ID.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public GameObject GetAnchor(int id);

		/// <summary>
		/// Hide or show the scene in the world.
		/// Make immediately the setting when the world is current.
		/// </summary>
		/// <param name="id">The ID of the instance in the world.</param>
		/// <param name="active"></param>
		/// <param name="save">If true, the visibility will be saved in the world.</param>
		public void SetVisibleInstance(int id, bool active, bool save);

		/// <summary>
		/// Returns the IDs of all visible instances in the world.
		/// </summary>
		/// <returns></returns>
		public int[] GetInstanceIds();

		/// <summary>
		/// Removes the scene instance from the world.
		/// </summary>
		/// <param name="id"></param>
		public void RemoveInstance(int id);

		/// <summary>
		/// Checks if the scene is active in the world.
		/// </summary>
		/// <returns></returns>
		public bool IsVisibleInstance(int id);
	}
}