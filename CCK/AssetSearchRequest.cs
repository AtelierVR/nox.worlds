using System.Linq;
using Nox.CCK.Utils;
using Nox.Worlds;

namespace Nox.CCK.Worlds {
	public class AssetSearchRequest : INoxObject, IAssetSearchRequest {
		public uint Offset { get; set; }
		public uint Limit { get; set; }
		public bool ShowEmpty { get; set; }
		public ushort[] Versions { get; set; }
		public string[] Engines { get; set; }
		public string[] Platforms { get; set; }

		public override string ToString() {
			var text = "";
			if (Offset > 0) text += (text.Length > 0 ? "&" : "") + $"offset={Offset}";
			if (Limit > 0) text += (text.Length > 0 ? "&" : "") + $"limit={Limit}";
			if (ShowEmpty) text += (text.Length > 0 ? "&" : "") + "empty";
			if (Versions != null)
				foreach (var v in Versions.Where(v => v != ushort.MaxValue))
					text += (text.Length > 0 ? "&" : "") + $"version={v}";
			if (Engines != null)
				foreach (var e in Engines)
					text += (text.Length > 0 ? "&" : "") + $"engine={e}";
			if (Platforms != null)
				foreach (var p in Platforms)
					text += (text.Length > 0 ? "&" : "") + $"platform={p}";
			return string.IsNullOrEmpty(text) ? "" : "?" + text;
		}
		
		public static AssetSearchRequest From(IAssetSearchRequest data)
			=> new AssetSearchRequest {
				Offset = data.Offset,
				Limit = data.Limit,
				ShowEmpty = data.ShowEmpty,
				Versions = data.Versions,
				Engines = data.Engines,
				Platforms = data.Platforms
			};
	}
}