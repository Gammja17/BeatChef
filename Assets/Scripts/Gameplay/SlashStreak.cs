using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 키 입력 순간의 참격 이펙트. 에셋 의존 없이 쿼드 + Sprites/Default로 즉석 생성.
    /// 늘어나며 얇아지고 사라진다. 히트스톱 중엔 정지 프레임으로 보여서 임팩트가 산다.
    /// </summary>
    public class SlashStreak : MonoBehaviour
    {
        const float Life = 0.12f;
        float _t;
        Material _mat;
        Vector3 _baseScale;

        /// <summary>lane: 0/1=가로 베기, 2/3=세로 베기, -1=랜덤 기울기 가로</summary>
        public static void Spawn(Vector3 pos, int lane)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "SlashStreak";
            Destroy(go.GetComponent<Collider>());

            float zRot = (lane == 2 || lane == 3) ? 90f : 0f;
            zRot += Random.Range(-18f, 18f);
            var camRot = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            go.transform.SetPositionAndRotation(pos, camRot * Quaternion.Euler(0f, 0f, zRot));
            go.transform.localScale = new Vector3(5.2f, 0.24f, 1f);
            go.AddComponent<SlashStreak>();
        }

        void Awake()
        {
            _mat = new Material(Shader.Find("Sprites/Default"));
            GetComponent<MeshRenderer>().sharedMaterial = _mat;
        }

        void Start()
        {
            _baseScale = transform.localScale;
        }

        void Update()
        {
            _t += Time.deltaTime / Life;
            float t = Mathf.Clamp01(_t);
            transform.localScale = new Vector3(_baseScale.x * (1f + t * 1.3f), _baseScale.y * (1f - t), 1f);
            _mat.color = new Color(1f, 1f, 0.92f, 1f - t);
            if (_t >= 1f) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
