using System;
using System.Linq;
using Nox.Search;
using Nox.Worlds.Runtime.Network;

namespace Nox.Worlds.Runtime.Search {
	public class SearchResult : IResult {
		public string Error { get; internal set; }

		public SearchResponse Response;

		public bool IsError
			=> !string.IsNullOrEmpty(Error);

		public bool HasNext()
			=> !IsError && Response.HasNext();

		public IResultData[] Data
			=> Response != null
				? Response.Worlds
					.Select(x => new SearchData { Reference = x })
					.Cast<IResultData>()
					.ToArray()
				: Array.Empty<IResultData>();
	}
}