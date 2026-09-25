using UnityEngine;

namespace BeatSlash.Rhythm
{
    /// <summary>
    /// 비트 그리드 기반 스테이지 생성기 — 런타임/에디터 공용.
    /// 1) 스펙트럴 플럭스 → 2) 자기상관 템포(BPM) 추정 → 3) 비트 위상 추정 → 4) 그리드 위에만 노트 배치.
    /// 온셋을 그대로 쓰면 노트가 박자와 따로 논다 — 모든 노트는 비트(또는 반박) 정각에 스냅되고,
    /// 그 박에 실제로 소리 에너지가 있을 때만 배치돼서 "박자에 맞춰 친다"는 감각이 보장된다.
    /// </summary>
    public static class OnsetDetector
    {
        const int WindowSize = 1024;
        const int HopSize = 512;
        const float MinBpm = 70f;
        const float MaxBpm = 180f;

        /// <param name="energyFactor">평균 대비 이만큼 에너지가 있는 박에만 노트 (높을수록 노트 적음)</param>
        /// <param name="density">박당 노트 슬롯 수 — 0.5=2박마다, 1=매 박, 2=8분음까지</param>
        public static Beatmap Detect(AudioClip clip, float energyFactor = 1.15f, float density = 1f, int laneCount = 4)
        {
            var raw = new float[clip.samples * clip.channels];
            clip.GetData(raw, 0); // 에디터/데스크톱 전용 — WebGL은 샘플 오버로드 사용
            int ch = clip.channels;
            var mono = new float[clip.samples];
            for (int i = 0; i < clip.samples; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += raw[i * ch + c];
                mono[i] = s / ch;
            }
            return Detect(mono, clip.frequency, clip.name, energyFactor, density, laneCount);
        }

        /// <summary>모노 샘플 직접 분석 — WebGL 업로드 곡용 (clip.GetData는 웹에서 못 쓴다).</summary>
        public static Beatmap Detect(float[] mono, int sampleRate, string songName,
            float energyFactor = 1.15f, float density = 1f, int laneCount = 4)
        {
            float spf = (float)HopSize / sampleRate; // 프레임당 초
            var map = new Beatmap { songName = songName, bpm = 120f };

            var flux = ComputeFlux(mono, sampleRate);
            if (flux.Length < 100) return map;

            int lagI = EstimateTempoLag(flux, spf);
            // 정수 lag(11.6ms 단위)는 BPM 오차가 곡 후반에 수백 ms 드리프트로 누적된다
            // → 자기상관 피크 포물선 보간으로 서브프레임 정밀도 확보
            float lag = RefineLag(flux, lagI);
            int phase = EstimatePhase(flux, lagI);
            map.bpm = 60f / (lag * spf);
            map.firstBeatOffset = phase * spf;

            float mean = 0f;
            foreach (var f in flux) mean += f;
            mean /= flux.Length;
            if (mean <= 0f) return map;

            // 그리드 슬롯을 돌며 에너지 있는 박에만 노트 배치 — 노트 시각은 항상 그리드 정각
            // 단, 조용한 구간이라도 MaxGapBeats박 넘게 비면 이질감이 크다 —
            // 로컬 에너지가 최소 바닥선만 넘으면 강제 배치 (진짜 무음 인트로는 그대로 빈다)
            const float MaxGapBeats = 4f;
            const float ForceFloor = 0.35f;
            float step = lag / Mathf.Max(0.25f, density);
            float maxGapFrames = lag * MaxGapBeats;
            float lastPlaced = phase - step;
            for (float slot = phase; slot < flux.Length - 2; slot += step)
            {
                int j = Mathf.RoundToInt(slot);
                float e = 0f;
                for (int k = Mathf.Max(0, j - 2); k <= Mathf.Min(flux.Length - 1, j + 2); k++)
                    e = Mathf.Max(e, flux[k]);

                bool force = slot - lastPlaced >= maxGapFrames && e > mean * ForceFloor;
                if (!force && e < mean * energyFactor) continue;

                map.events.Add(new BeatEvent
                {
                    time = slot * spf,
                    strength = e / mean,
                    lane = Random.Range(0, laneCount),
                    type = 0,
                });
                lastPlaced = slot;
            }
            return map;
        }

