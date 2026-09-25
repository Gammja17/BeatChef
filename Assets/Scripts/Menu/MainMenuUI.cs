using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatSlash.Rhythm;
using BeatSlash.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeatSlash.Menu
{
    /// <summary>
    /// 메인 화면. 곡(스크롤 목록/파일/웹 업로드) + 난이도 + 모드를 모두 고르면
    /// 게임 시작 버튼이 활성화된다. UI는 런타임에 스스로 구성 (에셋 의존 없음).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public string gameplayScene = "Prototype";

        // (라벨, 에너지 문턱, 박당 노트 밀도, 레인 수) — density 0.5=2박마다, 1=매 박, 2=8분음까지
        // WebGLBuilder가 내장곡 비트맵을 이 테이블 그대로 미리 굽는다
        public static readonly (string label, float energy, float density, int lanes)[] Difficulties =
        {
            ("쉬움", 1.5f, 0.5f, 2),
            ("보통", 1.15f, 1f, 4),
            ("어려움", 0.95f, 2f, 4),
        };

        // 0 = 화면 곳곳 산개(우리 게임 기본 문법 = 클래식), 1 = 4방향 접근(더 읽기 쉬움 = 이지)
        static readonly string[] Modes = { "클래식 모드", "이지 모드" };

        Text _status;
        RectTransform _listContent;
        Transform _canvasRoot;
        AudioSource _bgm;
        AudioSource _preview;
        string _previewFilePath; // 파일 미리듣기 로드 경쟁 방지 토큰
        GameObject _titlePage;
        GameObject _selectPage;
        bool _loading;

        // 선택 상태 — 셋 다 골라야 시작 가능
        int _difficulty = -1;
        int _mode = -1;
        int _sourceType = -1; // 0=내장, 1=로컬 파일, 2=웹 업로드
        AudioClip _builtinClip;
        string _filePath;
        AudioClip _upClip;
        float[] _upSamples;
        int _upRate;
        string _selectedName;

        Image[] _diffButtons;
        Image[] _modeButtons;
        Image[] _dishButtons;
        int _dish; // 기본 0 = 셰프 마음대로
        Image[] _tabButtons;
        bool _tabsUseArt;
        int _listTab; // 0=기본 제공 곡, 1=내 곡
        readonly List<(Image img, string key)> _songButtons = new List<(Image, string)>();
        Button _startButton;
        Image _startImage;
        Text _startLabel;
        GameObject _recordBubble;
        Text _recordText;

        WebSongUploader _uploader;
        string[] _savedSongs = Array.Empty<string>();
        UIManager _ui;

        /// <summary>WebGL엔 파일시스템이 없다 — 내장 곡(Resources/Songs)만 사용.</summary>
        static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>빌드에선 실행파일 옆 Songs/, 에디터에선 프로젝트 루트 Songs/.</summary>
        public static string SongsDir
        {
            get
            {
                var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Songs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        void Start()
        {
            // 로비 BGM (Assets/Resources/LobbySong)
            var lobbySong = Resources.Load<AudioClip>("LobbySong");
            if (lobbySong != null)
            {
                _bgm = gameObject.AddComponent<AudioSource>();
                _bgm.clip = lobbySong;
                _bgm.loop = true;
                _bgm.volume = 0.55f * GameSettings.BgmVolume;
                _bgm.Play();
            }
            GameSettings.OnBgmChanged += HandleBgmVolume;

            // 곡 미리듣기 전용 소스
            _preview = gameObject.AddComponent<AudioSource>();
            _preview.playOnAwake = false;
            _preview.loop = true;
            _preview.volume = 0.75f * GameSettings.BgmVolume;

            _uploader = gameObject.AddComponent<WebSongUploader>();
            _uploader.OnStatus = s => _status.text = s;
            _uploader.OnError = s => { _status.text = s; _loading = false; };
            _uploader.OnLoaded = (clip, samples, rate) =>
            {
                _upClip = clip;
                _upSamples = samples;
                _upRate = rate;
                _listTab = 1; // 업로드하면 "내 곡" 탭으로 전환해 바로 보여준다
                RefreshList();
                SelectSource(2, clip.name, "upload:" + clip.name);
                _status.text = $"업로드됨: {clip.name}";
            };
            _uploader.OnSavedList = names =>
            {
                _savedSongs = names;
                if (_listTab == 1) RefreshList();
            };

            BuildUi();
            RefreshList();
            if (IsWeb) _uploader.RequestSavedList();
        }

        void OnDestroy()
        {
            GameSettings.OnBgmChanged -= HandleBgmVolume;
        }

        void HandleBgmVolume(float v)
        {
            if (_bgm != null) _bgm.volume = 0.55f * v;
            if (_preview != null) _preview.volume = 0.75f * v;
        }

        /// <summary>곡 미리듣기 — 하이라이트쯤(30% 지점)부터 루프. 로비 BGM은 잠시 죽인다.</summary>
        void PlayPreview(AudioClip clip)
        {
            if (_preview == null || clip == null) return;
            _preview.clip = clip;
            _preview.time = Mathf.Min(clip.length * 0.3f, Mathf.Max(0f, clip.length - 5f));
            _preview.Play();
            if (_bgm != null) _bgm.volume = 0.03f;
        }

        void StopPreview()
        {
            if (_preview != null) _preview.Stop();
            _previewFilePath = null;
            if (_bgm != null) _bgm.volume = 0.55f * GameSettings.BgmVolume;
        }

        /// <summary>로컬 파일 곡 미리듣기 — 로드 후에도 여전히 그 곡이 선택돼 있을 때만 재생.</summary>
        IEnumerator PreviewFile(string path)
        {
            _previewFilePath = path;
            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                _ => AudioType.MPEG,
            };
            using var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            if (_previewFilePath != path || _sourceType != 1 || _filePath != path) yield break;
            var clip = DownloadHandlerAudioClip.GetContent(req);
            clip.name = Path.GetFileNameWithoutExtension(path);
            PlayPreview(clip);
        }

        void Update()
        {
            // 튜토리얼이 떠 있으면 키 입력은 튜토리얼 몫
            if (_ui != null && _ui.IsOpen(TutorialPanel.PrefabName)) return;

            // ESC: 곡 선택 페이지에선 뒤로, 타이틀에선 설정 토글
            if (Input.GetKeyDown(KeyCode.Escape) && _canvasRoot != null)
            {
                if (_selectPage != null && _selectPage.activeSelf) ShowTitle();
                else SettingsPanel.Toggle(_canvasRoot);
            }

            // 키아트 타이틀: 방향키/W·S 선택 + Enter 실행
            if (_artTitle && _titlePage != null && _titlePage.activeSelf && _titleItems.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    _titleSel = (_titleSel + 1) % _titleItems.Count;
                    UpdateTitleArrow();
                }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    _titleSel = (_titleSel - 1 + _titleItems.Count) % _titleItems.Count;
                    UpdateTitleArrow();
                }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                    _titleItems[_titleSel].act();
            }
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 폭/높이 균형 — 모바일 포함 어떤 비율에서도 비례 유지
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 화면 비율이 달라져도 UI 비율은 고정
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;
            _canvasRoot = root;
            _ui = gameObject.AddComponent<UIManager>();
            _ui.root = root;

            // 우상단 설정 버튼 — 우상단 코너 앵커라 어떤 화면에서도 안 잘림
            if (UISprites.Gear != null)
            {
                var gearGo = new GameObject("Btn_Settings");
                gearGo.transform.SetParent(root, false);
                var gearImg = gearGo.AddComponent<Image>();
                gearImg.sprite = UISprites.Gear;
                gearImg.preserveAspect = true;
                var grt = gearGo.GetComponent<RectTransform>();
                grt.sizeDelta = new Vector2(72f, 72f);
                AnchorTo(grt, new Vector2(1f, 1f), new Vector2(-80f, -80f));
                gearGo.AddComponent<Button>().onClick.AddListener(() => SettingsPanel.Toggle(root));
            }
            else
            {
                var settings = MakeButton(root, "설정", Vector2.zero, () => SettingsPanel.Toggle(root));
                settings.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 58f);
                AnchorTo(settings.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-110f, -70f));
            }

            // ── 페이지: 타이틀 ↔ 곡 선택 (BGM/디오라마는 공유) ──
            _titlePage = MakePage(root, "TitlePage");
            _selectPage = MakePage(root, "SelectPage");
            var tp = _titlePage.transform;
            var sel = _selectPage.transform;

            // 타이틀 페이지 — 기본은 3D 디오라마(키아트 구도) + 로고/버튼.
            // 풀스크린 아트 모드는 UI/title_fullscreen 넣었을 때만 (옵트인)
            var art = Resources.Load<Sprite>("UI/title_fullscreen");
            if (art != null)
            {
                BuildArtTitle(tp, art);
            }
            else
            {
                var logo = Resources.Load<Sprite>("UI/title_logo");
                if (logo != null)
                {
                    var lg = new GameObject("Logo");
                    lg.transform.SetParent(tp, false);
                    var li = lg.AddComponent<Image>();
                    li.sprite = logo;
                    li.preserveAspect = true;
                    li.raycastTarget = false;
                    // 키아트처럼 좌상단 — 큼직하게
                    var lrt = li.rectTransform;
                    lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                    lrt.anchoredPosition = new Vector2(-490f, 280f);
                    lrt.sizeDelta = new Vector2(940f, 480f);
                }
                else
                {
                    // 키아트 구도: 로고는 좌상단, 메뉴는 좌하단 열 (오른쪽은 3D 과일들 자리)
                    MakeText(tp, "Title", "BEATCHEF", 130, new Vector2(-480f, 390f), new Color(1f, 0.85f, 0.2f));
                    MakeText(tp, "Subtitle", "SLICE TO THE BEAT!", 38, new Vector2(-540f, 300f), new Color(1f, 0.45f, 0.75f));
                }

                MenuButton(tp, "게임 시작", new Vector2(-620f, -110f), ShowSelect);
                MenuButton(tp, "튜토리얼", new Vector2(-620f, -225f), () => _ui.Show(TutorialPanel.PrefabName));
                MenuButton(tp, "설정", new Vector2(-620f, -340f), () => SettingsPanel.Toggle(root));
                if (!IsWeb)
                    MenuButton(tp, "게임 종료", new Vector2(-620f, -455f), Application.Quit);

                // 키아트의 그 HI-SCORE — 우하단, 전 곡 통합 최고 기록
                int hi = Gameplay.HighScores.GetGlobal();
                if (hi > 0)
                    MakeText(tp, "HiScore", $"HI-SCORE  {hi:N0}", 42, new Vector2(640f, -470f), new Color(1f, 0.85f, 0.2f));
            }

            // 곡 선택 페이지 뒤에 어두운 백드롭 — 타이틀 아트/디오라마가 은은하게 비친다
            var selBg = new GameObject("SelBg");
            selBg.transform.SetParent(sel, false);
            var selBgImg = selBg.AddComponent<Image>();
            selBgImg.color = new Color(0.02f, 0.01f, 0.05f, 0.97f); // 목업처럼 거의 검정
            var sbRt = selBgImg.rectTransform;
            sbRt.anchorMin = Vector2.zero;
            sbRt.anchorMax = Vector2.one;
            sbRt.offsetMin = sbRt.offsetMax = Vector2.zero;

            // (화면 전체 테두리는 뺐음 — 뚱뚱해서 노이즈만 됨)

            // ── 곡 선택 페이지 ──
            // 타이틀: 아틀라스 아트 우선
            var titleArt = Resources.Load<Sprite>("UI/select_title");
            if (titleArt != null)
                AnchorTo(MakeImage(sel, "SelectHeader", titleArt, Vector2.zero, new Vector2(660f, 112f)).rectTransform,
                    new Vector2(0.5f, 1f), new Vector2(0f, -75f));
            else
                AnchorTo(MakeText(sel, "SelectHeader", "♪ 곡 선택 ♪", 60, Vector2.zero, new Color(1f, 0.85f, 0.2f)).rectTransform,
                    new Vector2(0.5f, 1f), new Vector2(0f, -72f));

            // 뒤로 — 좌상단 코너 앵커 (센터 앵커 + 큰 오프셋은 좁은 화면에서 잘린다)
            var backArt = Resources.Load<Sprite>("UI/select_back");
            if (backArt != null)
            {
                var backBtn = MakeImageButton(sel, backArt, Vector2.zero, new Vector2(190f, 88f), ShowTitle);
                AnchorTo(backBtn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(130f, -80f));
            }
            else
            {
                var back = MakeButton(sel, "← 뒤로", Vector2.zero, ShowTitle);
                back.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 58f);
                AnchorTo(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(125f, -70f));
            }

            // 파일/새로고침/업로드는 최상단
            var folderArt = Resources.Load<Sprite>("UI/select_folder");
            var refreshArt = Resources.Load<Sprite>("UI/select_refresh");
            // 상단 묶음은 화면 위 기준 간격 고정 — 납작한 화면에서도 헤더와 안 겹친다
            if (IsWeb)
            {
                var up = MakeButton(sel, "내 곡 업로드 (mp3)", Vector2.zero, () => _uploader.Open());
                up.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 62f);
                AnchorTo(up.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-120f, -160f));
                var rf = refreshArt != null
                    ? MakeImageButton(sel, refreshArt, Vector2.zero, new Vector2(330f, 78f), RefreshList)
                    : MakeButton(sel, "새로고침", Vector2.zero, RefreshList);
                AnchorTo(rf.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(200f, -160f));
            }
            else if (folderArt != null && refreshArt != null)
            {
                AnchorTo(MakeImageButton(sel, folderArt, Vector2.zero, new Vector2(345f, 80f), () => Application.OpenURL("file://" + SongsDir))
                    .GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-185f, -160f));
                AnchorTo(MakeImageButton(sel, refreshArt, Vector2.zero, new Vector2(335f, 80f), RefreshList)
                    .GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(185f, -160f));
            }
            else
            {
                AnchorTo(MakeButton(sel, "폴더 열기", Vector2.zero, () => Application.OpenURL("file://" + SongsDir))
                    .GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-150f, -160f));
                AnchorTo(MakeButton(sel, "새로고침", Vector2.zero, RefreshList)
                    .GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(150f, -160f));
            }

            // 목록 탭: [기본 제공 곡 | 내 곡] — 아틀라스 아트면 틴트로 선택 표시
            _tabButtons = new Image[2];
            var tabArts = new[] { Resources.Load<Sprite>("UI/select_tab0"), Resources.Load<Sprite>("UI/select_tab1") };
            string[] tabLabels = { "기본 제공 곡", "내 곡" };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                if (tabArts[i] != null)
                {
                    var tb = MakeImageButton(sel, tabArts[i], Vector2.zero, new Vector2(310f, 76f), () => SelectTab(idx));
                    AnchorTo(tb.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-170f + i * 335f, -253f));
                    _tabButtons[i] = tb.GetComponent<Image>();
                }
                else
                {
                    var tb = MakeButton(sel, tabLabels[i], Vector2.zero, () => SelectTab(idx));
                    AnchorTo(tb.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-170f + i * 335f, -272f));
                    _tabButtons[i] = tb.GetComponent<Image>();
                }
            }
            _tabsUseArt = tabArts[0] != null;

            BuildScrollList(sel);

            // 곡 기록 버블 — 곡을 고르면 목록 오른쪽에 최고 기록 표시
            _recordBubble = new GameObject("RecordBubble");
            _recordBubble.transform.SetParent(sel, false);
            var rbImg = _recordBubble.AddComponent<Image>();
            rbImg.sprite = UISprites.PlainFrame;
            rbImg.type = Image.Type.Sliced;
            rbImg.color = new Color(0.55f, 0.4f, 0.9f, 0.6f);
            rbImg.raycastTarget = false;
            var rbRt = rbImg.rectTransform;
            rbRt.anchorMin = rbRt.anchorMax = new Vector2(0.5f, 0.5f);
            rbRt.anchoredPosition = new Vector2(585f, 60f);
            rbRt.sizeDelta = new Vector2(310f, 230f);
            _recordText = MakeText(_recordBubble.transform, "Rec", "", 27, Vector2.zero, Color.white);
            var rtRt = _recordText.rectTransform;
            rtRt.anchorMin = Vector2.zero;
            rtRt.anchorMax = Vector2.one;
            rtRt.sizeDelta = Vector2.zero;
            _recordBubble.SetActive(false);

            // 하단 묶음은 화면 아래 기준 간격 고정
            // 난이도
            AnchorTo(MakeText(sel, "DiffLabel", "난이도", 30, Vector2.zero, new Color(1f, 1f, 1f, 0.6f)).rectTransform,
                new Vector2(0.5f, 0f), new Vector2(-430f, 305f));
            _diffButtons = new Image[Difficulties.Length];
            for (int i = 0; i < Difficulties.Length; i++)
            {
                int idx = i;
                var b = MakeButton(sel, Difficulties[i].label,
                    Vector2.zero, () => { _difficulty = idx; UpdateSelectionVisuals(); });
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 58f);
                AnchorTo(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-220f + i * 180f, 305f));
                _diffButtons[i] = b.GetComponent<Image>();
            }

            // 모드
            AnchorTo(MakeText(sel, "ModeLabel", "모드", 30, Vector2.zero, new Color(1f, 1f, 1f, 0.6f)).rectTransform,
                new Vector2(0.5f, 0f), new Vector2(-430f, 240f));
            _modeButtons = new Image[Modes.Length];
            for (int i = 0; i < Modes.Length; i++)
            {
                int idx = i;
                var b = MakeButton(sel, Modes[i],
                    Vector2.zero, () => { _mode = idx; UpdateSelectionVisuals(); });
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 58f);
                AnchorTo(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-180f + i * 260f, 240f));
                _modeButtons[i] = b.GetComponent<Image>();
            }

            // 요리 — 재료 풀 선택 (기본: 셰프 마음대로). 버튼 폭은 글자 실측 + 여백으로 자동
            AnchorTo(MakeText(sel, "DishLabel", "요리", 30, Vector2.zero, new Color(1f, 1f, 1f, 0.6f)).rectTransform,
                new Vector2(0.5f, 0f), new Vector2(-430f, 175f));
            _dishButtons = new Image[SongSelection.Dishes.Length];
            var dishRects = new RectTransform[SongSelection.Dishes.Length];
            const float dishGap = 12f;
            float dishRowW = 0f;
            for (int i = 0; i < SongSelection.Dishes.Length; i++)
            {
                int idx = i;
                var b = MakeButton(sel, SongSelection.Dishes[i].label,
                    Vector2.zero, () => { _dish = idx; UpdateSelectionVisuals(); });
                var rt = b.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(b.GetComponentInChildren<Text>().preferredWidth + 36f, 58f);
                dishRects[i] = rt;
                dishRowW += rt.sizeDelta.x + (i > 0 ? dishGap : 0f);
                _dishButtons[i] = b.GetComponent<Image>();
            }
            // 라벨(-430) 오른쪽에서 시작하도록 살짝 우측 정렬
            float dishX = -dishRowW * 0.5f + 30f;
            for (int i = 0; i < dishRects.Length; i++)
            {
                AnchorTo(dishRects[i], new Vector2(0.5f, 0f), new Vector2(dishX + dishRects[i].sizeDelta.x * 0.5f, 175f));
                dishX += dishRects[i].sizeDelta.x + dishGap;
            }

            // 시작 버튼 — 곡+난이도+모드 다 골라야 활성화
            var start = MakeButton(sel, "게임 시작", Vector2.zero, () => { if (!_loading) StartCoroutine(StartGame()); });
            AnchorTo(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 95f));
            _startButton = start;
            _startImage = start.GetComponent<Image>();
            _startLabel = start.GetComponentInChildren<Text>();
            start.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 78f);
            if (UISprites.ButtonFrame != null)
            {
                _startImage.sprite = UISprites.ButtonFrame;
                _startImage.type = Image.Type.Simple;
            }

            _status = MakeText(sel, "Status", "", 32, Vector2.zero, new Color(0.3f, 0.9f, 1f));
            AnchorTo(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f)); // 하단 변 앵커 — 시작 버튼(+95) 아래
            UpdateSelectionVisuals();
            ShowTitle(); // 시작은 타이틀 페이지
        }

        // ── 키아트 타이틀 모드 ──────────────────────────────────
        bool _artTitle;
        int _titleSel;
        readonly List<(RectTransform rt, Action act)> _titleItems = new List<(RectTransform, Action)>();
        Text _titleArrow;
        Text _titleToast;

        /// <summary>키아트를 풀스크린으로 깔고, 그림에 구워진 메뉴 리스트 위에 핫스팟을 얹는다.
        /// 방향키/W·S로 고르고 Enter로 실행 — 레트로 타이틀 문법.</summary>
        void BuildArtTitle(Transform tp, Sprite art)
        {
            _artTitle = true;

            // 레터박스 배경
            var bgGo = new GameObject("ArtLetterbox");
            bgGo.transform.SetParent(tp, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = Color.black;
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            // 아트 (비율 유지 풀스크린 — 좌표 매핑을 위해 크롭 대신 레터박스)
            var artGo = new GameObject("TitleArt");
            artGo.transform.SetParent(tp, false);
            var img = artGo.AddComponent<Image>();
            img.sprite = art;
            img.raycastTarget = false;
            var aRt = img.rectTransform;
            aRt.anchorMin = Vector2.zero;
            aRt.anchorMax = Vector2.one;
            aRt.offsetMin = aRt.offsetMax = Vector2.zero;
            var arf = artGo.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = art.rect.width / art.rect.height;

            // 아트에 구워진 메뉴 리스트 위 핫스팟 (이미지 정규화 좌표 — 아트 바뀌면 y만 손보면 됨)
            (string label, float y, Action act)[] items =
            {
                ("START", 0.268f, ShowSelect),
                ("MODE", 0.228f, ShowSelect),
                ("RANKING", 0.188f, () => Toast("랭킹은 준비 중!")),
                ("OPTIONS", 0.148f, () => SettingsPanel.Toggle(_canvasRoot)),
                ("EXIT", 0.108f, () => { if (IsWeb) Toast("웹에선 탭을 닫아주세요!"); else Application.Quit(); }),
            };
            _titleItems.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                var act = items[i].act;
                var go = new GameObject("Hot_" + items[i].label);
                go.transform.SetParent(artGo.transform, false);
                var hot = go.AddComponent<Image>();
                hot.color = new Color(1f, 1f, 1f, 0f); // 안 보이지만 클릭은 받는다
                var rt = hot.rectTransform;
                rt.anchorMin = new Vector2(0.02f, items[i].y - 0.034f);
                rt.anchorMax = new Vector2(0.27f, items[i].y + 0.034f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                go.AddComponent<Button>().onClick.AddListener(() => { _titleSel = idx; UpdateTitleArrow(); act(); });

                var trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter,
                };
                entry.callback.AddListener(_ => { _titleSel = idx; UpdateTitleArrow(); });
                trigger.triggers.Add(entry);

                _titleItems.Add((rt, act));
            }

            // 우리가 움직이는 선택 화살표 (아트의 고정 화살표와 별개, 더 밝음)
            _titleArrow = MakeText(artGo.transform, "Sel", ">", 40, Vector2.zero, new Color(1f, 0.9f, 0.25f));
            _titleArrow.fontStyle = FontStyle.Bold;
            _titleToast = MakeText(tp, "Toast", "", 34, Vector2.zero, new Color(1f, 0.85f, 0.2f));
            AnchorTo(_titleToast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 66f)); // 하단 변 앵커
            UpdateTitleArrow();
        }

        void UpdateTitleArrow()
        {
            if (_titleArrow == null || _titleItems.Count == 0) return;
            var target = _titleItems[Mathf.Clamp(_titleSel, 0, _titleItems.Count - 1)].rt;
            var rt = _titleArrow.rectTransform;
            rt.anchorMin = new Vector2(0.0f, target.anchorMin.y);
            rt.anchorMax = new Vector2(0.045f, target.anchorMax.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Toast(string msg)
        {
            if (_titleToast == null) return;
            _titleToast.text = msg;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 1.6f);
        }

        void ClearToast()
        {
            if (_titleToast != null) _titleToast.text = "";
        }

        /// <summary>가장자리 UI용: 코너/변 앵커 + 앵커 기준 오프셋 — 어떤 해상도·비율에서도 안 잘린다.</summary>
        static void AnchorTo(RectTransform rt, Vector2 anchor, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = offset;
        }

        static GameObject MakePage(Transform root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>타이틀 페이지용 대형 버튼 — 통일된 단색 프레임, 밝은 톤.</summary>
        Button MenuButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var b = MakeButton(parent, label, pos, onClick);
            b.GetComponent<RectTransform>().sizeDelta = new Vector2(410f, 96f);
            b.GetComponent<Image>().color = new Color(0.85f, 0.8f, 1f, 0.8f);
            var t = b.GetComponentInChildren<Text>();
            t.fontSize = 44;
            return b;
        }

        void ShowSelect()
        {
            _titlePage.SetActive(false);
            _selectPage.SetActive(true);
            // 첫 플레이면 노트 가이드를 한 번 띄운다 (닫으면 본 것으로 기록)
            if (!TutorialPanel.Seen) _ui.Show(TutorialPanel.PrefabName);
        }

        void ShowTitle()
        {
            StopPreview(); // 타이틀로 돌아가면 미리듣기 끄고 로비 BGM 복귀
            _selectPage.SetActive(false);
            _titlePage.SetActive(true);
        }

        void BuildScrollList(Transform root)
        {
            // 리스트 무지개 프레임 — 아틀라스 라운드 프레임 우선
            var frameArt = Resources.Load<Sprite>("UI/select_frame");
            var frameSprite = frameArt != null ? frameArt : UISprites.ButtonFrame;
            if (frameSprite != null)
            {
                var lf = new GameObject("ListFrame");
                lf.transform.SetParent(root, false);
                var lfImg = lf.AddComponent<Image>();
                lfImg.sprite = frameSprite;
                lfImg.type = Image.Type.Simple;
                lfImg.raycastTarget = false;
                var lfRt = lfImg.rectTransform;
                // 세로 stretch: 위/아래 인셋 고정(기준 1080에서 315/327) — 납작한 화면에선 리스트가 줄어들며 겹침 방지
                lfRt.anchorMin = new Vector2(0.5f, 0f);
                lfRt.anchorMax = new Vector2(0.5f, 1f);
                lfRt.anchoredPosition = new Vector2(0f, 6f);
                lfRt.sizeDelta = new Vector2(824f, -642f);
            }

            var scrollGo = new GameObject("SongScroll");
            scrollGo.transform.SetParent(root, false);
            var bg = scrollGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.03f, 0.09f, 0.85f);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, 6f);
            srt.sizeDelta = new Vector2(752f, -708f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = viewportGo.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.sizeDelta = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            _listContent = contentGo.AddComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.sizeDelta = new Vector2(0f, 0f);
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vrt;
            scroll.content = _listContent;
        }

        void RefreshList()
        {
            foreach (Transform child in _listContent) Destroy(child.gameObject);
            _songButtons.Clear();
            int count = 0;

            var builtinNames = new HashSet<string>();
            var builtinClips = Resources.LoadAll<AudioClip>("Songs");
            foreach (var c in builtinClips) builtinNames.Add(c.name);

            if (_listTab == 0)
            {
                // ── 탭: 기본 제공 곡 (Resources/Songs) ──
                foreach (var clip in builtinClips)
                {
                    var c = clip;
                    string key = "builtin:" + c.name;
                    var btn = MakeListButton(c.name, key);
                    btn.onClick.AddListener(() =>
                    {
                        _builtinClip = c;
                        SelectSource(0, c.name, key);
                    });
                    count++;
                }
            }
            else
            {
                // ── 탭: 내 곡 (업로드 / 로컬 파일) ──
                // 저장에 실패한 업로드(시크릿 모드 등)는 이번 세션 동안만 표시
                if (_upClip != null && Array.IndexOf(_savedSongs, _upClip.name) < 0)
                {
                    var c = _upClip;
                    string key = "upload:" + c.name;
                    var btn = MakeListButton($"{c.name}  (업로드)", key);
                    btn.onClick.AddListener(() => SelectSource(2, c.name, key));
                    count++;
                }
                // 브라우저에 저장된 곡 — 누르면 그때 디코딩 (이미 불러온 곡이면 바로 선택)
                foreach (var saved in _savedSongs)
                {
                    var n = saved;
                    string key = "upload:" + n;
                    var btn = MakeListButton(n, key);
                    btn.onClick.AddListener(() =>
                    {
                        if (_upClip != null && _upClip.name == n) SelectSource(2, n, key);
                        else _uploader.LoadSaved(n);
                    });
                    AddDeleteButton(btn.transform, n);
                    count++;
                }
                if (!IsWeb)
                {
                    string[] exts = { ".mp3", ".wav", ".ogg" };
                    var myFiles = Directory.GetFiles(SongsDir)
                        .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .Where(f => !builtinNames.Contains(Path.GetFileNameWithoutExtension(f)))
                        .OrderBy(Path.GetFileName)
                        .ToArray();
                    foreach (var file in myFiles)
                    {
                        var path = file;
                        var label = Path.GetFileNameWithoutExtension(path);
                        string key = "file:" + path;
                        var btn = MakeListButton(label, key);
                        btn.onClick.AddListener(() =>
                        {
                            _filePath = path;
                            SelectSource(1, label, key);
                        });
                        count++;
                    }
                }
            }

            if (count == 0)
            {
                _status.text = _listTab == 1
                    ? (IsWeb ? "위의 업로드 버튼으로 곡을 올리면 여기 떠요" : $"Songs 폴더가 비어있어요: {SongsDir}")
                    : "내장 곡이 없어요";
            }
            UpdateSelectionVisuals();
        }

        /// <summary>저장된 곡 행 오른쪽 삭제 버튼 — 브라우저 저장소에서 지운다.</summary>
        void AddDeleteButton(Transform row, string songName)
        {
            var del = MakeButton(row, "삭제", Vector2.zero, () =>
            {
                _uploader.DeleteSaved(songName);
                _savedSongs = _savedSongs.Where(s => s != songName).ToArray();
                if (_sourceType == 2 && _selectedName == songName)
                {
                    StopPreview();
                    _sourceType = -1;
                    _selectedName = _selectedKey = null;
                }
                if (_upClip != null && _upClip.name == songName)
                {
                    _upClip = null;
                    _upSamples = null;
                }
                RefreshList();
            });
            var rt = del.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110f, 50f);
            AnchorTo(rt, new Vector2(1f, 0.5f), new Vector2(-70f, 0f));
        }

        void SelectTab(int tab)
        {
            _listTab = tab;
            _status.text = "";
            RefreshList();
        }

        /// <summary>선택한 곡의 최고 기록 버블. 난이도를 골랐으면 그 난이도, 아니면 전 난이도 최고.</summary>
        void UpdateRecordBubble()
        {
            if (_recordBubble == null) return;
            bool show = _sourceType >= 0 && !string.IsNullOrEmpty(_selectedName);
            if (show)
            {
                int score, combo;
                if (_difficulty >= 0)
                {
                    score = Gameplay.HighScores.GetSong(_selectedName, _difficulty);
                    combo = Gameplay.HighScores.GetCombo(_selectedName, _difficulty);
                }
                else
                {
                    (score, combo) = Gameplay.HighScores.GetSongBest(_selectedName);
                }

                _recordText.text = score > 0
                    ? $"<color=#FFD84D>HIGH SCORE</color>\n{score:N0}\n\n<color=#FFD84D>MAX COMBO</color>\n{combo}"
                    : "아직 기록 없음\n\n첫 요리에\n도전해보세요!";
            }
            _recordBubble.SetActive(show);
        }

        void SelectSource(int type, string name, string key)
        {
            _sourceType = type;
            _selectedName = name;
            _selectedKey = key;
            UpdateSelectionVisuals();

            // 곡을 고르면 바로 미리듣기
            switch (type)
            {
                case 0: PlayPreview(_builtinClip); break;
                case 2: PlayPreview(_upClip); break;
                case 1: StartCoroutine(PreviewFile(_filePath)); break;
            }
        }

        string _selectedKey;

        void UpdateSelectionVisuals()
        {
            foreach (var (img, key) in _songButtons)
                img.color = key == _selectedKey
                    ? new Color(1f, 0.72f, 0.25f, 0.45f)          // 선택 = 주황 점화
                    : new Color(0.5f, 0.35f, 0.85f, 0.14f);       // 평소 = 보랏빛 은은 (목업 톤)

            // 단색 프레임 + 틴트: 선택=주황 점화, 비선택=보랏빛 흐림
            var selCol = new Color(1f, 0.7f, 0.22f, 1f);
            var unselCol = new Color(0.6f, 0.55f, 0.8f, 0.55f);

            if (_diffButtons != null)
                for (int i = 0; i < _diffButtons.Length; i++)
                    _diffButtons[i].color = i == _difficulty ? selCol : unselCol;

            if (_modeButtons != null)
                for (int i = 0; i < _modeButtons.Length; i++)
                    _modeButtons[i].color = i == _mode ? selCol : unselCol;

            if (_dishButtons != null)
                for (int i = 0; i < _dishButtons.Length; i++)
                    _dishButtons[i].color = i == _dish ? selCol : unselCol;

            if (_tabButtons != null)
                for (int i = 0; i < _tabButtons.Length; i++)
                    _tabButtons[i].color = _tabsUseArt
                        ? (i == _listTab ? Color.white : new Color(0.45f, 0.45f, 0.52f, 0.85f))
                        : (i == _listTab ? selCol : unselCol);

            UpdateRecordBubble();

            bool ready = _sourceType >= 0 && _difficulty >= 0 && _mode >= 0;
            if (_startButton != null)
            {
                _startButton.interactable = ready;
                var bannerArt = Resources.Load<Sprite>("UI/select_banner");

                if (bannerArt != null && !ready)
                {
                    // 대기 상태: 아틀라스 배너 ("곡·난이도·모드를 선택하세요" 구워진 아트)
                    _startImage.sprite = bannerArt;
                    _startImage.preserveAspect = true;
                    _startImage.color = new Color(1f, 1f, 1f, 0.9f);
                    _startLabel.text = "";
                    _startImage.rectTransform.sizeDelta = new Vector2(860f, 102f);
                }
                else
                {
                    _startImage.sprite = UISprites.PlainFrame;
                    _startImage.preserveAspect = false;
                    _startImage.type = Image.Type.Sliced;
                    _startImage.color = ready
                        ? new Color(1f, 0.7f, 0.22f, 1f)
                        : new Color(0.6f, 0.55f, 0.8f, 0.45f);
                    _startLabel.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                    _startLabel.text = ready ? $"게임 시작 — {_selectedName}" : "곡 · 난이도 · 모드를 고르세요";
                    // 곡명 길이에 맞춰 버튼 폭 갱신
                    _startImage.rectTransform.sizeDelta =
                        new Vector2(_startLabel.preferredWidth + 120f, _startLabel.preferredHeight + 28f);
                }
            }
        }

        IEnumerator StartGame()
        {
            _loading = true;
            StopPreview();
            var d = Difficulties[_difficulty];
            SongSelection.PromptMode = _mode;
            SongSelection.Dish = _dish;
            SongSelection.Difficulty = _difficulty;

            AudioClip clip = null;
            Beatmap map = null;

            if (_sourceType == 0) // 내장
            {
                clip = _builtinClip;
                var baked = Resources.Load<TextAsset>($"SongMaps/{clip.name}__{_difficulty}");
                if (baked != null)
                {
                    map = Beatmap.FromJson(baked.text);
                }
                else if (IsWeb)
                {
                    _status.text = "이 곡의 비트맵이 빌드에 없어요 (에디터에서 Bake 후 재빌드)";
                    _loading = false;
                    yield break;
                }
                else
                {
                    _status.text = $"비트 분석 중... ({d.label})";
                    yield return null;
                    map = OnsetDetector.Detect(clip, d.energy, d.density, d.lanes);
                }
            }
            else if (_sourceType == 1) // 로컬 파일
            {
                _status.text = "곡 불러오는 중...";
                var type = Path.GetExtension(_filePath).ToLowerInvariant() switch
                {
                    ".wav" => AudioType.WAV,
                    ".ogg" => AudioType.OGGVORBIS,
                    _ => AudioType.MPEG,
                };
                using var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(_filePath).AbsoluteUri, type);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    _status.text = "로드 실패: " + req.error;
                    _loading = false;
                    yield break;
                }
                clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = Path.GetFileNameWithoutExtension(_filePath);

                _status.text = $"비트 분석 중... ({d.label})";
                yield return null;
                map = OnsetDetector.Detect(clip, d.energy, d.density, d.lanes);
            }
            else // 웹 업로드
            {
                clip = _upClip;
                _status.text = $"비트 분석 중... ({d.label})";
                yield return null;
                map = OnsetDetector.Detect(_upSamples, _upRate, clip.name, d.energy, d.density, d.lanes);
            }

            _status.text = $"BPM {map.bpm:F0}, 노트 {map.events.Count}개 — 요리 시작!";
            SongSelection.Clip = clip;
            SongSelection.Map = map;
            SceneManager.LoadScene(gameplayScene);
        }

        // ── UI 헬퍼 ──────────────────────────────────────────────
        static Text MakeText(Transform parent, string name, string content, int size, Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UIFont.Get();
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            return t;
        }

        void MakeSectionHeader(string label)
        {
            var go = new GameObject("Header_" + label);
            go.transform.SetParent(_listContent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 46f;
            var t = MakeText(go.transform, "L", label, 26, Vector2.zero, new Color(1f, 0.85f, 0.2f, 0.75f));
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            t.alignment = TextAnchor.MiddleLeft;
        }

        Button MakeListButton(string label, string key)
        {
            var go = new GameObject("Song_" + label);
            go.transform.SetParent(_listContent, false);
            var img = go.AddComponent<Image>();
            img.sprite = UISprites.PlainFrame;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.5f, 0.35f, 0.85f, 0.14f);
            var btn = go.AddComponent<Button>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 66f;

            var t = MakeText(go.transform, "Label", "♪  " + label, 34, Vector2.zero, Color.white);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(26f, 0f);
            trt.offsetMax = new Vector2(-26f, 0f);
            t.alignment = TextAnchor.MiddleLeft;

            _songButtons.Add((img, key));
            return btn;
        }

        static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return img;
        }

        static Button MakeImageButton(Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Action onClick)
        {
            var img = MakeImage(parent, "ImgBtn_" + sprite.name, sprite, pos, size);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        Button MakeButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            // 단색 얇은 테두리 프레임 — 선택/비선택은 틴트로 구분 (무지개는 과해서 은퇴)
            img.sprite = UISprites.PlainFrame;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.6f, 0.55f, 0.8f, 0.55f);
            var btn = go.AddComponent<Button>();
            // 호버 시 살짝 밝아지는 공용 반응 (틴트는 CanvasRenderer 곱이라 선택 색과 공존)
            var cb = btn.colors;
            cb.normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            btn.onClick.AddListener(() => onClick());

            var t = MakeText(go.transform, "Label", label, 32, Vector2.zero, Color.white);
            // 글자 실측 크기 + 여백으로 버튼 크기 지정 — 텍스트 삐져나옴 방지
            rt.sizeDelta = new Vector2(t.preferredWidth + 120f, t.preferredHeight + 28f);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            return btn;
        }
    }
}
