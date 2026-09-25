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
        [DllImport("__Internal")] static extern void BeatChef_ListSavedSongs(string goName);
        [DllImport("__Internal")] static extern void BeatChef_LoadSavedSong(string goName, string songName);
        [DllImport("__Internal")] static extern void BeatChef_DeleteSavedSong(string songName);
#endif

        /// <summary>(클립, 모노 샘플, 샘플레이트) — 샘플은 온셋 분석에 직접 사용 (웹에선 GetData 불가).</summary>
        public Action<AudioClip, float[], int> OnLoaded;
        public Action<string> OnStatus;
        public Action<string> OnError;
        /// <summary>브라우저(IndexedDB)에 저장된 업로드 곡 이름 목록.</summary>
        public Action<string[]> OnSavedList;

        public void Open()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BeatChef_OpenFilePicker(gameObject.name);
#else
            OnError?.Invoke("웹 빌드에서만 동작합니다");
#endif
        }

        public void RequestSavedList()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BeatChef_ListSavedSongs(gameObject.name);
#endif
        }

        /// <summary>저장된 곡을 불러와 디코딩 — 끝나면 OnLoaded로 온다.</summary>
        public void LoadSaved(string songName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BeatChef_LoadSavedSong(gameObject.name, songName);
#endif
        }

        public void DeleteSaved(string songName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BeatChef_DeleteSavedSong(songName);
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

        void OnSavedSongList(string joined)
        {
            OnSavedList?.Invoke(string.IsNullOrEmpty(joined) ? Array.Empty<string>() : joined.Split('\n'));
        }

        void OnWebSongSaved(string songName)
        {
            RequestSavedList(); // 저장 끝나면 목록 갱신
        }
    }
}
