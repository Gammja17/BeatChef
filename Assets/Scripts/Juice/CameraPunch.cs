using BeatSlash.Rhythm;
using UnityEngine;

namespace BeatSlash.Juice
{
    /// <summary>카메라에 붙여서 Punch() 호출 시 흔들림 + FOV 킥. 픽셀 카메라 부모에 붙일 것.</summary>
    public class CameraPunch : MonoBehaviour
    {
        public static CameraPunch Instance { get; private set; }

        [Tooltip("흔들림 감쇠 속도")]
        public float damping = 10f;
        public float defaultStrength = 0.25f;
        [Tooltip("펀치 강도당 롤 각도(도) — 화면이 기울었다 복원")]
        public float rollPerStrength = 18f;
        [Tooltip("매 비트 카메라가 아래로 까딱하는 거리 — 쿵쿵 그루브")]
        public float beatNod = 0.09f;

        [Tooltip("펀치가 더해질 기준 위치 — CameraOrbit 같은 무버가 매 프레임 갱신 가능")]
        public Vector3 basePosition;
        Vector3 _offset;
        float _roll;
        CameraOrbit _orbit;

        void Awake()
        {
            Instance = this;
            basePosition = transform.localPosition;
            _orbit = GetComponent<CameraOrbit>();
        }

        void Start()
        {
            if (Conductor.Instance != null) Conductor.Instance.OnBeat += HandleBeat;
        }

        void OnDestroy()
        {
            if (Conductor.Instance != null) Conductor.Instance.OnBeat -= HandleBeat;
        }

        void HandleBeat(int beat)
        {
            _offset += Vector3.down * beatNod;
        }

        public void Punch(float strength = -1f)
        {
            if (strength < 0f) strength = defaultStrength;
            _offset = Random.insideUnitSphere * strength;
            _roll = (Random.value < 0.5f ? -1f : 1f) * strength * rollPerStrength;
        }

        void LateUpdate()
        {
            float k = Time.unscaledDeltaTime * damping;
            _offset = Vector3.Lerp(_offset, Vector3.zero, k);
            _roll = Mathf.Lerp(_roll, 0f, k);
            transform.localPosition = basePosition + _offset;
            // 궤도가 매 프레임 rotation을 새로 쓸 때만 롤을 얹는다 — 아니면 곱셈 누적으로 기울어짐
            if (_orbit != null && _orbit.enabled)
                transform.localRotation *= Quaternion.Euler(0f, 0f, _roll);
        }
    }
}
