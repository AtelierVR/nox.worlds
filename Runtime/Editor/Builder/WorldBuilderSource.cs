using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.Assets.Editor;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Editor.Panel;
using UnityEditor;
using UnityEngine;

namespace Nox.Worlds.Runtime.Editor {
	/// <summary>
	/// What a world declares to the generic builder: the descriptor it builds, its platforms, how to
	/// build it and what to do around the build. The panel itself — selector, notifications,
	/// platforms, output folder, progress and result — comes from <c>nox.assets</c>.
	/// </summary>
	public sealed class WorldBuilderSource : AssetBuilderSource {
		public override string   Panel  => "world";
		public override string   Label  => "World";
		public override string[] Path   => new[] { "world", "builder" };
		public override string   Title  => "World Builder";

		public override Type DescriptorType
			=> typeof(WorldDescriptor);

		public override UnityEngine.Object Selection {
			get => WorldDescriptorHelper.CurrentWorld;
			set {
				if (value is WorldDescriptor descriptor)
					WorldDescriptorHelper.SetCurrentWorld(descriptor);
			}
		}

		public override UnityEngine.Object[] Candidates()
			=> WorldDescriptorHelper.Candidates();

		public override string Describe(UnityEngine.Object candidate)
			=> candidate is WorldDescriptor descriptor && !string.IsNullOrEmpty(descriptor.gameObject.scene.name)
				? $"{descriptor.name} ({descriptor.gameObject.scene.name})"
				: base.Describe(candidate);

		public override Platform[] Platforms {
			get => WorldDescriptorHelper.CurrentWorld?.Targets ?? Array.Empty<Platform>();
			set {
				var live = WorldDescriptorHelper.Live(WorldDescriptorHelper.CurrentWorld);

				if (!live)
					return;

				live.Targets = value;
				EditorUtility.SetDirty(live);
			}
		}

		public override string DefaultOutputFolder
			=> "Assets/WorldBuilds/";

		public override Func<AssetBuildContext, UniTask<AssetBuildResult>> Build
			=> BuildAsync;

		/// <summary>Rebinds and saves the scene: the build pipeline refuses unsaved scenes.</summary>
		public override Func<string> Prepare
			=> () => {
				WorldDescriptorHelper.Rebind();

				var world = WorldDescriptorHelper.CurrentWorld;

				if (!world)
					return "No world descriptor to build.";

				if (!WorldDescriptorHelper.SaveScene(world))
					return "The world scene has unsaved changes and could not be saved. Save it and build again.";

				return null;
			};

		/// <summary>Called after a build: the scenes may have been reloaded.</summary>
		public override Action OnBuildFinished
			=> WorldDescriptorHelper.Rebind;

		/// <summary>Opens the publisher panel, as the previous builder did.</summary>
		public override IToolOption[] Options(IWindow window)
			=> new IToolOption[] {
				new DefaultToolOption("Publisher", () => {
					// The publisher panel is ours: use its static reference rather than asking the ModAPI for
					// our own mod (which logs an unrelated-module warning).
					var panel = PublisherPanel.Panel;

					if (panel == null)
						return;

					window.SetActive(panel);
					window.Repaint();
				})
			};

		/// <summary>Builds one bundle per platform of the descriptor.</summary>
		private static async UniTask<AssetBuildResult> BuildAsync(AssetBuildContext context) {
			if (context?.State is not WorldDescriptor descriptor)
				return AssetBuildResult.Failed("No world descriptor to build.");

			var build = await Nox.Worlds.Pipeline.Builder.Build(new Nox.Worlds.Pipeline.BuildData {
				Descriptor       = descriptor,
				OutputPath       = context.OutputPath,
				ProgressCallback = context.Report
			});

			if (build == null)
				return AssetBuildResult.Failed("The world build returned no result.");

			if (build.IsFailed)
				return AssetBuildResult.Failed(build.Message);

			var outputs = new Dictionary<Platform, string>();

			foreach (var output in build.Outputs)
				if (!string.IsNullOrEmpty(output.Path))
					outputs[output.Platform] = output.Path;

			return AssetBuildResult.Ok(outputs);
		}
	}
}
