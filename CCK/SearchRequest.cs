using System;
using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using Nox.Worlds;

namespace Nox.CCK.Worlds {
	public class SearchRequest : ISearchRequest, INoxObject {
		public string Server { get; set; } = null;
		
		public string Query { get; set; } = null;

		public uint[] Identifiers { get; set; } = Array.Empty<uint>();

		public uint Offset { get; set; } = 0;

		public uint Limit { get; set; } = 0;

		public override string ToString() {
			var text = "";
			if (!string.IsNullOrEmpty(Query))
				text += (text.Length > 0 ? "&" : "") + $"query={Query}";
			if (Identifiers != null)
				text = Identifiers
					.Aggregate(text, (current, u) => current + (current.Length > 0 ? "&" : "") + $"id={u}");
			if (Offset > 0)
				text += (text.Length > 0 ? "&" : "") + $"offset={Offset}";
			if (Limit > 0)
				text += (text.Length > 0 ? "&" : "") + $"limit={Limit}";
			return string.IsNullOrEmpty(text) ? "" : "?" + text;
		}

		public static SearchRequest From(ISearchRequest identifier)
			=> new() {
				Query       = identifier.Query,
				Identifiers = identifier.Identifiers,
				Offset      = identifier.Offset,
				Limit       = identifier.Limit
			};
	}
}