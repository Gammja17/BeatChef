using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// 빈 프로젝트를 URP로 전환하는 원타임 부트스트랩.
    /// 파이프라인 에셋이 이미 지정돼 있으면 아무것도 안 한다.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpBootstrap
    {
        static UrpBootstrap()
        {
            EditorApplication.delayCall += Setup;
        }

        // 배치모드용: -executeMethod BeatSlash.EditorTools.UrpBootstrap.Setup
        [MenuItem("BeatSlash/Setup URP Now")]
        public static void Setup()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP-Renderer.asset");

            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, "Assets/Settings/URP-Pipeline.asset");

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            AssetDatabase.SaveAssets();
            Debug.Log("[UrpBootstrap] URP 파이프라인 생성 및 지정 완료 (Linear color space)");
        }
    }
}
