using UnityEngine;

namespace BeatSlash
{
    /// <summary>
    /// 런타임 UI 공용 폰트. WebGL엔 OS 폰트 폴백이 없어서
    /// 한글은 Assets/Resources/Fonts/UIFont.ttf (예: Noto Sans KR)를 넣어야 나온다.
    /// 없으면 유니티 내장 폰트(라틴 전용)로 폴백.
    /// </summary>
    public static class UIFont
    {
        static Font _font;
        static bool _loaded;

        /// <summary>커스텀(한글 지원) 폰트가 로드됐는지 — 화살표 글리프 선택에 사용.</summary>
        public static bool HasCustom { get; private set; }

        public static Font Get()
        {
            if (!_loaded)
            {
                _font = Resources.Load<Font>("Fonts/UIFont");
                HasCustom = _font != null;
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _loaded = true;
            }
            return _font;
        }
    }
}
