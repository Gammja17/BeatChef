using System;
using UnityEngine;

namespace BeatSlash
{
    /// <summary>
    /// 전역 설정 (볼륨/타이밍). PlayerPrefs 저장.
    /// 타이밍 오프셋은 Conductor의 "timing_offset" 키를 그대로 공유한다 — F1/F2 조절과 슬라이더가 같은 값.
    /// </summary>
    public static class GameSettings
    {
        const string KeyBgm = "opt_bgm_volume";
        const string KeySfx = "opt_sfx_volume";
        public const string KeyTiming = "timing_offset";

        static float _bgm = -1f;
        static float _sfx = -1f;

        /// <summary>값이 바뀔 때 라이브 반영용 — 구독자는 OnDestroy에서 해제할 것.</summary>
        public static event Action<float> OnBgmChanged;
        public static event Action<float> OnSfxChanged;

        public static float BgmVolume
        {
            get
            {
                if (_bgm < 0f) _bgm = PlayerPrefs.GetFloat(KeyBgm, 0.8f);
                return _bgm;
            }
            set
            {
                _bgm = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyBgm, _bgm);
                OnBgmChanged?.Invoke(_bgm);
            }
        }

        public static float SfxVolume
        {
            get
            {
                if (_sfx < 0f) _sfx = PlayerPrefs.GetFloat(KeySfx, 0.9f);
                return _sfx;
            }
            set
            {
                _sfx = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeySfx, _sfx);
                OnSfxChanged?.Invoke(_sfx);
            }
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
