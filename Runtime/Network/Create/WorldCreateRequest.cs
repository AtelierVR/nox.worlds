using System;
using Newtonsoft.Json;
using Nox.CCK.Convertors;
using Nox.CCK.Network.Assets;

namespace Nox.Worlds.Runtime.Network {
	/// <summary>
	/// Body of <c>PUT /worlds</c>: the generic asset fields plus the world-only <c>capacity</c>,
	/// which is omitted (so the server keeps its default) unless the caller set one.
	/// </summary>
	[Serializable]
	public class WorldCreateRequest : AssetCreateRequest {
		private ushort _capacity;

		[JsonProperty("capacity")]
		public ushort Capacity {
			get => _capacity;
			set {
				_capacity = value;
				MarkAssigned("capacity");
			}
		}

		public bool ShouldSerializeCapacity()
			=> IsAssigned("capacity");

		public static WorldCreateRequest From(ICreateWorldRequest data) {
			if (data == null)
				return new WorldCreateRequest();

			var request = new WorldCreateRequest { Id = data.Id };

			// Unset fields are omitted so the server keeps its defaults.
			if (!string.IsNullOrEmpty(data.Title))
				request.Title = data.Title.ToTranslated();

			if (!string.IsNullOrEmpty(data.Description))
				request.Description = data.Description.ToTranslated();

			if (data.Capacity > 0)
				request.Capacity = data.Capacity;

			return request;
		}
	}
}