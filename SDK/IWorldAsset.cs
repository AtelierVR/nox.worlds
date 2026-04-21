using Nox.CCK.Utils;

namespace Nox.Worlds {
	public interface IWorldAsset {
		/// <summary>
		/// Gets the unique identifier of the asset.
		/// </summary>
		/// <returns></returns>
		public uint Id { get; }

		/// <summary>
		/// Gets the version group of the asset.
		/// Is used to get the group of assets compatible across different platforms and engines.
		/// </summary>
		/// <returns></returns>
		public ushort Version { get; }

		/// <summary>
		/// Gets the engine of the asset.
		/// Is a string but you can <see cref="Nox.CCK.Utils.Engine"/> for more information.
		/// </summary>
		/// <returns></returns>
		public string Engine { get; }

		/// <summary>
		/// Gets the platform of the asset.
		/// Is a string but you can <see cref="Nox.CCK.Utils.Platform"/> for more information.
		/// </summary>
		/// <returns></returns>
		public string Platform { get; }

		/// <summary>
		/// Indicates if the asset is empty.
		/// </summary>
		/// <returns></returns>
		public bool IsEmpty { get; }

		/// <summary>
		/// Gets the URL of the asset.
		/// The value can be null if the asset is empty.
		/// </summary>
		/// <returns></returns>
		public string Url { get; }

		/// <summary>
		/// Gets the hash of the asset.
		/// This is used to verify the integrity of the asset.
		/// The value can be null if the asset is empty.
		/// </summary>
		/// <returns></returns>
		public string Hash { get; }

		/// <summary>
		/// Gets the size of the asset.
		/// The value can be zero if the asset is empty.
		/// </summary>
		/// <returns></returns>
		public uint Size { get; }

		/// <summary>
		/// List of mods associated with this asset.
		/// Is used to identify the mods which are required to run the world.
		/// </summary>
		/// <returns></returns>
		public string[] Mods { get; }

		/// <summary>
		/// List of features associated with this asset.
		/// For example, if is a world for BasisVR, it will have the "basis" feature and require BasisVR integration.
		/// </summary>
		/// <returns></returns>
		public string[] Features { get; }

		/// <summary>
		/// Gets the identifier of the user who uploaded the asset.
		/// </summary>
		public Identifier Uploader { get; }
	}
}