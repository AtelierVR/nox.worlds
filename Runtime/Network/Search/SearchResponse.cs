using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Runtime.Network {
	public class SearchResponse : ISearchResponse, INoxObject {
		public string Server;

		[JsonProperty("query")]
		public string Query { get; private set; }

		[JsonProperty("ids")]
		public uint[] Ids { get; private set; }

		[JsonProperty("worlds")]
		public World[] Worlds { get; private set; }

		[JsonProperty("total")]
		public uint Total { get; private set; }

		[JsonProperty("limit")]
		public uint Limit { get; private set; }

		[JsonProperty("offset")]
		public uint Offset { get; private set; }

		IWorld[] ISearchResponse.Worlds
			=> Worlds.ToArray<IWorld>();

		public bool HasNext()
			=> Offset + Limit < Total;

		public bool HasPrevious()
			=> Offset > 0;

		async UniTask<ISearchResponse> ISearchResponse.Next()
			=> await Next();

		async UniTask<ISearchResponse> ISearchResponse.Previous()
			=> await Previous();

		private UniTask<SearchResponse> Next()
			=> HasNext()
				? Main.Instance.Network.Search(
					new SearchRequest {
						Query = Query,
						Ids = Ids,
						Offset = Offset + Limit,
						Limit = Limit
					},
					Server
				)
				: default;

		private UniTask<SearchResponse> Previous()
			=> HasPrevious()
				? Main.Instance.Network.Search(
					new SearchRequest {
						Query = Query,
						Ids = Ids,
						Offset = Offset >= Limit ? Offset - Limit : 0,
						Limit = Limit
					},
					Server
				) : default;
	}
}