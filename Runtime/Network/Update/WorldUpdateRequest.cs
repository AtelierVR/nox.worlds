using System;
using Newtonsoft.Json;
using Nox.CCK.Convertors;
using Nox.CCK.Network.Assets;

namespace Nox.Worlds.Runtime.Network {
	/// <summary>
	/// Body of <c>PATCH /worlds/{world}</c>: the generic asset fields plus the world-only
	/// <c>capacity</c>. Unset values are omitted, so they keep their current value.
	/// </summary>
	[Serializable]
	public class WorldUpdateRequest : AssetUpdateRequest {
		private ushort _capacity = ushort.MaxValue;

		/// <summary>
		/// New capacity; <see cref="ushort.MaxValue"/> (the default) leaves it untouched.
		/// </summary>
		[JsonProperty("capacity")]
		public ushort Capacity {
			get => _capacity;
			set {
				_capacity = value;
				MarkAssigned("capacity");
			}
		}

		public bool ShouldSerializeCapacity()
			=> IsAssigned("capacity") && _capacity != ushort.MaxValue;

		public static WorldUpdateRequest From(IUpdateWorldRequest data) {
			if (data == null)
				return new WorldUpdateRequest();

			var request = new WorldUpdateRequest();

			// Title: empty = no change, null = clear, other = set
			if (!string.IsNullOrEmpty(data.Title))
				request.Title = data.Title.ToTranslated();
			else if (data.Title == null)
				request.Title = null;

			if (!string.IsNullOrEmpty(data.Description))
				request.Description = data.Description.ToTranslated();
			else if (data.Description == null)
				request.Description = null;

			if (data.Capacity != ushort.MaxValue)
				request.Capacity = data.Capacity;

			return request;
		}
	}
}