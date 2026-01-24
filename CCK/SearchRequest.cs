using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using Nox.Worlds;

namespace Nox.CCK.Worlds {
	public class SearchRequest : ISearchRequest, INoxObject {
		public string Query { get; set; }
		public uint[] Ids { get; set; }
		public uint Offset { get; set; }
		public uint Limit { get; set; }

		public override string ToString() {
			var text = "";
			if (!string.IsNullOrEmpty(Query))
				text += (text.Length > 0 ? "&" : "") + $"query={Query}";
			if (Ids != null)
				text = Ids
					.Aggregate(text, (current, u) => current + (current.Length > 0 ? "&" : "") + $"id={u}");
			if (Offset > 0) text += (text.Length > 0 ? "&" : "") + $"offset={Offset}";
			if (Limit > 0) text += (text.Length > 0 ? "&" : "") + $"limit={Limit}";
			return string.IsNullOrEmpty(text) ? "" : "?" + text;
		}

		public static SearchRequest From(ISearchRequest identifier)
			=> new SearchRequest {
				Query = identifier.Query,
				Ids = identifier.Ids,
				Offset = identifier.Offset,
				Limit = identifier.Limit
			};
	}
}