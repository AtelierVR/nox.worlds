using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Runtime.Network {
	public class AssetSearchResponse : IAssetSearchResponse, INoxObject {
		public AssetSearchRequest Request;
		public string Server;
		public Identifier Identifier;

		[JsonProperty("total")]
		public uint Total { get; private set; }

		[JsonProperty("limit")]
		public uint Limit { get; private set; }

		[JsonProperty("offset")]
		public uint Offset { get; private set; }

		[JsonProperty("items")]
		public WorldAsset[] Items { get; private set; }

		IWorldAsset[] IAssetSearchResponse.Items
			=> Items.ToArray<IWorldAsset>();

		public bool HasNext()
			=> Offset + Limit < Total;

		public bool HasPrevious()
			=> Offset > 0;

		async UniTask<IAssetSearchResponse> IAssetSearchResponse.Previous()
			=> await Previous();

		private UniTask<AssetSearchResponse> Previous()
			=> HasNext()
				? Main.Instance.Network.SearchAssets(
					Identifier,
					new AssetSearchRequest {
						Offset = Offset >= Limit ? Offset - Limit : 0,
						Limit = Limit,
						ShowEmpty = Request.ShowEmpty,
						Versions = Request.Versions,
						Engines = Request.Engines,
						Platforms = Request.Platforms
					}
				)
				: default;

		async UniTask<IAssetSearchResponse> IAssetSearchResponse.Next()
			=> await Next();

		private UniTask<AssetSearchResponse> Next()
			=> HasPrevious()
				? Main.Instance.Network.SearchAssets(
					Identifier,
					new AssetSearchRequest {
						Offset = Offset + Limit,
						Limit = Limit,
						ShowEmpty = Request.ShowEmpty,
						Versions = Request.Versions,
						Engines = Request.Engines,
						Platforms = Request.Platforms
					}
				)
				: default;
	}
}