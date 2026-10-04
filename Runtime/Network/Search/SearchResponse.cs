using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Nox.CCK.Network.Assets;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Runtime.Network {
	/// <summary>
	/// Paginated result of a world search, filled from the generic collection response
	/// (<c>{ query, total, limit, offset, items }</c>).
	/// </summary>
	public class SearchResponse : ISearchResponse, INoxObject {
		[JsonIgnore]
		public ISearchRequest Request;

		[JsonProperty("items")]
		public World[] Items { get; private set; } = Array.Empty<World>();

		[JsonProperty("total")]
		public uint Total { get; private set; }

		[JsonProperty("limit")]
		public uint Limit { get; private set; }

		[JsonProperty("offset")]
		public uint Offset { get; private set; }

		IWorld[] ISearchResponse.Items
			=> Items.ToArray<IWorld>();

		public bool HasNext()
			=> Offset + Limit < Total;

		public bool HasPrevious()
			=> Offset > 0;

		async UniTask<ISearchResponse> ISearchResponse.Next()
			=> await Next();

		async UniTask<ISearchResponse> ISearchResponse.Previous()
			=> await Previous();

		/// <summary>Takes over the page returned by the asset pipeline.</summary>
		public static SearchResponse From(AssetSearchResponse<World> response)
			=> response == null
				? null
				: new SearchResponse {
					Items  = response.Items,
					Total  = response.Total,
					Limit  = response.Limit,
					Offset = response.Offset
				};

		private UniTask<SearchResponse> Next()
			=> HasNext()
				? Main.Instance.Network.Search(
					new SearchRequest {
						Server = Request.Server,
						Query  = Request.Query,
						Offset = Offset + Limit,
						Limit  = Limit
					}
				)
				: default;

		private UniTask<SearchResponse> Previous()
			=> HasPrevious()
				? Main.Instance.Network.Search(
					new SearchRequest {
						Server = Request.Server,
						Query  = Request.Query,
						Offset = Offset >= Limit ? Offset - Limit : 0,
						Limit  = Limit
					}
				) : default;
	}
}