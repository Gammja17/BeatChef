using System.Collections.Generic;
using BeatSlash.Gameplay;
using UnityEngine;

namespace BeatSlash.Juice
{
    /// <summary>
    /// 피버 병맛 배경 — 히맨 HEYYEYAA 감성. 무지개 스트라이프 + 반짝이 별 + 날아다니는 밈 고양이.
    /// 게임 카메라 자식 월드 쿼드라 426×240 픽셀화를 같이 먹고, 3D 무대 뒤에 깔린다.
    /// GameFlow가 런타임 부착 — 씬 수정 불필요.
    /// </summary>
    public class FeverBackdrop : MonoBehaviour
    {
        [Tooltip("배경이 카메라에서 떨어진 거리 — 무대 뒤로 충분히 멀게")]
        public float depth = 20f;
        [Tooltip("무지개 스크롤 속도")]
        public float scrollSpeed = 0.12f;
        public int starCount = 18;
        [Tooltip("고양이 마릿수 (스프라이트 3종 로테이션)")]
        public int catCount = 5;
        public float catSpeedMin = 4f;
        public float catSpeedMax = 8f;

        class Cat
        {
            public Transform t;
            public Material mat;
            public Texture2D frameA, frameB; // 2프레임 GIF식 애니 — frameB 없으면 정지컷
            public float speed, baseY, wobblePhase, dir, scale;
        }

        [Header("평상시 파형 (피버 아닐 때)")]
        [Tooltip("오디오 파형 진폭")]
        public float waveAmplitude = 2.2f;
        public Color waveColor = new Color(0.4f, 0.9f, 1f, 0.45f);

        Transform _rig;
        Transform _bgQuad;
        Material _bgMat;
        const float BandPeriod = 34f / 3.5f; // 쿼드에 무지개 3.5사이클 → 한 사이클 높이
        readonly List<Material> _starMats = new List<Material>();
        readonly List<Transform> _stars = new List<Transform>();
        readonly List<Cat> _cats = new List<Cat>();
        float _alpha;

        LineRenderer _wave;
        readonly float[] _waveData = new float[256];
        readonly Vector3[] _wavePoints = new Vector3[128];
        AudioSource _audioSrc;

        class Dancer
        {
            public Transform t;
            public Animation anim;
            public float speed, baseY, wobblePhase, dir, scale;
        }

        readonly List<Dancer> _dancers = new List<Dancer>();
        [Tooltip("피버 난입 댄서 셰프 한 루프가 차지할 박 수")]
        public float dancerLoopBeats = 2f;
        [Tooltip("댄서가 카메라(앞)를 보도록 하는 요 보정 — 옆 보면 ±90 조정")]
        public float dancerYaw = 90f;

