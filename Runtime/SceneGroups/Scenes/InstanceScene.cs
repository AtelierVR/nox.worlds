using UnityEngine;

namespace Nox.Worlds.Runtime.SceneGroups.Scenes {
	public class InstanceScene {
		public GameObject Container;
		public IWorldDescriptor Descriptor;
		public bool Visible = false;

		public int GetId()
			=> Descriptor.Anchor.GetEntityId().GetHashCode();
	}
}