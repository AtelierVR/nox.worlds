using System;
using Newtonsoft.Json;

namespace Nox.Worlds.Runtime.Network {
	/// <summary>
	/// Represents a release version that can be either a simple number (non-privileged)
	/// or an object with auto-detection metadata (privileged: owner/contributor).
	/// </summary>
	[Serializable]
	public class Release : IRelease {
		/// <summary>
		/// The release version number. -1 means none available.
		/// </summary>
		[JsonProperty("value")]
		public ushort Value { get; private set; }

		/// <summary>
		/// Whether the release version was auto-detected (latest available).
		/// </summary>
		[JsonProperty("auto")]
		public bool Auto { get; private set; }

		public Release(ushort value, bool auto = false) {
			Value = value;
			Auto  = auto;
		}

		public override string ToString()
			=> $"{GetType().Name}[value={Value}, auto={Auto}]";
	}
}
