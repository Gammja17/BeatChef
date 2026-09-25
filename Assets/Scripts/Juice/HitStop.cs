using System.Collections;
using UnityEngine;

namespace BeatSlash.Juice
{
    /// <summary>타격 순간 짧은 시간 정지. Perfect 판정 때 0.04~0.06초 정도가 적당.</summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        Coroutine _running;

        void Awake() => Instance = this;

        public void Do(float duration = 0.05f)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(duration));
        }

        IEnumerator Run(float duration)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            // 일시정지 중이면 복원 금지 — 히트스톱이 일시정지를 풀어버리는 사고 방지
            if (!Gameplay.PauseMenu.IsPaused)
                Time.timeScale = prev <= 0f ? 1f : prev;
            _running = null;
        }
    }
}
