namespace Nox.Worlds {
	/// <summary>
	/// Body of <c>PUT /worlds</c>: the generic asset fields a world needs on creation, plus the
	/// world-only <see cref="Capacity"/>.
	/// </summary>
	public interface ICreateWorldRequest {
		/// <summary>Custom numeric id; <c>0</c> (the default) lets the server allocate one.</summary>
		uint Id { get; set; }

		/// <summary>Display title. Defaults to the name when omitted.</summary>
		string Title { get; set; }

		/// <summary>Description, or <c>null</c>.</summary>
		string Description { get; set; }

		/// <summary>
		/// Maximum number of concurrent players. <c>0</c> (the default) leaves it to the server,
		/// which falls back to <c>32</c>.
		/// </summary>
		ushort Capacity { get; set; }
	}
}