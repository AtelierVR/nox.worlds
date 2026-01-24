using Newtonsoft.Json.Linq;
using Nox.CCK.Utils;

namespace Nox.Worlds.Runtime.Network {
	public class UpdateWorldRequest : INoxObject, IUpdateWorldRequest {
		public string Title { get; set; } = "";
		public string Description { get; set; } = "";
		public ushort Capacity { get; set; } = ushort.MaxValue;
		public string Thumbnail { get; set; } = "";

		public JObject ToJson() {
			var obj = new JObject();

			// Title: empty = no change, null = remove, other = set
			if (Title == null)
				obj["title"] = JValue.CreateNull();
			else if (Title.Length > 0)
				obj["title"] = JValue.CreateString(Title);

			// Description: empty = no change, null = remove, other = set
			if (Description == null)
				obj["description"] = JValue.CreateNull();
			else if (Description.Length > 0)
				obj["description"] = JValue.CreateString(Description);

			// Capacity: ushort.MaxValue = no change, 0 = unlimited, other = set
			if (Capacity != ushort.MaxValue)
				obj["capacity"] = (int)Capacity;

			// Thumbnail: empty = no change, null = remove, other = set
			if (Thumbnail == null)
				obj["thumbnail"] = JValue.CreateNull();
			else if (Thumbnail.Length > 0)
				obj["thumbnail"] = JValue.CreateString(Thumbnail);

			return obj;
		}
		public static UpdateWorldRequest From(IUpdateWorldRequest form)
			=> new UpdateWorldRequest {
				Title = form.Title,
				Description = form.Description,
				Capacity = form.Capacity,
				Thumbnail = form.Thumbnail
			};
	}
}