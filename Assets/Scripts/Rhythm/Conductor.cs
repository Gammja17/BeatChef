using System;
using UnityEngine;

namespace BeatSlash.Rhythm
{
    /// <summary>
    /// dspTime 기반 음악 클럭. 모든 판정/연출은 이 클래스의 SongPosition을 기준으로 한다.
    /// Time.time은 프레임 시간이라 오디오와 어긋난다 — 절대 판정에 쓰지 말 것.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : MonoBehaviour
    {
        public static Conductor Instance { get; private set; }

        [Header("Song")]
        public AudioClip songClip;
        public float bpm = 120f;
        [Tooltip("곡 시작부터 첫 박까지의 오프셋(초)")]
        public float firstBeatOffset = 0f;

        [Header("Latency")]
        [Tooltip("입력 판정 보정(초). 기기별 오디오 출력 지연 보정용. +면 판정을 늦춤")]
        public float inputLatency = 0f;
        [Tooltip("전역 타이밍 오프셋(초). 화살표가 음악보다 빠르게 느껴지면 F2(+), 늦으면 F1(-). PlayerPrefs에 저장")]
        public float timingOffset = 0.035f; // 실측 캘리브레이션 기본값 (+35ms)

        /// <summary>매 박마다 (beatIndex) 발행. 연출(BeatPulse 등)이 구독.</summary>
        public event Action<int> OnBeat;

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public float SecPerBeat => 60f / bpm;

        AudioSource _source;
        double _dspSongStart;
        double _dspPauseStart;
        int _lastBeat = -1;
        bool _beatTick;
        AudioClip _tickClip;

        void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.volume = GameSettings.BgmVolume;
            GameSettings.OnBgmChanged += ApplyBgmVolume;
            timingOffset = PlayerPrefs.GetFloat(GameSettings.KeyTiming, timingOffset);
        }

        void OnDestroy()
        {
            GameSettings.OnBgmChanged -= ApplyBgmVolume;
        }

        void ApplyBgmVolume(float v) => _source.volume = v;

        public void Play(AudioClip clip = null, double delay = 0.5)
        {
            if (clip != null) songClip = clip;
            _source.clip = songClip;
            // PlayScheduled로 시작 시점을 dspTime에 고정 — Play()는 시작 타이밍이 부정확하다
            _dspSongStart = AudioSettings.dspTime + delay;
            _source.PlayScheduled(_dspSongStart);
            _lastBeat = -1;
            IsPlaying = true;
        }

        public void Stop()
        {
            _source.Stop();
            IsPlaying = false;
            IsPaused = false;
        }

        /// <summary>일시정지: 오디오 멈추고 SongPosition을 동결. 판정/스폰/HUD가 전부 그 자리에 선다.</summary>
        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            _dspPauseStart = AudioSettings.dspTime;
            if (IsPlaying) _source.Pause();
        }

        /// <summary>재개: 멈춘 시간만큼 곡 시작점을 밀어 dspTime과 재동기화.</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            _dspSongStart += AudioSettings.dspTime - _dspPauseStart;
            IsPaused = false;
            if (IsPlaying) _source.UnPause();
        }

        void AdjustOffset(float delta)
        {
            SetTimingOffset(timingOffset + delta);
        }

        /// <summary>설정 슬라이더용. PlayerPrefs에 즉시 저장.</summary>
        public void SetTimingOffset(float value)
        {
            timingOffset = value;
            PlayerPrefs.SetFloat(GameSettings.KeyTiming, timingOffset);
            PlayerPrefs.Save();
        }

        /// <summary>곡 시작 기준 현재 위치(초). 곡 시작 전이면 음수.
        /// timingOffset을 빼서 스폰/프롬프트/판정 전부가 음악 대비 함께 밀리거나 당겨진다.</summary>
        public double SongPosition =>
            (IsPaused ? _dspPauseStart : AudioSettings.dspTime) - _dspSongStart - timingOffset;

        /// <summary>판정용 위치: 입력 레이턴시 보정 포함.</summary>
        public double JudgePosition => SongPosition - inputLatency;

        /// <summary>현재 위치(박 단위, 실수).</summary>
        public double SongPositionBeats => (SongPosition - firstBeatOffset) / SecPerBeat;

        /// <summary>정박 직후 1에서 지수 감쇠하는 킥 값(0~1) — 비트 동기 연출 공용.</summary>
        public float BeatKick01
        {
            get
            {
                if (!IsPlaying || IsPaused) return 0f;
                double beats = SongPositionBeats;
                float phase = (float)(beats - Math.Floor(beats));
                return Mathf.Exp(-phase * 6f);
            }
        }

        void Update()
        {
            // 실시간 싱크 보정: F1 = 화살표 당기기, F2 = 밀기 (10ms 단위)
            if (Input.GetKeyDown(KeyCode.F1)) AdjustOffset(-0.01f);
            if (Input.GetKeyDown(KeyCode.F2)) AdjustOffset(+0.01f);
            // F3 = 박자 체크: 게임이 생각하는 박마다 틱 소리 — 그리드 검증용
            if (Input.GetKeyDown(KeyCode.F3)) _beatTick = !_beatTick;

            if (!IsPlaying || IsPaused) return;

            int beat = (int)Math.Floor(SongPositionBeats);
            if (beat > _lastBeat && beat >= 0)
            {
                _lastBeat = beat;
                OnBeat?.Invoke(beat);
                if (_beatTick)
                {
                    if (_tickClip == null) _tickClip = Resources.Load<AudioClip>("Sfx/impactGeneric_light_000");
                    if (_tickClip != null) _source.PlayOneShot(_tickClip, 0.8f);
                }
            }

            if (_source.clip != null && SongPosition > _source.clip.length)
                IsPlaying = false;
        }
    }
}