        /// <summary>스펙트럴 플럭스: 프레임 간 스펙트럼 증가량 합 (Hann 윈도우).
        /// 킥/베이스가 박자의 기준이므로 저음 대역에 강한 가중치를 준다.</summary>
        static float[] ComputeFlux(float[] mono, int sampleRate)
        {
            int frameCount = Mathf.Max(0, (mono.Length - WindowSize) / HopSize);
            var flux = new float[frameCount];
            var prevMag = new float[WindowSize / 2];
            var re = new float[WindowSize];
            var im = new float[WindowSize];

            // 빈별 가중치: ~250Hz(킥·베이스) 3배, ~2kHz(스네어·바디) 1배, 그 위(하이햇·공기) 0.4배
            var weight = new float[WindowSize / 2];
            float binHz = (float)sampleRate / WindowSize;
            for (int i = 0; i < weight.Length; i++)
            {
                float hz = i * binHz;
                weight[i] = hz < 250f ? 3f : hz < 2000f ? 1f : 0.4f;
            }

            for (int f = 0; f < frameCount; f++)
            {
                int offset = f * HopSize;
                for (int i = 0; i < WindowSize; i++)
                {
                    float w = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (WindowSize - 1)));
                    re[i] = mono[offset + i] * w;
                    im[i] = 0f;
                }
                Fft(re, im);

                float sum = 0f;
                for (int i = 0; i < WindowSize / 2; i++)
                {
                    float mag = Mathf.Sqrt(re[i] * re[i] + im[i] * im[i]);
                    float diff = mag - prevMag[i];
                    if (diff > 0f) sum += diff * weight[i];
                    prevMag[i] = mag;
                }
                flux[f] = sum;
            }
            return flux;
        }

        /// <summary>자기상관으로 비트 주기(프레임 단위 lag) 추정. 2배 lag 지지를 얹어 반박/온박 혼동을 줄인다.</summary>
        static int EstimateTempoLag(float[] flux, float spf)
        {
            int minLag = Mathf.Max(1, Mathf.RoundToInt(60f / MaxBpm / spf));
            int maxLag = Mathf.Min(flux.Length / 2, Mathf.RoundToInt(60f / MinBpm / spf));

            double bestScore = double.MinValue;
            int best = minLag;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double s = 0;
                int n = flux.Length - lag;
                for (int i = 0; i < n; i++) s += (double)flux[i] * flux[i + lag];
                s /= n;

                if (2 * lag < flux.Length)
                {
                    double s2 = 0;
                    int n2 = flux.Length - 2 * lag;
                    for (int i = 0; i < n2; i++) s2 += (double)flux[i] * flux[i + 2 * lag];
                    s += 0.5 * s2 / n2;
                }

                if (s > bestScore) { bestScore = s; best = lag; }
            }
            return best;
        }

        /// <summary>자기상관 피크 주변 3점 포물선 보간 — lag를 실수 정밀도로 보정 (드리프트 방지).</summary>
        static float RefineLag(float[] flux, int lag)
        {
            if (lag <= 1 || lag >= flux.Length / 2 - 1) return lag;

            double Score(int l)
            {
                double s = 0;
                int n = flux.Length - l;
                for (int i = 0; i < n; i++) s += (double)flux[i] * flux[i + l];
                return s / n;
            }

            double y0 = Score(lag - 1), y1 = Score(lag), y2 = Score(lag + 1);
            double denom = y0 - 2.0 * y1 + y2;
            if (System.Math.Abs(denom) < 1e-12) return lag;
            double d = 0.5 * (y0 - y2) / denom;
            if (d < -0.5) d = -0.5;
            else if (d > 0.5) d = 0.5;
            return (float)(lag + d);
        }

        /// <summary>비트 위상(첫 박 프레임) 추정: 그리드에 걸리는 플럭스 합이 최대가 되는 오프셋.</summary>
        static int EstimatePhase(float[] flux, int lag)
        {
            double bestScore = double.MinValue;
            int best = 0;
            for (int phi = 0; phi < lag; phi++)
            {
                double s = 0;
                int count = 0;
                for (int j = phi; j < flux.Length; j += lag)
                {
                    float m = flux[j];
                    if (j > 0) m = Mathf.Max(m, flux[j - 1]);
                    if (j < flux.Length - 1) m = Mathf.Max(m, flux[j + 1]);
                    s += m;
                    count++;
                }
                s /= count;
                if (s > bestScore) { bestScore = s; best = phi; }
            }
            return best;
        }

        /// <summary>제자리 radix-2 FFT (WindowSize는 2의 거듭제곱).</summary>
        static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                float ang = -2f * Mathf.PI / len;
                float wRe = Mathf.Cos(ang), wIm = Mathf.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float curRe = 1f, curIm = 0f;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = i + k + len / 2;
                        float tRe = re[b] * curRe - im[b] * curIm;
                        float tIm = re[b] * curIm + im[b] * curRe;
                        re[b] = re[a] - tRe; im[b] = im[a] - tIm;
                        re[a] += tRe; im[a] += tIm;
                        float nRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = nRe;
                    }
                }
            }
        }
    }
}
