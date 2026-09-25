using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BeatSlash.Menu
{
    /// <summary>
    /// WebGL 곡 업로드 브릿지. 브라우저 파일 피커를 열고,
    /// Web Audio가 디코딩한 샘플을 받아 AudioClip으로 만든다.
    /// jslib(SendMessage)가 이 컴포넌트의 GameObject 이름으로 콜백한다.
    /// </summary>
    public class WebSongUploader : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BeatChef_OpenFilePicker(string goName);
        [DllImport("__Internal")] static extern int BeatChef_CopySamples(float[] buffer, int length);
#endif

        /// <summary>(클립, 모노 샘플, 샘플레이트) — 샘플은 온셋 분석에 직접 사용 (웹에선 GetData 불가).</summary>
        public Action<AudioClip, float[], int> OnLoaded;
        public Action<string> OnStatus;
        public Action<string> OnError;

        public void Open()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BeatChef_OpenFilePicker(gameObject.name);
#else
            OnError?.Invoke("웹 빌드에서만 동작합니다");
#endif
        }

        // ── jslib 콜백 (이름 변경 금지) ──────────────────────────
        void OnWebSongDecoding(string songName)
        {
            OnStatus?.Invoke($"'{songName}' 디코딩 중...");
        }

        void OnWebSongDecoded(string payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var parts = payload.Split('|');
            var songName = parts[0];
            int sampleRate = int.Parse(parts[1]);
            int length = int.Parse(parts[2]);

            var samples = new float[length];
            int copied = BeatChef_CopySamples(samples, length);
            if (copied <= 0)
            {
                OnError?.Invoke("샘플 복사 실패");
                return;
            }

            var clip = AudioClip.Create(songName, copied, 1, sampleRate, false);
            clip.SetData(samples, 0);
            OnLoaded?.Invoke(clip, samples, sampleRate);
#endif
        }

        void OnWebSongError(string error)
        {
            OnError?.Invoke(error);
        }
    }
}
