using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.CCK.Worlds;
using Nox.CCK.Utils;
using Nox.Editor.Panel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = Nox.CCK.Utils.Logger;
using IPanel = Nox.Editor.Panel.IPanel;

namespace Nox.Worlds.Runtime.Editor {
	public partial class PublisherInstance : IInstance {
		private readonly PublisherPanel _panel;
		private readonly IWindow _window;
		private Network.World _world;

		// UI Elements - Main
		private VisualElement _content;
		private ObjectField _selectedField;
		private EnumField _platformEnum;

		// UI Elements - Attach Section
		private VisualElement _attachContainer;
		private TextField _attachIdField;
		private TextField _attachServerField;
		private Button _attachButton;
		private VisualElement _attachedContainer;
		private TextField _infoServerField;
		private UnsignedIntegerField _infoIdField;
		private TextField _infoNameField;
		private TextField _infoDescriptionField;
		private Button _infoUpdateButton;
		private Button _infoDetachButton;
		private Button _infoRefreshButton;

		// UI Elements - Asset Settings
		private VisualElement _assetContainer;
		private UnsignedIntegerField _assetVersionField;
		private Button _assetDetectVersionButton;
		private Toggle _assetAutoVersionToggle;
		private Toggle _assetStrictToggle;

		// UI Elements - Thumbnail
		private VisualElement _thumbnailContainer;
		private ObjectField _thumbnailField;
		private VisualElement _thumbnailPreview;
		private Image _thumbnailImage;
		private Label _thumbnailStatus;
		private Button _thumbnailFixButton;
		private Button _thumbnailUploadButton;

		// UI Elements - Publish
		private Button _publishButton;

		// UI Elements - Progress & Results
		private VisualElement _buildingContainer;
		private Label _buildingStatusLabel;
		private ProgressBar _buildingProgressBar;
		private VisualElement _resultContainer;
		private Label _resultFailedLabel;
		private Label _resultSuccessLabel;
		private Button _resultOkButton;
		private Label _resultDetailsLabel;

		// UI Elements - States
		private VisualElement _notLoggedContainer;
		private VisualElement _noDescriptorContainer;
		private VisualElement _loadingContainer;

		private Texture2D _currentThumbnailTexture;

		public PublisherInstance(PublisherPanel panel, IWindow window, Dictionary<string, object> data) {
			_panel = panel;
			_window = window;
		}

		public IPanel GetPanel()
			=> _panel;

		public IWindow GetWindow()
			=> _window;

		public string GetTitle()
			=> "World Publisher";

		public void OnDestroy() {
			WorldDescriptorHelper.OnWorldSelected.RemoveListener(OnWorldSelected);
			_panel.Instance = null;
		}

		public VisualElement GetContent() {
			if (_content != null)
				return _content;

			var root = _panel.API.AssetAPI
				.GetAsset<VisualTreeAsset>("panels/publisher.uxml")
				.CloneTree();

			CacheUIElements(root);
			SetupEventHandlers();
			LoadConfiguration();

			_buildingContainer.style.display = DisplayStyle.None;
			_resultContainer.style.display = DisplayStyle.None;

			WorldDescriptorHelper.OnWorldSelected.AddListener(OnWorldSelected);
			OnWorldSelected(WorldDescriptorHelper.CurrentWorld);
			CheckLoginStatus().Forget();

			return _content = root;
		}

		private void CacheUIElements(VisualElement root) {
			// Main
			_selectedField = root.Q<ObjectField>("selected");
			_platformEnum = root.Q<EnumField>("platform");
			_publishButton = root.Q<Button>("publish");

			// Attach section
			_attachContainer = root.Q<VisualElement>("attach");
			_attachIdField = root.Q<TextField>("attach-id");
			_attachServerField = root.Q<TextField>("attach-server");
			_attachButton = root.Q<Button>("attach-button");

			// Attached info
			_attachedContainer = root.Q<VisualElement>("attached");
			_infoServerField = root.Q<TextField>("info-server");
			_infoIdField = root.Q<UnsignedIntegerField>("info-id");
			_infoNameField = root.Q<TextField>("info-name");
			_infoDescriptionField = root.Q<TextField>("info-description");
			_infoUpdateButton = root.Q<Button>("info-update");
			_infoDetachButton = root.Q<Button>("info-detach");
			_infoRefreshButton = root.Q<Button>("info-refresh");

			// Asset settings
			_assetContainer = root.Q<VisualElement>("asset-settings");
			_assetVersionField = root.Q<UnsignedIntegerField>("asset-version");
			_assetDetectVersionButton = root.Q<Button>("asset-detect-version");
			_assetAutoVersionToggle = root.Q<Toggle>("asset-auto-version");
			_assetStrictToggle = root.Q<Toggle>("asset-strict");

			// Thumbnail
			_thumbnailContainer = root.Q<VisualElement>("thumbnail-section");
			_thumbnailField = root.Q<ObjectField>("thumbnail-field");
			_thumbnailPreview = root.Q<VisualElement>("thumbnail-preview");
			_thumbnailImage = root.Q<Image>("thumbnail-image");
			_thumbnailStatus = root.Q<Label>("thumbnail-status");
			_thumbnailFixButton = root.Q<Button>("thumbnail-fix-button");
			_thumbnailUploadButton = root.Q<Button>("thumbnail-upload");

			// Progress & Results
			_buildingContainer = root.Q<VisualElement>("building");
			_buildingStatusLabel = root.Q<Label>("status");
			_buildingProgressBar = root.Q<ProgressBar>("progress");
			_resultContainer = root.Q<VisualElement>("result");
			_resultFailedLabel = root.Q<Label>("failed");
			_resultSuccessLabel = root.Q<Label>("success");
			_resultOkButton = root.Q<Button>("ok");
			_resultDetailsLabel = root.Q<Label>("details");

			// States
			_notLoggedContainer = root.Q<VisualElement>("not-logged");
			_noDescriptorContainer = root.Q<VisualElement>("no-descriptor");
			_loadingContainer = root.Q<VisualElement>("loading");
		}

