using Newtonsoft.Json;

namespace Nox.Worlds.Runtime.Network {
	public class UploadAssetResponse : AssetStatusResponse, IUploadAssetResponse {
		[JsonProperty("success")]
		public bool Success { get; internal set; }
	}
}