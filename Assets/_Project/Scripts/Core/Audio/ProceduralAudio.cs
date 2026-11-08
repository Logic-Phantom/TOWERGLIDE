using System;

namespace TowerGlide.Core.Audio
{
    public enum EngineSoundKind
    {
        /// <summary>소형 프로펠러: 낮은 기본음 + 프로펠러 맥동.</summary>
        Propeller = 0,
        /// <summary>제트: 높은 쇳소리 + 넓은 소음.</summary>
        Jet = 1,
        /// <summary>터보팬 여객기: 낮고 묵직한 웅웅거림.</summary>
        Turbofan = 2,
    }

    public enum SfxKind
    {
        Crash = 0,
        RingPass = 1,
        NearMiss = 2,
        UiClick = 3,
        BoostStart = 4,
    }

    /// <summary>
    /// 효과음·루프를 코드로 합성한다(외부 음원 0개 — 라이선스 문제 없음, docs/THIRD_PARTY_ASSETS.md).
    /// 결과는 -1..1 모노 샘플. Runtime이 AudioClip.Create로 만든다. 같은 입력 → 같은 결과(시드 고정).
    /// 루프 음원은 시작·끝을 교차 페이드해 이음매 클릭이 없게 한다.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int DefaultSampleRate = 22050; // 모바일 메모리 절약(효과음 대역에 충분)

