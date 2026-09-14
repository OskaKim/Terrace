using UnityEditor;
using UnityEngine;

namespace Terrace.Client.Editor
{
    /// <summary>
    /// Assets/Resources/Terrace/Art 配下の画像を、手で設定しなくても Sprite として取り込む。
    /// - 70 px = 1 unit(Kenney のタイル 1 枚 = 1 unit)
    /// - 原点は足元中央(キャラクターの位置 = 足元)
    /// - FullRect メッシュ(SpriteRenderer の Tiled 描画で足場やはしごを敷き詰めるため)
    /// </summary>
    public sealed class ArtImportProcessor : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/Resources/Terrace/Art/";
        public const float PixelsPerUnit = 70f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = assetPath.Contains("/Backgrounds/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
