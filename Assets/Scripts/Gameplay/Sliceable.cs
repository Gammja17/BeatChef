using DG.Tweening;
using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 썰 수 있는 재료. 런타임 메시 커팅 대신 반쪽 프리팹 2개 스왑 + 과즙 파티클.
    /// 통짜 프리팹에 붙이고, halfPrefab(반쪽 모델)과 juiceColor(재료 색)를 지정.
    /// </summary>
    public class Sliceable : MonoBehaviour
    {
        [Tooltip("잘린 반쪽 모델 (하나를 미러링해서 두 개 생성)")]
        public GameObject halfPrefab;
        [Tooltip("과즙/파편 파티클 (VFX_Klaus, ToonFX에서 고르기)")]
        public ParticleSystem juiceFxPrefab;
        [Tooltip("추가 연출 풀 — 매번 랜덤으로 하나 더 터진다 (다양성)")]
        public ParticleSystem[] extraFxPool;
        [Tooltip("파티클에 입힐 재료 색 (토마토 빨강, 오이 초록...)")]
        public Color juiceColor = Color.red;
        [Tooltip("반쪽이 갈라지는 힘")]
        public float splitImpulse = 3f;
        [Tooltip("칼 방향으로 날아가는 힘 — 누른 키 방향대로 재료가 날아간다")]
        public float slashImpulse = 7f;
        [Tooltip("썰리는 데 필요한 타격 수 — 대형 연타 재료용 (스포너가 설정)")]
        public int hitsRequired = 1;
        [Tooltip("연타 부분 타격 후 공중에 머무는 시간(초) — 연타 여유 시간")]
        public float mashHoverTime = 1.8f;

        int _hits;

        /// <summary>남은 연타 횟수 — HUD 카운터가 표시.</summary>
        public int RemainingHits => Mathf.Max(0, hitsRequired - _hits);

        /// <summary>타격 1회. 다 썰리면 true. 덜 썰렸으면 공중에 잠깐 멈춰 연타 시간을 준다.</summary>
        public bool TakeHit(Vector3 sliceDirection)
        {
            _hits++;
            if (_hits >= hitsRequired)
            {
                Slice(sliceDirection);
                return true;
            }

            // 부분 타격: 소형 과즙 + 공중 정지
            if (juiceFxPrefab != null)
            {
                var fx = Instantiate(juiceFxPrefab, transform.position, Quaternion.identity);
                fx.transform.localScale *= 0.7f;
                var main = fx.main;
                main.startColor = juiceColor;
                Destroy(fx.gameObject, 2f);
            }
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity *= 0.1f;
                rb.useGravity = false;
                CancelInvoke(nameof(RestoreGravity));
                Invoke(nameof(RestoreGravity), mashHoverTime);
            }
            return false;
        }

        void RestoreGravity()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.useGravity = true;
        }

        void Start()
        {
            // 스폰 팝인: 작게 태어나 통통 튀며 원래 크기로
            var baseScale = transform.localScale;
            transform.localScale = baseScale * 0.1f;
            transform.DOScale(baseScale, 0.3f).SetEase(Ease.OutBack, 4f).SetLink(gameObject);
        }

        public void Slice(Vector3 sliceDirection)
        {
            float size = Mathf.Max(0.1f, Mathf.Abs(transform.localScale.x)); // 스포너가 준 크기 배율

            if (halfPrefab == null)
                Debug.LogWarning($"[Sliceable] {name}: halfPrefab 미지정 — 반쪽 없이 사라짐. 프리팹 확인 필요");
            if (halfPrefab != null)
            {
                // 갈라지는 축 = 칼 방향의 수직. 관성은 조금만 남기고 칼 방향 임펄스가 주도한다
                var slash = sliceDirection.normalized;
                var side = Vector3.Cross(slash, Vector3.forward);
                var rb = GetComponent<Rigidbody>();
                var inherit = rb != null ? rb.linearVelocity * 0.25f : Vector3.zero;
                SpawnHalf(side, inherit, slash, size);
                SpawnHalf(-side, inherit, slash, size, mirrored: true);
            }

            if (juiceFxPrefab != null)
            {
                var fx = Instantiate(juiceFxPrefab, transform.position, Quaternion.identity);
                fx.transform.localScale *= 1.5f * size; // 큰 재료 = 큰 폭발
                var main = fx.main;
                main.startColor = juiceColor;
                Destroy(fx.gameObject, 3f);
            }

            // 매번 다른 보너스 이펙트 하나 — 화려함 + 다양성
            if (extraFxPool != null && extraFxPool.Length > 0)
            {
                var extra = extraFxPool[Random.Range(0, extraFxPool.Length)];
                if (extra != null)
                {
                    var fx2 = Instantiate(extra, transform.position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                    fx2.transform.localScale *= 1.2f;
                    Destroy(fx2.gameObject, 3f);
                }
            }

            Destroy(gameObject);
        }

        void SpawnHalf(Vector3 dir, Vector3 inheritVelocity, Vector3 slashDir, float size, bool mirrored = false)
        {
            var half = Instantiate(halfPrefab, transform.position + dir * 0.15f, transform.rotation);
            half.transform.localScale *= size;
            if (mirrored)
            {
                var s = half.transform.localScale;
                half.transform.localScale = new Vector3(-s.x, s.y, s.z);
            }
            // 스쿼시&스트레치: 찌그러진 채 태어나 탄성 복원. 히트스톱 중엔 얼어붙어 임팩트가 산다
            var baseHalfScale = half.transform.localScale;
            half.transform.localScale = Vector3.Scale(baseHalfScale, new Vector3(1.9f, 0.35f, 1.9f));
            half.transform.DOScale(baseHalfScale, 0.5f).SetEase(Ease.OutElastic, 1.2f, 0.3f).SetLink(half);
            if (half.GetComponentInChildren<Collider>() == null)
            {
                var sc = half.AddComponent<SphereCollider>();
                sc.radius = 0.35f; // 바닥에 튕기기용 대략치
            }
            var rb = half.GetComponent<Rigidbody>();
            if (rb == null) rb = half.AddComponent<Rigidbody>();
            rb.linearVelocity = inheritVelocity;
            // 주 임펄스 = 칼 방향 (누른 키대로 날아감) + 양옆 분리 + 카메라 쪽으로 톡
            rb.AddForce(slashDir * slashImpulse + dir * splitImpulse + Vector3.back * splitImpulse * 0.3f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * splitImpulse * 1.5f, ForceMode.Impulse);
            Destroy(half, 3f);
        }
    }
}
