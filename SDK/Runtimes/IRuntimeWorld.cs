using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.CCK.Utils;

namespace Nox.Worlds {
	/// <summary>
	/// Represents the world in which all scenes are loaded.
	/// </summary>
	public interface IRuntimeWorld {
		/// <summary>
		/// Returns the world identifier.
		/// </summary>
		/// <returns></returns>
		public Identifier Identifier { get; set; }

		/// <summary>
		/// Returns all loaded scenes in the world.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<IRuntimeWorldDimension> Dimensions { get; }
		
		/// <summary>
		/// Returns the scene at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public IRuntimeWorldDimension GetDimension(int index);

		/// <summary>
		/// Checks if the world is current.
		/// </summary>
		/// <returns></returns>
		public bool IsCurrent { get; set; }

		/// <summary>
		/// Disposes the world and unloads all scenes.
		/// </summary>
		/// <returns></returns>
		public UniTask Dispose();
	}
}