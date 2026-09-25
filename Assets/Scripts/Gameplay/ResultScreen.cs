using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 곡 종료 후 결과 화면 — 점수에 따라 얼마나 괜찮은 요리가 완성됐는지 보여준다.
    /// GameFlow가 ResultScreen.Show()로 띄운다. UI는 런타임 생성.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        public static void Show()
        {
            new GameObject("ResultScreen").AddComponent<ResultScreen>();
        }

        void Start()
        {
            // 버튼 클릭용 이벤트 시스템 (게임플레이 씬엔 없다)
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("ResultCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 폭/높이 균형 — 모바일 포함 어떤 비율에서도 비례 유지
            canvasGo.AddComponent<GraphicRaycaster>();

            // 어두운 배경
            var dim = new GameObject("Dim");
            dim.transform.SetParent(canvasGo.transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            var dimRt = dim.GetComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.sizeDelta = Vector2.zero;

            float acc = ScoreTracker.Accuracy;
            // 등급 F~S: 정확도 기반 6단계 — 꾸진 요리에서 초호화 요리까지
            (string rank, string title, Color color) = acc switch
            {
                >= 0.95f => ("S", "전설의 요리 탄생!", new Color(1f, 0.85f, 0.2f)),
                >= 0.85f => ("A", "훌륭한 요리!", new Color(0.4f, 1f, 0.5f)),
                >= 0.7f => ("B", "제법 그럴듯한 요리", new Color(0.3f, 0.9f, 1f)),
                >= 0.55f => ("C", "먹을 만한 요리", new Color(0.85f, 0.85f, 0.85f)),
                >= 0.4f => ("D", "대충 만든 요리...", new Color(0.6f, 0.55f, 0.5f)),
                _ => ("F", "요리가 타버렸다...", new Color(1f, 0.4f, 0.3f)),
            };

            // 선택한 요리 이름으로 완성 헤더 (셰프 마음대로면 그냥 "요리 완성!")
            string dishHeader = Menu.SongSelection.Dish > 0
                ? $"{Menu.SongSelection.DishLabel} 완성!"
                : "요리 완성!";
            MakeText(canvasGo.transform, dishHeader, 48, new Vector2(0f, 400f), new Color(1f, 1f, 1f, 0.7f));
            MakeText(canvasGo.transform, title, 80, new Vector2(0f, 320f), color);

            // 등급별 요리 이미지 + 대형 랭크 알파벳 (이미지 없으면 알파벳만 중앙에)
            // 선택한 요리 전용 세트 우선 (dish_salad_a 등), 없으면 공용 세트 폴백
            string dishKey = Menu.SongSelection.DishKey;
            var dish = dishKey != null ? Resources.Load<Sprite>($"UI/dish_{dishKey}_{rank.ToLower()}") : null;
            if (dish == null) dish = Resources.Load<Sprite>($"UI/dish_{rank.ToLower()}");
            if (dish != null)
            {
                var dishGo = new GameObject("Dish");
                dishGo.transform.SetParent(canvasGo.transform, false);
                var dishImg = dishGo.AddComponent<Image>();
                dishImg.sprite = dish;
                dishImg.preserveAspect = true;
                dishImg.raycastTarget = false;
                var drt2 = dishImg.rectTransform;
                drt2.anchorMin = drt2.anchorMax = new Vector2(0.5f, 0.5f);
                drt2.anchoredPosition = new Vector2(-90f, 90f);
                drt2.sizeDelta = new Vector2(340f, 340f);

                var rankText = MakeText(canvasGo.transform, rank, 220, new Vector2(200f, 90f), color);
                rankText.fontStyle = FontStyle.Bold;
            }
            else
            {
                MakeText(canvasGo.transform, rank, 220, new Vector2(0f, 90f), color);
            }

            MakeText(canvasGo.transform, $"{ScoreTracker.Score:N0}", 90, new Vector2(0f, -130f), Color.white);
            MakeText(canvasGo.transform, $"정확도 {acc * 100f:F1}%   최대 콤보 {ScoreTracker.MaxCombo}", 40, new Vector2(0f, -205f), new Color(1f, 1f, 1f, 0.85f));
            MakeText(canvasGo.transform,
                $"PERFECT {ScoreTracker.Perfect}   GOOD {ScoreTracker.Good}   MISS {ScoreTracker.Miss}",
                34, new Vector2(0f, -260f), new Color(1f, 1f, 1f, 0.6f));

            // 하이스코어: 곡×난이도별 기록 갱신 판정
            string songName = Menu.SongSelection.Clip != null ? Menu.SongSelection.Clip.name : "unknown";
            int prevBest = HighScores.GetSong(songName, Menu.SongSelection.Difficulty);
            bool newRecord = HighScores.Submit(songName, Menu.SongSelection.Difficulty, ScoreTracker.Score, ScoreTracker.MaxCombo);
            if (newRecord)
            {
                var rec = MakeText(canvasGo.transform, "★ 신기록! ★", 44, new Vector2(330f, -125f), new Color(1f, 0.85f, 0.2f));
                rec.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                if (prevBest > 0)
                    MakeText(canvasGo.transform, $"이전 기록 {prevBest:N0}", 26, new Vector2(0f, -300f), new Color(1f, 1f, 1f, 0.45f));
            }
            else if (prevBest > 0)
            {
                MakeText(canvasGo.transform, $"최고 기록 {prevBest:N0}", 26, new Vector2(0f, -300f), new Color(1f, 1f, 1f, 0.45f));
            }

            MakeButton(canvasGo.transform, "다시 요리", new Vector2(-180f, -350f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            MakeButton(canvasGo.transform, "메뉴로", new Vector2(180f, -350f),
                () => SceneManager.LoadScene("MainMenu"));
        }

        static Text MakeText(Transform parent, string content, int size, Vector2 pos, Color color)
        {
            var go = new GameObject("T_" + content);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UIFont.Get();
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            return t;
        }

        static void MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0.2f, 0.2f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(300f, 80f);

            var t = MakeText(go.transform, label, 40, Vector2.zero, Color.white);
            t.rectTransform.sizeDelta = rt.sizeDelta;
        }
    }
}
