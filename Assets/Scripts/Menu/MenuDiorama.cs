using System.Collections.Generic;
using UnityEngine;

namespace BeatSlash.Menu
{
    /// <summary>
    /// 타이틀 화면 3D 디오라마 — 키아트 무드 재현.
    /// 무지개→남색 그라데이션 배경 + 댄스 셰프(ChefDance GLB) + 떠다니는 재료들.
    /// 재료는 씬 빌더가 "Float_" 이름으로 심어둔 것을 찾아 둥실둥실 돌린다.
    /// </summary>
    public class MenuDiorama : MonoBehaviour
    {
        [Tooltip("재료 상하 부유 폭")]
        public float bobAmount = 0.18f;
        [Tooltip("재료 자전 속도(도/초)")]
        public float rotSpeed = 30f;
        [Tooltip("카메라 스웨이 각도")]
        public float cameraSway = 1.5f;

        Transform[] _floaters;
        Vector3[] _basePos;
        float[] _phase;

        [Header("셰프 연출 (키아트 구도: 왼쪽 크게, 칼끝은 오른쪽 과일들을 향해)")]
        [Tooltip("셰프 키(유닛) — 클수록 화면을 채운다")]
        public float chefHeight = 7.5f;
        [Tooltip("셰프 위치")]
        public Vector3 chefPosition = new Vector3(0f, -2.8f, 0.9f);
        [Tooltip("셰프 Y 회전 — 180이면 카메라 정면")]
        public float chefYaw = 180f;

        void Start()
        {
            // 풀스크린 아트 모드(UI/title_fullscreen)일 때만 디오라마를 끈다
            if (Resources.Load<Sprite>("UI/title_fullscreen") != null)
            {
                gameObject.SetActive(false);
                return;
            }

            BuildBackdrop();
            SpawnChef();
            SetupLights();
            LayoutFloaters();
            CollectFloaters();
        }

        /// <summary>재료를 "화면 좌표" 기준으로 재배치 — 월드 좌표로 흩뿌리면 원근 때문에 화면에선 뭉친다.
        /// 우측 절반에 대각선 흐름으로 골고루, 깊이는 엇갈리게 (가까운 놈 큼직, 먼 놈 작게 = 원근 리듬).</summary>
        void LayoutFloaters()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // (화면x 0~1, 화면y 0~1, 카메라로부터 깊이, 화면상 크기 배율)
            // 화면 전체 산개 — 오른쪽일수록 가깝고 크게, 왼쪽일수록 멀고 작게 (셰프 뒤 하늘을 채움)
            var vpSpots = new[]
            {
                // 좌측 — 멀리, 작게 (셰프 뒤 배경 하늘)
                (0.05f, 0.90f, 13f, 0.8f),
                (0.18f, 0.72f, 12f, 0.75f),
                (0.08f, 0.50f, 14f, 0.7f),
                (0.30f, 0.92f, 11f, 0.85f),
                (0.22f, 0.28f, 13f, 0.7f),
                // 중앙 상단 — 중간 거리
                (0.46f, 0.80f, 8f, 1.0f),
                (0.55f, 0.55f, 6.5f, 1.0f),
                (0.44f, 0.15f, 9f, 0.9f),
                // 우측 — 가깝게, 큼직하게
                (0.72f, 0.80f, 4f, 1.5f),
                (0.88f, 0.62f, 3.2f, 1.8f), // 주인공급
                (0.68f, 0.38f, 5f, 1.2f),
                (0.93f, 0.88f, 6f, 1.1f),
                (0.80f, 0.16f, 4.2f, 1.4f),
                (0.60f, 0.05f, 5.5f, 1.1f),
            };

            var floats = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Float_")) floats.Add(t);

            // 셰프의 화면상 금지구역 — 이 안에 떨어지는 재료는 바깥으로 밀어낸다
            var chefVp = cam.WorldToViewportPoint(chefPosition + Vector3.up * (chefHeight * 0.45f));
            const float keepX = 0.17f, keepYTop = 0.95f;

