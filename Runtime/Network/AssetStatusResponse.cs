using System;
using Newtonsoft.Json;

namespace Nox.Worlds.Runtime.Network {
	public class AssetStatusResponse : IAssetStatusResponse {
		[JsonProperty("status")]
		public string InitStatus;

		public AssetStatusType Status
			=> AssetStatusTypeExtensions.FromString(InitStatus);

		[JsonProperty("progress")]
		public uint Progress { get; private set; }

		[JsonProperty("queue_position")]
		public uint QueuePosition { get; private set; }

		[JsonProperty("message")]
		public string Message { get; private set; }

		[JsonProperty("hash")]
		public string Hash { get; private set; }

		[JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
		public long Size { get; private set; }

		[JsonProperty("error")]
		public string Error { get; internal set; }

		[JsonProperty("created_at")]
		public long? InitCreatedAt;

		[JsonProperty("started_at")]
		public long? InitStartedAt;

		[JsonProperty("completed_at")]
		public long? InitCompletedAt;

		public DateTime CreatedAt
			=> InitCreatedAt is >= -62135596800 and <= 253402300799
				? DateTimeOffset.FromUnixTimeSeconds(InitCreatedAt.Value).UtcDateTime
				: default;

		public DateTime StartedAt
			=> InitStartedAt is > 0 and <= 253402300799
				? DateTimeOffset.FromUnixTimeSeconds(InitStartedAt.Value).UtcDateTime
				: default;

		public DateTime CompletedAt
			=> InitCompletedAt is > 0 and <= 253402300799
				? DateTimeOffset.FromUnixTimeSeconds(InitCompletedAt.Value).UtcDateTime
				: default;

		[JsonProperty("next_at")]
		public long? InitNextTryAt;

		public DateTime NextTryAt
			=> InitNextTryAt is > 0 and <= 253402300799
				? DateTimeOffset.FromUnixTimeSeconds(InitNextTryAt.Value).UtcDateTime
				: default;
	}
}