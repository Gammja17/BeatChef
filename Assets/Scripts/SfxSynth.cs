using UnityEngine;

namespace BeatSlash
{
    /// <summary>
    /// 런타임 효과음 신디사이저 — 에셋 없이 타이코식 리듬게임 톤을 만든다.
    /// Don(동): 묵직한 저음 타격, Ting(팅): Perfect용 맑은 종소리, Swish: 헛스윙 바람.
    /// 웹 포함 전 플랫폼 동작 (AudioClip.Create).
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 44100;

        /// <summary>묵직한 타격 "동" — 피치가 살짝 떨어지는 저음 + 초반 노이즈 트랜지언트.</summary>
        public static AudioClip Don()
        {
            const float dur = 0.11f;
            int n = (int)(Rate * dur);
            var d = new float[n];
            float phase = 0f;
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float freq = 175f * (1f - 0.4f * (t / dur)); // 피치 다운 스윕
                phase += 2f * Mathf.PI * freq / Rate;
                float env = Mathf.Exp(-t * 34f);
                float s = Mathf.Sin(phase) * env;
                if (t < 0.006f) // 타격감의 핵심: 짧은 노이즈 어택
                    s += ((float)rng.NextDouble() * 2f - 1f) * 0.4f * (1f - t / 0.006f);
                d[i] = Mathf.Clamp(s * 0.85f, -1f, 1f);
            }
            return Make("sfx_don", d);
        }

        /// <summary>맑은 "팅" — Perfect 전용 보상음. 배음 두 개의 짧은 종소리.</summary>
        public static AudioClip Ting()
        {
            const float dur = 0.18f;
            int n = (int)(Rate * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 20f);
                float s = Mathf.Sin(2f * Mathf.PI * 1860f * t) * 0.55f
                        + Mathf.Sin(2f * Mathf.PI * 2794f * t) * 0.25f; // 단3도 위 배음
                d[i] = Mathf.Clamp(s * env * 0.5f, -1f, 1f);
            }
            return Make("sfx_ting", d);
        }

        /// <summary>헛스윙 "휙" — 하이패스 느낌의 부드러운 노이즈.</summary>
        public static AudioClip Swish()
        {
            const float dur = 0.09f;
            int n = (int)(Rate * dur);
            var d = new float[n];
            var rng = new System.Random(13);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, white, 0.22f);
                float hp = white - lp; // 저음 제거 → 바람 소리
                float env = Mathf.Sin(Mathf.PI * t / dur); // 부드럽게 떴다 사라짐
                d[i] = Mathf.Clamp(hp * env * 0.35f, -1f, 1f);
            }
            return Make("sfx_swish", d);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
