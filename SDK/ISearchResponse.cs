using Cysharp.Threading.Tasks;

namespace Nox.Worlds {
	/// <summary>
	/// Represents the response from a world search operation.
	/// </summary>
	public interface ISearchResponse {
		/// <summary>
		/// The limit used in the search.
		/// </summary>
		public uint Limit { get; }
		
		/// <summary>
		/// The offset used in the search.
		/// </summary>
		public uint Offset { get; }
		
		/// <summary>
		/// The worlds returned by the search.
		/// </summary>
		public IWorld[] Items { get; }
		
		/// <summary>
		/// The total number of worlds matching the search criteria.
		/// </summary>
		public uint Total { get; }
		
		/// <summary>
		/// Indicates if there is a next page of results.
		/// </summary>
		/// <returns></returns>
		public bool HasNext();
		
		/// <summary>
		/// Indicates if there is a previous page of results.
		/// </summary>
		/// <returns></returns>
		public bool HasPrevious();

		/// <summary>
		/// Retrieves the next page of search results.
		/// </summary>
		/// <returns></returns>
		public UniTask<ISearchResponse> Next();
		
		/// <summary>
		/// Retrieves the previous page of search results.
		/// </summary>
		/// <returns></returns>
		public UniTask<ISearchResponse> Previous();
	}
}