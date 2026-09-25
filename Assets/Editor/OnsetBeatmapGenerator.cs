using System.IO;
using BeatSlash.Rhythm;
using UnityEditor;
using UnityEngine;

namespace BeatSlash.EditorTools
{
    /// <summary>
    /// AudioClip에서 비트 그리드 비트맵 JSON을 생성하는 에디터 툴 (엔진은 OnsetDetector 공용).
    /// 메뉴: BeatSlash > Onset Beatmap Generator — 결과: Assets/Beatmaps/<곡명>.json
    /// BPM/첫 박은 자동 추정된다. 런타임(메뉴/업로드)도 같은 엔진을 쓰므로 이 툴은 개발/튜닝용.
    /// </summary>
    public class OnsetBeatmapGenerator : EditorWindow
    {
        AudioClip _clip;
        [Range(0.8f, 2f)] float _energyFactor = 1.15f;
        float _density = 1f;
        int _laneCount = 4;

        [MenuItem("BeatSlash/Onset Beatmap Generator")]
        static void Open() => GetWindow<OnsetBeatmapGenerator>("Beat Grid Beatmap");

        void OnGUI()
        {
            _clip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", _clip, typeof(AudioClip), false);
            _energyFactor = EditorGUILayout.Slider("에너지 문턱 (높을수록 노트 적음)", _energyFactor, 0.8f, 2f);
            int densityIdx = EditorGUILayout.Popup("노트 밀도",
                _density switch { 0.5f => 0, 2f => 2, _ => 1 },
                new[] { "2박마다 (쉬움)", "매 박 (보통)", "8분음까지 (어려움)" });
            _density = densityIdx switch { 0 => 0.5f, 2 => 2f, _ => 1f };
            _laneCount = EditorGUILayout.IntSlider("레인 수 (2=좌우, 4=상하좌우)", _laneCount, 1, 4);

            EditorGUI.BeginDisabledGroup(_clip == null);
            if (GUILayout.Button("Generate Beatmap JSON")) Generate();
            EditorGUI.EndDisabledGroup();
        }

        void Generate()
        {
            var map = OnsetDetector.Detect(_clip, _energyFactor, _density, _laneCount);

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Beatmaps"));
            string path = $"Assets/Beatmaps/{_clip.name}.json";
            File.WriteAllText(path, map.ToJson());
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("완료",
                $"BPM {map.bpm:F1} 추정, 노트 {map.events.Count}개\n{path}", "OK");
        }
    }
}
