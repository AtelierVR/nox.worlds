using UnityEngine;

namespace Nox.Worlds {
	/// <summary>
	/// Describes a world and provides access to its modules.
	/// </summary>
	public interface IWorldDescriptor {
		/// <summary>
		/// Base GameObject that anchors the world in the scene.
		/// </summary>
		public GameObject Anchor { get; }

		/// <summary>
		/// Get all modules of type T in the world.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T[] GetModules<T>() where T : IWorldModule;

		/// <summary>
		/// Get all modules in the world.
		/// </summary>
		/// <returns></returns>
		public IWorldModule[] GetModules();

		/// <summary>
		/// Refresh and find all modules in the world.
		/// </summary>
		/// <returns></returns>
		public IWorldModule[] FindModules();
	}
}