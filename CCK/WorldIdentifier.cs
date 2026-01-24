using System;
using System.Collections.Generic;
using System.Linq;
using Nox.Worlds;

namespace Nox.CCK.Worlds {
	public readonly struct WorldIdentifier : IWorldIdentifier, IEquatable<WorldIdentifier> {
		public static WorldIdentifier Invalid
			=> new(InvalidId);

		public const uint   InvalidId      = 0;
		public const string LocalServer    = "::";
		public const ushort DefaultVersion = ushort.MaxValue;
		public const string VersionKey     = "v";
		public const string PasswordKey    = "p";
		public const string HashKey        = "h";

		public uint   Id     { get; }
		public string Server { get; }

		private readonly Dictionary<string, string[]> _metadata;

		public IReadOnlyDictionary<string, string[]> Metadata
			=> _metadata;

		public WorldIdentifier(uint id, IReadOnlyDictionary<string, string[]> meta, string server = LocalServer)
			: this(id, meta?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), server) { }

		public WorldIdentifier(uint id, Dictionary<string, string[]> meta = null, string server = LocalServer) {
			Id        = id;
			Server    = server;
			_metadata = meta ?? new Dictionary<string, string[]>();
		}

		public bool IsValid
			=> Id != InvalidId;

		public bool IsLocal
			=> Server == LocalServer || string.IsNullOrEmpty(Server);

		public string ToString(string fallbackServer)
			=> $"{Id.ToString()}{(IsLocal ? string.IsNullOrEmpty(fallbackServer) ? "" : "@" + fallbackServer : "@" + Server)}";


		public static WorldIdentifier From(IWorldIdentifier identifier) {
			if (identifier == null) return Invalid;
			return new WorldIdentifier(
				identifier.Id,
				identifier.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
				identifier.Server
			);
		}

		public static WorldIdentifier From(string identifier) {
			if (string.IsNullOrEmpty(identifier))
				return Invalid;

			var parts = identifier.Split('@');
			switch (parts.Length) {
				case > 2:
					return Invalid;
				case 1:
					parts = new[] { parts[0], null };
					break;
			}

			if (string.IsNullOrEmpty(parts[1]))
				parts[1] = LocalServer;

			var split = parts[0].Split('?');
			if (split.Length > 2)
				return Invalid;
			var idPart = split[0];
			if (!uint.TryParse(idPart, out var id))
				return Invalid;
			var metadata = new Dictionary<string, string[]>();
			if (split.Length != 2)
				return new WorldIdentifier(id, metadata, parts[1]);

			var metaParts = split[1].Split('&');
			foreach (var part in metaParts) {
				var metaSplit = part.Split('=');
				if (metaSplit.Length < 1)
					continue;
				var key   = metaSplit[0];
				var value = metaSplit.Length > 1 ? string.Join("=", metaSplit, 1, metaSplit.Length - 1) : null;
				if (string.IsNullOrEmpty(key))
					continue;
				if (metadata.TryGetValue(key, out var values)) {
					var newValues = new string[values.Length + 1];
					values.CopyTo(newValues, 0);
					newValues[^1] = value;
					metadata[key] = newValues;
				} else metadata[key] = new[] { value };
			}

			return new WorldIdentifier(id, metadata, parts[1]);
		}

		public ushort Version
			=> _metadata.TryGetValue(VersionKey, out var versions) && versions.Length > 0 && ushort.TryParse(versions[0], out var version)
				? version
				: DefaultVersion;


		public string Password
			=> _metadata.TryGetValue(PasswordKey, out var passwords) && !string.IsNullOrEmpty(passwords[0])
				? passwords[0]
				: null;

		public string Hash
			=> _metadata.TryGetValue(HashKey, out var hashes) && !string.IsNullOrEmpty(hashes[0])
				? hashes[0]
				: null;

		/// <summary>
		/// Implicit conversion to string
		/// </summary>
		/// <param name="identifier"></param>
		/// <returns></returns>
		public static implicit operator string(WorldIdentifier identifier)
			=> identifier.ToString();

		/// <summary>
		/// Implicit conversion from string
		/// </summary>
		/// <param name="identifier"></param>
		/// <returns></returns>
		public static implicit operator WorldIdentifier(string identifier)
			=> From(identifier);

		public override string ToString()
			=> ToString(null);

		public bool Equals(WorldIdentifier other)
			=> Id         == other.Id
				&& Server == other.Server
				&& (Version == DefaultVersion || other.Version == DefaultVersion || Version == other.Version);

		public override bool Equals(object obj)
			=> obj is WorldIdentifier other && Equals(other);

		public override int GetHashCode()
			=> HashCode.Combine(Id, Server, Version);
	}
}