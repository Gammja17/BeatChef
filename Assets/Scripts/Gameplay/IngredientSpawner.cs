using System.Collections.Generic;
using BeatSlash.Rhythm;
using UnityEngine;

namespace BeatSlash.Gameplay
{
    /// <summary>
    /// 재료 투척 스포너 — 셰프 중심 4방향 공간 액션 구조.
    /// lane 방향(화면 기준 좌/우/상/하)에서 날아와 셰프 옆 그 방향 존에 비트 시각 정각 도달.
    /// "왼쪽에서 온 건 ←로 벤다" — 프롬프트 없이 화면만 봐도 읽힌다.
    /// 강한 비트(strength)는 대형 연타 재료가 된다.
    /// </summary>
    public class IngredientSpawner : MonoBehaviour
    {
        [Tooltip("셰프 위치(중심). 썰기 존은 이 주위 4방향")]
        public Transform sliceZone;
        [Tooltip("재료 프리팹 풀 — 랜덤 선택")]
        public GameObject[] ingredientPrefabs;
        [Tooltip("투척 후 존까지 비행 시간(초)")]
        public float flightTime = 1.2f;
        [Tooltip("중심에서 각 방향 존까지 거리")]
        public float zoneRadius = 1.8f;
        [Tooltip("존에서 스폰 지점까지 거리 (그 방향 연장선)")]
        public float spawnDistance = 9f;

        [Header("대형 연타 재료")]
        [Tooltip("이 강도 이상의 비트만 연타 재료 후보")]
        public float bigStrength = 3f;
        [Tooltip("대형 재료 최소 간격(초) — 남발 방지")]
        public float bigCooldown = 12f;
        [Tooltip("대형 전후 이 시간 안의 일반 노트는 제거 (연타에 집중)")]
        public float bigClearWindow = 1.3f;
        [Tooltip("연타 필요 타격 수")]
        public int bigHits = 5;

        [Header("홀드 재료 (type=3)")]
        [Tooltip("이 강도 이상의 비트가 홀드 후보")]
        public float holdStrength = 1.7f;
        [Tooltip("홀드 최소 간격(초)")]
        public float holdCooldown = 18f;
        [Tooltip("누르고 있는 길이(박)")]
        public float holdBeats = 2f;

        [Header("슬로우 컷 재료 (type=2)")]
        [Tooltip("이 강도 이상의 비트가 슬로우 컷 후보")]
        public float slowStrength = 2.2f;
        [Tooltip("슬로우 컷 최소 간격(초)")]
        public float slowCooldown = 25f;
        [Tooltip("슬로우 전후 이 시간 안의 일반 노트는 제거")]
        public float slowClearWindow = 1.5f;

        /// <summary>대형 이벤트 선정(type=1) + 주변 일반 노트 정리. 판정 큐 로드 전에 호출.</summary>
        public static void MarkBigEvents(Beatmap map, float bigStrength, float cooldown, float clearWindow)
        {
            float lastBig = -999f;
            var bigTimes = new List<float>();
            foreach (var e in map.events)
            {
                if (e.strength >= bigStrength && e.time - lastBig >= cooldown)
                {
                    e.type = 1;
                    lastBig = e.time;
                    bigTimes.Add(e.time);
                }
            }
            map.events.RemoveAll(e =>
            {
                if (e.type != 0) return false;
                foreach (var t in bigTimes)
                    if (Mathf.Abs(e.time - t) < clearWindow) return true;
                return false;
            });
        }

        /// <summary>슬로우 컷 이벤트 선정(type=2) + 주변 정리. MarkBigEvents 다음에 호출 (type=0만 대상).</summary>
        public static void MarkSlowEvents(Beatmap map, float minStrength, float cooldown, float clearWindow)
        {
            float last = -999f;
            var slowTimes = new List<float>();
            foreach (var e in map.events)
            {
                if (e.type != 0) { continue; }
                if (e.strength >= minStrength && e.time - last >= cooldown)
                {
                    e.type = 2;
                    last = e.time;
                    slowTimes.Add(e.time);
                }
            }
            map.events.RemoveAll(e =>
            {
                if (e.type != 0) return false;
                foreach (var t in slowTimes)
                    if (Mathf.Abs(e.time - t) < clearWindow) return true;
                return false;
            });
        }

        /// <summary>홀드 이벤트 선정(type=3) + 홀드 구간 내 노트 정리. Big/Slow 마킹 다음에 호출.</summary>
        public static void MarkHoldEvents(Beatmap map, float minStrength, float cooldown, float holdBeats)
        {
            float holdSec = holdBeats * 60f / Mathf.Max(1f, map.bpm);
            float last = -999f;
            var holdTimes = new List<float>();
            foreach (var e in map.events)
            {
                if (e.type != 0) continue;
                if (e.strength >= minStrength && e.time - last >= cooldown)
                {
                    e.type = 3;
                    last = e.time;
                    holdTimes.Add(e.time);
                }
            }
            // 홀드 유지 중 + 직후 여유까지 일반 노트 제거 — 누르고 있는 동안 다른 입력이 없어야 함
            map.events.RemoveAll(e =>
            {
                if (e.type != 0) return false;
                foreach (var t in holdTimes)
                    if (e.time > t - 0.8f && e.time < t + holdSec + 0.6f) return true;
                return false;
            });
        }

