using System;

namespace TowerGlide.Core.Util
{
    /// <summary>
    /// 엔진 의존 없는 수학 유틸. Unity의 Mathf 대신 사용해 Core 어셈블리를 순수 C#으로 유지한다.
    /// 단위 규칙: 1 Unity unit = 1 m, 속도는 내부적으로 m/s, 표시만 km/h.
    /// </summary>
    public static class FlightMath
    {
        public const float MpsToKmh = 3.6f;

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float InverseLerp(float a, float b, float value)
        {
            if (Math.Abs(b - a) < 1e-6f) return 0f;
            return Clamp01((value - a) / (b - a));
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        public static float MetersPerSecondToKmh(float mps) => mps * MpsToKmh;

        public static float KmhToMetersPerSecond(float kmh) => kmh / MpsToKmh;

        /// <summary>NaN/Infinity를 0으로 바꾼다. 입력·저장 데이터 방어용.</summary>
        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
