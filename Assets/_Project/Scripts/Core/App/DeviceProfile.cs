using System;
using TowerGlide.Core.Save;

namespace TowerGlide.Core.App
{
    /// <summary>기기 사양(UnityEngine.SystemInfo 값을 Runtime이 채워 넘김).</summary>
    [Serializable]
    public struct DeviceSpec
    {
        public int SystemMemoryMB;
        public int ProcessorCount;
        public bool IsMobile;
    }

    /// <summary>
    /// 첫 실행 시 그래픽 단계·프레임 자동 선택(명세 7.1 "저사양 기기에서 자동으로 낮춤", 10.2 발열·배터리).
    /// 기준은 보수적인 시작점이며 실기기 측정 후 docs/PERFORMANCE.md에 맞춰 조정한다. 사용자가 설정에서 바꾸면 그 값이 우선.
    /// </summary>
    public static class QualityAutoSelector
    {
        public static QualityTier SelectTier(DeviceSpec d)
        {
            if (!d.IsMobile) return QualityTier.Quality;
            if (d.SystemMemoryMB > 0 && d.SystemMemoryMB < 4000) return QualityTier.Performance;
            if (d.ProcessorCount > 0 && d.ProcessorCount < 6) return QualityTier.Performance;
            if (d.SystemMemoryMB >= 8000 && d.ProcessorCount >= 8) return QualityTier.Quality;
            return QualityTier.Balanced;
        }

        /// <summary>저사양은 30fps로 시작(발열·배터리·안정적인 프레임 우선), 나머지는 60.</summary>
        public static int SelectFrameRate(QualityTier tier) => tier == QualityTier.Performance ? 30 : 60;

        /// <summary>단계별 동시 상주 청크 상한(메모리 예산, 애드온 명세 7.2).</summary>
        public static int MaxResidentChunks(QualityTier tier) => tier == QualityTier.Performance ? 9 : tier == QualityTier.Balanced ? 12 : 16;

        /// <summary>아직 자동 선택을 안 했으면 적용하고 true. 한 번만 실행(사용자 선택 덮어쓰지 않음).</summary>
        public static bool ApplyOnce(SettingsData settings, DeviceSpec device)
        {
            if (settings == null || settings.qualityAutoApplied) return false;
            QualityTier tier = SelectTier(device);
            settings.qualityTier = (int)tier;
            settings.targetFrameRate = SelectFrameRate(tier);
            settings.qualityAutoApplied = true;
            return true;
        }
    }

    /// <summary>
    /// 프레임 시간 통계(실기기 측정용 개발 오버레이). 최근 N프레임 링 버퍼 → 평균 FPS·1% Low·최대 프레임 시간. 할당 없음.
    /// </summary>
    public sealed class FrameStats
    {
        private readonly float[] _ms;
        private readonly float[] _sorted;
        private int _count;
        private int _next;

        public FrameStats(int window = 300)
        {
            if (window < 10) throw new ArgumentOutOfRangeException(nameof(window));
            _ms = new float[window];
            _sorted = new float[window];
        }

        public int Count => _count;

        public void Add(float frameSeconds)
        {
            if (frameSeconds <= 0f || float.IsNaN(frameSeconds) || float.IsInfinity(frameSeconds)) return;
            _ms[_next] = frameSeconds * 1000f;
            _next = (_next + 1) % _ms.Length;
            if (_count < _ms.Length) _count++;
        }

        public float AverageFps
        {
            get
            {
                if (_count == 0) return 0f;
                double sum = 0;
                for (int i = 0; i < _count; i++) sum += _ms[i];
                return (float)(1000.0 / (sum / _count));
            }
        }

        public float MaxFrameMs
        {
            get
            {
                float m = 0f;
                for (int i = 0; i < _count; i++) m = Math.Max(m, _ms[i]);
                return m;
            }
        }

        /// <summary>가장 느린 1% 프레임들의 경계 FPS(99번째 백분위 프레임 시간의 역수).</summary>
        public float OnePercentLowFps
        {
            get
            {
                if (_count == 0) return 0f;
                Array.Copy(_ms, _sorted, _count);
                Array.Sort(_sorted, 0, _count);
                int idx = Math.Min(_count - 1, (int)Math.Floor(_count * 0.99)); // 가장 느린 1% 구간의 시작
                return 1000f / _sorted[Math.Max(0, idx)];
            }
        }

        /// <summary>목표 프레임 시간을 넘긴 프레임 비율(0..1) — "30 FPS 미만 구간" 기록용.</summary>
        public float FractionSlowerThan(float fps)
        {
            if (_count == 0 || fps <= 0f) return 0f;
            float limit = 1000f / fps;
            int n = 0;
            for (int i = 0; i < _count; i++) if (_ms[i] > limit) n++;
            return (float)n / _count;
        }

        public void Reset()
        {
            _count = 0;
            _next = 0;
        }
    }
}
