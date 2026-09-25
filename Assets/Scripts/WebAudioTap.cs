using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BeatSlash
{
    /// <summary>
    /// WebGL 실시간 오디오 탭 — Unity가 웹에서 막아둔 GetOutputData/GetSpectrumData를
    /// Web Audio AnalyserNode로 대체해 에디터와 동일한 실측 데이터를 제공한다.
    /// (Plugins/WebGL/WebAudioTap.jslib와 세트)
    /// </summary>
    public static class WebAudioTap
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int WebAudioTap_Init();
        [DllImport("__Internal")] static extern int WebAudioTap_GetWave(float[] data, int len);
        [DllImport("__Internal")] static extern int WebAudioTap_GetSpectrum(float[] data, int len);

        static bool _inited;
        static bool _ok;

        public static bool Available
        {
            get
            {
                if (!_inited)
                {
                    _ok = WebAudioTap_Init() == 1;
                    _inited = true;
                }
                return _ok;
            }
        }

        /// <summary>시간영역 파형 -1..1 — GetOutputData 동등물. 채운 샘플 수 반환(실패 0).</summary>
        public static int GetWave(float[] data) => Available ? WebAudioTap_GetWave(data, data.Length) : 0;

        /// <summary>주파수 스펙트럼(dB) — GetSpectrumData 동등물. 채운 빈 수 반환(실패 0).</summary>
        public static int GetSpectrumDb(float[] data) => Available ? WebAudioTap_GetSpectrum(data, data.Length) : 0;
#else
        public static bool Available => false;
        public static int GetWave(float[] data) => 0;
        public static int GetSpectrumDb(float[] data) => 0;
#endif
    }
}
