using System;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSlash.UI
{
    /// <summary>
    /// 튜토리얼 안내 카드 (프리팹: Resources/UIPrefabs/TutorialCard — BeatSlash > Build Tutorial Card Prefab).
    /// 인게임 단계 안내와 메뉴의 튜토리얼 권유에 같이 쓴다. Enter/Space = 주 버튼.
    /// </summary>
    public class TutorialCard : MonoBehaviour
    {
        public const string PrefabName = "TutorialCard";

        public Text feedbackText;
        public Text titleText;
        public Text bodyText;
        public Button primaryButton;
        public Text primaryLabel;
        public Button secondaryButton;
        public Text secondaryLabel;

        Action _onPrimary;

        void Awake() => primaryButton.onClick.AddListener(Fire);

        /// <summary>secondary가 null이면 보조 버튼 숨김.</summary>
        public void Show(string feedback, string title, string body, string primary, Action onPrimary,
            string secondary = null, Action onSecondary = null)
        {
            feedbackText.text = feedback;
            titleText.text = title;
            bodyText.text = body;
            primaryLabel.text = primary;
            _onPrimary = onPrimary;
            secondaryButton.onClick.RemoveAllListeners();
            secondaryButton.gameObject.SetActive(secondary != null);
            if (secondary != null)
            {
                secondaryLabel.text = secondary;
                secondaryButton.onClick.AddListener(() => onSecondary?.Invoke());
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Fire();
        }

        // 클릭과 키가 같은 프레임에 겹쳐도 한 번만 실행
        void Fire()
        {
            var a = _onPrimary;
            _onPrimary = null;
            a?.Invoke();
        }
    }
}
