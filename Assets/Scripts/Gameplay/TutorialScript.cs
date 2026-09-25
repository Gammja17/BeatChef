using System.Collections.Generic;
using BeatSlash.Rhythm;
using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 인게임 튜토리얼 대본: 단계별 안내 문구 + 연습 노트 + 클릭 비트 트랙.
    /// 단계 시작마다 TutorialDirector가 게임을 멈추고 안내 카드를 띄운다.
    /// </summary>
    public static class TutorialScript
    {
        public const float Bpm = 100f;
        const string SeenKey = "tutorial_seen";

        /// <summary>튜토리얼을 했거나 권유를 한 번 받았는지 — 첫 플레이 권유는 한 번만.</summary>
        public static bool Seen
        {
            get => PlayerPrefs.GetInt(SeenKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(SeenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public class Step
        {
            public string title;
            public string body;
            public float pauseAt; // 이 곡 위치(초)에서 멈추고 카드 표시
            public readonly List<BeatEvent> notes = new List<BeatEvent>();
        }

        // (제목, 설명, 노트: (단계 시작 후 박, 방향 0좌 1우 2상 3하, 타입 0일반 1연타 3홀드))
        static readonly (string title, string body, (float beat, int lane, int type)[] notes)[] Script =
        {
            ("1. 기본 썰기",
             "화살표 주위의 색 링이 점점 조여들어요.\n" +
             "링이 <color=#FFFFFF>흰 원에 딱 닿는 순간</color> 그 방향키를 누르세요!\n" +
             "딱 맞으면 원이 하얗게 번쩍해요.\n" +
             "(방향키 · WASD · 마우스 스와이프 모두 OK)\n\n먼저 천천히 4개 해볼게요.",
             new[] { (0f, 2, 0), (4f, 0, 0), (8f, 1, 0), (12f, 3, 0) }),
            ("2. 박자 타기",
             "이번엔 박자에 맞춰 조금 빠르게!\n딸깍 소리에 맞춰 방향을 바꿔가며 썰어보세요.",
             new[] { (0f, 0, 0), (2f, 1, 0), (4f, 2, 0), (6f, 3, 0), (8f, 1, 0), (10f, 0, 0) }),
            ("3. 연타 노트",
             "<color=#FF7319>주황색 큰 재료</color>는 연타 노트예요!\n" +
             "타이밍에 맞춰 한 번 썬 다음,\n재료가 떠 있는 동안 아무 방향키나 5번 빠르게 연타!",
             new[] { (0f, 2, 1) }),
            ("4. 홀드 노트",
             "<color=#B3FF80>연두색 HOLD</color> 재료는 누르고 버티기!\n" +
             "타이밍에 맞춰 방향키를 누른 채로 유지하다가,\n화면 가운데 링이 겹칠 때 떼세요.",
             new[] { (0f, 1, 3) }),
            ("5. 실전 연습",
             "마지막! 지금까지 배운 걸 섞어서 해볼게요.",
             new[]
             {
                 (0f, 0, 0), (2f, 1, 0), (4f, 2, 0), (6f, 3, 0), (8f, 0, 0), (10f, 2, 0),
                 (12f, 1, 3), (18f, 3, 0), (20f, 0, 0), (22f, 2, 1),
             }),
        };

        public static List<Step> Steps { get; private set; }
        /// <summary>완료 카드 위치(초).</summary>
        public static float EndAt { get; private set; }

        /// <summary>대본 → 단계 목록 + 비트맵 + 클릭 트랙. 모든 노트는 박 그리드 위.</summary>
        public static (Beatmap map, AudioClip clip) Build()
        {
            float spb = 60f / Bpm;
            var map = new Beatmap { songName = "튜토리얼", bpm = Bpm, firstBeatOffset = 0f };
            Steps = new List<Step>();
            float cursor = 2f; // 박 단위 — 첫 카드는 곡 시작 2박 뒤
            foreach (var s in Script)
            {
                var step = new Step { title = s.title, body = s.body, pauseAt = cursor * spb };
                float first = cursor + 4f; // 카드 닫고 4박 뒤 첫 노트 (프롬프트는 2박 전부터 보임)
                float last = first;
                int lastType = 0;
                foreach (var (beat, lane, type) in s.notes)
                {
                    // strength 1 — MarkBig/MarkHold 자동 선정 문턱 아래라 대본 타입이 그대로 유지된다
                    var e = new BeatEvent { time = (first + beat) * spb, lane = lane, type = type, strength = 1f };
                    step.notes.Add(e);
                    map.events.Add(e);
                    last = first + beat;
                    lastType = type;
                }
                Steps.Add(step);
                // 다음 카드까지 여유: 연타는 체공(1.8초), 홀드는 유지 2박을 기다린다
                cursor = last + (lastType == 0 ? 4f : 6f);
            }
            EndAt = cursor * spb;
            return (map, CreateClickTrack(Mathf.CeilToInt(cursor) + 8));
        }

        /// <summary>BPM 클릭 트랙 — 4박마다 강박(높은 음).</summary>
        static AudioClip CreateClickTrack(int beats)
        {
            const int rate = 44100;
            float spb = 60f / Bpm;
            var data = new float[Mathf.RoundToInt(beats * spb * rate)];
            int clickLen = rate / 20; // 50ms
            for (int b = 0; b < beats; b++)
            {
                int start = Mathf.RoundToInt(b * spb * rate);
                float freq = b % 4 == 0 ? 1500f : 1000f;
                for (int i = 0; i < clickLen && start + i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    data[start + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 60f) * 0.5f;
                }
            }
            var clip = AudioClip.Create("튜토리얼", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
