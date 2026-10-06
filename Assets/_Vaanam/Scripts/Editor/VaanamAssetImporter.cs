using UnityEditor;

namespace Vaanam.Editor
{
    /// <summary>
    /// Makes sure the pictures rendered by Tools/render-text-sprites.ps1 import as
    /// UI sprites (not plain textures), and voice WAVs import as short,
    /// decompress-on-load clips. Without this, Resources.Load&lt;Sprite&gt; would
    /// return null for a freshly dropped PNG.
    /// </summary>
    public class VaanamAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/_Vaanam/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/_Vaanam/Resources/Voice/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = UnityEngine.AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = UnityEngine.AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
        }
    }
}
