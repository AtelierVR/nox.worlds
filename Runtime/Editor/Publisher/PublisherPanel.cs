using System;
using System.Collections.Generic;
using Nox.Assets.Editor;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.Editor.Panel;
using IPanel = Nox.Editor.Panel.IPanel;

namespace Nox.Worlds.Runtime.Editor {
	/// <summary>
	/// Registers the world publisher panel. The panel itself is the generic one from
	/// <c>nox.assets</c>; this class only hands it the <see cref="WorldPublisherSource"/>, i.e. the
	/// world's instructions (build, rules, capacity, version).
	/// </summary>
	public class PublisherPanel : AssetPublisherPanel, IEditorModInitializer, IPanel {
		public static PublisherPanel Panel { get; private set; }

		internal IEditorModCoreAPI API;

		public PublisherPanel() : base(new WorldPublisherSource()) { }

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
