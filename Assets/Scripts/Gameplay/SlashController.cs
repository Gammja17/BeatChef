using BeatSlash.Juice;
using BeatSlash.Rhythm;
using DG.Tweening;
using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 플레이어 입력 → 판정 → 썰기/연출 발동. 씬에 하나.
    /// 좌클릭(또는 스페이스) 한 버튼 입력이 기본 — 방향/레인 확장은 lane 파라미터로.
    /// </summary>
    public class SlashController : MonoBehaviour
    {
        public IngredientSpawner spawner;
        [Tooltip("칼질 궤적 이펙트 (선택)")]
        public ParticleSystem slashFxPrefab;

        [Header("판정별 연출 강도")]
        public float perfectHitStop = 0.09f;
        public float perfectPunch = 0.55f;
        public float goodPunch = 0.25f;

        [Header("사운드 — 기본은 신디사이즈 타이코 톤. 클립 지정 시 그것 사용")]
        public AudioClip[] sliceSounds;
        public AudioClip whiffSound;

        AudioClip _don;
        AudioClip _ting;

        int _combo;
        public int Combo => _combo;
        AudioSource _sfx;

        void Start()
        {
            if (BeatJudge.Instance != null) BeatJudge.Instance.OnJudged += HandleJudged;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.volume = GameSettings.SfxVolume;
            GameSettings.OnSfxChanged += HandleSfxVolume;

            // 기본 타격음은 신디사이즈 타이코 톤 — 에셋/씬 배선 불필요, 웹 포함 동작
            _don = SfxSynth.Don();
            _ting = SfxSynth.Ting();
            if (whiffSound == null) whiffSound = SfxSynth.Swish();

            // 커스텀 히트음(Resources/Sfx) — Perfect는 쨍한 원본 그대로, Good은 둔탁 버전
            _perfectClip = Resources.Load<AudioClip>("Sfx/note_perfect");
            _goodClip = Resources.Load<AudioClip>("Sfx/note_good");
            var whiffClip = Resources.Load<AudioClip>("Sfx/note_whiff");
            if (whiffClip != null) whiffSound = whiffClip;
        }

        AudioClip _perfectClip;
        AudioClip _goodClip;

        [Header("입력")]
        [Tooltip("방향 판정 사용 여부. 끄면 아무 키나 타이밍만 판정")]
        public bool laneMatters = true;

        public static SlashController Instance { get; private set; }
        /// <summary>연타 중인지 — HUD/카메라 연출이 참조.</summary>
        public static bool MashActive => Instance != null && Instance._mash != null;
        /// <summary>연타 중인 재료 — HUD 카운터가 위치/남은 횟수 표시에 사용.</summary>
        public static Sliceable MashTarget => Instance != null ? Instance._mash : null;
        /// <summary>슬로우 컷 진행 중인지 — HUD/카메라 연출이 참조.</summary>
        public static bool SlowActive => Instance != null && Instance._slow;
        /// <summary>홀드 유지 중인지 — HUD/카메라 연출이 참조.</summary>
        public static bool HoldActive => Instance != null && Instance._holdTarget != null;

        [Header("슬로우 컷 플러리시 — 일반 Perfect 컷에 랜덤 발동")]
        [Tooltip("발동 확률 (Perfect당)")]
        public float slowFlourishChance = 0.12f;
        [Tooltip("최소 간격(초) — 남발 방지")]
        public float slowFlourishCooldown = 9f;
        float _lastSlowFlourish = -999f;

        Sliceable _mash;       // 연타 중인 대형 재료
        float _mashUntil;      // 연타 유효시간 — 지나면 해제 (입력 블랙홀 방지)
        bool _slow;            // 슬로우 컷 연출 중
        Sliceable _holdTarget; // 홀드 중인 재료
        double _holdEnd;
        double _holdStart;
        int _holdLane;
        Vector3 _holdDir;

        /// <summary>홀드 남은 비율 1→0 (0 = 지금 떼야 함). HUD 릴리즈 링이 사용.</summary>
        public static float HoldProgress
        {
            get
            {
                var i = Instance;
                if (i == null || i._holdTarget == null || Conductor.Instance == null) return 0f;
                float len = (float)(i._holdEnd - i._holdStart);
                if (len <= 0f) return 0f;
                return Mathf.Clamp01((float)(i._holdEnd - Conductor.Instance.SongPosition) / len);
            }
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            GameSettings.OnSfxChanged -= HandleSfxVolume;
        }

        void HandleSfxVolume(float v)
        {
            if (_sfx != null) _sfx.volume = v;
        }

        Vector2 _swipeStart;
        bool _swiping;

        /// <summary>스와이프 인식 최소 거리(px) — 모바일 고DPI는 비례 확대.</summary>
        static float SwipeThreshold => Screen.dpi > 0f ? Mathf.Max(50f, Screen.dpi * 0.25f) : 50f;

        void Update()
        {
            if (PauseMenu.IsPaused) return; // 일시정지 중 썰기 입력 차단

            // 홀드 유지 중엔 다른 입력을 받지 않는다 — 떼는 순간만 본다
            if (_holdTarget != null)
            {
                UpdateHold();
                return;
            }

            // 레인: 0=좌, 1=우, 2=상, 3=하 (WASD + 방향키)
            if (Pressed(KeyCode.A, KeyCode.LeftArrow)) Hit(0);
            else if (Pressed(KeyCode.D, KeyCode.RightArrow)) Hit(1);
            else if (Pressed(KeyCode.W, KeyCode.UpArrow)) Hit(2);
            else if (Pressed(KeyCode.S, KeyCode.DownArrow)) Hit(3);
            else if (Input.GetKeyDown(KeyCode.Space)) Hit(-1); // 테스트용 방향 무시 입력

            HandleSwipe();
        }

        /// <summary>마우스 드래그/터치 스와이프 → 방향 입력. 터치는 유니티 마우스 에뮬레이션 경로 공유.
        /// 임계 거리를 넘는 즉시 발동 — 손을 뗄 때까지 기다리면 리듬게임에선 늦다.</summary>
        void HandleSwipe()
        {
            // 곡 진행 중에만 — 결과/카운트다운 화면의 UI 클릭이 헛스윙 되는 것 방지
            if (Conductor.Instance == null || !Conductor.Instance.IsPlaying) { _swiping = false; return; }

            if (Input.GetMouseButtonDown(0))
            {
                _swiping = true;
                _swipeStart = Input.mousePosition;
            }
            else if (_swiping && Input.GetMouseButton(0))
            {
                var delta = (Vector2)Input.mousePosition - _swipeStart;
                if (delta.magnitude >= SwipeThreshold)
                {
                    _swiping = false;
                    Hit(LaneFromSwipe(delta));
                }
            }
            else if (_swiping && Input.GetMouseButtonUp(0))
            {
                _swiping = false;
                var delta = (Vector2)Input.mousePosition - _swipeStart;
                // 짧게 그은 채 뗀 경우도 절반 임계로 인정. 제자리 탭은 무시 —
                // 방향 무시 판정(-1)으로 넣으면 아무 화살표나 맞는 치트가 된다
                if (delta.magnitude >= SwipeThreshold * 0.5f) Hit(LaneFromSwipe(delta));
            }
        }

        static int LaneFromSwipe(Vector2 d) =>
            Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x < 0f ? 0 : 1) : (d.y > 0f ? 2 : 3);

        static bool Pressed(KeyCode a, KeyCode b) => Input.GetKeyDown(a) || Input.GetKeyDown(b);

        void Hit(int lane)
        {
            // 판정과 무관하게 키를 누르면 항상 칼이 나간다 — 헛스윙도 보여야 조작감이 산다
            if (spawner != null && spawner.sliceZone != null)
            {
                var pos = lane >= 0 ? spawner.ZonePos(lane) : spawner.sliceZone.position;
                SlashStreak.Spawn(pos, lane);
            }
            ChefAvatar.Instance?.Swing(lane);

            // 연타 유효시간 초과 → 해제. 안 그러면 떨어진 재료가 입력을 계속 흡수해
            // 다음 노트가 전부 미스나면서 쨍한 소리만 남 ("미스인데 퍼펙트 소리" 버그)
            if (_mash != null && Time.time > _mashUntil) _mash = null;

            // 연타 중인 대형 재료가 있으면 그쪽 우선 — 추가 타격은 판정 없이 보너스
            if (_mash != null)
            {
                bool done = _mash.TakeHit(IngredientSpawner.LaneDir(lane));
                _mashUntil = Time.time + _mash.mashHoverTime; // 타격마다 연장
                CameraPunch.Instance?.Punch(goodPunch);
                PlaySliceSound(perfect: true); // 연타는 성공 타격 — 쨍한 소리 유지
                ScoreTracker.AddBonus(25);
                if (done)
                {
                    _mash = null;
                    HitStop.Instance?.Do(perfectHitStop);
                    CameraPunch.Instance?.Punch(perfectPunch);
                }
                return;
            }

            var result = BeatJudge.Instance != null
                ? BeatJudge.Instance.TryHit(laneMatters ? lane : -1)
                : JudgeResult.None;

            if (result == JudgeResult.None && whiffSound != null && _sfx != null)
            {
                _sfx.pitch = Random.Range(0.95f, 1.05f);
                _sfx.PlayOneShot(whiffSound, 0.35f);
            }
        }

        void PlaySliceSound(bool perfect = false)
        {
            if (_sfx == null) return;

            // Perfect = 매번 똑같은 쨍한 원본(피치 고정), Good/기타 = 둔탁 버전(살짝 랜덤 피치)
            if (perfect && _perfectClip != null)
            {
                _sfx.pitch = 1f;
                _sfx.PlayOneShot(_perfectClip);
                return;
            }
            if (_goodClip != null)
            {
                _sfx.pitch = 1f; // Good도 단일 사운드 — 랜덤 피치 없음
                _sfx.PlayOneShot(_goodClip);
                return;
            }

            // 폴백: 씬 배선 클립 → 신디사이즈 톤
            _sfx.pitch = Random.Range(0.96f, 1.04f);
            if (sliceSounds != null && sliceSounds.Length > 0)
            {
                _sfx.PlayOneShot(sliceSounds[Random.Range(0, sliceSounds.Length)]);
            }
            else if (_don != null)
            {
                _sfx.PlayOneShot(_don, 0.9f);
            }
            if (perfect && _ting != null)
                _sfx.PlayOneShot(_ting, 0.6f); // Perfect 보상음
        }

        void HandleJudged(JudgeResult result, BeatEvent e, float delta)
        {
            var target = spawner != null ? spawner.CurrentTarget(e.lane) : null;
            var sliceDir = IngredientSpawner.LaneDir(e.lane); // 칼 방향 = 누른 키 방향 (화면 기준)

            // 슬로우 컷(type=2): 판정 성공 시 시간 늦추고 카메라 줌 — 천천히 갈라지는 한 컷
            if (e.type == 2 && result != JudgeResult.Miss && target != null)
            {
                _combo++;
                StartCoroutine(SlowSliceRoutine(target, sliceDir, result == JudgeResult.Perfect));
                return;
            }

            // 홀드(type=3): 판정 성공 시 누르고 있기 시작 — 박자 끝에 떼면 성공
            if (e.type == 3 && result != JudgeResult.Miss && target != null)
            {
                _combo++;
                BeginHold(target, e, sliceDir, result == JudgeResult.Perfect);
                return;
            }

            switch (result)
            {
                case JudgeResult.Perfect:
                    _combo++;
                    // 가끔 슬로우 컷 연출 — 예고 없이 터지는 서비스 컷 (일반 노트만, 쿨다운 있음)
                    if (e.type == 0 && target != null && target.hitsRequired <= 1
                        && Time.time - _lastSlowFlourish > slowFlourishCooldown
                        && Random.value < slowFlourishChance)
                    {
                        _lastSlowFlourish = Time.time;
                        StartCoroutine(SlowSliceRoutine(target, sliceDir, perfectEntry: true)); // 플러리시는 Perfect에서만 발동
                        PlaySliceSound(perfect: true);
                        break;
                    }
                    HitDown(target, sliceDir);
                    HitStop.Instance?.Do(perfectHitStop);
                    CameraPunch.Instance?.Punch(perfectPunch);
                    CameraDirector.Instance?.HitKick(true);
                    // 8콤보마다 카메라가 요리사를 크게 훽 도는 스핀 — 컷신 무빙
                    CameraOrbit.Instance?.AngleKick(WhipFor(e.lane,
                        ScoreTracker.CurrentCombo % 8 == 0 ? 85f : 32f));
                    PlaySlashFx(target);
                    PlaySliceSound(perfect: true);
                    break;

                case JudgeResult.Good:
                    _combo++;
                    HitDown(target, sliceDir);
                    CameraPunch.Instance?.Punch(goodPunch);
                    CameraDirector.Instance?.HitKick(false);
                    CameraOrbit.Instance?.AngleKick(WhipFor(e.lane, 16f));
                    PlaySlashFx(target);
                    PlaySliceSound();
                    break;

                case JudgeResult.Miss:
                    _combo = 0;
                    // Miss: 재료가 그냥 지나가서 바닥에 떨어짐 — 별도 처리 없음 (물리가 알아서)
                    break;
            }
        }

        /// <summary>홀드 시작: 재료 공중 고정 + 서서히 찌부러뜨리기. 입력을 유지해야 한다.</summary>
        void BeginHold(Sliceable target, BeatEvent e, Vector3 sliceDir, bool perfectEntry)
        {
            _holdTarget = target;
            _holdLane = e.lane;
            _holdDir = sliceDir;
            float secPerBeat = Conductor.Instance != null ? Conductor.Instance.SecPerBeat : 0.5f;
            float holdBeats = spawner != null ? spawner.holdBeats : 2f;
            _holdStart = Conductor.Instance?.SongPosition ?? e.time;
            _holdEnd = e.time + holdBeats * secPerBeat;

            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
            }
            // 누르는 동안 서서히 찌부러진다 — 진행도가 눈에 보이는 게이지 역할
            float holdSec = (float)(_holdEnd - (Conductor.Instance?.SongPosition ?? 0.0));
            var s = target.transform.localScale;
            target.transform.DOScale(new Vector3(s.x * 1.25f, s.y * 0.45f, s.z * 1.25f), Mathf.Max(0.2f, holdSec))
                .SetEase(Ease.InQuad).SetLink(target.gameObject);
            PlaySliceSound(perfectEntry); // 진입 판정 반영 — Perfect면 쨍, Good이면 둔탁
        }

        /// <summary>홀드 유지/해제 감시. 릴리즈 타이밍도 노트와 같은 창으로 3단 판정.</summary>
        void UpdateHold()
        {
            if (_holdTarget == null) return;
            double now = Conductor.Instance != null ? Conductor.Instance.SongPosition : 0.0;

            if (now >= _holdEnd)
            {
                FinishHold(JudgeResult.Perfect); // 끝까지 누름 = 정타
                return;
            }
            if (!IsHoldInputHeld())
            {
                float early = (float)(_holdEnd - now); // 얼마나 일찍 뗐나
                float perfectW = BeatJudge.Instance != null ? BeatJudge.Instance.perfectWindow : 0.09f;
                float goodW = BeatJudge.Instance != null ? BeatJudge.Instance.goodWindow : 0.2f;
                FinishHold(early <= perfectW ? JudgeResult.Perfect
                         : early <= goodW ? JudgeResult.Good
                         : JudgeResult.Miss);
            }
        }

        /// <summary>홀드 입력 유지 확인 — 키보드(해당 레인 키) 또는 마우스/터치.</summary>
        bool IsHoldInputHeld()
        {
            if (Input.GetMouseButton(0)) return true;
            if (Input.GetKey(KeyCode.Space)) return true;
            return _holdLane switch
            {
                0 => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
                1 => Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow),
                2 => Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
                3 => Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                _ => false,
            };
        }

        void FinishHold(JudgeResult result)
        {
            var target = _holdTarget;
            _holdTarget = null;
            if (target == null) return;

            target.transform.DOKill();
            PromptHUD.Instance?.ShowJudge(result);

            switch (result)
            {
                case JudgeResult.Perfect:
                    _combo++;
                    ScoreTracker.Register(JudgeResult.Perfect, 200);
                    ChefAvatar.Instance?.Swing(_holdLane);
                    target.Slice(_holdDir);
                    HitStop.Instance?.Do(perfectHitStop);
                    CameraPunch.Instance?.Punch(perfectPunch);
                    CameraDirector.Instance?.HitKick(true);
                    PlaySlashFx(target);
                    PlaySliceSound(perfect: true);
                    break;

                case JudgeResult.Good:
                    _combo++;
                    ScoreTracker.Register(JudgeResult.Good, 100);
                    ChefAvatar.Instance?.Swing(_holdLane);
                    target.Slice(_holdDir);
                    CameraPunch.Instance?.Punch(goodPunch);
                    PlaySlashFx(target);
                    PlaySliceSound();
                    break;

                default:
                    // 너무 일찍 뗌 = 놓침 — 재료는 안 썰리고 찌부러진 채 떨어진다. 콤보 끊김
                    _combo = 0;
                    ScoreTracker.Register(JudgeResult.Miss, 0);
                    var rb = target.GetComponent<Rigidbody>();
                    if (rb != null) rb.useGravity = true;
                    if (whiffSound != null && _sfx != null)
                        _sfx.PlayOneShot(whiffSound, 0.5f);
                    break;
            }
        }

        /// <summary>슬로우 컷: 시간 0.2배 + 재료 공중 고정 + 줌인 → 천천히 벤 뒤 폭발 마무리.</summary>
        System.Collections.IEnumerator SlowSliceRoutine(Sliceable target, Vector3 sliceDir, bool perfectEntry)
        {
            _slow = true;
            Time.timeScale = 0.2f;
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity *= 0.05f;
                rb.useGravity = false;
            }
            PlaySliceSound(perfectEntry); // 진입 판정 반영 — Perfect면 쨍, Good이면 둔탁

            float t = 0f;
            while (t < 0.9f && target != null && !PauseMenu.IsPaused)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = PauseMenu.IsPaused ? 0f : 1f;
            if (target != null) target.Slice(sliceDir);
            HitStop.Instance?.Do(perfectHitStop);
            CameraPunch.Instance?.Punch(perfectPunch);
            CameraDirector.Instance?.HitKick(true);
            PlaySlashFx(target);
            PlaySliceSound(perfect: true);
            ScoreTracker.AddBonus(150);
            _slow = false;
        }

        /// <summary>벤 방향으로 궤도가 홱 도는 앵글 휩 각도 (상하는 랜덤 방향).</summary>
        static float WhipFor(int lane, float amount)
        {
            float sign = lane switch { 0 => -1f, 1 => 1f, _ => Random.value < 0.5f ? -1f : 1f };
            return sign * amount;
        }

        /// <summary>재료 타격. 대형이면 연타 모드 진입 (첫 타는 판정, 나머지는 자유 연타).</summary>
        void HitDown(Sliceable target, Vector3 sliceDir)
        {
            if (target == null) return;
            bool done = target.TakeHit(sliceDir);
            _mash = done ? null : target;
            if (_mash != null) _mashUntil = Time.time + _mash.mashHoverTime; // 연타 시작 유효시간
        }

        void PlaySlashFx(Sliceable target)
        {
            if (slashFxPrefab == null) return;
            var pos = target != null ? target.transform.position : transform.position;
            var fx = Instantiate(slashFxPrefab, pos, Quaternion.identity);
            Destroy(fx.gameObject, 2f);
        }
    }
}
