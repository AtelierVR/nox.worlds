using Nox.CCK.Utils;

namespace Nox.Worlds {
	/// <summary>
	/// Represents a collection of favorite items,
	/// each with a label and an array of identifier values.
	/// </summary>
	public interface IFavorites {
		/// <summary>
		/// Gets the label associated with this collection of favorites.
		/// </summary>
		public string Label { get; }
		
		/// <summary>
		/// Gets the array of identifier values representing the favorites in this collection.
		/// </summary>
		public Identifier[] Values { get; }
	}
}