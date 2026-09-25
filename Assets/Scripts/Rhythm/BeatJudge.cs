using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatSlash.Rhythm
{
    public enum JudgeResult { Perfect, Good, Miss, None }

    /// <summary>
    /// 판정 코어. 비트맵 이벤트 큐를 들고 있다가 입력 시점(JudgePosition)과 비교한다.
    /// 액션형이므로 "가장 가까운 미처리 이벤트"에 대해 판정하는 방식.
    /// </summary>
    public class BeatJudge : MonoBehaviour
    {
        public static BeatJudge Instance { get; private set; }

        [Header("판정 윈도우(초) — ±기준")]
        public float perfectWindow = 0.09f;
        public float goodWindow = 0.2f;
        [Tooltip("이 시간만큼 지나가면 자동 Miss 처리")]
        public float missAfter = 0.26f;

        /// <summary>(result, event, deltaSec) — delta는 +면 늦은 입력.</summary>
        public event Action<JudgeResult, BeatEvent, float> OnJudged;

        readonly Queue<BeatEvent> _pending = new Queue<BeatEvent>();

        void Awake() => Instance = this;

        public void Load(Beatmap map)
        {
            _pending.Clear();
            foreach (var e in map.events) _pending.Enqueue(e);
        }

        /// <summary>다음 판정 대상 이벤트 (스폰/연출 미리보기용).</summary>
        public BeatEvent PeekNext() => _pending.Count > 0 ? _pending.Peek() : null;

        /// <summary>앞에서 index번째 이벤트 (0=다음). HUD 대기열 미리보기용.</summary>
        public BeatEvent PeekAt(int index)
        {
            if (index >= _pending.Count) return null;
            int i = 0;
            foreach (var e in _pending)
                if (i++ == index) return e;
            return null;
        }

        /// <summary>플레이어 입력 시 호출. 큐 맨 앞 이벤트와 시간차로 판정.
        /// lane >= 0이면 방향까지 맞아야 함 — 틀린 방향은 헛스윙 (이벤트 소모 안 함).</summary>
        public JudgeResult TryHit(int lane = -1)
        {
            if (_pending.Count == 0) return JudgeResult.None;

            var e = _pending.Peek();
            float delta = (float)(Conductor.Instance.JudgePosition - e.time);

            // 아직 너무 이른 입력은 헛스윙 취급 (이벤트 소모 안 함)
            if (delta < -goodWindow) return JudgeResult.None;

            // 방향 불일치도 헛스윙 — 이벤트는 남겨서 올바른 방향 재입력 기회를 줌
            if (lane >= 0 && e.lane != lane) return JudgeResult.None;

            _pending.Dequeue();
            var result = Mathf.Abs(delta) <= perfectWindow ? JudgeResult.Perfect
                       : Mathf.Abs(delta) <= goodWindow ? JudgeResult.Good
                       : JudgeResult.Miss;
            OnJudged?.Invoke(result, e, delta);
            return result;
        }

        void Update()
        {
            if (Conductor.Instance == null || !Conductor.Instance.IsPlaying) return;

            // 입력 없이 지나간 이벤트 자동 Miss
            while (_pending.Count > 0 &&
                   Conductor.Instance.JudgePosition - _pending.Peek().time > missAfter)
            {
                var e = _pending.Dequeue();
                OnJudged?.Invoke(JudgeResult.Miss, e, float.MaxValue);
            }
        }
    }
}
