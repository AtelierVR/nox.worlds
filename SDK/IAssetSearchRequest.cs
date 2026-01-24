namespace Nox.Worlds {
	/// <summary>
	/// Represents a request for searching assets with various filtering options.
	/// </summary>
	public interface IAssetSearchRequest {
		/// <summary>
		/// Gets the offset for the search results.
		/// 
		/// Set the offset for the search results.
		/// Zero means no offset.
		/// </summary>
		/// <returns></returns>
		public uint Offset { get; set; }

		/// <summary>
		/// Gets the limit for the search results.
		///
		/// Set the limit for the search results.
		/// The minimum is 1 and the maximum is defined by the server.
		/// </summary>
		/// <returns></returns>
		public uint Limit { get; set; }

		/// <summary>
		/// Gets if the search results should include empty assets.
		///
		/// Set if you want to search for assets that are empty.
		/// </summary>
		/// <returns></returns>
		public bool ShowEmpty { get; set; }

		/// <summary>
		/// Gets the versions to filter the search results.
		///
		/// Set the versions to filter the search results.
		/// </summary>
		/// <returns></returns>
		public ushort[] Versions { get; set; }

		/// <summary>
		/// Gets the engines to filter the search results.
		///
		/// Set the engines to filter the search results.
		/// See <see cref="Nox.CCK.Utils.Engine"/> for more information.
		/// </summary>
		/// <returns></returns>
		public string[] Engines { get; set; }

		/// <summary>
		/// Gets the platforms to filter the search results.
		///
		/// Set the platforms to filter the search results.
		/// See <see cref="Nox.CCK.Utils.Platform"/> for more information.
		/// </summary>
		/// <returns></returns>
		public string[] Platforms { get; set; }
	}
}