        void Start()
        {
            var cam = Camera.main;
            if (cam == null) { enabled = false; return; }

            _rig = new GameObject("FeverBackdrop").transform;
            _rig.SetParent(cam.transform, false);
            _rig.localPosition = new Vector3(0f, 0f, depth);
            _rig.localRotation = Quaternion.identity;

            // 무지개 스트라이프 배경판 — Sprites/Default는 UV 타일링/오프셋을 무시하므로
            // 띠를 텍스처에 통째로 굽고, 스크롤은 쿼드 y이동(주기 반복)으로 처리
            var bg = MakeQuad(_rig, Vector3.zero, new Vector3(60f, 34f, 1f), RainbowTexture(), out _bgMat);
            _bgQuad = bg.transform;

            // 반짝이 별
            var starTex = StarTexture();
            for (int i = 0; i < starCount; i++)
            {
                var star = MakeQuad(_rig,
                    new Vector3(Random.Range(-9f, 9f), Random.Range(-4.5f, 4.5f), -0.5f),
                    Vector3.one * Random.Range(0.6f, 1.6f), starTex, out var starMat);
                _starMats.Add(starMat);
                _stars.Add(star.transform);
            }

            // 밈 고양이들 — Resources/UI/fever_cat_N (+ 있으면 fever_cat_Nb = 2프레임 애니)
            var catTexes = new List<(Texture2D a, Texture2D b)>();
            for (int i = 0; i < 3; i++)
            {
                var a = Resources.Load<Texture2D>($"UI/fever_cat_{i}");
                if (a != null) catTexes.Add((a, Resources.Load<Texture2D>($"UI/fever_cat_{i}b")));
            }
            for (int i = 0; i < catCount && catTexes.Count > 0; i++)
            {
                var (tex, texB) = catTexes[i % catTexes.Count];
                float aspect = (float)tex.width / tex.height;
                float scale = Random.Range(2.2f, 3.5f);
                var go = MakeQuad(_rig, Vector3.zero, new Vector3(scale * aspect, scale, 1f), tex, out var catMat);
                var cat = new Cat
                {
                    t = go.transform,
                    mat = catMat,
                    frameA = tex,
                    frameB = texB,
                    speed = Random.Range(catSpeedMin, catSpeedMax),
                    baseY = Random.Range(-3.2f, 3.2f), // 화면 세로(±4.2) 안 — 밖에 스폰되면 안 보임
                    wobblePhase = Random.value * 10f,
                    dir = i % 2 == 0 ? 1f : -1f,
                    scale = scale,
                };
                cat.t.localPosition = new Vector3(Random.Range(-24f, 24f), cat.baseY, -1f);
                // 왼쪽으로 나는 애는 좌우 반전
                if (cat.dir < 0f) cat.t.localScale = new Vector3(-scale * aspect, scale, 1f);
                _cats.Add(cat);
            }

            // 피버 난입 댄서 셰프 (Fast_Lightning) — 고양이처럼 날아다니면서 춤춘다
            var dancerPrefab = Resources.Load<GameObject>("ChefFever");
            if (dancerPrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    var d = Instantiate(dancerPrefab, _rig);
                    float dir = i % 2 == 0 ? 1f : -1f;
                    float scale = Random.Range(1.9f, 2.3f); // 춤 실루엣이 읽히는 크기
                    d.transform.localRotation = Quaternion.Euler(0f, dancerYaw, 0f); // 앞(카메라) 바라보기
                    d.transform.localScale = Vector3.one * scale;
                    foreach (var col in d.GetComponentsInChildren<Collider>())
                        DestroyImmediate(col);
                    // 무광 교체 — 스펙큘러 각질 방지 (ChefAvatar와 동일 처리)
                    var lit = Shader.Find("Universal Render Pipeline/Lit");
                    foreach (var r in d.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.materials;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            var flat = new Material(lit) { mainTexture = mats[m].mainTexture };
                            flat.SetFloat("_Smoothness", 0.15f);
                            mats[m] = flat;
                        }
                        r.materials = mats;
                    }
                    var anim = d.GetComponentInChildren<Animation>();
                    if (anim != null && anim.clip != null)
                    {
                        anim.wrapMode = WrapMode.Loop;
                        anim.Play();
                    }
                    var dancer = new Dancer
                    {
                        t = d.transform,
                        anim = anim,
                        speed = Random.Range(2.5f, 4.5f), // 고양이보다 느긋하게 유영
                        baseY = Random.Range(-3f, 1.5f),
                        wobblePhase = Random.value * 10f,
                        dir = dir,
                        scale = scale,
                    };
                    dancer.t.localPosition = new Vector3(Random.Range(-12f, 12f), dancer.baseY, -2f);
                    _dancers.Add(dancer);
                }
            }

            _rig.gameObject.SetActive(false);

            // 평상시 오실로스코프 파형 — 카메라 자식, 무대 뒤에서 음악 출력 파형이 그대로 흐른다
            var waveGo = new GameObject("BeatWave");
            waveGo.transform.SetParent(cam.transform, false);
            waveGo.transform.localPosition = new Vector3(0f, -0.5f, depth - 1f);
            _wave = waveGo.AddComponent<LineRenderer>();
            _wave.useWorldSpace = false;
            _wave.positionCount = _wavePoints.Length;
            _wave.startWidth = _wave.endWidth = 0.09f;
            _wave.material = new Material(Shader.Find("Sprites/Default"));
            _wave.textureMode = LineTextureMode.Stretch;
            for (int i = 0; i < _wavePoints.Length; i++)
                _wavePoints[i] = new Vector3(Mathf.Lerp(-9f, 9f, i / (float)(_wavePoints.Length - 1)), 0f, 0f);
            _wave.SetPositions(_wavePoints);

