namespace Nox.Worlds {
	/// <summary>
	/// Represents a release version of a world, which can be either a simple number (non-privileged)
	/// or an object with auto-detection metadata (privileged: owner/contributor).
	/// </summary>
	public interface IRelease {
		/// <summary>
		/// The release version number. -1 means none available.
		/// </summary>
		public ushort Value { get; }

		/// <summary>
		/// Whether the release version was auto-detected (latest available).
		/// </summary>
		public bool Auto { get; }
	}
}