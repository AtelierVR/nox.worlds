using Nox.Assets.Editor;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Editor.Panel;
using IPanel = Nox.Editor.Panel.IPanel;

namespace Nox.Worlds.Runtime.Editor {
	/// <summary>
	/// Registers the world builder panel. The panel itself is the generic one from
	/// <c>nox.assets</c>; this class only hands it the <see cref="WorldBuilderSource"/>, i.e. the
	/// world's instructions (selection, platforms, build).
	/// </summary>
	public class BuilderPanel : AssetBuilderPanel, IEditorModInitializer, IPanel {
		public static BuilderPanel Panel { get; private set; }

		internal IEditorModCoreAPI API;

		public BuilderPanel() : base(new WorldBuilderSource()) { }

		public void OnInitializeEditor(IEditorModCoreAPI api) {
			API = api;
			Panel = this;
			Source.Initialize(api);
		}

		public void OnDisposeEditor() {
			API = null;
			Panel = null;
		}
	}
}
