namespace Nox.Worlds {
	/// <summary>
	/// Represents a request to search for worlds based on specific criteria.
	/// </summary>
	public interface ISearchRequest {
		/// <summary>
		/// The search query string.
		/// This can include keywords, phrases, or specific terms to filter the search results.
		/// </summary>
		public string Query { get; set; }

		/// <summary>
		/// An array of world IDs to specifically include in the search results.
		/// If provided, the search will return only the worlds matching these IDs.
		/// If empty or null, the search will consider all available worlds.
		/// </summary>
		public uint[] Ids { get; set; }

		/// <summary>
		/// The offset for pagination.
		/// This indicates the number of items to skip before starting to collect the result set.
		/// </summary>
		public uint Offset { get; set; }

		/// <summary>
		/// The maximum number of results to return.
		/// This limits the size of the result set to the specified number.
		/// </summary>
		public uint Limit { get; set; }
	}
}