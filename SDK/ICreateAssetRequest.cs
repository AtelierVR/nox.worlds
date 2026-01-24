namespace Nox.Worlds {
	/// <summary>
	/// Represents a request to create a new asset with specified properties.
	/// </summary>
	public interface ICreateAssetRequest {
		/// <summary>
		/// Get the ID of the asset.
		///
		/// Set the id of the asset.
		/// If the id is 0, the asset will use a generated id.
		/// </summary>
		/// <returns></returns>
		public uint Id { get; set; }

		/// <summary>
		/// Gets the version group of the asset.
		///
		/// Set the version of the asset.
		/// This is used to determine if the asset is up to date.
		/// </summary>
		/// <returns></returns>
		public uint Version { get; set; }

		/// <summary>
		/// Gets the engine of the asset.
		///
		/// Set the engine of the asset.
		/// This is used to determine if the asset is compatible with the current engine.
		/// </summary>
		/// <returns></returns>
		public string Engine { get; set; }

		/// <summary>
		/// Gets the platform of the asset.
		///
		/// Set the platform of the asset.
		/// This is used to determine if the asset is compatible with the current platform.
		/// </summary>
		/// <returns></returns>
		public string Platform { get; set; }

		/// <summary>
		/// Gets the Url of the asset.
		///
		/// Set the URL of the asset.
		/// This is the custom URL where the asset can be downloaded from.
		/// If the URL is not set, the asset will be empty and will not be downloaded.
		/// </summary>
		/// <returns></returns>
		public string Url { get; set; }

		/// <summary>
		/// Gets the hash of the asset.
		///
		/// Set the hash (Sha256) of the asset.
		/// This is used to verify the integrity of the asset.
		/// If it is not set, the asset will be empty and will not be downloaded.
		/// </summary>
		/// <returns></returns>
		public string Hash { get; set; }

		/// <summary>
		/// Gets the size of the asset in bytes.
		///
		/// Set the size of the asset in bytes.
		/// This is used to determine the size of the asset when downloading it.
		/// If it is 0, the asset will be empty and will not be downloaded.
		/// </summary>
		/// <returns></returns>
		public uint Size { get; set; }
	}
}