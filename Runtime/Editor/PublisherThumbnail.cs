using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Worlds.Runtime.Editor {
	// Thumbnail management partial class
	public partial class PublisherInstance {
		private void OnThumbnailFieldChanged(ChangeEvent<UnityEngine.Object> evt) {
			var texture = evt.newValue as Texture2D;
			_currentThumbnailTexture = texture;
			UpdateThumbnailPreviewWithTexture(texture);
		}

		private void OnThumbnailFixClicked() {
			if (_currentThumbnailTexture)
				MakeTextureReadable(_currentThumbnailTexture);
		}

		private void MakeTextureReadable(Texture2D texture) {
			if (!texture) return;

			try {
				var assetPath = AssetDatabase.GetAssetPath(texture);
				if (string.IsNullOrEmpty(assetPath)) {
					Logger.OpenDialog("Error", "Cannot find texture asset path.", "Ok");
					return;
				}

				var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
				if (!importer) {
					Logger.OpenDialog("Error", "Cannot access texture import settings.", "Ok");
					return;
				}

				importer.isReadable = true;
				importer.textureType = TextureImporterType.Default;
				importer.textureCompression = TextureImporterCompression.Uncompressed;

				AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
				AssetDatabase.Refresh();

				var testData = texture.EncodeToPNG();
				if (testData == null || testData.Length == 0) {
					Logger.OpenDialog(
						"Warning",
						$"Texture '{texture.name}' is now readable but still cannot be encoded to PNG.\nYou may need to manually adjust the texture format in import settings.",
						"Ok"
					);
				}

				UpdateThumbnailPreviewWithTexture(texture);
				Logger.Log($"Made texture '{texture.name}' readable for thumbnail upload.");
			} catch (Exception ex) {
				Logger.OpenDialog("Error", $"Failed to make texture readable: {ex.Message}", "Ok");
				Logger.LogError($"Failed to make texture readable: {ex.Message}");
			}
		}

		private async UniTask OnThumbnailUploadAsync() {
			if (_world == null) {
				Logger.OpenDialog("Error", "No world attached.", "Ok");
				return;
			}

			var texture = _thumbnailField?.value as Texture2D;
			if (!texture) {
				Logger.OpenDialog("Error", "Please select a thumbnail image.", "Ok");
				return;
			}

			if (!texture.isReadable) {
				Logger.OpenDialog("Error", "Texture must be readable. Please check the texture import settings.", "Ok");
				return;
			}

			try {
				var testData = texture.EncodeToPNG();
				if (testData == null || testData.Length == 0) {
					Logger.OpenDialog(
						"Error",
						"Texture cannot be encoded to PNG. This may be due to:\n" +
						"• Unsupported texture format\n" +
						"• Compressed texture that can't be read\n" +
						"• Non-power-of-2 dimensions on some platforms\n\n" +
						"Try:\n" +
						"• Setting texture format to 'RGBA32' or 'RGB24'\n" +
						"• Enabling 'Read/Write Enabled'\n" +
						"• Using power-of-2 dimensions",
						"Ok"
					);
					return;
				}
			} catch (Exception ex) {
				Logger.OpenDialog("Error", $"Texture encoding test failed: {ex.Message}\n\nPlease check texture import settings.", "Ok");
				return;
			}

			try {
				Logger.Log("Uploading thumbnail...");
				_thumbnailStatus.text = "Uploading thumbnail...";

				var success = await Main.Instance.Network.UploadThumbnail(
					_world.Identifier,
					texture,
					progress => _thumbnailStatus.text = $"Uploading thumbnail... {progress * 100:F0}%"
				);

				if (success) {
					_thumbnailStatus.text = "Thumbnail uploaded successfully.";
					Logger.Log("Thumbnail uploaded successfully.");
					UpdateThumbnailPreview();
				}
				else {
					_thumbnailStatus.text = "Failed to upload thumbnail.";
					Logger.OpenDialog("Error", "Failed to upload thumbnail.", "Ok");
					Logger.LogError("Failed to upload thumbnail.");
				}
			} catch (Exception ex) {
				_thumbnailStatus.text = "Error occurred during thumbnail upload.";
				Logger.OpenDialog("Error", $"An error occurred while uploading thumbnail: {ex.Message}", "Ok");
				Logger.LogError($"An error occurred while uploading thumbnail: {ex.Message}");
			}
			finally {
			}
		}
	}
}
