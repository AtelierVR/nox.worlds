using Nox.Worlds.Runtime.Network;
using Cysharp.Threading.Tasks;
using Nox.CCK.Worlds;
using Nox.Search;

namespace Nox.Worlds.Runtime.Search {
	public class SearchWorker : IWorker {
		public string Title;
		public string Server;

		public string[] TitleArguments
			=> new[] { Title };

		public float Ratio
			=> 4f / 3f;

		public async UniTask<IResult> Fetch(IFetchOptions options) {
			if (string.IsNullOrEmpty(Server))
				return new SearchResult { Error = "Invalid server address." };
			var data = await Main.Instance.Network.Search(
				new SearchRequest {
					Server = Server,
					Query  = options.Query,
					Offset = options.Page * options.Limit,
					Limit  = options.Limit,
				}
			);
			if (data == null)
				return new SearchResult { Error = "Error fetching users." };
			return new SearchResult {
				Response = data,
				Error    = null
			};
		}
	}
}