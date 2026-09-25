using System.Collections;
using BeatSlash.Rhythm;
using DG.Tweening;
using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 중앙의 요리사 실루엣 + 방향별 칼 스윙. 아트 나오기 전 프로시저럴 플레이스홀더 —
    /// 교체 시 Swing(lane)만 유지하면 됨. 항상 카메라를 향해 돌아서 실루엣이 읽힌다.
    /// </summary>
    public class ChefAvatar : MonoBehaviour
    {
        public static ChefAvatar Instance { get; private set; }

        [Header("비트 댄스 (데바디 그루브)")]
        [Tooltip("비트마다 쿵 하는 스쿼시 강도")]
        public float bounceAmount = 0.22f;
        [Tooltip("좌우로 몸을 흔드는 각도(도)")]
        public float swayDegrees = 13f;
        [Tooltip("피버 중 배율")]
        public float feverMultiplier = 1.8f;

        [Header("3D 모델 (Resources/ChefModel — 없으면 프로시저럴 폴백)")]
        [Tooltip("모델을 이 키(유닛)로 자동 스케일")]
        public float targetHeight = 1.9f;
        [Tooltip("발바닥이 놓일 로컬 Y — 다리가 도마에 꺼지면 올리기")]
        public float footY = -0.26f;
        [Tooltip("칼 피벗(손) 위치 — 반대손이면 x 부호 반전")]
        public Vector3 knifeHandOffset = new Vector3(0.5f, 0.3f, 0f);
        [Tooltip("모델 정면 보정 (Y 회전)")]
        public float modelYaw = 180f;
        [Tooltip("칼 GLB(Resources/KnifeModel) 전체 길이")]
        public float knifeLength = 1.15f;

        [Header("방향 런지 (노트 입력 시 그 방향으로 액션)")]
        public float lungeDistance = 0.6f;
        public float lungeTilt = 18f;

        [Header("리깅 애니메이션 (Resources/ChefDance, ChefAttack — Legacy 임포트 필요)")]
        [Tooltip("공격 클립 재생 속도 — 겐지 Shift 필")]
        public float attackSpeed = 1.7f;
        [Tooltip("공격 클립 앞부분(예비동작) 건너뛰기 비율 — 즉발감")]
        public float attackWindupSkip = 0.25f;
        [Tooltip("댄스 한 루프가 차지할 박 수 (BPM 동기)")]
        public float danceLoopBeats = 4f;

        Transform _bodyRoot;
        Transform _actionRoot;
        Transform _knifePivot;
        Coroutine _swing;
        AudioController _audio; // Sindri Music Beat 스펙트럼 분석기
        GameObject _bodyVisual;   // 평상시 몸 (댄스 or 정적)
        GameObject _attackBody;   // 공격 클립 몸 (평소 숨김)
        Animation _danceAnim;
        Animation _attackAnim;
        Coroutine _attackCo;

        void Awake()
        {
            Instance = this;
            BuildBody();
        }

        void Start()
        {
            // Music Beat(Sindri) 분석기를 Conductor의 AudioSource에 부착 — 곡 시작 전에 붙여야
            // AudioController.Start()의 Play() 호출이 빈 클립 no-op으로 끝난다 (재생 중 붙이면 곡이 처음부터 다시 감)
            var con = Conductor.Instance;
            if (con != null)
            {
                _audio = con.GetComponent<AudioController>();
                if (_audio == null) _audio = con.gameObject.AddComponent<AudioController>();
            }
        }

        float[] _webSpec;
        float _webBassMax;

        /// <summary>저음역(밴드 0~3) 평균 0..1 — 초반 프레임 NaN(0/0 정규화) 가드 포함.</summary>
        float BassLevel()
        {
            // WebGL: Web Audio 탭에서 실측 스펙트럼 — 저음 빈 평균을 러닝맥스로 정규화 (Sindri와 같은 방식)
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                _webSpec ??= new float[64];
                if (WebAudioTap.GetSpectrumDb(_webSpec) > 0)
                {
                    float bassSum = 0f;
                    for (int i = 0; i < 8; i++) // ~0-700Hz 저음역
                        bassSum += Mathf.Pow(10f, Mathf.Clamp(_webSpec[i], -100f, 0f) / 20f);
                    float bass = bassSum / 8f;
                    _webBassMax = Mathf.Max(_webBassMax * 0.999f, bass); // 서서히 잊는 러닝맥스
                    return _webBassMax > 1e-5f ? Mathf.Clamp01(bass / _webBassMax) : 0f;
                }
                // 탭 실패 폴백
                return Conductor.Instance != null ? Conductor.Instance.BeatKick01 * 0.6f : 0f;
            }
            if (_audio == null) return 0f;
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                float v = _audio.audioBandBuffer[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                sum += Mathf.Clamp01(v);
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        void BuildBody()
        {
            // 레이어 구조: transform(빌보드) > BodyRoot(비트 댄스, 매 프레임 덮어씀) > ActionRoot(런지 트윈 전용)
            _bodyRoot = new GameObject("BodyRoot").transform;
            _bodyRoot.SetParent(transform, false);
            _actionRoot = new GameObject("ActionRoot").transform;
            _actionRoot.SetParent(_bodyRoot, false);

            var dark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.06f, 0.06f, 0.09f) };
            var white = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.95f, 0.95f, 0.92f) };
            var steel = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.75f, 0.8f, 0.85f) };

            // 몸: 댄스 리깅 GLB > 정적 GLB > 프로시저럴 순 폴백
            _bodyVisual = SpawnModel("ChefDance");
            if (_bodyVisual != null)
            {
                _danceAnim = _bodyVisual.GetComponentInChildren<Animation>();
                if (_danceAnim != null && _danceAnim.clip != null)
                {
                    _danceAnim.wrapMode = WrapMode.Loop;
                    _danceAnim.Play();
                }
            }
            else
            {
                _bodyVisual = SpawnModel("ChefModel");
            }

            if (_bodyVisual == null)
            {
                Add(PrimitiveType.Capsule, new Vector3(0f, -0.15f, 0f), new Vector3(0.55f, 0.6f, 0.55f), dark);   // 몸통
                Add(PrimitiveType.Sphere, new Vector3(0f, 0.8f, 0f), Vector3.one * 0.45f, dark);                  // 머리
                Add(PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.42f, 0.2f, 0.42f), white);   // 토크(모자)
            }

            // 공격 클립 몸 — 평소 숨겨두고 스윙 순간만 스왑
            _attackBody = SpawnModel("ChefAttack");
            if (_attackBody != null)
            {
                _attackAnim = _attackBody.GetComponentInChildren<Animation>();
                _attackBody.SetActive(false);
            }

            _knifePivot = new GameObject("KnifePivot").transform;
            _knifePivot.SetParent(_actionRoot, false);
            _knifePivot.localPosition = knifeHandOffset;

            var knifeModel = Resources.Load<GameObject>("KnifeModel");
            if (knifeModel != null)
            {
                // 로고 무지개 칼 GLB — 손잡이 끝을 피벗(손 위치)에 스냅. 피벗이 아직 무회전일 때 실측해야 함
                var knife = Instantiate(knifeModel, _knifePivot);
                foreach (var col in knife.GetComponentsInChildren<Collider>())
                    DestroyImmediate(col);
                var krends = knife.GetComponentsInChildren<Renderer>();
                if (krends.Length > 0)
                {
                    var kb = krends[0].bounds;
                    foreach (var r in krends) kb.Encapsulate(r.bounds);
                    float ks = knifeLength / Mathf.Max(0.01f, kb.size.y);
                    knife.transform.localScale = Vector3.one * ks;

                    kb = krends[0].bounds;
                    foreach (var r in krends) kb.Encapsulate(r.bounds);
                    knife.transform.position += new Vector3(
                        _knifePivot.position.x - kb.center.x,
                        _knifePivot.position.y - kb.min.y,
                        _knifePivot.position.z - kb.center.z);
                }
            }
            else
            {
                var blade = Add(PrimitiveType.Cube, Vector3.zero, new Vector3(0.09f, 0.95f, 0.02f), steel);
                blade.transform.SetParent(_knifePivot, true);
                blade.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            }
            _knifePivot.localRotation = Quaternion.Euler(0f, 0f, -70f); // 대기 자세

            // 댄스 클립이 몸을 움직이는 동안 고정 칼은 따로 놀아 보임 — 댄스 중엔 숨김
            if (_danceAnim != null)
                _knifePivot.gameObject.SetActive(false);
        }

        /// <summary>GLB 스폰 공통 처리: 회전/콜라이더 제거/무광/키·발 스냅. 없으면 null.</summary>
        GameObject SpawnModel(string resName)
        {
            var prefab = Resources.Load<GameObject>(resName);
            if (prefab == null) return null;
            var go = Instantiate(prefab, _actionRoot);
            go.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            foreach (var col in go.GetComponentsInChildren<Collider>())
                DestroyImmediate(col); // 재료 물리 방해 금지
            // glTFast 기본 머티리얼은 광택이 있어 검은 몸에 흰 스펙큘러가 점점이 맺힘 → 무광 교체
            ApplyFlatMaterials(go);
            FitToFeet(go);
            return go;
        }

        /// <summary>바운즈 실측으로 targetHeight 키 맞춤 + 발바닥을 로컬 footY(도마 윗면)에 스냅.</summary>
        void FitToFeet(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float s = targetHeight / Mathf.Max(0.01f, b.size.y);
            go.transform.localScale = Vector3.one * s;

            b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            var footTarget = transform.TransformPoint(new Vector3(0f, footY, 0f));
            go.transform.position += new Vector3(
                transform.position.x - b.center.x,
                footTarget.y - b.min.y,
                transform.position.z - b.center.z);
        }

        static void ApplyFlatMaterials(GameObject root)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var flat = new Material(lit) { mainTexture = mats[i].mainTexture };
                    flat.SetFloat("_Smoothness", 0.15f);
                    mats[i] = flat;
                }
                r.materials = mats;
            }
        }

        GameObject Add(PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            DestroyImmediate(go.GetComponent<Collider>()); // 재료 물리 방해 금지 + 발 스냅 레이캐스트 오염 방지
            go.transform.SetParent(_actionRoot, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        void Update()
        {
            var con = Conductor.Instance;
            float mult = (FeverMode.Instance != null && FeverMode.Instance.IsFever) ? feverMultiplier : 1f;
            // 음악 에너지 연동: 기본 그루브는 항상 유지, 드랍/후렴에서 추가 부스트 (Music Beat 스펙트럼)
            mult *= 0.9f + BassLevel() * 0.7f;

            // 댄스 클립 비트 동기: 곡 위치(박)로 애니메이션 시간을 직접 구동 — 루프 경계가 항상 정박
            if (_danceAnim != null && _danceAnim.clip != null)
            {
                var st = _danceAnim[_danceAnim.clip.name];
                if (st != null)
                {
                    if (con != null && con.IsPlaying && !con.IsPaused)
                    {
                        st.speed = 0f; // 자동 재생 끄고 위상 스크럽
                        st.normalizedTime = Mathf.Repeat((float)(con.SongPositionBeats / danceLoopBeats), 1f);
                    }
                    else
                    {
                        st.speed = 1f; // 곡 전/일시정지엔 자연 재생
                    }
                }
            }

            if (con != null && con.IsPlaying && !con.IsPaused)
            {
                // 비트 위상 기반 연속 그루브 — 어떤 BPM이든 박자에 정확히 붙는다
                float beats = (float)con.SongPositionBeats;
                float phase = beats - Mathf.Floor(beats);            // 0(정박)→1
                float kick = Mathf.Exp(-phase * 6f);                 // 정박 직후 쿵, 지수 감쇠
                // 댄스 클립이 몸을 움직일 땐 프로시저럴 좌우 스웨이는 끔 — 서로 싸워서 어색함
                float sway = _danceAnim != null && _danceAnim.clip != null
                    ? 0f
                    : Mathf.Sin(beats * Mathf.PI);                   // 비트마다 좌↔우 교대

                _bodyRoot.localScale = new Vector3(
                    1f + kick * bounceAmount * 0.7f * mult,
                    1f - kick * bounceAmount * mult,
                    1f + kick * bounceAmount * 0.7f * mult);
                _bodyRoot.localRotation = Quaternion.Euler(0f, 0f, sway * swayDegrees * mult);
                _bodyRoot.localPosition = new Vector3(sway * 0.12f * mult, kick * -0.05f, 0f);
            }
            else
            {
                // 곡 전/일시정지: 느긋한 아이들 바운스
                float t = Time.unscaledTime * 2f;
                float idle = (Mathf.Sin(t) + 1f) * 0.5f;
                _bodyRoot.localScale = Vector3.Lerp(_bodyRoot.localScale,
                    new Vector3(1f + idle * 0.03f, 1f - idle * 0.04f, 1f + idle * 0.03f), Time.unscaledDeltaTime * 5f);
                _bodyRoot.localRotation = Quaternion.Slerp(_bodyRoot.localRotation, Quaternion.identity, Time.unscaledDeltaTime * 5f);
                _bodyRoot.localPosition = Vector3.Lerp(_bodyRoot.localPosition, Vector3.zero, Time.unscaledDeltaTime * 5f);
            }
        }

        void LateUpdate()
        {
            // 궤도 카메라를 향해 회전 (Y축만) — 실루엣 유지
            var cam = Camera.main;
            if (cam == null) return;
            var to = transform.position - cam.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(to);
        }

        /// <summary>lane: 0좌 1우 2상 3하 — 그 방향으로 칼을 휘두르며 몸도 런지.</summary>
        public void Swing(int lane)
        {
            if (_swing != null) StopCoroutine(_swing);
            // 좌우 스윙은 그쪽 손끝에서 칼이 나가야 대칭이 맞는다 — 피벗 x 미러링
            var hand = knifeHandOffset;
            if (lane == 0) hand.x = -Mathf.Abs(hand.x);
            else if (lane == 1) hand.x = Mathf.Abs(hand.x);
            _knifePivot.localPosition = hand;
            _swing = StartCoroutine(SwingRoutine(lane));
            Lunge(lane);

            // 공격 클립 있으면 몸을 통째로 스왑 — 겐지 Shift처럼 훅 치고 복귀
            if (_attackAnim != null && _attackAnim.clip != null)
            {
                if (_attackCo != null) StopCoroutine(_attackCo);
                _attackCo = StartCoroutine(AttackRoutine());
            }
        }

        IEnumerator AttackRoutine()
        {
            _bodyVisual.SetActive(false);
            _knifePivot.gameObject.SetActive(true); // 공격 순간엔 칼 등장 — 스윙 연출과 함께
            _attackBody.SetActive(true);

            var st = _attackAnim[_attackAnim.clip.name];
            st.speed = attackSpeed;
            _attackAnim.Play(st.name);
            st.time = st.length * attackWindupSkip; // Play가 time을 리셋할 수 있어 반드시 재생 후 스킵
            // 스케일드 시간 대기 — 히트스톱 중엔 공격 포즈가 얼어붙는다(의도)
            yield return new WaitForSeconds(st.length * (1f - attackWindupSkip) / attackSpeed);

            _attackBody.SetActive(false);
            _bodyVisual.SetActive(true);
            _knifePivot.gameObject.SetActive(_danceAnim == null); // 댄스 중엔 칼 계속 숨김
            ReturnPose(); // 공격 몸이 꺼지는 바로 그 프레임에 정면 복귀 — 어긋남 원천 차단
            _attackCo = null;
        }

        /// <summary>누른 방향으로 몸이 홱 튀었다 복귀 — 히트스톱 중엔 포즈가 얼어붙는다(의도).</summary>
        void Lunge(int lane)
        {
            if (_actionRoot == null) return;
            _actionRoot.DOKill();
            _actionRoot.localPosition = Vector3.zero;
            _actionRoot.localRotation = Quaternion.identity;
            _actionRoot.localScale = Vector3.one;

            // 화면 기준 방향 → 로컬 변환 (카메라 궤도 대응)
            var cam = Camera.main != null ? Camera.main.transform : null;
            var world = lane switch
            {
                0 => cam != null ? -cam.right : Vector3.left,
                1 => cam != null ? cam.right : Vector3.right,
                2 => Vector3.up,
                // 아래: 순수 하강이면 도마에 발이 묻힘 — 카메라 쪽(도마 앞)으로 빠지며 내려간다
                _ => (Vector3.down + (cam != null
                        ? Vector3.ProjectOnPlane(-cam.forward, Vector3.up).normalized
                        : Vector3.back) * 0.9f).normalized,
            };
            var local = _bodyRoot.InverseTransformDirection(world.normalized);
            // 썰는 방향으로 몸을 돌린다/기울인다 — 좌우=요, 위=젖히기, 아래=숙이기 (겐지 대시 필)
            float yaw = lane == 0 ? 90f : lane == 1 ? -90f : 0f;
            float pitch = 0f; // 위/아래 피치는 몸이 늘어져 보여서 제외 — 좌우 요만 사용
            // 위는 화면상 이동이 작게 읽혀 더 길게, 아래는 도마 관통 방지로 짧게
            float dist = lane == 2 ? lungeDistance * 1.3f
                       : lane == 3 ? lungeDistance * 0.7f
                       : lungeDistance;

            // 이동축으로 살짝만 늘려 속도감 — 과하면 엿가락 됨
            var stretch = new Vector3(
                1f + Mathf.Abs(local.x) * 0.22f - Mathf.Abs(local.y) * 0.06f,
                1f + Mathf.Abs(local.y) * 0.18f - Mathf.Abs(local.x) * 0.06f,
                1f);

            // 위아래는 회전 큐가 없고 화면 이동량도 작아 순간이동처럼 읽힘 —
            // 대시를 길게(중간 프레임 확보) + 살짝 롤 기울기로 모션 궤적을 만들어준다
            bool vertical = lane >= 2;
            float dashTime = vertical ? 0.13f : 0.07f;
            float tilt = vertical
                ? (lane == 2 ? 12f : -12f)
                : -local.x * lungeTilt;

            // 대시 인 — 복귀는 공격 루틴이 끝나는 프레임에 ReturnPose()로 직접 트리거
            // (시간 계산 예약은 클립 재생 오차와 어긋나 "정면 휘두름"이 새는 원인)
            var seq = DOTween.Sequence().SetLink(_actionRoot.gameObject);
            seq.Append(_actionRoot.DOLocalMove(local * dist, dashTime).SetEase(Ease.OutQuint));
            seq.Join(_actionRoot.DOLocalRotate(new Vector3(pitch, yaw, tilt), dashTime));
            seq.Join(_actionRoot.DOScale(stretch, dashTime).SetEase(Ease.OutQuint));

            // 공격 클립 없으면(폴백) 스스로 복귀
            if (_attackAnim == null || _attackAnim.clip == null)
                seq.AppendCallback(ReturnPose);
        }

        /// <summary>대시 포즈에서 정면/제자리 복귀 — 공격 몸이 꺼지는 프레임에 호출된다.</summary>
        void ReturnPose()
        {
            _actionRoot.DOKill();
            _actionRoot.DOLocalMove(Vector3.zero, 0.28f).SetEase(Ease.OutBack).SetLink(_actionRoot.gameObject);
            _actionRoot.DOLocalRotate(Vector3.zero, 0.28f).SetEase(Ease.OutBack).SetLink(_actionRoot.gameObject);
            _actionRoot.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutBack).SetLink(_actionRoot.gameObject);
        }

        IEnumerator SwingRoutine(int lane)
        {
            float target = lane switch { 0 => 90f, 1 => -90f, 2 => 0f, 3 => 180f, _ => -90f };
            // 오른쪽(+110)은 머리 위→내리베기. 왼쪽은 같은 공식이면 바닥에서 올려베기가 돼서
            // 칼이 도마에 파묻힘 — 미러(-110)로 좌우 대칭 내리베기
            float from = lane == 0 ? target - 110f : target + 110f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.09f;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                _knifePivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, target, eased));
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);
            // 대기 자세 복귀
            t = 0f;
            var start = _knifePivot.localRotation;
            var rest = Quaternion.Euler(0f, 0f, -70f);
            while (t < 1f)
            {
                t += Time.deltaTime / 0.2f;
                _knifePivot.localRotation = Quaternion.Slerp(start, rest, t);
                yield return null;
            }
            _knifePivot.localPosition = knifeHandOffset; // 대기 자세는 기본 손으로 복귀
            _swing = null;
        }
    }
}
