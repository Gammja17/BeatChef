using UnityEditor;
using UnityEngine;

namespace BeatSlash.EditorTools
{
    /// <summary>Assets/Resources/UI 아래 텍스처를 자동으로 Sprite로 임포트.</summary>
    class UiSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }

        [MenuItem("BeatSlash/Reimport UI Sprites")]
        static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UI" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log("[UiSpriteImporter] UI 스프라이트 재임포트 완료");
        }
    }
}
