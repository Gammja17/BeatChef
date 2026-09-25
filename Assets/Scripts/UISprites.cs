using UnityEngine;

namespace BeatSlash
{
    /// <summary>
    /// AI 생성 UI 스킨 (Assets/Resources/UI). 없으면 null — 호출부는 플랫 컬러로 폴백.
    /// </summary>
    public static class UISprites
    {
        static bool _loaded;
        static Sprite _panel, _knob, _gear;
        static Sprite _buttonFrame;
        static readonly Sprite[] _arrows = new Sprite[4];

        public static Sprite Panel { get { Load(); return _panel; } }
        public static Sprite Knob { get { Load(); return _knob; } }
        public static Sprite Gear { get { Load(); return _gear; } }

        /// <summary>버튼 프레임: 무지개 테두리 + 투명한 속 — 글자가 절대 안 묻힌다. 9-slice용.
        /// AI 생성 프레임(UI/button_frame) 우선, 없으면 프로시저럴 폴백.</summary>
        public static Sprite ButtonFrame
        {
            get
            {
                if (_buttonFrame != null) return _buttonFrame;

                var aiTex = Resources.Load<Texture2D>("UI/button_frame");
                if (aiTex != null)
                {
                    // 9-slice는 그라데이션 테두리를 단색 블록으로 조각내서 못 씀 — 통짜 스트레치
                    _buttonFrame = Sprite.Create(aiTex, new Rect(0, 0, aiTex.width, aiTex.height),
                        new Vector2(0.5f, 0.5f));
                    return _buttonFrame;
                }

                const int s = 48;
                const int border = 7;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point, // 픽셀 톤
                };
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        bool edge = x < border || x >= s - border || y < border || y >= s - border;
                        tex.SetPixel(x, y, edge
                            ? Color.HSVToRGB(x / (float)(s - 1) * 0.85f, 0.9f, 1f)
                            : Color.clear);
                    }
                }
                tex.Apply();
                _buttonFrame = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(10f, 10f, 10f, 10f));
                return _buttonFrame;
            }
        }

        static Sprite _plainFrame;

        /// <summary>단색 얇은 테두리 + 어두운 속 9-슬라이스 (흰색 — 틴트로 색 지정). 공용 버튼 스킨.</summary>
        public static Sprite PlainFrame
        {
            get
            {
                if (_plainFrame != null) return _plainFrame;
                const int s = 32;
                const int border = 3;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        bool edge = x < border || x >= s - border || y < border || y >= s - border;
                        tex.SetPixel(x, y, edge ? Color.white : new Color(0f, 0f, 0f, 0.35f));
                    }
                tex.Apply();
                _plainFrame = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(6f, 6f, 6f, 6f));
                return _plainFrame;
            }
        }

        /// <summary>노트 화살표 (0좌 1우 2상 3하). 없으면 null — 텍스트 글리프 폴백.</summary>
        public static Sprite Arrow(int lane)
        {
            Load();
            return lane >= 0 && lane < 4 ? _arrows[lane] : null;
        }

        static void Load()
        {
            if (_loaded) return;
            _panel = Resources.Load<Sprite>("UI/panel");
            _knob = Resources.Load<Sprite>("UI/note_knob");
            _gear = Resources.Load<Sprite>("UI/gear");
            _arrows[0] = Resources.Load<Sprite>("UI/arrow_left");
            _arrows[1] = Resources.Load<Sprite>("UI/arrow_right");
            _arrows[2] = Resources.Load<Sprite>("UI/arrow_up");
            _arrows[3] = Resources.Load<Sprite>("UI/arrow_down");
            _loaded = true;
        }
    }
}
