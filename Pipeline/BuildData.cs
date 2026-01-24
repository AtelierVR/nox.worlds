using System;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;

namespace Nox.Worlds.Pipeline {
	public class BuildData {
		public WorldDescriptor      Descriptor;
		public bool                  ShowDialog = false;
		public string                OutputPath;
		public Platform              Target;
		public string                Filename;
		public string                TempPath;
		public Action<float, string> ProgressCallback = (_, _) => { };
	}
}