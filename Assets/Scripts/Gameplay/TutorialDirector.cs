using System.Collections.Generic;
using BeatSlash.Rhythm;
using BeatSlash.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 인게임 튜토리얼 진행. 단계 시작 위치에서 게임을 멈추고(일시정지와 같은 방식)
    /// 안내 카드를 띄운다. 직전 단계 성공 수로 칭찬/팁을 함께 보여준다.
    /// GameFlow가 SongSelection.Tutorial일 때 붙인다.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        UIManager _ui;
        PauseMenu _pauseMenu;
        int _step;     // 다음에 보여줄 단계 (Steps.Count면 완료 카드)
        bool _carding; // 카드 표시 중
        readonly Dictionary<BeatEvent, int> _stepOf = new Dictionary<BeatEvent, int>();
        int[] _ok;     // 단계별 성공 수

        void Start()
        {
            var steps = TutorialScript.Steps;
            _ok = new int[steps.Count];
            for (int i = 0; i < steps.Count; i++)
                foreach (var e in steps[i].notes) _stepOf[e] = i;
            _pauseMenu = GetComponent<PauseMenu>();
            BeatJudge.Instance.OnJudged += HandleJudged;

            // 카드 전용 캔버스 — 카운트다운(100) 아래, HUD 위
            var canvasGo = new GameObject("TutorialCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _ui = canvasGo.AddComponent<UIManager>();
            _ui.root = canvasGo.transform;
        }

        void OnDestroy()
        {
            if (BeatJudge.Instance != null) BeatJudge.Instance.OnJudged -= HandleJudged;
        }

        void HandleJudged(JudgeResult result, BeatEvent e, float delta)
        {
            if (result != JudgeResult.Miss && _stepOf.TryGetValue(e, out int s)) _ok[s]++;
        }

        void Update()
        {
            var c = Conductor.Instance;
            if (_carding || c == null || !c.IsPlaying || c.IsPaused) return;
            var steps = TutorialScript.Steps;
            if (_step < steps.Count && c.SongPosition >= steps[_step].pauseAt) ShowStep();
            else if (_step == steps.Count && c.SongPosition >= TutorialScript.EndAt) ShowEnd();
        }

        void ShowStep()
        {
            var s = TutorialScript.Steps[_step];
            OpenCard(Feedback(_step - 1), s.title, s.body, "시작하기", () =>
            {
                _step++;
                CloseCard();
            });
        }

        void ShowEnd()
        {
            TutorialScript.Seen = true;
            OpenCard(Feedback(_step - 1), "튜토리얼 완료!",
                "실전에선 20콤보를 넘기면 <color=#FFD84D>피버 타임</color> — 점수 배율이 올라가요.\n" +
                "ESC 일시정지 · F1 / F2 판정 타이밍 보정\n\n이제 진짜 요리하러 가볼까요?",
                "메뉴로", () =>
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("MainMenu");
                });
        }

        /// <summary>직전 단계 결과 한 줄 — 잘했으면 칭찬, 아니면 팁.</summary>
        string Feedback(int step)
        {
            if (step < 0) return "";
            int total = TutorialScript.Steps[step].notes.Count;
            int ok = _ok[step];
            if (ok * 4 >= total * 3) return $"<color=#7CFF7C>좋아요! {total}개 중 {ok}개 성공</color>";
            if (ok * 5 >= total * 2) return $"<color=#FFD84D>괜찮아요! {total}개 중 {ok}개 성공 — 링이 흰 원에 닿는 순간을 노려보세요</color>";
            return $"<color=#FF9A9A>{total}개 중 {ok}개 성공 — 너무 일찍 누르지 말고, 링이 흰 원에 닿을 때 누르세요</color>";
        }

        void OpenCard(string feedback, string title, string body, string button, System.Action onContinue)
        {
            _carding = true;
            // 일시정지와 같은 방식: 시간 정지 + 곡 위치 동결. ESC 일시정지 메뉴는 카드 동안 끈다
            Time.timeScale = 0f;
            Conductor.Instance.Pause();
            if (_pauseMenu != null) _pauseMenu.enabled = false;
            _ui.Show(TutorialCard.PrefabName).GetComponent<TutorialCard>()
                .Show(feedback, title, body, button, onContinue);
        }

        void CloseCard()
        {
            _ui.Hide(TutorialCard.PrefabName);
            Time.timeScale = 1f;
            Conductor.Instance.Resume();
            if (_pauseMenu != null) _pauseMenu.enabled = true;
            _carding = false;
        }
    }
}