        public static float[] EngineLoop(EngineSoundKind kind, int sampleRate = DefaultSampleRate, float seconds = 2f)
        {
            int n = Samples(sampleRate, seconds);
            var s = new float[n];
            var rng = new Random(1000 + (int)kind);
            float lp = 0f;
            // 기본 주파수는 루프 길이에 정수 주기가 들어가게 맞춤 → 이음매 없음
            float baseHz = Fit(kind == EngineSoundKind.Propeller ? 90f : kind == EngineSoundKind.Jet ? 220f : 70f, seconds);
            float pulseHz = Fit(kind == EngineSoundKind.Propeller ? 28f : kind == EngineSoundKind.Jet ? 0f : 9f, seconds);
            float noiseMix = kind == EngineSoundKind.Propeller ? 0.25f : kind == EngineSoundKind.Jet ? 0.6f : 0.35f;
            float lpCoef = kind == EngineSoundKind.Jet ? 0.35f : 0.08f; // 제트는 밝은 소음, 나머지는 둔한 소음
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / sampleRate;
                float tone = (float)(Math.Sin(2 * Math.PI * baseHz * t) * 0.6 + Math.Sin(2 * Math.PI * baseHz * 2 * t) * 0.25
                                     + Math.Sin(2 * Math.PI * baseHz * 3 * t) * 0.1);
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += (white - lp) * lpCoef;
                float pulse = pulseHz > 0f ? 0.65f + 0.35f * (float)Math.Sin(2 * Math.PI * pulseHz * t) : 1f;
                s[i] = ((1f - noiseMix) * tone + noiseMix * lp * 2f) * pulse;
            }
            s = CrossfadeLoop(s, Math.Min(n / 8, sampleRate / 20));
            Normalize(s, 0.8f);
            return s;
        }

        /// <summary>바람 소리 루프: 저역 통과 소음에 느린 출렁임.</summary>
        public static float[] WindLoop(int sampleRate = DefaultSampleRate, float seconds = 3f)
        {
            int n = Samples(sampleRate, seconds);
            var s = new float[n];
            var rng = new Random(77);
            float lp = 0f, lp2 = 0f;
            float swell = Fit(0.5f, seconds);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / sampleRate;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.05f;
                lp2 += (lp - lp2) * 0.2f;
                s[i] = lp2 * (0.7f + 0.3f * (float)Math.Sin(2 * Math.PI * swell * t));
            }
            s = CrossfadeLoop(s, Math.Min(n / 6, sampleRate / 10));
            Normalize(s, 0.7f);
            return s;
        }

        public static float[] Sfx(SfxKind kind, int sampleRate = DefaultSampleRate)
        {
            switch (kind)
            {
                case SfxKind.Crash: return Crash(sampleRate);
                case SfxKind.RingPass: return Chime(sampleRate);
                case SfxKind.NearMiss: return Whoosh(sampleRate);
                case SfxKind.BoostStart: return BoostStart(sampleRate);
                default: return Click(sampleRate);
            }
        }

        private static float[] Crash(int sr)
        {
            int n = Samples(sr, 0.6f);
            var s = new float[n];
            var rng = new Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.25f;
                float thump = (float)Math.Sin(2 * Math.PI * 55 * i / sr) * (float)Math.Exp(-t * 10);
                s[i] = (lp * 1.5f + thump) * (float)Math.Exp(-t * 6);
            }
            FadeEdges(s, sr);
            Normalize(s, 0.9f);
            return s;
        }

        /// <summary>링 통과: 밝은 2음 상승 차임.</summary>
        private static float[] Chime(int sr)
        {
            int n = Samples(sr, 0.45f);
            var s = new float[n];
            int half = n / 3;
            for (int i = 0; i < n; i++)
            {
                double f = i < half ? 880.0 : 1318.5; // A5 → E6
                int local = i < half ? i : i - half;
                float env = (float)Math.Exp(-local / (sr * 0.12));
                s[i] = (float)(Math.Sin(2 * Math.PI * f * i / sr) + 0.3 * Math.Sin(2 * Math.PI * f * 2 * i / sr)) * env;
            }
            FadeEdges(s, sr);
            Normalize(s, 0.7f);
            return s;
        }

        /// <summary>근접 회피: 짧은 소음이 높은 쪽으로 스윕.</summary>
        private static float[] Whoosh(int sr)
        {
            int n = Samples(sr, 0.4f);
            var s = new float[n];
            var rng = new Random(9);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float coef = 0.02f + 0.3f * t; // 점점 밝게
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * coef;
                float env = (float)Math.Sin(Math.PI * t);
                s[i] = lp * env;
            }
            FadeEdges(s, sr);
            Normalize(s, 0.6f);
            return s;
        }

        private static float[] BoostStart(int sr)
        {
            int n = Samples(sr, 0.5f);
            var s = new float[n];
            var rng = new Random(13);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.15f;
                double f = 120 + 260 * t;
                s[i] = (lp * 0.7f + 0.4f * (float)Math.Sin(2 * Math.PI * f * i / sr)) * (1f - t);
            }
            FadeEdges(s, sr);
            Normalize(s, 0.6f);
            return s;
        }

        private static float[] Click(int sr)
        {
            int n = Samples(sr, 0.05f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
                s[i] = (float)(Math.Sin(2 * Math.PI * 1500 * i / sr) * Math.Exp(-i / (sr * 0.008)));
            FadeEdges(s, sr);
            Normalize(s, 0.5f);
            return s;
        }

        // ------------------------------------------------------------------

        private static int Samples(int sampleRate, float seconds)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            return Math.Max(1, (int)Math.Round(sampleRate * seconds));
        }

        /// <summary>루프 길이에 정수 주기가 들어가도록 주파수를 맞춘다(0이면 0).</summary>
        public static float Fit(float hz, float seconds)
        {
            if (hz <= 0f || seconds <= 0f) return 0f;
            float cycles = Math.Max(1f, (float)Math.Round(hz * seconds));
            return cycles / seconds;
        }

        /// <summary>
        /// 루프 이음매 제거: 마지막 fade 구간을 시작부와 교차 페이드한 뒤 그 구간을 잘라낸다.
        /// 결과의 마지막 샘플 다음(= 첫 샘플)이 원래 신호에서 바로 이어지는 샘플이 되어 클릭이 없다. 길이 = n - fade.
        /// </summary>
        public static float[] CrossfadeLoop(float[] s, int fade)
        {
            if (fade <= 0 || fade * 2 > s.Length) return s;
            int n = s.Length - fade;
            var r = new float[n];
            Array.Copy(s, r, n);
            for (int i = 0; i < fade; i++)
            {
                float a = (float)i / fade;
                r[i] = s[i] * a + s[n + i] * (1f - a);
            }
            return r;
        }

        private static void FadeEdges(float[] s, int sr)
        {
            int f = Math.Min(s.Length / 4, sr / 200); // 5ms
            for (int i = 0; i < f; i++)
            {
                float a = (float)i / f;
                s[i] *= a;
                s[s.Length - 1 - i] *= a;
            }
        }

        public static void Normalize(float[] s, float peak)
        {
            float max = 0f;
            foreach (float v in s)
                if (!float.IsNaN(v)) max = Math.Max(max, Math.Abs(v));
            float k = max > 1e-6f ? peak / max : 0f;
            for (int i = 0; i < s.Length; i++) s[i] = float.IsNaN(s[i]) ? 0f : s[i] * k;
        }
    }

    /// <summary>
    /// 엔진·바람 소리 상태(순수 함수). 속도·스로틀·부스트 → 음높이·음량. 기종별 음원은 EngineSoundKind로 구분.
    /// </summary>
    public static class EngineSoundModel
    {
        /// <param name="speed01">최소 비행 속도 0 ~ 부스트 최고 속도 1</param>
        public static float Pitch(EngineSoundKind kind, float speed01, float throttle, bool boosting)
        {
            speed01 = Clamp01(speed01);
            float range = kind == EngineSoundKind.Turbofan ? 0.35f : kind == EngineSoundKind.Jet ? 0.7f : 0.6f;
            float p = 0.8f + range * speed01 + 0.08f * Math.Max(0f, Math.Min(1f, throttle));
            if (boosting) p += kind == EngineSoundKind.Turbofan ? 0f : 0.12f;
            return p;
        }

        public static float EngineVolume(float speed01, float throttle, bool boosting) =>
            Clamp01(0.45f + 0.35f * Clamp01(speed01) + 0.1f * Math.Max(0f, throttle) + (boosting ? 0.15f : 0f));

        /// <summary>바람 소리는 속도의 제곱에 비례 — 저속에서는 거의 안 들림.</summary>
        public static float WindVolume(float speed01) => Clamp01(speed01) * Clamp01(speed01) * 0.8f;

        public static float Speed01(float speed, float minSpeed, float maxSpeed) =>
            maxSpeed - minSpeed < 1e-3f ? 0f : Clamp01((speed - minSpeed) / (maxSpeed - minSpeed));

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
