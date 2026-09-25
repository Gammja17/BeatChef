using UnityEngine;
using UnityEngine.UI;

namespace BeatSlash.UI
{
    /// <summary>
    /// 노트 가이드 튜토리얼. 프리팹은 Resources/UIPrefabs/TutorialPanel
    /// (메뉴 BeatSlash > Build Tutorial Prefab으로 생성). 페이지 넘김 + 닫기.
    /// 한 번 닫으면 본 것으로 기록 → 첫 플레이 자동 표시를 끈다.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        public const string PrefabName = "TutorialPanel";
        const string SeenKey = "tutorial_seen";

        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) == 1;

        public GameObject[] pages;
        public Button prevButton;
        public Button nextButton;
        public Button closeButton;
        public Text pageLabel;

        int _page;

        void Awake()
        {
            prevButton.onClick.AddListener(() => Go(_page - 1));
            nextButton.onClick.AddListener(() => Go(_page + 1));
            closeButton.onClick.AddListener(Close);
        }

        void OnEnable() => Go(0);

        // LateUpdate: 메뉴의 Update(ESC=뒤로/설정)보다 늦게 처리해서 같은 프레임 ESC가 겹치지 않게
        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Go(_page + 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Go(_page - 1);
        }

        void Go(int page)
        {
            _page = Mathf.Clamp(page, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == _page);
            prevButton.interactable = _page > 0;
            nextButton.gameObject.SetActive(_page < pages.Length - 1);
            pageLabel.text = $"{_page + 1} / {pages.Length}";
        }

        void Close()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }
    }
}
