namespace Nox.Worlds {
	public interface IUploadAssetResponse : IAssetStatusResponse {
		public bool Success { get; }
	}
}