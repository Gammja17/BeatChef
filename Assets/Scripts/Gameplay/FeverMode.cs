using BeatSlash.Juice;
using BeatSlash.Rhythm;
using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 콤보가 쌓이면 발동하는 피버 — 요리사의 내적 댄스타임.
    /// 카메라가 빠르고 무작위하게 돌고, 점수 배율이 올라간다. Miss로 해제.
    /// </summary>
    public class FeverMode : MonoBehaviour
    {
        public static FeverMode Instance { get; private set; }

        [Tooltip("피버 발동에 필요한 콤보")]
        public int feverCombo = 20;
        [Tooltip("피버 중 점수 배율")]
        public float scoreMultiplier = 2f;

        public bool IsFever { get; private set; }
        public float Multiplier => IsFever ? scoreMultiplier : 1f;

        CameraOrbit _orbit;

        void Awake() => Instance = this;

        void Update()
        {
            // 화면에 보이는 콤보(ScoreTracker)를 그대로 읽는다 —
            // 별도 이벤트 구독으로 콤보를 따로 세면 구독 타이밍에 따라 조용히 어긋날 수 있다
            bool fever = ScoreTracker.CurrentCombo >= feverCombo;
            if (fever != IsFever)
            {
                IsFever = fever;
                if (_orbit == null) _orbit = FindAnyObjectByType<CameraOrbit>();
                if (_orbit != null) _orbit.Fever = fever;
                if (fever) Juice.CameraPunch.Instance?.Punch(0.5f); // 발동 순간 임팩트
            }

            // 피버 무지개 배경 — 음악에 빠진 황홀경. 꺼지면 검정으로 복귀
            var cam = Camera.main;
            if (cam != null)
            {
                var target = IsFever
                    ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.35f, 1f), 0.75f, 0.4f)
                    : Color.black;
                cam.backgroundColor = Color.Lerp(cam.backgroundColor, target, Time.deltaTime * 3f);
            }
        }
    }
}
