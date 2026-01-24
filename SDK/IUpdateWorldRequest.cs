namespace Nox.Worlds {
	/// <summary>
	/// Represents a request to update a world's properties.
	/// </summary>
	public interface IUpdateWorldRequest {
		/// <summary>
		/// Gets the title of the world.
		/// 
		/// Sets the title of the world.
		/// If the title is empty, no change will be made.
		/// If the title is null, the current title will be removed.
		/// Any other value will set the title to the given value.
		/// </summary>
		/// <returns></returns>
		public string Title { get; set; }

		/// <summary>
		/// Gets the description of the world.
		///
		/// Sets the description of the world.
		/// If the description is empty, no change will be made.
		/// If the description is null, the current description will be removed.
		/// Any other value will set the description to the given value.
		/// </summary>
		/// <returns></returns>
		public string Description { get; set; }

		/// <summary>
		/// Gets the capacity of the world.
		///
		/// Sets the capacity of the world.
		/// If the capacity is 0, the capacity will be set as unlimited.
		/// If the capacity is <see cref="ushort.MaxValue"/>, no change will be made.
		/// Any other value will set the capacity to the given value.
		/// </summary>
		/// <returns></returns>
		public ushort Capacity { get; set; }

		/// <summary>
		/// Gets the thumbnail of the world.
		///
		/// Sets the thumbnail for the world.
		/// If the thumbnail is empty, no change will be made.
		/// If the thumbnail is null, the current thumbnail will be removed.
		/// Any other value will set the thumbnail to the given value.
		/// </summary>
		/// <returns></returns>
		public string Thumbnail { get; set; }
	}
}