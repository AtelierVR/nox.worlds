using Newtonsoft.Json.Linq;
using Nox.CCK.Utils;

namespace Nox.Worlds.Runtime.Network {
	public struct CreateWorldRequest : INoxObject, ICreateWorldRequest {
		public uint Id { get; set; }
		public string Title { get; set; }
		public string Description { get; set; }
		public ushort Capacity { get; set; }
		public string Thumbnail { get; set; }

		public JObject ToJson() {
			var obj = new JObject();

			if (Id > 0) obj["id"] = Id;

			if (!string.IsNullOrEmpty(Title))
				obj["title"] = Title;

			if (!string.IsNullOrEmpty(Description))
				obj["description"] = Description;

			obj["capacity"] = Capacity;

			if (!string.IsNullOrEmpty(Thumbnail))
				obj["thumbnail"] = Thumbnail;

			return obj;
		}
		public static CreateWorldRequest From(ICreateWorldRequest data)
			=> new CreateWorldRequest {
				Id = data.Id,
				Title = data.Title,
				Description = data.Description,
				Capacity = data.Capacity,
				Thumbnail = data.Thumbnail
			};
	}
}