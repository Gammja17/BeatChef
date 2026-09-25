using System;
using BeatSlash.Rhythm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeatSlash.Menu
{
    /// <summary>
    /// 공용 설정 모달 — 메인 메뉴/일시정지 어디서든 Open(canvasRoot)로 띄운다.
    /// BGM/SFX 볼륨 + 판정 타이밍 슬라이더. 값은 즉시 반영/저장.
    /// </summary>
    public static class SettingsPanel
    {
        static GameObject _root;
        public static bool IsOpen => _root != null;

        static readonly Color Gold = new Color(1f, 0.85f, 0.2f);

        public static void Toggle(Transform canvasRoot)
        {
            if (IsOpen) Close();
            else Open(canvasRoot);
        }

        public static void Open(Transform canvasRoot)
        {
            if (IsOpen) return;
            EnsureEventSystem();

            // 어두운 딤 — 뒤 클릭 차단
            _root = new GameObject("SettingsPanel");
            _root.transform.SetParent(canvasRoot, false);
            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            var drt = _root.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;

            // 패널 본체
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);
            var pImg = panel.AddComponent<Image>();
            if (UISprites.Panel != null)
            {
                pImg.sprite = UISprites.Panel;
                pImg.color = Color.white;
            }
            else
            {
                pImg.color = new Color(0.09f, 0.06f, 0.12f, 0.97f); // 다크 퍼플 폴백
            }
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1140f, 620f);

            // 스킨 스프라이트의 무지개 테두리(~45px)를 피해서 안쪽에만 배치
            MakeText(panel.transform, "Title", "설정", 50, new Vector2(0f, 195f), Gold);

            MakeSlider(panel.transform, "브금", new Vector2(0f, 100f), 0f, 1f, GameSettings.BgmVolume,
                v => GameSettings.BgmVolume = v, v => $"{Mathf.RoundToInt(v * 100)}%");

            MakeSlider(panel.transform, "효과음", new Vector2(0f, 15f), 0f, 1f, GameSettings.SfxVolume,
                v => GameSettings.SfxVolume = v, v => $"{Mathf.RoundToInt(v * 100)}%");

            float timing = PlayerPrefs.GetFloat(GameSettings.KeyTiming, 0.035f);
            MakeSlider(panel.transform, "판정 타이밍", new Vector2(0f, -70f), -0.15f, 0.15f, timing,
                v =>
                {
                    if (Conductor.Instance != null) Conductor.Instance.SetTimingOffset(v);
                    else { PlayerPrefs.SetFloat(GameSettings.KeyTiming, v); PlayerPrefs.Save(); }
                },
                v => $"{v * 1000f:+0;-0}ms");

            MakeText(panel.transform, "TimingHint", "화살표가 음악보다 빠르게 느껴지면 +, 늦으면 -  (게임 중 F1/F2)", 22,
                new Vector2(0f, -125f), new Color(1f, 1f, 1f, 0.5f));

            MakeButton(panel.transform, "닫기", new Vector2(0f, -200f), Close);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            GameSettings.Save();
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        static Texture2D _rainbowTex;

        /// <summary>가로 무지개 그라데이션 1줄 텍스처 — 슬라이더 필 테두리용.</summary>
        static Texture2D RainbowTex()
        {
            if (_rainbowTex != null) return _rainbowTex;
            _rainbowTex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
                _rainbowTex.SetPixel(x, 0, Color.HSVToRGB(x / 255f * 0.85f, 0.9f, 1f));
            _rainbowTex.Apply();
            return _rainbowTex;
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        // ── UI 헬퍼 ──────────────────────────────────────────────
        static void MakeSlider(Transform parent, string label, Vector2 pos,
            float min, float max, float value, Action<float> onChanged, Func<float, string> fmt)
        {
            MakeText(parent, label + "Label", label, 30, pos + new Vector2(-390f, 0f), Color.white);
            var valueText = MakeText(parent, label + "Value", fmt(value), 28, pos + new Vector2(395f, 0f),
                new Color(1f, 1f, 1f, 0.8f));

            var go = new GameObject("Slider_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos + new Vector2(45f, 0f);
            rt.sizeDelta = new Vector2(520f, 28f);

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.12f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = faRt.offsetMax = Vector2.zero;
            // 필: 무지개 테두리 + 흰색 속 (테두리 = 무지개 그라데이션 레이어, 속 = 3px 인셋 흰색)
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillBorder = fill.AddComponent<RawImage>();
            fillBorder.texture = RainbowTex();
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;

            var fillInner = new GameObject("FillInner");
            fillInner.transform.SetParent(fill.transform, false);
            var innerImg = fillInner.AddComponent<Image>();
            innerImg.color = Color.white;
            innerImg.raycastTarget = false;
            var innerRt = fillInner.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(3f, 3f);
            innerRt.offsetMax = new Vector2(-3f, -3f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(go.transform, false);
            var hImg = handle.AddComponent<Image>();
            var hRt = handle.GetComponent<RectTransform>();
            if (UISprites.Knob != null)
            {
                hImg.sprite = UISprites.Knob; // 무지개 음표 노브
                hImg.color = Color.white;
                hRt.sizeDelta = new Vector2(52f, 52f);
            }
            else
            {
                hImg.color = Color.white;
                hRt.sizeDelta = new Vector2(26f, 26f);
            }

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                onChanged(v);
                valueText.text = fmt(v);
            });
        }

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

        static void MakeButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = UISprites.PlainFrame; // 공용 단색 테두리 스킨 (곡 선택 화면과 통일)
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

            var t = MakeText(go.transform, "Label", label, 32, Vector2.zero, Color.white);
            // 글자 실측 크기 + 여백으로 버튼 크기 지정 — 텍스트 삐져나옴 방지
            rt.sizeDelta = new Vector2(t.preferredWidth + 150f, t.preferredHeight + 46f);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
        }
    }
}
