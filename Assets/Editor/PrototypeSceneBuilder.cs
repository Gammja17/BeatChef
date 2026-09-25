using BeatSlash.Gameplay;
using BeatSlash.Juice;
using BeatSlash.Rhythm;
using PixelCamera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// 원클릭 프로토타입 씬 생성. 메뉴: BeatSlash > Create Prototype Scene
    /// 카메라/시스템/스포너/임시 재료 프리팹까지 전부 배선된 플레이 가능한 씬을 만든다.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string MusicPath = "Assets/Music/Sleepwalking.mp3";
        const string BeatmapPath = "Assets/Beatmaps/Sleepwalking.json";

        [MenuItem("BeatSlash/Create Prototype Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ── 3DPixelCamera 리그 조립 ─────────────────────────────
            // 구조: PixelCameraRig ─ (게임 카메라 + PixelCameraManager, CameraTarget)
            //       UpscaledDisplay ─ (RT를 띄우는 쿼드 + 그걸 찍는 ViewCamera)
            int canvasLayer = EnsureLayer("PixelCanvas");

            // 1) 디스플레이(캔버스 + 뷰카메라)를 먼저 만든다 —
            //    PixelCameraManager가 OnEnable에서 FindAnyObjectByType으로 이 둘을 찾기 때문
            var display = new GameObject("UpscaledDisplay");
            display.transform.position = new Vector3(0f, -500f, 0f); // 월드에서 격리

            var canvasQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canvasQuad.name = "UpscaledCanvas";
            Object.DestroyImmediate(canvasQuad.GetComponent<Collider>());
            canvasQuad.layer = canvasLayer;
            canvasQuad.transform.SetParent(display.transform, false);
            var canvasMat = new Material(Shader.Find("BeatSlash/UpscaledCanvas"));
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
            AssetDatabase.CreateAsset(canvasMat, "Assets/Settings/Mat_UpscaledCanvas.mat");
            canvasQuad.GetComponent<MeshRenderer>().sharedMaterial = canvasMat;
            canvasQuad.AddComponent<UpscaledCanvas>();

            var viewCamGo = new GameObject("ViewCamera");
            viewCamGo.transform.SetParent(display.transform);
            viewCamGo.transform.localPosition = new Vector3(0f, 0f, -1f);
            var viewCam = viewCamGo.AddComponent<Camera>();
            viewCam.orthographic = true;
            viewCam.clearFlags = CameraClearFlags.SolidColor;
            viewCam.backgroundColor = Color.black;
            viewCam.cullingMask = 1 << canvasLayer;
            viewCamGo.AddComponent<CanvasViewCamera>();

            // 2) 카메라 리그 — 매니저는 카메라를 꺼둔 상태에서 붙여 OnEnable 타이밍을 통제
            var rig = new GameObject("PixelCameraRig");
            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(rig.transform);
            camTarget.transform.position = new Vector3(0f, 5f, -9f);
            camTarget.transform.rotation = Quaternion.Euler(25f, 0f, 0f); // 대각선 부감
            // 카메라 펀치는 타겟을 흔든다 — 매니저가 카메라 위치를 매 프레임 덮어쓰기 때문
            camTarget.AddComponent<CameraPunch>();

            var cam = Camera.main;
            cam.transform.SetParent(rig.transform);
            cam.orthographic = true; // 픽셀 카메라 시스템은 오소그래픽 기준
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black; // 완전 검정
            cam.cullingMask = ~(1 << canvasLayer);

            cam.gameObject.SetActive(false); // OnEnable 억제
            var pixelMgr = cam.gameObject.AddComponent<PixelCameraManager>();
            pixelMgr.FollowedTransform = camTarget.transform;
            pixelMgr.GameResolution = new Vector2Int(426, 240); // 내부 해상도 — 낮을수록 픽셀이 굵어짐
            pixelMgr.GameCameraZoom = 3.7f; // 타이트하게 — 셰프 중심 프레이밍
            cam.gameObject.AddComponent<CameraDirector>(); // 비트 펄스/연타 클로즈업/피버 증폭
            cam.gameObject.SetActive(true); // 필드가 다 채워진 상태로 OnEnable 실행
            // ────────────────────────────────────────────────────────

            // 바닥: 보이지 않는 충돌면만 — 배경은 완전 검정, 반쪽은 여전히 튕긴다
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (invisible)";
            ground.transform.position = new Vector3(0f, -2f, 0f);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(ground.GetComponent<MeshFilter>());

            // Conductor
            var conductorGo = new GameObject("Conductor");
            conductorGo.AddComponent<AudioSource>();
            var conductor = conductorGo.AddComponent<Conductor>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            conductor.songClip = clip;

            // Systems
            var systems = new GameObject("Systems");
            systems.AddComponent<BeatJudge>();
            systems.AddComponent<HitStop>();
            var flow = systems.AddComponent<GameFlow>();
            var slash = systems.AddComponent<SlashController>();
            var hud = systems.AddComponent<PromptHUD>();
            systems.AddComponent<FeverMode>();

            // Spawner + 스폰 포인트/썰기 존
            var spawnerGo = new GameObject("Spawner");
            var spawner = spawnerGo.AddComponent<IngredientSpawner>();
            // 스폰 위치는 lane 방향에서 자동 계산 — 존/스폰 포인트 배치 불필요
            spawner.sliceZone = MakePoint(spawnerGo.transform, "SliceZone", new Vector3(0f, 1.5f, 0f));

            // 중앙의 요리사 실루엣 (방향별 칼 스윙)
            var chef = new GameObject("ChefAvatar");
            chef.transform.position = spawner.sliceZone.position + Vector3.down * 0.4f;
            chef.AddComponent<ChefAvatar>();

            // 천천히 도는 카메라 — 썰기 존이 피벗
            var orbit = camTarget.AddComponent<CameraOrbit>();
            orbit.pivot = spawner.sliceZone;
            orbit.degreesPerSecond = 14f;

            // 재료 프리팹: Kenney 재료가 생성돼 있으면 전부 사용, 없으면 임시 토마토(빨간 구)
            var kenney = new System.Collections.Generic.List<GameObject>();
            if (AssetDatabase.IsValidFolder("Assets/Prefabs/Ingredients"))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Ingredients" }))
                {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (p != null && p.GetComponent<Sliceable>() != null) kenney.Add(p); // 반쪽 프리팹은 제외
                }
            }
            spawner.ingredientPrefabs = kenney.Count > 0 ? kenney.ToArray() : new[] { BuildIngredientPrefab() };

            // 도마: 썰기 존 아래에 Kenney cutting-board 배치 (있으면)
            var board = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenneyFoodKit/Models/cutting-board.fbx");
            if (board != null)
            {
                var boardGo = (GameObject)PrefabUtility.InstantiatePrefab(board);
                boardGo.name = "CuttingBoard";
                boardGo.transform.position = new Vector3(0f, 0.6f, 0f);
                boardGo.transform.localScale = Vector3.one * 4f;
            }

            // SFX는 SlashController가 Resources/Sfx에서 런타임 로드 — 씬 배선 불필요

            // 배선
            flow.beatmapJson = AssetDatabase.LoadAssetAtPath<TextAsset>(BeatmapPath);
            flow.songClip = clip;
            flow.spawner = spawner;
            slash.spawner = spawner;
            hud.spawner = spawner;
            slash.laneMatters = true; // 공간 구조: 방향까지 맞아야 함 (왼쪽에서 온 건 ←)

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Prototype.unity");

            string warn = "";
            if (clip == null) warn += $"\n- {MusicPath} 없음 (Conductor/GameFlow에 곡 지정 필요)";
            if (flow.beatmapJson == null) warn += $"\n- {BeatmapPath} 없음 (온셋 생성기로 생성 필요)";
            EditorUtility.DisplayDialog("프로토타입 씬 생성 완료",
                "Assets/Scenes/Prototype.unity 저장됨. Play 하면 시작합니다." +
                (warn.Length > 0 ? "\n\n주의:" + warn : ""), "OK");
        }

        /// <summary>레이어가 없으면 TagManager의 빈 슬롯에 등록하고 인덱스를 돌려준다.</summary>
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            Debug.LogWarning("[PrototypeSceneBuilder] 빈 레이어 슬롯이 없어 Default 레이어를 사용합니다");
            return 0;
        }

        static Transform MakePoint(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            return go.transform;
        }

        static GameObject BuildIngredientPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            // 빨간 URP 머티리얼
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.9f, 0.15f, 0.1f) };
            AssetDatabase.CreateAsset(mat, "Assets/Prefabs/Mat_Tomato.mat");

            // 반쪽: 납작한 반구 느낌
            var halfTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halfTemp.name = "TomatoHalf";
            halfTemp.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            halfTemp.GetComponent<Renderer>().sharedMaterial = mat;
            halfTemp.AddComponent<Rigidbody>();
            var halfPrefab = PrefabUtility.SaveAsPrefabAsset(halfTemp, "Assets/Prefabs/TomatoHalf.prefab");
            Object.DestroyImmediate(halfTemp);

            // 통짜
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.name = "Tomato";
            temp.transform.localScale = Vector3.one * 0.8f;
            temp.GetComponent<Renderer>().sharedMaterial = mat;
            var sliceable = temp.AddComponent<Sliceable>();
            sliceable.halfPrefab = halfPrefab;
            sliceable.juiceColor = new Color(0.9f, 0.15f, 0.1f);
            sliceable.juiceFxPrefab = FindParticlePrefab();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, "Assets/Prefabs/Tomato.prefab");
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>VFX_Klaus/ToonFX에서 히트 계열 파티클 프리팹을 하나 찾아온다.</summary>
        public static ParticleSystem FindParticlePrefab()
        {
            var list = FindParticlePrefabs(1);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>히트 계열 우선으로 파티클 프리팹을 최대 max개 수집 (이펙트 다양성용).</summary>
        public static System.Collections.Generic.List<ParticleSystem> FindParticlePrefabs(int max)
        {
            string[] roots = { "Assets/VFX_Klaus", "Assets/ToonFX" };
            string[] preferred = { "hit", "impact", "explo", "splash", "burst", "spark", "star" };

            var hits = new System.Collections.Generic.List<ParticleSystem>();
            var others = new System.Collections.Generic.List<ParticleSystem>();
            foreach (var root in roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var ps = go != null ? go.GetComponentInChildren<ParticleSystem>() : null;
                    if (ps == null) continue;

                    var lower = go.name.ToLowerInvariant();
                    bool isHit = false;
                    foreach (var key in preferred)
                        if (lower.Contains(key)) { isHit = true; break; }
                    (isHit ? hits : others).Add(ps);
                }
            }
            hits.AddRange(others);
            if (hits.Count > max) hits.RemoveRange(max, hits.Count - max);
            return hits;
        }
    }
}