        Beatmap _map;
        int _next;
        readonly List<(GameObject obj, float hitTime, int lane)> _airborne = new List<(GameObject, float, int)>();

        GameObject[] _activePrefabs;

        public void Begin(Beatmap map)
        {
            _map = map;
            _next = 0;

            // 선택한 요리의 재료 풀만 등장 (null = 전 재료). 이름 매칭 실패 시 전체 폴백
            var allowed = Menu.SongSelection.DishIngredients;
            if (allowed == null)
            {
                _activePrefabs = ingredientPrefabs;
            }
            else
            {
                var picked = new List<GameObject>();
                foreach (var p in ingredientPrefabs)
                    if (p != null && System.Array.IndexOf(allowed, p.name) >= 0)
                        picked.Add(p);
                _activePrefabs = picked.Count > 0 ? picked.ToArray() : ingredientPrefabs;
            }
        }

        /// <summary>화면(카메라) 기준 lane 방향. 0=좌 1=우 2=상 3=하.</summary>
        public static Vector3 LaneDir(int lane)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            var right = cam != null
                ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized
                : Vector3.right;
            return lane switch { 0 => -right, 1 => right, 2 => Vector3.up, _ => Vector3.down };
        }

        /// <summary>lane 방향 썰기 존 월드 위치.</summary>
        public Vector3 ZonePos(int lane) => sliceZone.position + LaneDir(Mathf.Clamp(lane, 0, 3)) * zoneRadius;

        void Update()
        {
            if (_map == null || Conductor.Instance == null || !Conductor.Instance.IsPlaying) return;

            double now = Conductor.Instance.SongPosition;
            while (_next < _map.events.Count && _map.events[_next].time - flightTime <= now)
            {
                Launch(_map.events[_next]);
                _next++;
            }

            // 판정 시각을 한참 지난 재료(놓친 것)는 목록에서 빼고 치운다
            for (int i = _airborne.Count - 1; i >= 0; i--)
            {
                var a = _airborne[i];
                if (a.obj == null) { _airborne.RemoveAt(i); continue; }
                if (now - a.hitTime > 2f)
                {
                    Destroy(a.obj);
                    _airborne.RemoveAt(i);
                }
            }
        }

        void Launch(BeatEvent e)
        {
            int lane = Mathf.Clamp(e.lane, 0, 3);
            var dir = LaneDir(lane);
            var target = sliceZone.position + dir * zoneRadius;
            var spawn = target + dir * spawnDistance
                      + new Vector3(0f, lane <= 1 ? Random.Range(-0.5f, 1f) : 0f, Random.Range(-0.5f, 0.5f));

            var pool = _activePrefabs != null && _activePrefabs.Length > 0 ? _activePrefabs : ingredientPrefabs;
            var prefab = pool[Random.Range(0, pool.Length)];
            var obj = Instantiate(prefab, spawn, Random.rotation);

            // 비트 강도 → 크기. type=1은 MarkBigEvents가 선정한 대형 연타 재료
            bool big = e.type == 1;
            float size = Mathf.Lerp(0.85f, 1.6f, Mathf.Clamp01((e.strength - 1f) / 3f));
            if (big) size = Mathf.Max(size, 1.9f);
            if (e.type == 2) size = Mathf.Max(size, 1.5f); // 슬로우 컷 재료는 큼직하게
            if (e.type == 3) size = Mathf.Max(size, 1.35f); // 홀드 재료도 눈에 띄게
            obj.transform.localScale *= size;

            var sliceable = obj.GetComponent<Sliceable>();
            if (sliceable != null && big) sliceable.hitsRequired = bigHits;

            // 포물선 역산: p1 = p0 + v0*t + 0.5*g*t^2  →  v0 = (p1 - p0 - 0.5*g*t^2) / t
            var rb = obj.GetComponent<Rigidbody>();
            if (rb == null) rb = obj.AddComponent<Rigidbody>();
            Vector3 g = Physics.gravity;
            Vector3 v0 = (target - spawn - 0.5f * g * flightTime * flightTime) / flightTime;
            rb.linearVelocity = v0;
            rb.angularVelocity = Random.insideUnitSphere * 5f;

            _airborne.Add((obj, e.time, lane));
        }

        /// <summary>지금 판정 시각 근처(±0.35초)의 재료 중 가장 이른 것. lane 지정 시 그 방향만.</summary>
        public Sliceable CurrentTarget(int lane = -1)
        {
            double now = Conductor.Instance != null ? Conductor.Instance.SongPosition : 0.0;
            Sliceable best = null;
            float bestTime = float.MaxValue;
            foreach (var a in _airborne)
            {
                if (a.obj == null) continue;
                if (lane >= 0 && a.lane != lane) continue;
                if (Mathf.Abs((float)(now - a.hitTime)) > 0.35f) continue;
                if (a.hitTime < bestTime)
                {
                    bestTime = a.hitTime;
                    best = a.obj.GetComponent<Sliceable>();
                }
            }
            return best;
        }
    }
}
