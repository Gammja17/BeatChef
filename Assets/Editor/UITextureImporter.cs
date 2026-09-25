using UnityEditor;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// Resources/UI 텍스처는 자동으로 Sprite(2D and UI) + Point 필터(픽셀아트)로 임포트.
    /// 유저가 png만 던져 넣으면 바로 Resources.Load&lt;Sprite&gt;로 잡힌다.
    /// </summary>
    public class UITextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/UI")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = UnityEngine.FilterMode.Point; // 픽셀아트 또렷하게
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
        }
    }
}
