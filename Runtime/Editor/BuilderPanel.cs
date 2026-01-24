using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Nox.Worlds.Pipeline;
using Nox.CCK.Worlds;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.Editor.Panel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using IPanel = Nox.Editor.Panel.IPanel;

namespace Nox.Worlds.Runtime.Editor {
	public class BuilderPanel : IEditorModInitializer, IPanel {
		private static readonly string[] PanelPath = { "world", "builder" };
		internal IEditorModCoreAPI API;

		public void OnInitializeEditor(IEditorModCoreAPI api)
			=> API = api;

		public void OnDisposeEditor()
			=> API = null;

		public string[] GetPath()
			=> PanelPath;

		internal BuilderInstance Instance;

		public IInstance[] GetInstances()
			=> Instance != null
				? new IInstance[] { Instance }
				: Array.Empty<IInstance>();

		public string GetLabel()
			=> "World/Builder";

		public static string OutputFolder {
			get => Config.LoadEditor().Get("world.builder.output_folder", "Assets/WorldBuilds/");
			set {
				var config = Config.LoadEditor();
				config.Set("world.builder.output_folder", value);
				config.Save();
			}
		}

		public IInstance Instantiate(IWindow window, Dictionary<string, object> data) {
			if (Instance != null)
				throw new InvalidOperationException("BuilderInstance only supports a single instance.");
			return Instance = new BuilderInstance(this, window, data);
		}
	}

	public class BuilderInstance : IInstance {
		private readonly BuilderPanel _panel;
		private readonly IWindow _window;

		public BuilderInstance(BuilderPanel panel, IWindow window, Dictionary<string, object> data) {
			_panel = panel;
			_window = window;
			WorldDescriptorHelper.OnWorldSelected.AddListener(OnWorldSelected);
			WorldNotificationHelper.OnNotificationsChanged.AddListener(OnNotificationsChanged);
			Builder.OnBuildFinished.AddListener(OnBuildFinished);
			Builder.OnBuildStarted.AddListener(OnBuildStarted);
			Builder.OnBuildProgress.AddListener(OnBuildProgress);
		}


		public IPanel GetPanel()
			=> _panel;

		public IWindow GetWindow()
			=> _window;

		public string GetTitle()
			=> "World Builder";

		public void OnDestroy() {
			WorldDescriptorHelper.OnWorldSelected.RemoveListener(OnWorldSelected);
			WorldNotificationHelper.OnNotificationsChanged.RemoveListener(OnNotificationsChanged);
			Builder.OnBuildFinished.RemoveListener(OnBuildFinished);
			Builder.OnBuildStarted.RemoveListener(OnBuildStarted);
			Builder.OnBuildProgress.RemoveListener(OnBuildProgress);
			_panel.Instance = null;
		}

		private void OnWorldSelected(WorldDescriptor arg0) {
			_selectedField.SetValueWithoutNotify(arg0);
			_buildButton.SetEnabled(arg0 && WorldNotificationHelper.Allowed);
			_platformEnum.SetValueWithoutNotify(!arg0 ? Platform.None : arg0.target);
			_platformEnum.SetEnabled(arg0);
		}

		private static void OnValueChanged(ChangeEvent<WorldDescriptor> evt)
			=> WorldDescriptorHelper.SetCurrentWorld(evt.newValue);

		private void OnNotificationsChanged(WorldNotification[] arg0) {
			var world = WorldDescriptorHelper.CurrentWorld;
			_buildButton.SetEnabled(world && WorldNotificationHelper.Allowed);
			_notificationsList.Clear();
			var prefab = Resources.Load<VisualTreeAsset>("nox.cck.notification");
			foreach (var notification in arg0) {
				var element = prefab.CloneTree();

				element.AddToClassList(notification.Type.ToString().ToLowerInvariant());
				var content = element.Q<VisualElement>("content");

				content.Clear();
				content.Add(
					new Label(LanguageManager.Get(notification.Content[0], notification.Content[1..] as string[])) {
						style = {
							flexWrap = Wrap.Wrap,
							whiteSpace = WhiteSpace.Normal,
							unityTextAlign = TextAnchor.MiddleLeft,
							flexGrow = 1
						}
					}
				);

				var actions = element.Q<VisualElement>("actions");
				actions.Clear();

				foreach (var action in notification.Actions) {
					var actionButton = new Button(() => action.Action.Invoke()) {
						text = LanguageManager.Get(action.Content[0], action.Content[1..] as string[])
					};
					actions.Add(actionButton);
				}

				_notificationsList.Add(element);
			}
		}

		private void OnOpenOutputClicked(ClickEvent evt) {
			if (string.IsNullOrEmpty(_outputField.value)) return;
			if (!Directory.Exists(_outputField.value))
				Directory.CreateDirectory(_outputField.value);
			Application.OpenURL(_outputField.value);
		}

		private void OnSelectOutputClicked(ClickEvent evt) {
			var path = EditorUtility.OpenFolderPanel("Select Output Folder", "", "");
			if (string.IsNullOrEmpty(path)) return;
			var applicationPath = Application.dataPath;
			if (path.StartsWith(applicationPath))
				path = "Assets" + path[applicationPath.Length..];
			_outputField.SetValueWithoutNotify(path);
			BuilderPanel.OutputFolder = path;
		}

