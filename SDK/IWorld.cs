using Nox.Network.Assets;

namespace Nox.Worlds {
	/// <summary>
	/// A world published on a node: the generic asset (<see cref="IAsset"/>) plus the values that
	/// only a world carries. Title, description, owner, contributors, tags, alias, release,
	/// images and dates all come from <see cref="IAsset"/>.
	/// </summary>
	public interface IWorld : IAsset {
		/// <summary>Maximum number of concurrent users allowed in the world.</summary>
		ushort Capacity { get; }
	}
}