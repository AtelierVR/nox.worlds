namespace Nox.Worlds {
	/// <summary>
	/// Represents a request to create a new virtual world with specified properties.
	/// </summary>
	public interface ICreateWorldRequest {
		/// <summary>
		/// Get the ID of the world to create.
		/// 
		/// Sets the ID of the world to create.
		/// If the ID is 0, a new ID will be generated.
		/// </summary>
		/// <returns></returns>
		public uint Id { get; set; }

		/// <summary>
		/// Gets the title of the world to create.
		///
		/// Sets the title of the world to create.
		/// If the title is null or empty, a default title will be used.
		/// </summary>
		/// <returns></returns>
		public string Title { get; set; }
		
		/// <summary>
		/// Gets the description of the world to create.
		///
		/// Sets the description of the world to create.
		/// If the description is null or empty, no description will be set.
		/// </summary>
		/// <returns></returns>
		public string Description { get; set; }
		
		/// <summary>
		/// Gets the capacity of the world to create.
		///
		/// Sets the capacity of the world to create.
		/// If the capacity is 0, the capacity will be defaulted to unlimited.
		/// </summary>
		/// <returns></returns>
		public ushort Capacity { get; set; }

		/// <summary>
		/// Gets the thumbnail of the world to create.
		///
		/// Sets the thumbnail of the world to create.
		/// If the thumbnail is null or empty, no thumbnail will be set.
		/// </summary>
		/// <returns></returns>
		public string Thumbnail { get; set; }
	}
}