using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Nox.CCK.Utils;
using Nox.Search;
using UnityEngine;

namespace Nox.Worlds.Runtime.Search {
	public class SearchHandler : IHandler {
		public string GetId()
			=> Main.Instance.CoreAPI.ModMetadata.GetId();

		public string GetTitleKey()
			=> "world.search.title";

		public string[] GetTitleArguments()
			=> Array.Empty<string>();

		public string GetPlaceholderKey()
			=> "world.search.placeholder";

		public string[] GetPlaceholderArguments()
			=> Array.Empty<string>();

		public Texture2D GetIcon()
			=> Main.Instance.CoreAPI.AssetAPI
				.GetAsset<Texture2D>("ui:icons/globe.png");

		public string GetDescriptionKey()
			=> "avatar.search.description";

		public string[] GetDescriptionArguments()
			=> Array.Empty<string>();

		public IWorker[] GetWorkers() {
			var x0 = Config.Load().Get("servers");
			if (x0 == null) return Array.Empty<IWorker>();
			var x1 = x0.ToObject<Dictionary<string, JObject>>();
			var x2 = new List<IWorker>();
			foreach (var (address, value) in x1) {
				var title    = value["title"]?.ToString();
				var features = value["features"]?.Values<string>().ToArray() ?? Array.Empty<string>();
				var search   = value["search"]?.ToObject<bool>()             ?? false;
				if (!(search && features.Contains("world"))) continue;
				x2.Add(new SearchWorker { Title = title, Server = address });
			}

			return x2.ToArray();
		}
	}
}