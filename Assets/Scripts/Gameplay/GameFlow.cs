using System.Collections;
using BeatSlash.Menu;
using BeatSlash.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 한 판 흐름: 비트맵 로드 → Conductor/Judge/Spawner 시동. 씬에 하나.
    /// 메인 메뉴에서 곡을 골라 들어오면 SongSelection이 우선.
    /// 씬을 직접 Play할 땐 인스펙터의 beatmapJson/songClip 사용 (개발용).
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public TextAsset beatmapJson;
        public AudioClip songClip;
        public IngredientSpawner spawner;
        [Tooltip("씬 시작 시 자동 시작")]
        public bool autoStart = true;

        [Tooltip("시작 전 카운트다운 초")]
        public int countdownFrom = 3;

        void Start()
        {
            // 정적 집계는 씬 진입 즉시 리셋 — Begin(카운트다운 후)에서 하면
            // 전판 콤보가 살아있는 3초 동안 피버가 켜진 채 시작하는 버그가 난다
            ScoreTracker.Reset();

            gameObject.AddComponent<PauseMenu>(); // ESC 일시정지 — 씬 수정 없이 런타임 부착
            gameObject.AddComponent<Juice.FeverBackdrop>(); // 피버 병맛 배경
            if (SongSelection.Tutorial) gameObject.AddComponent<TutorialDirector>(); // 단계별 안내 카드

            // 도마가 비트마다 쿵쿵 — 무대 자체가 박자를 탄다
            var board = GameObject.Find("CuttingBoard");
            if (board != null && board.GetComponent<BeatPulse>() == null)
            {
                var pulse = board.AddComponent<BeatPulse>();
                pulse.pulseScale = 1.06f;
                pulse.returnSpeed = 9f;
            }
            if (autoStart) StartCoroutine(CountdownThenBegin());
        }

        IEnumerator CountdownThenBegin()
        {
            var canvasGo = new GameObject("CountdownCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 폭/높이 균형 — 모바일 포함 어떤 비율에서도 비례 유지

            var go = new GameObject("Count");
            go.transform.SetParent(canvasGo.transform, false);
            var t = go.AddComponent<Text>();
            t.font = UIFont.Get();
            t.fontSize = 280;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(0f, 60f);

            for (int i = countdownFrom; i >= 1; i--)
            {
                t.text = i.ToString();
                float el = 0f;
                while (el < 1f)
                {
                    el += Time.deltaTime;
                    // 팍 나타나서 서서히 줄고 옅어짐
                    t.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 0.9f, el);
                    t.color = new Color(1f, 0.85f, 0.2f, 1f - el * 0.55f);
                    yield return null;
                }
            }

            t.text = "쿡!";
            t.color = Color.white;
            float el2 = 0f;
            while (el2 < 0.5f)
            {
                el2 += Time.deltaTime;
                t.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.8f, el2 * 2f);
                t.color = new Color(1f, 1f, 1f, 1f - el2 * 2f);
                yield return null;
            }
            Destroy(canvasGo);

            Begin();
        }

        public void Begin()
        {
            ScoreTracker.Reset();
            StartCoroutine(WatchSongEnd());

            Beatmap map;
            AudioClip clip;
            if (SongSelection.Map != null && SongSelection.Clip != null)
            {
                map = SongSelection.Map;
                clip = SongSelection.Clip;
            }
            else
            {
                map = Beatmap.FromJson(beatmapJson.text);
                clip = songClip;
            }

            // 대형 연타/홀드 이벤트 선정 + 주변 정리 — 판정 큐와 스포너가 같은 맵을 공유
            // (슬로우 컷은 노트 타입에서 제거 — 일반 Perfect 컷에 랜덤 연출로 나온다)
            IngredientSpawner.MarkBigEvents(map, spawner.bigStrength, spawner.bigCooldown, spawner.bigClearWindow);
            IngredientSpawner.MarkHoldEvents(map, spawner.holdStrength, spawner.holdCooldown, spawner.holdBeats);

            BeatJudge.Instance.Load(map);
            spawner.Begin(map);
            Conductor.Instance.bpm = map.bpm;
            Conductor.Instance.firstBeatOffset = map.firstBeatOffset;
            Conductor.Instance.Play(clip);
        }

        IEnumerator WatchSongEnd()
        {
            yield return new WaitUntil(() => Conductor.Instance != null && Conductor.Instance.IsPlaying);
            yield return new WaitUntil(() => !Conductor.Instance.IsPlaying);
            yield return new WaitForSeconds(0.8f); // 마지막 파티클 볼 시간
            ResultScreen.Show();
        }
    }
}
