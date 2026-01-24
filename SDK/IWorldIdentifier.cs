using System.Collections.Generic;

namespace Nox.Worlds {
	/// <summary>
	/// Represents a world identifier.
	/// <example>1564?v=latest&amp;dev@nox.org</example>
	/// </summary>
	public interface IWorldIdentifier {
		/// <summary>
		/// Indicates if the identifier is valid.
		/// </summary>
		/// <returns></returns>
		public bool IsValid { get; }

		/// <summary>
		/// Indicates if the identifier have a server associated with this identifier.
		/// </summary>
		/// <returns></returns>
		public bool IsLocal { get; }

		/// <summary>
		/// Indicates if the identifier is an ID.
		/// </summary>
		public uint Id { get; }

		/// <summary>
		/// Gets the server address associated with this identifier.
		/// </summary>
		/// <returns></returns>
		public string Server { get; }

		/// <summary>
		/// Gets the version of the asset in the metadata.
		/// If the version is not set, it will return <see cref="ushort.MaxValue"/>.
		/// The key used in the metadata is "v".
		/// </summary>
		/// <returns></returns>
		public ushort Version { get; }

		/// <summary>
		/// Linked with <see cref="Version"/> and <see cref="Hash"/>,
		/// used to load the cryptographic protected worlds.
		/// If no password is set, it will return null.
		/// The key used in the metadata is "p" as Base64.
		/// </summary>
		public string Password { get; }

		/// <summary>
		/// Linked with <see cref="Version"/>,
		/// is used to verify the integrity of the world assets.
		/// If no hash is set, it will return null.
		/// The key used in the metadata is "h" as Base64.
		/// </summary>
		public string Hash { get; }

		/// <summary>
		/// Converts the identifier to a string.
		/// </summary>
		/// <param name="fallbackServer"></param>
		/// <returns></returns>
		public string ToString(string fallbackServer = null);


		/// <summary>
		/// Gets the metadata associated with this identifier.
		/// The metadata is to help to identify a world or associated assets.
		/// </summary>
		/// <returns></returns>
		public IReadOnlyDictionary<string, string[]> Metadata { get; }
	}
}