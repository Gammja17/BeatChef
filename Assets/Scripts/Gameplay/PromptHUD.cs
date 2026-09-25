using System.Collections.Generic;
using BeatSlash.Menu;
using BeatSlash.Rhythm;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 리듬히어로식 산개 프롬프트 HUD.
    /// 각 노트의 방향 화살표가 화면 랜덤 위치에 팝업하고, 어프로치 링이 조여들어
    /// 겹치는 순간이 정답 타이밍. 유저는 등장 순서/링 크기로 입력 순서를 유추한다.
    /// 재료는 여전히 셰프에게 날아간다 — 프롬프트는 별도 레이어.
    /// 대형 연타 재료는 주황 대형 화살표 + 예고/연타 배너.
    /// </summary>
    public class PromptHUD : MonoBehaviour
    {
        public static PromptHUD Instance { get; private set; }

        string[] _arrows;

        [Tooltip("비트 몇 초 전에 화면에 등장하는지")]
        public float leadTime = 1.2f;
        public Color hitColor = new Color(1f, 0.85f, 0.2f, 1f);
        public Color bigColor = new Color(1f, 0.45f, 0.1f, 1f);
        [Tooltip("동시에 표시할 최대 프롬프트 수 — 정신없음 방지")]
        public int maxVisible = 4;
        [Tooltip("프롬프트가 뜨는 원 반경 (최소~최대) — 좁을수록 시선 이동 적음")]
        public float promptRadiusMin = 230f;
        public float promptRadiusMax = 330f;

        [Header("클래식 모드 (4방향 고정 리셉터로 날아옴)")]
        public float classicReceptorRadius = 300f;
        public float classicFlyDistance = 430f;

        static readonly Vector2[] Dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        bool _classic;
        readonly RawImage[] _receptors = new RawImage[4];
        RawImage _holdRingInner;
        RawImage _holdRingOuter;
        [Tooltip("씬 빌더가 연결 (현재 미사용, 확장용)")]
        public IngredientSpawner spawner;

        class Prompt
        {
            public RectTransform root;   // 이동/스케일 컨테이너
            public Graphic arrow;        // Image(스프라이트) 또는 Text(글리프 폴백)
            public RawImage ring;
            public RawImage target;      // 고정 판정 원 — 어프로치 링이 여기에 닿는 순간이 정타
            public Vector2 anchor;
            public Color typeColor;      // 노트 종류 색 — 링/플레이트/뱃지가 공유
            public RawImage plate;       // 배경 원판 — "지금!" 플래시용
            public Color plateColor;
        }

        // "지금 쳐!" 신호 — 순백 점화 (초록은 너무 강렬해서 톤다운)
        static readonly Color NowColor = new Color(1f, 1f, 1f, 0.95f);

        // 노트 종류 색: 연타=주황(bigColor), 슬로우=하늘, 홀드=연두, 일반=골드(hitColor)
        static readonly Color SlowColor = new Color(0.5f, 0.9f, 1f);
        static readonly Color HoldColor = new Color(0.7f, 1f, 0.5f);
        Texture2D _discTex;

        Transform _canvas;
        Texture2D _ringTex;
        RawImage _flash;
        Text _result;
        Text _combo;
        Text _score;
        Text _fever;
        Text _stateBanner;
        Text _mashCount;
        int _lastMashCount = -1;
        readonly Dictionary<BeatEvent, Prompt> _prompts = new Dictionary<BeatEvent, Prompt>();
        readonly List<(Graphic g, float t)> _bursts = new List<(Graphic, float)>();
        readonly List<Prompt> _visibleOrdered = new List<Prompt>();
        readonly List<RawImage> _links = new List<RawImage>();
        float _resultUntil;

        void Awake()
        {
            Instance = this;
            UIFont.Get();
            _arrows = UIFont.HasCustom
                ? new[] { "◀", "▶", "▲", "▼" }
                : new[] { "<", ">", "^", "v" };

            var canvasGo = new GameObject("PromptCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 폭/높이 균형 — 모바일 포함 어떤 비율에서도 비례 유지
            _canvas = canvasGo.transform;

            // 커스텀 링 스프라이트(UI/note_ring) 있으면 그걸로, 없으면 프로시저럴 (두껍게 — 얇으면 안 읽힌다)
            var ringSprite = Resources.Load<Sprite>("UI/note_ring");
            _ringTex = ringSprite != null ? ringSprite.texture : MakeRingTexture(128, 0.5f, 0.3f);
            _discTex = MakeDiscTexture(128);

            // 전체 화면 플래시 — 제일 먼저 만들어 다른 UI 뒤에 깔린다
            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(_canvas, false);
            _flash = flashGo.AddComponent<RawImage>();
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;
            var frt = _flash.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;

            // 프롬프트 연결선 — 지금 화살표에서 다음 화살표로 시선을 끌어준다 (항상 화살표 뒤에 그려짐)
            for (int i = 0; i < Mathf.Max(1, maxVisible - 1); i++)
            {
                var go = new GameObject($"Link{i}");
                go.transform.SetParent(_canvas, false);
                var img = go.AddComponent<RawImage>();
                img.texture = Texture2D.whiteTexture;
                img.raycastTarget = false;
                img.enabled = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                _links.Add(img);
            }

            // 클래식 모드용 4방향 고정 리셉터 링
            for (int i = 0; i < 4; i++)
            {
                _receptors[i] = MakeRing(_canvas, $"Receptor{i}", 170f, _ringTex);
                _receptors[i].enabled = false;
            }

            // 홀드 릴리즈 링 — 홀드 중 화면 중앙에서 바깥 링이 조여들고, 안쪽 링과 겹치는 순간 = 떼는 타이밍
            _holdRingInner = MakeRing(_canvas, "HoldRingInner", 200f, _ringTex);
            _holdRingOuter = MakeRing(_canvas, "HoldRingOuter", 200f, _ringTex);
            _holdRingInner.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            _holdRingOuter.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            _holdRingInner.enabled = _holdRingOuter.enabled = false;

            _stateBanner = MakeText(_canvas, "StateBanner", 40, new Vector2(0f, 225f));
            _stateBanner.text = "";
            _mashCount = MakeText(_canvas, "MashCount", 150, Vector2.zero);
            _mashCount.text = "";
            _mashCount.color = new Color(1f, 0.55f, 0.15f);

            _result = MakeText(_canvas, "Result", 64, new Vector2(0f, 300f));
            _combo = MakeText(_canvas, "Combo", 48, new Vector2(0f, -380f));
            _score = MakeText(_canvas, "Score", 56, new Vector2(0f, 460f));
            _fever = MakeText(_canvas, "Fever", 80, new Vector2(0f, 380f));
            _result.text = "";
            _combo.text = "";
            _score.text = "0";
            _score.color = new Color(1f, 1f, 1f, 0.85f);
            _fever.text = "";
        }

        void Start()
        {
            if (BeatJudge.Instance != null) BeatJudge.Instance.OnJudged += HandleJudged;
            if (spawner == null) spawner = FindAnyObjectByType<IngredientSpawner>();
            _classic = SongSelection.PromptMode == 1; // 메인 메뉴에서 고른 모드
        }

        /// <summary>클래식 모드 고정 리셉터 위치 (좌우는 넓게).</summary>
        Vector2 ReceptorPos(int lane)
        {
            var d = Dirs[lane];
            return new Vector2(d.x * classicReceptorRadius * 1.35f, d.y * classicReceptorRadius * 0.9f);
        }

        void OnDestroy()
        {
            if (BeatJudge.Instance != null) BeatJudge.Instance.OnJudged -= HandleJudged;
        }

        void Update()
        {
            if (BeatJudge.Instance == null || Conductor.Instance == null) return;

            float songPos = (float)Conductor.Instance.SongPosition;
            float beatKick = Conductor.Instance.BeatKick01; // 정박 쿵 — HUD 전체가 박자를 탄다
            var hotLanes = new bool[4];
            _visibleOrdered.Clear();

            for (int i = 0; i < maxVisible; i++)
            {
                var e = BeatJudge.Instance.PeekAt(i);
                if (e == null) break;
                float remain = e.time - songPos;
                if (remain > leadTime) break; // 이벤트는 시간순

                int lane = Mathf.Clamp(e.lane, 0, 3);
                bool big = e.type == 1;
                bool slowNote = e.type == 2;
                bool holdNote = e.type == 3;

                if (!_prompts.TryGetValue(e, out var p))
                {
                    p = CreatePrompt(e, lane, i);
                    _prompts[e] = p;
                }

                float frac = Mathf.Clamp01(remain / leadTime); // 1=멀다, 0=도착
                float near = 1f - frac;
                bool ready = remain <= 0.3f;                 // 곧이다 — 종류색으로 점화
                // 지금! — Perfect 판정 창 안에서만 번쩍 (0.14초였을 땐 정타보다 먼저 번쩍여 이르게 치게 만들었음)
                bool hot = Mathf.Abs(remain) <= BeatJudge.Instance.perfectWindow;

                // 순번별 강조: 1순위 뚜렷+살짝 크게, 뒤로 갈수록 투명+작게 — 시선 우선순위
                float orderFade = i switch { 0 => 1f, 1 => 0.6f, 2 => 0.38f, _ => 0.22f };
                float focus = i == 0 ? 1.12f : i >= 2 ? 0.82f : 1f;

                // 화살표: 팝인 후 자리 고정, 다가올수록 진해짐. 판정권 진입 시 점프 강조
                float appear = Mathf.Clamp01(near * 7f);
                float bigScale = big ? 1.6f : slowNote ? 1.35f : holdNote ? 1.25f : 1f;
                float pop = 1f + (1f - appear) * 0.6f; // 등장 순간 살짝 크게
                float hotPop = hot ? 1.35f : ready ? 1.12f : 1f; // 곧→지금 2단 점프
                p.root.localScale = Vector3.one *
                    (Mathf.Lerp(0.8f, 1.05f, near) * bigScale * pop * focus * hotPop * (1f + beatKick * 0.05f));
                // 스프라이트 화살표는 자체 색이 있으니 흰색 유지(틴트 안 함), 글리프만 색 연출
                bool spriteArrow = p.arrow is Image;
                var col = spriteArrow
                    ? Color.white
                    : e.type != 0 ? p.typeColor : Color.Lerp(Color.white, hitColor, near * near);
                if (!spriteArrow && hot) col = Color.white;
                col.a = appear * (big ? Mathf.Max(orderFade, 0.85f) : orderFade);
                p.arrow.color = col;

                if (_classic)
                {
                    // 클래식: 화살표가 자기 방향 바깥에서 리셉터로 등속 접근 — 위치 = 타이밍
                    p.root.anchoredPosition = p.anchor + Dirs[lane] * (classicFlyDistance * frac);
                    if (hot) hotLanes[lane] = true;
                }
                else if (p.ring != null)
                {
                    // 산개: 어프로치 링이 조여들어 화살표와 겹치는 순간 = 지금.
                    // 3단 신호: 멀다=흐린 종류색 → 곧(0.3s)=진한 종류색 → 지금(±0.14s)=초록 풀점화
                    // 링은 1·2순위만 — 셋 이상 겹치면 링 소음이 가독성을 죽인다
                    p.ring.enabled = i < 2;
                    p.target.enabled = i < 2;
                    var tc = p.typeColor;
                    // 링은 판정 원에 정확히 닿고(remain=0), 늦으면 안쪽으로 계속 파고든다 — 늦었다는 게 보인다
                    float approach = 1f + 1.4f * Mathf.Max(remain / leadTime, -0.2f);
                    p.ring.transform.localScale = Vector3.one * approach * bigScale * focus;
                    p.target.transform.localScale = Vector3.one * bigScale * focus;
                    p.target.color = hot ? NowColor : new Color(1f, 1f, 1f, 0.55f * appear * orderFade);
                    p.ring.color = hot
                        ? NowColor
                        : ready
                            ? new Color(tc.r, tc.g, tc.b, 1f)
                            : new Color(tc.r, tc.g, tc.b, (e.type != 0 ? 0.7f : 0.45f) * appear * orderFade);
                }

                // 배경 원판: "지금!" 순간 은은하게 밝아짐 — 깜빡임 없이 차분한 점화
                if (p.plate != null)
                {
                    p.plate.color = hot
                        ? Color.Lerp(p.plateColor, new Color(1f, 1f, 1f, 0.75f), 0.55f)
                        : ready
                            ? Color.Lerp(p.plateColor, new Color(p.typeColor.r, p.typeColor.g, p.typeColor.b, 0.6f), 0.6f)
                            : p.plateColor;
                }

                _visibleOrdered.Add(p);
            }

            // 클래식 리셉터: 항상 표시, 판정권 진입 시 점화
            for (int lane = 0; lane < 4; lane++)
            {
                var rec = _receptors[lane];
                rec.enabled = _classic && Conductor.Instance.IsPlaying;
                if (!rec.enabled) continue;
                rec.rectTransform.anchoredPosition = ReceptorPos(lane);
                rec.color = hotLanes[lane]
                    ? new Color(hitColor.r, hitColor.g, hitColor.b, 0.95f)
                    : new Color(1f, 1f, 1f, 0.16f);
                rec.transform.localScale = Vector3.one * (hotLanes[lane] ? 1.12f : 1f) * (1f + beatKick * 0.07f);
            }

            // 연결선: 지금 → 다음 → 그다음. 첫 연결이 제일 진하다 (산개 모드 전용)
            int li = 0;
            for (int i = 0; !_classic && i + 1 < _visibleOrdered.Count && li < _links.Count; i++, li++)
            {
                var a = _visibleOrdered[i].anchor;
                var b = _visibleOrdered[i + 1].anchor;
                var link = _links[li];
                var delta = b - a;
                float dist = delta.magnitude;
                if (dist < 200f) { link.enabled = false; continue; }

                var dir = delta / dist;
                var start = a + dir * 100f; // 화살표 글리프 안 가리게 양끝 여백
                var end = b - dir * 100f;
                link.enabled = true;
                link.rectTransform.anchoredPosition = (start + end) * 0.5f;
                link.rectTransform.sizeDelta = new Vector2(Vector2.Distance(start, end), 5f);
                link.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                link.color = new Color(1f, 1f, 1f, i == 0 ? 0.5f : 0.2f);
            }
            for (; li < _links.Count; li++) _links[li].enabled = false;

            // 홀드 릴리즈 링: 남은 시간에 비례해 조여든다 — 겹치는 순간 떼면 성공
            bool holding = SlashController.HoldActive;
            _holdRingInner.enabled = _holdRingOuter.enabled = holding;
            if (holding)
            {
                float p = SlashController.HoldProgress; // 1→0
                var ringCol = new Color(0.7f, 1f, 0.5f); // 배너 폰트와 같은 연두
                _holdRingInner.color = new Color(ringCol.r, ringCol.g, ringCol.b, 0.85f);
                _holdRingInner.transform.localScale = Vector3.one;
                _holdRingOuter.color = new Color(ringCol.r, ringCol.g, ringCol.b, Mathf.Lerp(1f, 0.45f, p));
                _holdRingOuter.transform.localScale = Vector3.one * Mathf.Lerp(1f, 2.6f, p);
            }

            // 대형 배너 은퇴 — 홀드 안내만 차분한 고정 텍스트로 (흔들림 없음)
            if (SlashController.HoldActive)
            {
                _stateBanner.text = "링이 겹칠 때 떼!";
                _stateBanner.color = new Color(0.7f, 1f, 0.5f, 0.9f);
            }
            else
            {
                _stateBanner.text = "";
            }

            // 연타 카운터: 연타 재료 위에 남은 횟수 숫자 — 칠 때마다 줄며 팍 튄다
            var mashTarget = SlashController.MashTarget;
            if (mashTarget != null && Camera.main != null)
            {
                int remaining = mashTarget.RemainingHits;
                _mashCount.text = remaining.ToString();
                var vp = Camera.main.WorldToViewportPoint(mashTarget.transform.position + Vector3.up * 0.9f);
                _mashCount.rectTransform.anchoredPosition = new Vector2((vp.x - 0.5f) * 1920f, (vp.y - 0.5f) * 1080f);
                if (remaining != _lastMashCount)
                {
                    _lastMashCount = remaining;
                    _mashCount.transform.DOKill();
                    _mashCount.transform.localScale = Vector3.one * 1.6f;
                    _mashCount.transform.DOScale(1f, 0.15f).SetUpdate(true).SetLink(_mashCount.gameObject);
                }
            }
            else
            {
                _mashCount.text = "";
                _lastMashCount = -1;
            }

            // 판정 버스트: 커지며 사라짐
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var (txt, bt) = _bursts[i];
                bt += Time.unscaledDeltaTime * 4f;
                if (bt >= 1f || txt == null)
                {
                    // 화살표는 root(플레이트 포함) 컨테이너의 자식 — 루트째 정리해야 누수 없음
                    if (txt != null)
                        Destroy(txt.transform.parent != null ? txt.transform.parent.gameObject : txt.gameObject);
                    _bursts.RemoveAt(i);
                    continue;
                }
                txt.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 2f, bt);
                var c = txt.color;
                c.a = 1f - bt;
                txt.color = c;
                _bursts[i] = (txt, bt);
            }

            // FEVER 배너
            if (FeverMode.Instance != null && FeverMode.Instance.IsFever)
            {
                _fever.text = "FEVER!!";
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.12f;
                _fever.transform.localScale = Vector3.one * pulse;
                _fever.color = Color.Lerp(
                    new Color(1f, 0.85f, 0.2f), new Color(1f, 0.4f, 0.15f),
                    (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f);
            }
            else
            {
                _fever.text = "";
            }

            if (Time.unscaledTime > _resultUntil) _result.text = "";

            // 연타 보너스 등 판정 밖 점수 변동도 즉시 반영 + 점수판이 비트를 탄다
            _score.text = ScoreTracker.Score.ToString("N0");
            _score.transform.localScale = Vector3.one * (1f + beatKick * 0.09f);
        }

        Prompt CreatePrompt(BeatEvent e, int lane, int orderIndex)
        {
            // 클래식: 고정 리셉터가 과녁 — 개별 링 없이 화살표가 날아온다
            var anchor = _classic ? ReceptorPos(lane) : PickAnchor(lane, orderIndex);
            var typeColor = e.type switch { 1 => bigColor, 2 => SlowColor, 3 => HoldColor, _ => hitColor };
            var p = new Prompt { anchor = anchor, typeColor = typeColor };

            // 컨테이너(root) — 이동/스케일은 여기, 화살표는 자식
            var rootGo = new GameObject("PromptNote");
            rootGo.transform.SetParent(_canvas, false);
            p.root = rootGo.AddComponent<RectTransform>();
            p.root.anchorMin = p.root.anchorMax = new Vector2(0.5f, 0.5f);
            p.root.anchoredPosition = anchor;

            // 배경 원판: 모든 노트가 가짐 — 뒤의 파티클/재료 난장판에서 화살표를 분리해준다
            // 일반 = 어두운 원판, 특수 = 종류 색 원판 + 뱃지
            var plateGo = new GameObject("Plate");
            plateGo.transform.SetParent(rootGo.transform, false);
            var plate = plateGo.AddComponent<RawImage>();
            plate.texture = _discTex;
            plate.raycastTarget = false;
            var plateColor = e.type != 0
                ? new Color(typeColor.r, typeColor.g, typeColor.b, 0.45f)
                : new Color(0f, 0f, 0f, 0.55f);
            plate.color = plateColor;
            plate.rectTransform.sizeDelta = new Vector2(185f, 185f);
            p.plate = plate;
            p.plateColor = plateColor;

            if (e.type != 0)
            {
                string badge = e.type switch
                {
                    1 => $"연타 ×{(spawner != null ? spawner.bigHits : 5)}",
                    2 => "SLOW",
                    3 => "HOLD",
                    _ => "",
                };
                var bt = MakeText(rootGo.transform, "Badge", 36, new Vector2(0f, -112f));
                bt.text = badge;
                bt.color = typeColor;
            }

            var sprite = UISprites.Arrow(lane);
            if (sprite != null)
            {
                var arrowGo = new GameObject("Arrow");
                arrowGo.transform.SetParent(rootGo.transform, false);
                var img = arrowGo.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = new Vector2(150f, 150f);
                p.arrow = img;
            }
            else
            {
                var t = MakeText(rootGo.transform, "Arrow", 120, Vector2.zero);
                t.text = _arrows[lane];
                p.arrow = t;
            }

            if (!_classic)
            {
                // 고정 판정 원(흰색) + 어프로치 링 — 화살표 뒤에 만들어 원판 위에 그린다.
                // 원판(정타 때 최대 ~260px)보다 큰 230px 기준이라 겹치는 순간이 안 가려진다
                p.target = MakeRing(_canvas, "PromptTarget", 230f, _ringTex);
                p.target.rectTransform.anchoredPosition = anchor;
                p.ring = MakeRing(_canvas, "PromptRing", 230f, _ringTex);
                p.ring.rectTransform.anchoredPosition = anchor;
            }
            return p;
        }

        /// <summary>셰프 주변 좁은 원 안에서만 생성 — 시선 이동 최소화.
        /// 각도는 자기 방향 부채꼴이라 위치로 방향이 읽힌다. 겹치면 부채꼴만 넓혀가며 재시도.</summary>
        Vector2 PickAnchor(int lane, int orderIndex)
        {
            float baseAngle = lane switch { 0 => 180f, 1 => 0f, 2 => 90f, _ => 270f };
            Vector2 best = Vector2.zero;
            float bestClearance = -1f;
            for (int tries = 0; tries < 24; tries++)
            {
                float spread = 30f + tries * 6f;
                float angle = (baseAngle + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
                float r = Random.Range(promptRadiusMin, promptRadiusMax + tries * 5f);
                var candidate = new Vector2(Mathf.Cos(angle) * r * 1.25f, Mathf.Sin(angle) * r * 0.85f);

                float clearance = float.MaxValue;
                foreach (var p in _prompts.Values)
                    clearance = Mathf.Min(clearance, Vector2.Distance(p.anchor, candidate));
                if (clearance >= 245f) return candidate;
                // 전부 실패해도 "가장 덜 겹치는 자리"를 기억해뒀다 쓴다 — 대놓고 겹치는 일 방지
                if (clearance > bestClearance) { bestClearance = clearance; best = candidate; }
            }
            return best;
        }

        void HandleJudged(JudgeResult result, BeatEvent e, float delta)
        {
            // 점수: Perfect 100 / Good 50, 피버 중 배율 적용 — 집계는 ScoreTracker가 담당
            int gain = result switch
            {
                JudgeResult.Perfect => 100,
                JudgeResult.Good => 50,
                _ => 0,
            };
            float mult = FeverMode.Instance != null ? FeverMode.Instance.Multiplier : 1f;
            ScoreTracker.Register(result, Mathf.RoundToInt(gain * mult));

            // 프롬프트 정리: 링은 즉시 제거, 화살표는 그 자리에서 버스트
            if (_prompts.TryGetValue(e, out var p))
            {
                _prompts.Remove(e);
                if (p.ring != null) Destroy(p.ring.gameObject);
                if (p.target != null) Destroy(p.target.gameObject);
                if (p.arrow != null)
                {
                    // 스프라이트 화살표는 자체 색 유지 (틴트하면 노랗게 물듦) — 글리프 폴백만 판정색
                    if (!(p.arrow is Image))
                    {
                        p.arrow.color = result switch
                        {
                            JudgeResult.Perfect => new Color(1f, 0.85f, 0.2f),
                            JudgeResult.Good => new Color(0.3f, 0.9f, 1f),
                            _ => new Color(1f, 0.3f, 0.3f),
                        };
                    }
                    else
                    {
                        p.arrow.color = Color.white; // 접근 페이드로 낮아진 알파 복구
                    }
                    _bursts.Add((p.arrow, 0f));
                }
            }

            switch (result)
            {
                case JudgeResult.Perfect:
                    Show("PERFECT!", new Color(1f, 0.85f, 0.2f));
                    // 화이트 플래시 — "한 방" 체감의 핵심
                    _flash.DOKill();
                    _flash.color = new Color(1f, 1f, 1f, 0.3f);
                    _flash.DOFade(0f, 0.15f).SetUpdate(true).SetLink(_flash.gameObject);
                    break;
                case JudgeResult.Good:
                    Show(delta < 0 ? "GOOD (빠름)" : "GOOD (느림)", new Color(0.3f, 0.9f, 1f));
                    break;
                case JudgeResult.Miss:
                    Show("MISS", new Color(1f, 0.3f, 0.3f));
                    break;
            }

            int combo = ScoreTracker.CurrentCombo;
            if (combo > 1)
            {
                var fm = FeverMode.Instance;
                string feverHint = fm != null && !fm.IsFever
                    ? $"  <size=32>FEVER까지 {Mathf.Max(0, fm.feverCombo - combo)}</size>"
                    : "";
                _combo.text = $"{combo} COMBO{feverHint}";
            }
            else
            {
                _combo.text = "";
            }
            _combo.color = new Color(1f, 1f, 1f, 0.8f);
            if (combo > 1)
            {
                _combo.transform.DOKill(true);
                _combo.transform.DOPunchScale(Vector3.one * (0.45f + 0.02f * Mathf.Min(combo, 30)), 0.25f, 8, 0.7f)
                    .SetUpdate(true).SetLink(_combo.gameObject);
            }
        }

        /// <summary>판정 결과 텍스트 외부 표시용 — 홀드 릴리즈처럼 BeatJudge를 안 거치는 판정에 사용.</summary>
        public void ShowJudge(JudgeResult result)
        {
            switch (result)
            {
                case JudgeResult.Perfect: Show("PERFECT!", new Color(1f, 0.85f, 0.2f)); break;
                case JudgeResult.Good: Show("GOOD", new Color(0.3f, 0.9f, 1f)); break;
                default: Show("MISS", new Color(1f, 0.3f, 0.3f)); break;
            }
        }

        void Show(string text, Color color)
        {
            _result.text = text;
            _result.color = color;
            _resultUntil = Time.unscaledTime + 0.5f;
            _result.transform.DOKill();
            _result.transform.localScale = Vector3.one * 2.3f;
            _result.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-10f, 10f));
            _result.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(_result.gameObject);
            _result.rectTransform.DOLocalRotate(Vector3.zero, 0.22f).SetUpdate(true).SetLink(_result.gameObject);
        }

        // ── UI 헬퍼 ──────────────────────────────────────────────
        static Texture2D MakeRingTexture(int size, float outer, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            float mid = (outer + inner) / 2f;
            float half = (outer - inner) / 2f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / half);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>부드러운 가장자리의 채운 원판 — 특수 노트 플레이트용.</summary>
        static Texture2D MakeDiscTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
                    float a = Mathf.Clamp01((0.95f - d) / 0.12f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static RawImage MakeRing(Transform parent, string name, float size, Texture2D tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return img;
        }

        static Text MakeText(Transform parent, string name, int size, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UIFont.Get();
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            return t;
        }
    }
}
