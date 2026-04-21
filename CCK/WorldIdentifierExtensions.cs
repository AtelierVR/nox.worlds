using Nox.CCK.Utils;
namespace Nox.CCK.Worlds {
	public static class WorldIdentifierExtensions {
		public const string WorldType = "w";

		public const string VersionQuery = "v";

		public const string HashQuery = "h";

		public const string PasswordQuery = "p";

		public const ushort DefaultVersion = ushort.MaxValue;

		public static ushort GetVersion(this Identifier identifier)
			=> identifier.IsValid()
				&& identifier.Query.TryGetValue(VersionQuery, out var v)
				&& v.Length > 0
				&& ushort.TryParse(v[0], out var version)
					? version
					: DefaultVersion;
	}
}