            var rhythmCon = Rhythm.Conductor.Instance;
            if (rhythmCon != null) _audioSrc = rhythmCon.GetComponent<AudioSource>();

            // WebGL: 오디오 연결 전에 탭 패치를 깔아둬야 곡 재생이 애널라이저를 경유한다
            _ = WebAudioTap.Available;
        }

        /// <summary>쿼드 생성 공통 — Sprites/Default(투명 지원), 콜라이더 제거.</summary>
        GameObject MakeQuad(Transform parent, Vector3 localPos, Vector3 scale, Texture tex, out Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        void Update()
        {
            bool fever = FeverMode.Instance != null && FeverMode.Instance.IsFever;
            _alpha = Mathf.MoveTowards(_alpha, fever ? 1f : 0f, Time.deltaTime * 2.5f);

            UpdateWave();

            bool visible = _alpha > 0.01f;
            if (_rig.gameObject.activeSelf != visible) _rig.gameObject.SetActive(visible);
            if (!visible) return;

            // 무지개 스크롤 (히맨 그 배경) — 쿼드 y를 한 사이클 주기로 반복 이동 + 비트마다 밝기 쿵쿵
            _bgQuad.localPosition = new Vector3(0f,
                Mathf.Repeat(Time.time * scrollSpeed * 8f, BandPeriod) - BandPeriod * 0.5f, 0f);
            float pulse = 0.8f;
            var con = Rhythm.Conductor.Instance;
            if (con != null && con.IsPlaying && !con.IsPaused)
            {
                float beats = (float)con.SongPositionBeats;
                pulse = 0.8f + Mathf.Exp(-(beats - Mathf.Floor(beats)) * 5f) * 0.35f;
            }
            _bgMat.color = new Color(pulse, pulse, pulse, _alpha * 0.85f);

            // 별 트윙클
            for (int i = 0; i < _starMats.Count; i++)
            {
                float tw = (Mathf.Sin(Time.time * 4f + i * 1.7f) + 1f) * 0.5f;
                _starMats[i].color = new Color(1f, 1f, 1f, _alpha * Mathf.Lerp(0.08f, 0.55f, tw));
                _stars[i].localRotation = Quaternion.Euler(0f, 0f, Time.time * 30f + i * 40f);
            }

            // GIF식 2프레임 플립 — 반박마다 전환 (곡 없으면 4fps)
            var flipCon = Rhythm.Conductor.Instance;
            int frame = flipCon != null && flipCon.IsPlaying
                ? (int)(flipCon.SongPositionBeats * 2.0) & 1
                : (int)(Time.time * 4f) & 1;

            // 난입 댄서 셰프: 고양이처럼 유영하면서 곡 위상에 맞춰 춤 (루프 경계 = 정박)
            var danceCon = Rhythm.Conductor.Instance;
            foreach (var d in _dancers)
            {
                var p = d.t.localPosition;
                p.x += d.dir * d.speed * Time.deltaTime;
                if (p.x > 16f) p.x = -16f;
                else if (p.x < -16f) p.x = 16f;
                p.y = d.baseY + Mathf.Sin(Time.time * 1.2f + d.wobblePhase) * 1.2f;
                d.t.localPosition = p;

                if (d.anim == null || d.anim.clip == null) continue;
                var st = d.anim[d.anim.clip.name];
                if (st == null) continue;
                if (danceCon != null && danceCon.IsPlaying && !danceCon.IsPaused)
                {
                    st.speed = 0f;
                    st.normalizedTime = Mathf.Repeat((float)(danceCon.SongPositionBeats / dancerLoopBeats), 1f);
                }
                else
                {
                    st.speed = 1f;
                }
            }

            // 고양이 순항 — 사인파 타고 좌우로, 화면 밖 나가면 반대편에서 재진입
            foreach (var c in _cats)
            {
                if (c.frameB != null)
                    c.mat.mainTexture = frame == 0 ? c.frameA : c.frameB;
                var p = c.t.localPosition;
                p.x += c.dir * c.speed * Time.deltaTime;
                if (p.x > 28f) p.x = -28f;
                else if (p.x < -28f) p.x = 28f;
                p.y = c.baseY + Mathf.Sin(Time.time * 1.5f + c.wobblePhase) * 1.6f;
                c.t.localPosition = p;
                c.t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f + c.wobblePhase) * 8f);
                c.mat.color = new Color(1f, 1f, 1f, _alpha);
            }
        }

        /// <summary>평상시 파형: 실제 오디오 출력(GetOutputData)을 라인으로. 비트 킥에 진폭 증폭, 피버 중엔 페이드아웃.</summary>
        void UpdateWave()
        {
            if (_wave == null) return;
            var con = Rhythm.Conductor.Instance;
            bool playing = con != null && con.IsPlaying && !con.IsPaused && _audioSrc != null;
            float waveAlpha = (1f - _alpha) * waveColor.a * (playing ? 1f : 0f);

            bool active = waveAlpha > 0.01f;
            if (_wave.gameObject.activeSelf != active) _wave.gameObject.SetActive(active);
            if (!active) return;

            float beats = (float)con.SongPositionBeats;
            float kick = Mathf.Exp(-(beats - Mathf.Floor(beats)) * 5f);
            float amp = waveAmplitude * (0.7f + kick * 0.7f); // 정박마다 파형이 크게 요동

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // WebGL은 GetOutputData 미지원 — Web Audio 탭으로 에디터와 동일한 실측 파형
                if (WebAudioTap.GetWave(_waveData) == 0)
                {
                    // 탭 실패(브라우저/버전 이슈) 최후 폴백: 비트킥 합성 파형
                    float t = Time.time;
                    for (int i = 0; i < _waveData.Length; i += 2)
                    {
                        float x = i * 0.11f;
                        _waveData[i] = (Mathf.Sin(t * 9f + x) * 0.5f
                                      + Mathf.Sin(t * 23f + x * 2.7f) * 0.3f
                                      + Mathf.Sin(t * 5f - x * 1.3f) * 0.2f) * (0.25f + kick * 0.75f) * 0.5f;
                    }
                }
            }
            else
            {
                _audioSrc.GetOutputData(_waveData, 0);
            }

            for (int i = 0; i < _wavePoints.Length; i++)
            {
                float target = _waveData[i * 2] * amp;
                var p = _wavePoints[i];
                p.y = Mathf.Lerp(p.y, target, 0.55f); // 프레임 간 살짝 부드럽게
                _wavePoints[i] = p;
            }
            _wave.SetPositions(_wavePoints);

            // 색상 순환: 휴가 천천히 돌고, 라인 양끝은 다른 색 → 흐르는 무지개 그라데이션.
            // 정박 킥마다 채도/밝기가 살짝 튀어 박자를 같이 탄다
            float hue = Mathf.Repeat(Time.time * 0.1f, 1f);
            float a = waveAlpha * (0.6f + kick * 0.4f);
            var c1 = Color.HSVToRGB(hue, 0.75f, 1f);
            var c2 = Color.HSVToRGB(Mathf.Repeat(hue + 0.28f, 1f), 0.75f, 1f);
            c1.a = c2.a = a;
            _wave.startColor = c1;
            _wave.endColor = c2;
        }

        // ── 프로시저럴 텍스처 ──────────────────────────────────
        static Texture2D RainbowTexture()
        {
            // 히맨 배경 감성: 파스텔 무지개가 부드럽게 흐르는 그라데이션 (3.5사이클을 통째로 굽는다)
            // — Sprites/Default가 타일링을 무시해도 화면엔 항상 풀 무지개가 보인다
            const int h = 448;
            const float cycles = 3.5f;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear, // 부드러운 그라데이션
            };
            for (int y = 0; y < h; y++)
            {
                float hue = Mathf.Repeat(y / (float)h * cycles, 1f);
                tex.SetPixel(0, y, Color.HSVToRGB(hue, 0.32f, 1f)); // 저채도 파스텔 — 눈 안 아프게
            }
            tex.Apply();
            return tex;
        }

        static Texture2D StarTexture()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (s - 1) / 2f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // 4각 별: 축 방향으로 길게 뻗는 반짝이
                    float dx = Mathf.Abs(x - c) / c;
                    float dy = Mathf.Abs(y - c) / c;
                    float a = Mathf.Clamp01(1f - (dx + dy)) + Mathf.Clamp01(1f - Mathf.Max(dx, dy) * 4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