		private void SetupEventHandlers() {
			_selectedField?.RegisterCallback<ChangeEvent<WorldDescriptor>>(OnValueChanged);
			_platformEnum?.RegisterCallback<ChangeEvent<Enum>>(OnPlatformChanged);
			_publishButton?.RegisterCallback<ClickEvent>(evt => OnPublishAsync().Forget());
			_attachButton?.RegisterCallback<ClickEvent>(evt => OnAttachAsync().Forget());
			_infoUpdateButton?.RegisterCallback<ClickEvent>(evt => OnUpdateInfoAsync().Forget());
			_infoDetachButton?.RegisterCallback<ClickEvent>(OnDetachClicked);
			_infoRefreshButton?.RegisterCallback<ClickEvent>(evt => OnRefreshInfoAsync().Forget());
			_thumbnailField?.RegisterCallback<ChangeEvent<UnityEngine.Object>>(OnThumbnailFieldChanged);
			_thumbnailFixButton?.RegisterCallback<ClickEvent>(evt => OnThumbnailFixClicked());
			_thumbnailUploadButton?.RegisterCallback<ClickEvent>(evt => OnThumbnailUploadAsync().Forget());
			_assetVersionField?.RegisterCallback<ChangeEvent<ushort>>(OnAssetVersionChanged);
			_assetDetectVersionButton?.RegisterCallback<ClickEvent>(evt => OnDetectVersionAsync().Forget());
			_assetAutoVersionToggle?.RegisterCallback<ChangeEvent<bool>>(OnAutoVersionChanged);
			_assetStrictToggle?.RegisterCallback<ChangeEvent<bool>>(OnStrictVersionChanged);
			_resultOkButton?.RegisterCallback<ClickEvent>(OnResultOKClicked);
		}

		private void LoadConfiguration() {
			var config = Config.Load();
			if (_assetAutoVersionToggle != null)
				_assetAutoVersionToggle.SetValueWithoutNotify(config.Get("sdk.auto_version", true));
			if (_assetStrictToggle != null)
				_assetStrictToggle.SetValueWithoutNotify(config.Get("sdk.strict_version", true));
		}

		private void OnWorldSelected(WorldDescriptor descriptor) {
			_selectedField?.SetValueWithoutNotify(descriptor);
			_publishButton?.SetEnabled(descriptor && WorldNotificationHelper.Allowed && _world != null);
			_platformEnum?.SetValueWithoutNotify(!descriptor ? Platform.None : descriptor.target);
			_platformEnum?.SetEnabled(descriptor);
			_assetVersionField?.SetValueWithoutNotify(descriptor?.publishVersion ?? 0);

			CheckLoginStatus().Forget();
		}

		private static void OnValueChanged(ChangeEvent<WorldDescriptor> evt)
			=> WorldDescriptorHelper.SetCurrentWorld(evt.newValue);

		private void OnPlatformChanged(ChangeEvent<Enum> evt) {
			var world = WorldDescriptorHelper.CurrentWorld;
			if (!world) return;
			var platform = (Platform)evt.newValue;
			if (platform != Platform.None && !platform.IsSupported()) {
				EditorUtility.DisplayDialog("Error", $"\"{platform}\" is not supported.", "Ok");
				Logger.LogError($"Platform \"{platform.GetPlatformName()}\" ({platform.GetBuildTarget()}) is not supported.");
				_platformEnum?.SetValueWithoutNotify(evt.previousValue);
				return;
			}

			world.target = platform;
			EditorUtility.SetDirty(world);
		}

		private void OnAssetVersionChanged(ChangeEvent<ushort> evt) {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor) return;
			descriptor.publishVersion = evt.newValue;
			EditorUtility.SetDirty(descriptor);
		}

		private void OnAutoVersionChanged(ChangeEvent<bool> evt) {
			var config = Config.Load();
			config.Set("sdk.auto_version", evt.newValue);
			config.Save();
		}

		private void OnStrictVersionChanged(ChangeEvent<bool> evt) {
			var config = Config.Load();
			config.Set("sdk.strict_version", evt.newValue);
			config.Save();
		}

		private void OnDetachClicked(ClickEvent evt) {
			var descriptor = WorldDescriptorHelper.CurrentWorld;
			if (!descriptor || _world == null) return;

			descriptor.publishId = 0;
			descriptor.publishServer = "";
			EditorUtility.SetDirty(descriptor);
			_world = null;
			UpdateWorldUI();
			UpdateDisplayState();
		}

		private void OnResultOKClicked(ClickEvent evt) {
			WorldDescriptorHelper.Find();
			_resultContainer.style.display = DisplayStyle.None;
			var world = WorldDescriptorHelper.CurrentWorld;
			_publishButton?.SetEnabled(world && WorldNotificationHelper.Allowed && _world != null);
		}
	}
}
