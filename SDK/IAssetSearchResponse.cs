using Cysharp.Threading.Tasks;

namespace Nox.Worlds {
	/// <summary>
	/// Interface representing the response from an asset search query.
	/// </summary>
	public interface IAssetSearchResponse {
		/// <summary>
		/// Gets the total number of assets that match the search criteria.
		/// </summary>
		/// <returns></returns>
		public uint Total { get; }

		/// <summary>
		/// Gets the limit of assets returned in this response.
		/// </summary>
		/// <returns></returns>
		public uint Limit { get; }

		/// <summary>
		/// Gets the offset of the assets returned in this response.
		/// </summary>
		/// <returns></returns>
		public uint Offset { get; }

		/// <summary>
		/// Gets the assets that match the search criteria at the specified offset and limit.
		/// </summary>
		/// <returns></returns>
		public IWorldAsset[] Items { get; }

		/// <summary>
		/// Determines if there is a previous page of results.
		/// </summary>
		/// <returns></returns>
		public bool HasPrevious();
		
		/// <summary>
		/// Determines if there is a next page of results.
		/// </summary>
		/// <returns></returns>
		public bool HasNext();
		
		/// <summary>
		/// Gets the previous page of results.
		/// </summary>
		/// <returns></returns>
		public UniTask<IAssetSearchResponse> Previous();
		
		/// <summary>
		/// Gets the next page of results.
		/// </summary>
		/// <returns></returns>
		public UniTask<IAssetSearchResponse> Next();
	}
}