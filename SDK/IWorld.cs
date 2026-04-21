using Nox.CCK.Utils;
namespace Nox.Worlds {
	/// <summary>
	/// Represents a virtual world with its properties and metadata.
	/// </summary>
	public interface IWorld {
		/// <summary>
		/// Unique identifier of the world.
		/// </summary>
		public uint Id { get; }

		/// <summary>
		/// Title of the world.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// Description of the world.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// Maximum number of concurrent users allowed in the world.
		/// </summary>
		public ushort Capacity { get; }

		/// <summary>
		/// Tags associated with the world for categorization and searchability.
		/// </summary>
		public string[] Tags { get; }

		/// <summary>
		/// Server address where the world is hosted.
		/// </summary>
		public string Server { get; }

		/// <summary>
		/// Owner of the world.
		/// It can be converted to IUserIdentifier as UId.
		/// </summary>
		public Identifier Owner { get; }

		/// <summary>
		/// Contributors to the world.
		/// It can be converted to IUserIdentifiers as UId.
		/// </summary>
		public Identifier[] Contributors { get; }

		/// <summary>
		/// URL of the thumbnail image representing the world.
		/// </summary>
		public string Thumbnail { get; }

		/// <summary>
		/// Unique world identifier.
		/// </summary>
		public Identifier Identifier { get; }
	}
}