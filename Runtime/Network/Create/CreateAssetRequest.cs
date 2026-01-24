using Newtonsoft.Json.Linq;
using Nox.CCK.Utils;

namespace Nox.Worlds.Runtime.Network {
	public struct CreateAssetRequest : INoxObject, ICreateAssetRequest {
		public uint Id { get; set; }
		public uint Version { get; set; }
		public string Engine { get; set; }
		public string Platform { get; set; }
		public string Url { get; set; }
		public string Hash { get; set; }
		public uint Size { get; set; }

		public JObject ToJson() {
			var obj = new JObject {
				["version"] = Version,
				["engine"] = Engine,
				["platform"] = Platform
			};

			if (Id > 0) obj["id"] = Id;
			if (Size > 0) obj["size"] = Size;

			if (!string.IsNullOrEmpty(Engine))
				obj["engine"] = Engine;

			if (!string.IsNullOrEmpty(Platform))
				obj["platform"] = Platform;

			if (!string.IsNullOrEmpty(Url))
				obj["url"] = Url;

			if (!string.IsNullOrEmpty(Hash))
				obj["hash"] = Hash;

			return obj;
		}

		public static CreateAssetRequest From(ICreateAssetRequest request)
			=> new CreateAssetRequest {
				Id = request.Id,
				Version = request.Version,
				Engine = request.Engine,
				Platform = request.Platform,
				Url = request.Url,
				Hash = request.Hash,
				Size = request.Size
			};
	}
}