using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.Assets;
using Nox.Assets.Editor;
using Nox.CCK.Convertors;
using Nox.CCK.Network.Assets;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Editor.Panel;
using Nox.Network.Assets;
using Nox.Worlds.Pipeline;
using Nox.Worlds.Runtime.Network;
using UnityEditor;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Editor {
	/// <summary>
	/// What a world declares to the generic publisher: where it lives, how to build it, who may
	/// publish it, the extra value only a world carries (its capacity) and the version stored in the
	/// descriptor. Everything else — the form, the attach flow, the images, the release and the
	/// uploads — comes from <c>nox.assets</c>.
	/// </summary>
	public sealed class WorldPublisherSource : AssetPublisherSource {
		/// <summary>Capacity typed by the user, sent with the next update; <c>null</c> until edited.</summary>
		private uint? _capacity;

		/// <summary>Release picked in the panel, kept when the descriptor cannot be written.</summary>
		private uint _release;

		public override string   Panel  => "world";
		public override string   Label  => "World";
		public override string[] Path   => new[] { "world", "publisher" };
		public override string   Title  => "World Publisher";
		public override AssetEndpoint Endpoint => WorldsEndpoint.Endpoint;

		public override Type DescriptorType => typeof(WorldDescriptor);

		// --- selection ---------------------------------------------------------------------

		public override UnityEngine.Object Selection {
			get => WorldDescriptorHelper.CurrentWorld;
			set {
				if (value is WorldDescriptor descriptor)
					WorldDescriptorHelper.SetCurrentWorld(descriptor);
			}
		}

		public override UnityEngine.Object[] Candidates()
			=> WorldDescriptorHelper.Candidates();

		public override void Initialize(IEditorModCoreAPI api) {
			base.Initialize(api);

			WorldDescriptorHelper.OnWorldSelected.RemoveListener(OnDescriptorSelected);
			WorldDescriptorHelper.OnWorldSelected.AddListener(OnDescriptorSelected);
		}

		private void OnDescriptorSelected(WorldDescriptor world)
			=> RaiseSelectionChanged(world);

		public override string Describe(UnityEngine.Object candidate)
			=> candidate is WorldDescriptor descriptor && !string.IsNullOrEmpty(descriptor.gameObject.scene.name)
				? $"{descriptor.name} ({descriptor.gameObject.scene.name})"
				: base.Describe(candidate);

		// --- release -----------------------------------------------------------------------

		public override uint Release {
			get {
				var pinned = WorldDescriptorHelper.CurrentWorld?.Attached.GetRelease() ?? 0;

				return pinned != 0 ? pinned : _release;
			}
			set {
				_release = value;

				var live = WorldDescriptorHelper.Live(WorldDescriptorHelper.CurrentWorld);

				if (!live)
					return;

				live.Attached = live.Attached.WithRelease(value);
				EditorUtility.SetDirty(live);
			}
		}

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

		// --- attached asset ----------------------------------------------------------------

		public override async UniTask<IAsset> AttachAsync(uint id) {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			var assets     = Nox.Assets.Editor.Editor.Pipeline;
			if (descriptor == null || assets == null)
				return null;

			var server = UserServer();

			if (string.IsNullOrEmpty(server)) {
				Logger.OpenDialog("Error", "No server address available.", "Ok");
				return null;
			}

			WorldAsset asset = null;

			if (id > 0)
				asset = await assets.Fetch<WorldAsset>(server, Endpoint, id.ToString());

			if (asset == null)
				asset = await assets.Create<WorldAsset>(server, Endpoint, new AssetCreateRequest { Id = id });

			if (asset == null) {
				Logger.OpenDialog("Error", "Failed to create or find the world.", "Ok");
				return null;
			}

			var user = Nox.Worlds.Runtime.Main.UserAPI?.Current;

			if (user == null || !asset.IsContributor(user.Identifier)) {
				Logger.OpenDialog("Error", "You are not a contributor of this world.", "Ok");
				SetAttached(null);
				return null;
			}

			var live = WorldDescriptorHelper.Live(descriptor);

			if (live) {
				live.Attached = new Identifier(null, asset.Id, null, asset.Server);
				EditorUtility.SetDirty(live);
			}

			SetAttached(asset);
			return asset;
		}

		/// <summary>
		/// Reopens the world that was published before: the descriptor stores its id and server, so the
		/// form attaches it silently instead of showing the « Attach » prompt.
		/// </summary>
		public override async UniTask<bool> TryAutoAttachAsync() {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			var assets     = Nox.Assets.Editor.Editor.Pipeline;

			if (descriptor == null || assets == null || descriptor.Attached.NumericId == 0)
				return false;

			var server = descriptor.Attached.IsLocal()
				? UserServer()
				: descriptor.Attached.Server;

			if (string.IsNullOrEmpty(server))
				return false;

			var asset = await assets.Fetch<WorldAsset>(server, Endpoint, descriptor.Attached.StringId);

			if (asset == null)
				return false;

			var user = Nox.Worlds.Runtime.Main.UserAPI?.Current;

			if (user == null || !asset.IsContributor(user.Identifier))
				return false;

			SetAttached(asset);
			return true;
		}

		public override async UniTask<bool> UpdateAsync(string title, string description) {
			var assets = Nox.Assets.Editor.Editor.Pipeline;

			if (assets == null || Attached == null)
				return false;

			var request = new WorldUpdateRequest {
				Title       = title.ToTranslated(),
				Description = description.ToTranslated()
			};

			// Capacity is only sent when the user changed it (the node treats 0 as unlimited and
			// ushort.MaxValue as « untouched »).
			if (_capacity.HasValue)
				request.Capacity = (ushort)Math.Min(_capacity.Value, ushort.MaxValue - 1);

			var updated = await assets.Update(Attached.Server, Endpoint, AssetRef, request);

			if (updated == null)
				return false;

			_capacity = null;
			SetAttached(updated);
			return true;
		}

		public override void Detach() {
			var live = WorldDescriptorHelper.Live(WorldDescriptorHelper.CurrentWorld);

			if (live) {
				live.Attached = Identifier.Invalid;
				EditorUtility.SetDirty(live);
			}

			SetAttached(null);
		}

		// --- publish -----------------------------------------------------------------------

		public override IAssetBuilder Builder
			=> new DelegateAssetBuilder(BuildAsync, "world");

		public override AssetPublishConstraints Constraints
			=> new AssetPublishRules {
				PlatformsFunc = request => (request.State as WorldDescriptor)?.Targets is { Length: > 0 } targets
					? targets
					: new[] { PlatformExtensions.CurrentPlatform },
				OwnershipFunc = (_, asset) => {
					var user = Nox.Worlds.Runtime.Main.UserAPI?.Current;

					if (asset == null)
						return "Failed to verify the world.";
					if (user == null)
						return "You are not logged in.";

					return asset.IsContributor(user.Identifier) ? null : "You are not a contributor of this world.";
				},
				PrepareFunc = (request, version) => SaveBeforeBuild(request, version)
			};

		public override AssetInfoEntry[] Info
			=> new[] {
				AssetInfoEntry.Unsigned(
					"Capacity",
					() => _capacity ?? (Attached as WorldAsset)?.Capacity ?? 0u,
					value => _capacity = value,
					"0 = unlimited",
					"Maximum number of concurrent users the world accepts."
				)
			};

		/// <summary>Opens the builder panel of the world, as the previous publisher did.</summary>
		public override IToolOption[] Options(IWindow window)
			=> new IToolOption[] {
				new DefaultToolOption("Builder", () => {
					// The builder panel is ours: use its static reference rather than asking the ModAPI for
					// our own mod (which logs an unrelated-module warning).
					var panel = BuilderPanel.Panel;

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

			var build = await Nox.Worlds.Pipeline.Builder.Build(new BuildData {
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

		/// <summary>
		/// Writes the version to the live descriptor and saves the scene: the Scriptable Build Pipeline
		/// refuses to run while a loaded scene has unsaved changes.
		/// </summary>
		private static string SaveBeforeBuild(AssetPublishRequest request, uint release) {
			var live = WorldDescriptorHelper.Live(request?.State as WorldDescriptor);

			if (!live)
				return null;

			live.Attached = live.Attached.WithRelease(release);
			EditorUtility.SetDirty(live);

			return WorldDescriptorHelper.SaveScene(live)
				? null
				: "The world scene has unsaved changes and could not be saved. Save it and publish again.";
		}

		private static string UserServer()
			=> Nox.Worlds.Runtime.Main.UserAPI?.Current?.Server;
	}
}
