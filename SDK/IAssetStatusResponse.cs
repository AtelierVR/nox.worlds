using System;
namespace Nox.Worlds {

	public enum AssetStatusType {
		PENDING,
		PROCESSING,
		COMPLETED,
		FAILED,
		EMPTY
	}

	public static class AssetStatusTypeExtensions {
		public static AssetStatusType FromString(string status)
			=> status.ToLower() switch {
				"pending"    => AssetStatusType.PENDING,
				"processing" => AssetStatusType.PROCESSING,
				"completed"  => AssetStatusType.COMPLETED,
				"failed"     => AssetStatusType.FAILED,
				"empty"      => AssetStatusType.EMPTY,
				_            => throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}"),
			};

		public static string ToSerializedString(this AssetStatusType status)
			=> status switch {
				AssetStatusType.PENDING    => "pending",
				AssetStatusType.PROCESSING => "processing",
				AssetStatusType.COMPLETED  => "completed",
				AssetStatusType.FAILED     => "failed",
				AssetStatusType.EMPTY      => "empty",
				_                          => throw new ArgumentOutOfRangeException(nameof(status), $"Not expected status value: {status}"),
			};

	}
	public interface IAssetStatusResponse {
		public AssetStatusType Status { get; }

		public uint Progress { get; }
		public uint QueuePosition { get; }
		public string Message { get; }
		public string Hash { get; }
		public long Size { get; }
		public string Error { get; }

		public DateTime CreatedAt { get; }
		public DateTime StartedAt { get; }
		public DateTime CompletedAt { get; }

		public DateTime NextTryAt { get; }
	}
}