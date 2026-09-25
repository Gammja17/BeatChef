using BeatSlash.Menu;
using BeatSlash.Rhythm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 게임 중 ESC 일시정지. GameFlow가 런타임에 붙인다 — 씬 수정 불필요.
    /// 일시정지 = timeScale 0 + Conductor 위치 동결(오디오 Pause + dsp 재동기화).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        GameObject _canvas;

        void OnDestroy()
        {
            // 씬 전환 안전망 — 멈춘 채로 다음 씬 가는 사고 방지
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (SettingsPanel.IsOpen) SettingsPanel.Close();
            else if (IsPaused) Resume();
            else Pause();
        }

        void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            Conductor.Instance?.Pause();
            BuildCanvas();
        }

        void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            Conductor.Instance?.Resume();
            if (_canvas != null) Destroy(_canvas);
        }

        void GoToMenu()
        {
            Resume(); // timeScale/Conductor 정리 후 전환
            SceneManager.LoadScene("MainMenu");
        }

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            _canvas = new GameObject("PauseCanvas");
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // HUD/카운트다운보다 위
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 폭/높이 균형 — 모바일 포함 어떤 비율에서도 비례 유지
            _canvas.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("Dim");
            dim.transform.SetParent(_canvas.transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            MakeText(_canvas.transform, "Title", "일시정지", 72, new Vector2(0f, 210f), new Color(1f, 0.85f, 0.2f));
            MakeButton(_canvas.transform, "계속하기", new Vector2(0f, 70f), Resume);
            MakeButton(_canvas.transform, "설정", new Vector2(0f, -45f), () => SettingsPanel.Toggle(_canvas.transform));
            MakeButton(_canvas.transform, "메인 메뉴", new Vector2(0f, -160f), GoToMenu);
        }

        // ── UI 헬퍼 ──────────────────────────────────────────────
        static Text MakeText(Transform parent, string name, string content, int size, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
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

        static void MakeButton(Transform parent, string label, Vector2 pos, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = UISprites.PlainFrame; // 공용 단색 테두리 스킨 (메뉴/설정과 통일)
            img.type = Image.Type.Sliced;
            img.color = new Color(0.85f, 0.8f, 1f, 0.85f);
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            btn.onClick.AddListener(() => onClick());

            var t = MakeText(go.transform, "Label", label, 36, Vector2.zero, Color.white);
            // 글자 실측 크기 + 여백으로 버튼 크기 지정 — 텍스트 삐져나옴 방지
            rt.sizeDelta = new Vector2(t.preferredWidth + 140f, t.preferredHeight + 34f);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
        }
    }
}
