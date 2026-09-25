using UnityEngine;

namespace BeatSlash.Juice
{
    /// <summary>
    /// 피벗(썰기 존)을 중심으로 천천히 도는 카메라 무버.
    /// CameraTarget에 붙인다 — 픽셀 카메라 매니저가 이 트랜스폼을 따라간다.
    /// CameraPunch와 같이 있으면 basePosition을 갱신해 펀치와 겹쳐도 안 싸운다.
    /// </summary>
    public class CameraOrbit : MonoBehaviour
    {
        public Transform pivot;
        [Tooltip("초당 회전 각도 — 14면 약 26초에 한 바퀴")]
        public float degreesPerSecond = 14f;
        [Tooltip("바라볼 지점의 피벗 기준 높이 오프셋")]
        public float lookHeightOffset = 0f;

        [Header("피버 — 요리사의 내적 댄스타임")]
        [Tooltip("피버 중 회전 속도")]
        public float feverSpeed = 45f;
        [Tooltip("피버 중 상하 출렁임 폭")]
        public float feverBobAmplitude = 0.9f;

        [Header("연타 다이브 — 사선 클로즈업")]
        [Tooltip("연타 중 반경 축소 비율 (0.45 = 절반 이하로 접근)")]
        public float diveRadiusFactor = 0.45f;
        [Tooltip("연타 진입 시 사선으로 홱 도는 각도")]
        public float diveAngleSwing = 40f;

        public static CameraOrbit Instance { get; private set; }

        /// <summary>FeverMode가 켜고 끈다.</summary>
        public bool Fever { get; set; }

        void Awake() => Instance = this;

        /// <summary>히트 순간 궤도를 홱 돌리는 앵글 휩 — 컷신 느낌. 부호로 방향.</summary>
        public void AngleKick(float degrees) => _kick += degrees;

        float _angle;
        float _radius;
        float _height;
        float _speed;
        float _bob;
        int _flipSign = 1;
        float _flipTimer;
        float _dive;          // 0=평소, 1=연타 클로즈업
        float _diveAngle;     // 다이브 사선 오프셋 (진입 때마다 좌/우 랜덤)
        float _kick;          // 히트 앵글 휩 (감쇠하는 각도 오프셋)
        bool _wasMash;
        CameraPunch _punch;

        void Start()
        {
            _punch = GetComponent<CameraPunch>();
            if (pivot == null)
            {
                enabled = false;
                return;
            }
            // 현재 위치에서 반경/높이/시작 각도를 역산 — 씬 배치 그대로 이어서 돈다
            var off = transform.position - pivot.position;
            _angle = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
            _radius = new Vector2(off.x, off.z).magnitude;
            _height = transform.position.y;
            _speed = degreesPerSecond;
        }

        void Update()
        {
            // 목표 속도로 부드럽게 수렴 — 피버 진입/해제가 덜컥거리지 않게
            float target = Fever ? feverSpeed * _flipSign : degreesPerSecond;
            _speed = Mathf.Lerp(_speed, target, Time.deltaTime * 2.5f);

            if (Fever)
            {
                // 무작위 타이밍에 회전 방향 반전 + 상하 출렁임 = 정신없는 댄스캠
                _flipTimer -= Time.deltaTime;
                if (_flipTimer <= 0f)
                {
                    _flipTimer = Random.Range(0.9f, 2f);
                    if (Random.value < 0.6f) _flipSign = -_flipSign;
                }
                _bob = Mathf.Lerp(_bob, Mathf.Sin(Time.time * 3.2f) * feverBobAmplitude, Time.deltaTime * 4f);
            }
            else
            {
                _flipSign = 1;
                _bob = Mathf.Lerp(_bob, 0f, Time.deltaTime * 3f);
            }

            // 연타 다이브: 반경 확 줄이고 눈높이로 내려가서 사선 각도로 붙는다
            bool mash = Gameplay.SlashController.MashActive;
            if (mash && !_wasMash)
                _diveAngle = (Random.value < 0.5f ? 1f : -1f) * diveAngleSwing;
            _wasMash = mash;
            _dive = Mathf.Lerp(_dive, mash ? 1f : 0f, Time.deltaTime * 5f);

            // 앵글 휩은 빠르게 감쇠 — 홱 돌았다가 원 궤도로 스르륵 복귀
            _kick = Mathf.Lerp(_kick, 0f, Time.deltaTime * 4.5f);

            _angle += _speed * Time.deltaTime;
            float useRadius = _radius * Mathf.Lerp(1f, diveRadiusFactor, _dive);
            float rad = (_angle + _diveAngle * _dive + _kick) * Mathf.Deg2Rad;
            var p = pivot.position;
            float useHeight = Mathf.Lerp(_height + _bob, p.y + 0.9f, _dive * 0.75f);
            var pos = new Vector3(p.x + Mathf.Sin(rad) * useRadius, useHeight, p.z + Mathf.Cos(rad) * useRadius);

            if (_punch != null) _punch.basePosition = pos; // 펀치가 LateUpdate에서 최종 적용
            else transform.position = pos;

            transform.rotation = Quaternion.LookRotation(new Vector3(p.x, p.y + lookHeightOffset, p.z) - pos);
        }
    }
}
