using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeatSlash.Rhythm
{
    /// <summary>곡 하나의 노트/이벤트 데이터. JSON으로 저장/로드 (온셋 생성기가 출력).</summary>
    [Serializable]
    public class Beatmap
    {
        public string songName;
        public float bpm;
        public float firstBeatOffset;
        public List<BeatEvent> events = new List<BeatEvent>();

        public static Beatmap FromJson(string json) => JsonUtility.FromJson<Beatmap>(json);
        public string ToJson() => JsonUtility.ToJson(this, true);
    }

    [Serializable]
    public class BeatEvent
    {
        [Tooltip("곡 시작 기준 시간(초)")]
        public float time;
        [Tooltip("이벤트 종류: 0=일반 적, 1=강적, 2=연타 등 — 게임 규칙에서 정의")]
        public int type;
        [Tooltip("방향/레인 (좌우 베기 등)")]
        public int lane;
        [Tooltip("온셋 검출 강도 — 강한 비트일수록 큼. 적 배치 가중치로 활용")]
        public float strength;
    }
}
