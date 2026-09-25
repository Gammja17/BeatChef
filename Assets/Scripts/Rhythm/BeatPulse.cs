using UnityEngine;

namespace BeatSlash.Rhythm
{
    /// <summary>붙이면 매 박마다 스케일이 튀었다가 돌아오는 연출. 조명 세기/이미시브에도 응용.</summary>
    public class BeatPulse : MonoBehaviour
    {
        [Tooltip("박마다 커지는 배율")]
        public float pulseScale = 1.15f;
        [Tooltip("원래 크기로 돌아오는 속도")]
        public float returnSpeed = 8f;
        [Tooltip("N박마다 한 번 (1=매 박)")]
        public int everyNBeats = 1;

        Vector3 _baseScale;

        void Start()
        {
            _baseScale = transform.localScale;
            if (Conductor.Instance != null) Conductor.Instance.OnBeat += HandleBeat;
        }

        void OnDestroy()
        {
            if (Conductor.Instance != null) Conductor.Instance.OnBeat -= HandleBeat;
        }

        void HandleBeat(int beat)
        {
            if (beat % Mathf.Max(1, everyNBeats) == 0)
                transform.localScale = _baseScale * pulseScale;
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * returnSpeed);
        }
    }
}