            for (int i = 0; i < floats.Count; i++)
            {
                // 재료 수가 자리 수보다 적어도 좌/중/우 전 구역에 고르게 분배되도록 건너뛰며 선택
                var (vx, vy, depth, vis) = vpSpots[(i * vpSpots.Length) / Mathf.Max(1, floats.Count) % vpSpots.Length];

                // 셰프 반경 회피: 가로로 가까우면 셰프 반대쪽으로 밀기
                if (Mathf.Abs(vx - chefVp.x) < keepX && vy < keepYTop)
                    vx = chefVp.x + Mathf.Sign(vx - chefVp.x == 0f ? 1f : vx - chefVp.x) * (keepX + 0.06f);
                vx = Mathf.Clamp(vx, 0.03f, 0.97f);

                floats[i].position = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));
                // 화면상 크기가 vis에 비례하도록 월드 스케일을 깊이에 맞춰 보정
                floats[i].localScale = Vector3.one * (vis * depth * 0.14f);
            }
        }

        /// <summary>키아트 무드 조명: 셰프를 때리는 따뜻한 키 스팟 + 뒤의 보라 림 라이트 + 어두운 전역광.</summary>
        void SetupLights()
        {
            // 전역광은 밝게 — 재료들이 쨍한 본색으로 보여야 한다 (어두우면 칙칙한 갈색 덩어리가 됨)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 0.9f;
                    l.color = new Color(0.97f, 0.94f, 1f);
                }
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.43f, 0.55f);

            // 키 스팟: 앞-위-오른쪽에서 셰프를 정조준
            var keyGo = new GameObject("ChefKeyLight");
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.position = chefPosition + new Vector3(3f, 4.5f, -3f);
            keyGo.transform.LookAt(chefPosition + Vector3.up * (chefHeight * 0.55f));
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Spot;
            key.spotAngle = 55f;
            key.range = 16f;
            key.intensity = 9f;
            key.color = new Color(1f, 0.95f, 0.85f);

            // 림 라이트: 뒤-왼쪽에서 보라빛 — 무지개 배경과 이어지는 톤
            var rimGo = new GameObject("ChefRimLight");
            rimGo.transform.SetParent(transform, false);
            rimGo.transform.position = chefPosition + new Vector3(-2.5f, 2f, 3f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.range = 10f;
            rim.intensity = 5f;
            rim.color = new Color(0.85f, 0.35f, 1f);
        }

        void BuildBackdrop()
        {
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.name = "GradientBg";
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = new Vector3(0f, 2.5f, 16f);
            bg.transform.localScale = new Vector3(56f, 30f, 1f);
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = MakeGradient() };
            bg.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>키아트 톤: 위는 깊은 남색→보라, 좌하단에서 뻗어나오는 선명한 아치 무지개.
        /// 7색 밴드를 계단식으로 — 픽셀아트 무지개 느낌. 에셋 불필요.</summary>
        static Texture2D MakeGradient()
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var navy = new Color(0.05f, 0.03f, 0.14f);
            var purple = new Color(0.30f, 0.10f, 0.42f);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            {
                float v = y / (float)(S - 1); // 0=아래, 1=위
                var baseCol = Color.Lerp(purple, navy, Mathf.SmoothStep(0f, 1f, v));
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)(S - 1);
                    var c = baseCol;

                    // 키아트식 대각 무지개 밴드: 좌하단을 덮는 넓은 띠, 우상단 가장자리가 빨강
                    // 대각 축 투영값 s — 작을수록 좌하단
                    float s = u * 0.5f + v * 0.87f;
                    const float bandStart = 0.58f; // 이보다 작으면 무지개 영역
                    const float bandWidth = 0.26f; // 날씬한 리본
                    float raw = (bandStart - s) / bandWidth; // 0=빨강 가장자리, 1=보라 가장자리
                    if (raw >= 0f && raw < 1f) // 리본 안쪽만 — 밖으로 클램프하면 보라가 좌하단을 통째로 먹는다
                    {
                        float band = Mathf.Floor(raw * 7f) / 7f;   // 7색 계단
                        float hue = band * 0.78f;                  // 빨강(0) → 보라(0.78)
                        float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Min(raw, 1f - raw) * 9f);
                        var rainbow = Color.HSVToRGB(hue, 0.88f, 1f);
                        c = Color.Lerp(c, rainbow, 0.92f * Mathf.Min(edgeFade, 1f));
                    }
                    px[y * S + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        void SpawnChef()
        {
            var model = Resources.Load<GameObject>("ChefDance");
            if (model == null) model = Resources.Load<GameObject>("ChefModel");
            if (model == null) return;

            var chef = Instantiate(model, transform);
            chef.name = "MenuChef";
            chef.transform.localPosition = chefPosition;
            chef.transform.localRotation = Quaternion.Euler(0f, chefYaw, 0f);

            var rends = chef.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                chef.transform.localScale = Vector3.one * (chefHeight / Mathf.Max(0.01f, b.size.y));
            }

            var anim = chef.GetComponentInChildren<Animation>();
            if (anim != null && anim.clip != null)
            {
                anim.wrapMode = WrapMode.Loop;
                anim.Play();
            }

            // 도마(Stage)는 항상 셰프 발밑으로 — 셰프 위치를 옮겨도 같이 따라온다
            var stage = GameObject.Find("Stage");
            if (stage != null)
                stage.transform.position = chefPosition + new Vector3(0f, -1.2f, 0.15f);
        }

        void CollectFloaters()
        {
            var list = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Float_")) list.Add(t);
            _floaters = list.ToArray();
            _basePos = new Vector3[_floaters.Length];
            _phase = new float[_floaters.Length];
            for (int i = 0; i < _floaters.Length; i++)
            {
                _basePos[i] = _floaters[i].localPosition;
                _phase[i] = Random.value * Mathf.PI * 2f;
            }
        }

        void Update()
        {
            if (_floaters != null)
            {
                for (int i = 0; i < _floaters.Length; i++)
                {
                    var f = _floaters[i];
                    if (f == null) continue;
                    f.localPosition = _basePos[i] + Vector3.up * (Mathf.Sin(Time.time * 0.9f + _phase[i]) * bobAmount);
                    f.Rotate(Vector3.up, rotSpeed * Time.deltaTime * (0.6f + _phase[i] * 0.1f), Space.World);
                }
            }

            var cam = Camera.main;
            if (cam != null)
                cam.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(Time.time * 0.27f) * cameraSway * 0.7f,
                    Mathf.Sin(Time.time * 0.21f) * cameraSway,
                    0f);
        }
    }
}
