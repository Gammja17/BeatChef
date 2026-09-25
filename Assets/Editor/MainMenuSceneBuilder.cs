using System.Collections.Generic;
using System.Linq;
using BeatSlash.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// 원클릭 메인 메뉴 씬 생성 + 빌드 세팅 씬 등록.
    /// 메뉴: BeatSlash > Create Main Menu Scene
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        [MenuItem("BeatSlash/Create Main Menu Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.06f);
            cam.transform.position = new Vector3(0f, 0.6f, -7f);
            cam.transform.rotation = Quaternion.identity;

            // ── 픽셀 카메라 파이프라인 (게임플레이와 같은 도트 룩) ──
            // 메뉴는 원근 카메라 유지 + RT 픽셀화만 사용 — 그리드/서브픽셀 보정은 끔
            int canvasLayer = PrototypeSceneBuilder.EnsureLayer("PixelCanvas");

            var display = new GameObject("UpscaledDisplay");
            display.transform.position = new Vector3(0f, -500f, 0f);

            var canvasQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canvasQuad.name = "UpscaledCanvas";
            Object.DestroyImmediate(canvasQuad.GetComponent<Collider>());
            canvasQuad.layer = canvasLayer;
            canvasQuad.transform.SetParent(display.transform, false);
            var canvasMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Mat_UpscaledCanvas.mat");
            if (canvasMat == null)
            {
                canvasMat = new Material(Shader.Find("BeatSlash/UpscaledCanvas"));
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                    AssetDatabase.CreateFolder("Assets", "Settings");
                AssetDatabase.CreateAsset(canvasMat, "Assets/Settings/Mat_UpscaledCanvas.mat");
            }
            canvasQuad.GetComponent<MeshRenderer>().sharedMaterial = canvasMat;
            canvasQuad.AddComponent<PixelCamera.UpscaledCanvas>();

            var viewCamGo = new GameObject("ViewCamera");
            viewCamGo.transform.SetParent(display.transform);
            viewCamGo.transform.localPosition = new Vector3(0f, 0f, -1f);
            var viewCam = viewCamGo.AddComponent<Camera>();
            viewCam.orthographic = true;
            viewCam.clearFlags = CameraClearFlags.SolidColor;
            viewCam.backgroundColor = Color.black;
            viewCam.cullingMask = 1 << canvasLayer;
            viewCamGo.AddComponent<PixelCamera.CanvasViewCamera>();

            var rig = new GameObject("PixelCameraRig");
            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(rig.transform);
            camTarget.transform.position = cam.transform.position;
            camTarget.transform.rotation = cam.transform.rotation;

            cam.transform.SetParent(rig.transform);
            cam.cullingMask = ~(1 << canvasLayer);
            cam.gameObject.SetActive(false); // OnEnable 타이밍 통제
            var pixelMgr = cam.gameObject.AddComponent<PixelCamera.PixelCameraManager>();
            pixelMgr.FollowedTransform = camTarget.transform;
            pixelMgr.GameResolution = new UnityEngine.Vector2Int(480, 270); // 메뉴는 살짝 섬세하게
            pixelMgr.VoxelGridMovement = false;      // 원근 카메라 — 그리드 스냅 무의미
            pixelMgr.SubpixelAdjustments = false;
            pixelMgr.ControlGameZoom = false;        // 원근 유지 (오소 사이즈 강제 안 함)
            cam.gameObject.SetActive(true);

            // ── 3D 디오라마: 키아트 무드 (셰프 + 떠다니는 재료 + 무지개 배경은 MenuDiorama가 런타임 구성)
            var diorama = new GameObject("Diorama");
            diorama.AddComponent<MenuDiorama>();

            // 도마 무대 — 셰프 발밑 (키아트의 바위 자리)
            var board = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenneyFoodKit/Models/cutting-board.fbx");
            if (board != null)
            {
                var b = (GameObject)PrefabUtility.InstantiatePrefab(board);
                b.name = "Stage";
                b.transform.SetParent(diorama.transform);
                b.transform.position = new Vector3(-2.1f, -3.4f, 1.1f);
                b.transform.localScale = Vector3.one * 5f;
            }

            // 떠다니는 재료들 — "Float_" 이름이면 MenuDiorama가 부유/자전시킨다
            if (AssetDatabase.IsValidFolder("Assets/Prefabs/Ingredients"))
            {
                var pool = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Ingredients" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(p => p != null && !p.name.Contains("half") && !p.name.Contains("slice") && !p.name.Contains("bones") && !p.name.Contains("patty"))
                    .ToArray();
                // 위치는 MenuDiorama가 런타임에 화면 좌표 기준으로 재배치 — 여기선 개수만 확보 (14개)
                var spots = new Vector3[14];
                for (int i = 0; i < spots.Length; i++) spots[i] = new Vector3(i * 1.5f, 0f, 5f);
                for (int i = 0; i < spots.Length && pool.Length > 0; i++)
                {
                    var prefab = pool[i % pool.Length];
                    // 프리팹 링크 없이 일반 복제 — 프리팹 인스턴스는 컴포넌트 삭제가 막혀 빌드가 중단된다
                    var f = Object.Instantiate(prefab);
                    f.name = "Float_" + prefab.name;
                    f.transform.SetParent(diorama.transform);
                    f.transform.position = spots[i];
                    f.transform.rotation = Quaternion.Euler(i * 40f, i * 70f, i * 25f);
                    f.transform.localScale = Vector3.one * (1.4f + (i % 3) * 0.5f);
                    // 게임플레이 컴포넌트 제거 — 장식용
                    foreach (var s in f.GetComponentsInChildren<MonoBehaviour>()) Object.DestroyImmediate(s);
                    foreach (var c in f.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    foreach (var r in f.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(r);
                }
            }

            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuUI>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");

            RegisterScenes("Assets/Scenes/MainMenu.unity", "Assets/Scenes/Prototype.unity");

            EditorUtility.DisplayDialog("메인 메뉴 씬 생성 완료",
                "Assets/Scenes/MainMenu.unity 저장 + 빌드 세팅에 씬 등록됨.\n" +
                "Play 하면 프로젝트 루트 Songs/ 폴더의 곡 목록이 뜹니다.", "OK");
        }

        /// <summary>빌드 세팅에 씬들을 순서대로 등록 (없는 파일은 건너뜀, 기존 항목 유지).</summary>
        static void RegisterScenes(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (scenes.All(s => s.path != existing.path))
                    scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
