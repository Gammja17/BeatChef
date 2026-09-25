using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// WebGL 빌드 → docs/ 폴더 (GitHub Pages가 main 브랜치 /docs를 서빙).
    /// 메뉴: BeatSlash > Build WebGL (docs) — 또는 배치모드 -executeMethod.
    /// 빌드 후 docs/를 커밋·푸시하면 몇 분 안에 사이트가 자동 갱신된다.
    /// </summary>
    public static class WebGLBuilder
    {
        const string Output = "docs";

        [MenuItem("BeatSlash/Build WebGL (docs)")]
        public static void BuildMenu()
        {
            var ok = Build();
            EditorUtility.DisplayDialog(ok ? "빌드 완료" : "빌드 실패",
                ok ? "docs/ 생성됨. 커밋+푸시하면 GitHub Pages가 자동 갱신됩니다."
                   : "콘솔 로그를 확인하세요.", "OK");
        }

        public static bool Build()
        {
            BakeBuiltinBeatmaps(); // 웹에선 GetData가 안 되므로 내장곡 비트맵은 빌드에 미리 굽는다

            // GitHub Pages는 Content-Encoding 헤더를 못 만지므로 압축 해제 폴백 필수
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // 기본 WASM 스택(64KB)은 에셋 초기화 호출 깊이에서 터진다("RangeError: call stack exceeded")
            // → 2MB로 증량. 8MB는 초기 힙 레이아웃을 침범해 "memory access out of bounds" 유발했음
            PlayerSettings.WebGL.emscriptenArgs = "-sSTACK_SIZE=2097152";
            PlayerSettings.WebGL.initialMemorySize = 128; // MB — 대형 에셋 초기화 여유
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "BeatChef";

            var scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Prototype.unity" };
            var report = BuildPipeline.BuildPlayer(scenes, Output, BuildTarget.WebGL, BuildOptions.None);

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[WebGLBuilder] {(ok ? "성공" : "실패")} — {report.summary.totalSize / (1024 * 1024)}MB, {report.summary.totalTime.TotalMinutes:F1}분");
            return ok;
        }

        /// <summary>배치모드 진입점: 실패 시 종료코드 1.</summary>
        public static void BuildBatch()
        {
            if (!Build()) EditorApplication.Exit(1);
        }

        /// <summary>배치 풀 파이프라인: 씬 재생성(최신 기본값 반영) → 비트맵 굽기 → WebGL 빌드.</summary>
        public static void BuildAllBatch()
        {
            PrototypeSceneBuilder.Create();
            MainMenuSceneBuilder.Create();
            if (!Build()) EditorApplication.Exit(1);
        }

        /// <summary>내장곡(Resources/Songs) × 난이도별 비트맵 JSON을 Resources/SongMaps에 저장.</summary>
        [MenuItem("BeatSlash/Bake Builtin Beatmaps")]
        public static void BakeBuiltinBeatmaps()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Songs" });
            if (guids.Length == 0) return;

            System.IO.Directory.CreateDirectory("Assets/Resources/SongMaps");
            int baked = 0;
            foreach (var guid in guids)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                for (int d = 0; d < Menu.MainMenuUI.Difficulties.Length; d++)
                {
                    var diff = Menu.MainMenuUI.Difficulties[d];
                    var map = Rhythm.OnsetDetector.Detect(clip, diff.energy, diff.density, diff.lanes);
                    System.IO.File.WriteAllText($"Assets/Resources/SongMaps/{clip.name}__{d}.json", map.ToJson());
                    baked++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[WebGLBuilder] 내장곡 비트맵 {baked}개 베이크 완료");
        }
    }
}
