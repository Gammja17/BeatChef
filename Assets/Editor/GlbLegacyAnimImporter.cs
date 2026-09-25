using UnityEditor;
using UnityEngine;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// Resources의 셰프 GLB를 Legacy 애니메이션으로 자동 임포트.
    /// glTFast 에디터 임포터 기본값이 Mecanim이라 Animation 컴포넌트가 안 생기는 문제 해결.
    /// (GltfImporter 클래스가 internal이라 SerializedObject로 설정 주입)
    /// </summary>
    class GlbLegacyAnimImporter : AssetPostprocessor
    {
        const int LegacyValue = 1; // GLTFast.AnimationMethod: None=0, Legacy=1, Mecanim=2

        void OnPreprocessAsset()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Resources/Chef") || !path.EndsWith(".glb")) return;

            var so = new SerializedObject(assetImporter);
            var prop = so.FindProperty("importSettings.animationMethod");
            if (prop != null && prop.intValue != LegacyValue)
            {
                prop.intValue = LegacyValue;
                so.ApplyModifiedProperties();
            }
        }

        [MenuItem("BeatSlash/Reimport Chef GLBs (Legacy Anim)")]
        static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("Chef", new[] { "Assets/Resources" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".glb"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[GlbLegacyAnimImporter] 셰프 GLB 재임포트 완료 — Animation Method: Legacy");
        }
    }
}