		private static void OnPlatformChanged(ChangeEvent<Enum> evt) {
			var world = WorldDescriptorHelper.CurrentWorld;
			if (!world) return;
			world.target = (Platform)evt.newValue;
			EditorUtility.SetDirty(world);
		}

		private static void OnBuildClicked(ClickEvent evt) {
			var world = WorldDescriptorHelper.CurrentWorld;
			if (!world) {
				Debug.LogError("No world selected.");
				return;
			}

			var data = new BuildData {
				Descriptor = world,
				Target = world.target,
				OutputPath = BuilderPanel.OutputFolder,
				ShowDialog = false
			};

			Builder.Build(data).Forget();
		}

		private void OnBuildStarted() {
			_buildingContainer.style.display = DisplayStyle.Flex;
			_buildingStatusLabel.text = LanguageManager.Get("world.builder.status.starting");
			_buildingProgressBar.value = 0f;
			_buildButton.SetEnabled(false);
		}

		private void OnBuildStarted(BuildData arg0)
			=> OnBuildStarted();

		private void OnBuildFinished(BuildResult arg0) {
			_buildingContainer.style.display = DisplayStyle.None;
			_resultContainer.style.display = DisplayStyle.Flex;
			_resultFailedLabel.style.display = arg0.IsFailed ? DisplayStyle.Flex : DisplayStyle.None;
			_resultSuccessLabel.style.display = arg0.IsFailed ? DisplayStyle.None : DisplayStyle.Flex;
			_resultDetailsLabel.text = !arg0.IsFailed
				? LanguageManager.Get("world.builder.result.success", new object[] { arg0.Output })
				: arg0.Message;
		}

		private void OnBuildProgress(float progress, string status) {
			_buildingProgressBar.value = progress * 100;
			_buildingStatusLabel.text = status;
		}

		private void OnBuildResultOKClicked(ClickEvent evt) {
			_resultContainer.style.display = DisplayStyle.None;
			var world = WorldDescriptorHelper.CurrentWorld;
			_buildButton.SetEnabled(world && WorldNotificationHelper.Allowed);
		}

		private ObjectField _selectedField;
		private VisualElement _notificationsList;
		private TextField _outputField;
		private Button _openOutputButton;
		private Button _buildButton;
		private Button _selectOutputButton;
		private EnumField _platformEnum;

		private VisualElement _buildingContainer;
		private Label _buildingStatusLabel;
		private ProgressBar _buildingProgressBar;

		private VisualElement _resultContainer;
		private Label _resultFailedLabel;
		private Label _resultSuccessLabel;
		private Button _resultOkButton;
		private Label _resultDetailsLabel;

		private VisualElement _content;

		public VisualElement GetContent() {
			if (_content != null)
				return _content;

			var root = _panel.API.AssetAPI
				.GetAsset<VisualTreeAsset>("panels/builder.uxml")
				.CloneTree();

			_selectedField = root.Q<ObjectField>("selected");
			_notificationsList = root.Q<VisualElement>("notifications");
			_outputField = root.Q<TextField>("output");
			_openOutputButton = root.Q<Button>("open-output");
			_buildButton = root.Q<Button>("build");
			_selectOutputButton = root.Q<Button>("select-output");
			_platformEnum = root.Q<EnumField>("platform");

			_buildingContainer = root.Q<VisualElement>("building");
			_buildingStatusLabel = _buildingContainer.Q<Label>("status");
			_buildingProgressBar = _buildingContainer.Q<ProgressBar>("progress");

			_resultContainer = root.Q<VisualElement>("result");
			_resultFailedLabel = _resultContainer.Q<Label>("failed");
			_resultSuccessLabel = _resultContainer.Q<Label>("success");
			_resultOkButton = _resultContainer.Q<Button>("ok");
			_resultDetailsLabel = _resultContainer.Q<Label>("details");

			_selectedField.RegisterCallback<ChangeEvent<WorldDescriptor>>(OnValueChanged);
			_openOutputButton.RegisterCallback<ClickEvent>(OnOpenOutputClicked);
			_selectOutputButton.RegisterCallback<ClickEvent>(OnSelectOutputClicked);
			_buildButton.RegisterCallback<ClickEvent>(OnBuildClicked);
			_outputField.SetValueWithoutNotify(BuilderPanel.OutputFolder);
			_platformEnum.RegisterCallback<ChangeEvent<Enum>>(OnPlatformChanged);
			_resultOkButton.RegisterCallback<ClickEvent>(OnBuildResultOKClicked);

			_buildingContainer.style.display = DisplayStyle.None;
			_resultContainer.style.display = DisplayStyle.None;

			OnNotificationsChanged(WorldNotificationHelper.Notifications.ToArray());
			OnWorldSelected(WorldDescriptorHelper.CurrentWorld);
			if (Builder.IsBuilding) OnBuildStarted();

			return _content = root;
		}
	}
}
