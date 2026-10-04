namespace Nox.Worlds {
	/// <summary>
	/// Body of <c>PATCH /worlds/{world}</c>: only the fields that are set are sent, so an unset
	/// value leaves the current one untouched.
	/// </summary>
	public interface IUpdateWorldRequest {
		/// <summary>New title: empty leaves it untouched, <c>null</c> clears it.</summary>
		string Title { get; set; }

		/// <summary>New description: empty leaves it untouched, <c>null</c> clears it.</summary>
		string Description { get; set; }

		/// <summary>
		/// New capacity: <see cref="ushort.MaxValue"/> (the default) leaves it untouched,
		/// <c>0</c> makes the world unlimited, any other value sets it.
		/// </summary>
		ushort Capacity { get; set; }
	}
}