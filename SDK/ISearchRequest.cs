namespace Nox.Worlds {
	/// <summary>
	/// Represents a request to search for worlds based on specific criteria.
	/// </summary>
	public interface ISearchRequest {
		/// <summary>
		/// Specifies the server to search for worlds.
		/// If null or empty, the search will be performed on the current server.
		/// </summary>
		public string Server { get; set; }
		
		/// <summary>
		/// The search query string.
		/// This can include keywords, phrases, or specific terms to filter the search results.
		/// </summary>
		public string Query { get; set; }

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