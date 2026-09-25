using System.Collections.Generic;
using BeatSlash.Gameplay;
using UnityEditor;
using UnityEngine;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// Kenney Food Kit 모델로 재료 프리팹을 일괄 생성.
    /// 메뉴: BeatSlash > Build Kenney Ingredients → Assets/Prefabs/Ingredients/
    /// 통짜 + 잘린 반쪽 모델 페어링, 재료군별 과즙 색 지정, 크기 자동 정규화.
    /// </summary>
    public static class KenneyIngredientBuilder
    {
        const string ModelDir = "Assets/KenneyFoodKit/Models";
        const string OutDir = "Assets/Prefabs/Ingredients";
        const float TargetSize = 1.3f; // 재료의 최대 변 길이 (월드 단위)

        // (통짜 모델, 반쪽 모델, 과즙 색) — 반쪽이 두 개 스폰되며 갈라진다.
        // fish→fish-bones는 의도된 개그: 생선을 썰면 뼈만 남는다.
        static readonly (string whole, string half, Color juice)[] Table =
        {
            // 과일
            ("apple", "apple-half", new Color(0.95f, 0.9f, 0.5f)),
            ("lemon", "lemon-half", new Color(1f, 0.95f, 0.3f)),
            ("pear", "pear-half", new Color(0.8f, 0.95f, 0.45f)),
            ("coconut", "coconut-half", new Color(0.98f, 0.98f, 0.95f)),
            ("avocado", "advocado-half", new Color(0.55f, 0.85f, 0.35f)), // Kenney 원본 오타: 통짜만 avocado
            ("tomato", "tomato-slice", new Color(0.95f, 0.2f, 0.12f)),
            // 채소
            ("onion", "onion-half", new Color(0.95f, 0.9f, 0.75f)),
            ("mushroom", "mushroom-half", new Color(0.9f, 0.85f, 0.75f)),
            ("paprika", "paprika-slice", new Color(1f, 0.45f, 0.1f)),
            // 고기 — 생고기를 썰면 패티 두 장
            ("meat-raw", "meat-patty", new Color(0.75f, 0.15f, 0.15f)),
            ("meat-sausage", "sausage-half", new Color(0.8f, 0.35f, 0.2f)),
            // 해산물 — 파란 물보라
            ("fish", "fish-bones", new Color(0.3f, 0.65f, 1f)),
            // 기타
            ("egg", "egg-half", new Color(1f, 0.85f, 0.3f)),
        };

        [MenuItem("BeatSlash/Build Kenney Ingredients")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(ModelDir))
            {
                EditorUtility.DisplayDialog("에러", $"{ModelDir} 가 없습니다. Kenney Food Kit부터 복사하세요.", "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(OutDir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Ingredients");

            var fxPool = PrototypeSceneBuilder.FindParticlePrefabs(6);
            var juiceFx = fxPool.Count > 0 ? fxPool[0] : null;
            var extras = fxPool.Count > 1 ? fxPool.GetRange(1, fxPool.Count - 1).ToArray() : new ParticleSystem[0];
            var built = new List<string>();

            foreach (var (whole, half, juice) in Table)
            {
                var wholeModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{whole}.fbx");
                var halfModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{half}.fbx");
                if (wholeModel == null || halfModel == null)
                {
                    Debug.LogWarning($"[KenneyIngredientBuilder] 모델 없음: {whole} / {half} — 건너뜀");
                    continue;
                }

                var halfPrefab = BuildHalfPrefab(halfModel, half);
                BuildWholePrefab(wholeModel, whole, halfPrefab, juice, juiceFx, extras);
                built.Add(whole);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("완료",
                $"재료 프리팹 {built.Count}개 생성 → {OutDir}\n" +
                "Create Prototype Scene을 다시 실행하면 스포너에 자동 연결됩니다.", "OK");
        }

        static GameObject BuildHalfPrefab(GameObject model, string name)
        {
            var root = new GameObject(name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(root.transform, false);
            NormalizeScale(root, visual);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildWholePrefab(GameObject model, string name, GameObject halfPrefab, Color juice, ParticleSystem juiceFx, ParticleSystem[] extraFx)
        {
            var root = new GameObject(name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(root.transform, false);
            NormalizeScale(root, visual);

            var col = root.AddComponent<SphereCollider>();
            col.radius = TargetSize * 0.45f;

            var sliceable = root.AddComponent<Sliceable>();
            sliceable.halfPrefab = halfPrefab;
            sliceable.juiceColor = juice;
            sliceable.juiceFxPrefab = juiceFx;
            sliceable.extraFxPool = extraFx;

            PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>모델의 렌더 바운드를 기준으로 최대 변이 TargetSize가 되게 정규화.</summary>
        static void NormalizeScale(GameObject root, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxExtent < 0.0001f) return;
            float scale = TargetSize / maxExtent;
            visual.transform.localScale = Vector3.one * scale;
            // 중심을 루트 원점으로
            visual.transform.localPosition = -bounds.center * scale;
        }
    }
}
