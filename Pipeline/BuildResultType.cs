using System;

namespace Nox.Worlds.Pipeline {
	[Flags]
	public enum BuildResultType {
		Success,
		AlreadyBuilding,
		EditorCompiling,
		EditorPlaying,
		UnsupportedTarget,
		InvalidTarget,
		InvalidScenes,
		Failed = AlreadyBuilding | EditorCompiling | EditorPlaying,
	}
}