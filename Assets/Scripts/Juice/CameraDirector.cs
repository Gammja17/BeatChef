using BeatSlash.Gameplay;
using BeatSlash.Rhythm;
using UnityEngine;
using PixelCamera;

namespace BeatSlash.Juice
{
    /// <summary>
    /// 카메라 줌 연출 총괄 — 게임 카메라(PixelCameraManager와 같은 오브젝트)에 붙인다.
    /// 비트마다 미세 줌 펄스, 대형 재료 연타 중 클로즈업, 피버 중 펄스 증폭.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        [Tooltip("비트 줌 펄스 크기 (오소 사이즈 감소량)")]
        public float beatPulse = 0.16f;
        [Tooltip("연타 클로즈업 줌 (오소 사이즈 감소량)")]
        public float mashZoomIn = 1.5f;
        public float feverPulseMultiplier = 2.2f;

        public static CameraDirector Instance { get; private set; }

        [Tooltip("일반 히트 줌 킥 (Perfect 기준, Good은 절반) — 히트 순간만 확 박고 빠르게 복귀")]
        public float hitKick = 0.95f;

        [Tooltip("피버 중 유지 줌 — 피버는 어차피 혼돈이니 들어가도 됨")]
        public float feverZoom = 0.6f;

        PixelCameraManager _mgr;
        float _baseZoom;
        float _pulse;
        float _zoom; // 현재 적용 중인 추가 줌 (연타)

        void Awake() => Instance = this;

        /// <summary>히트 순간 줌 킥 — 대형 클로즈업보다 얕고 빠르게 튕긴다.</summary>
        public void HitKick(bool perfect)
        {
            _pulse = Mathf.Max(_pulse, hitKick * (perfect ? 1f : 0.5f));
        }

        void Start()
        {
            _mgr = GetComponent<PixelCameraManager>();
            if (_mgr == null) { enabled = false; return; }
            _baseZoom = _mgr.GameCameraZoom;
            if (Conductor.Instance != null) Conductor.Instance.OnBeat += HandleBeat;
        }

        void OnDestroy()
        {
            if (Conductor.Instance != null) Conductor.Instance.OnBeat -= HandleBeat;
        }

        void HandleBeat(int beat)
        {
            bool fever = FeverMode.Instance != null && FeverMode.Instance.IsFever;
            _pulse = beatPulse * (fever ? feverPulseMultiplier : 1f);
        }

        void LateUpdate()
        {
            _pulse = Mathf.Lerp(_pulse, 0f, Time.deltaTime * 8f);

            // 유지 줌은 "읽을 필요 없는 순간"에만: 연타(대형) > 피버. 평상시엔 넓게 = 재료 잘 보임
            bool fever = FeverMode.Instance != null && FeverMode.Instance.IsFever;
            float targetZoom = SlashController.MashActive || SlashController.SlowActive
                ? mashZoomIn
                : fever ? feverZoom : 0f;
            _zoom = Mathf.Lerp(_zoom, targetZoom, Time.deltaTime * 6f);

            _mgr.GameCameraZoom = _baseZoom - _pulse - _zoom;
        }
    }
}
