using System;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Input
{
    /// <summary>
    /// 비행 물리가 소비하는 정규화된 입력. 축은 모두 -1..1, Throttle은 -1(감속)..1(가속), 0 = 자동 순항.
    /// UI·키보드·게임패드는 이 구조체만 만들고, 비행 컨트롤러는 이 구조체만 읽는다 (D-007).
    /// </summary>
    [Serializable]
    public struct FlightInput
    {
        public float Pitch;
        public float Roll;
        public float Yaw;
        public float Throttle;
        public bool Boost;
        public bool AirBrake;

        public static FlightInput Neutral => default;

        public FlightInput Sanitized()
        {
            return new FlightInput
            {
                Pitch = FlightMath.Clamp(FlightMath.Sanitize(Pitch), -1f, 1f),
                Roll = FlightMath.Clamp(FlightMath.Sanitize(Roll), -1f, 1f),
                Yaw = FlightMath.Clamp(FlightMath.Sanitize(Yaw), -1f, 1f),
                Throttle = FlightMath.Clamp(FlightMath.Sanitize(Throttle), -1f, 1f),
                Boost = Boost,
                AirBrake = AirBrake,
            };
        }

        /// <summary>
        /// 여러 입력 소스(터치·키보드·게임패드)를 합친다. 축은 절댓값이 큰 쪽, 버튼은 OR.
        /// </summary>
        public static FlightInput Combine(FlightInput a, FlightInput b)
        {
            return new FlightInput
            {
                Pitch = Dominant(a.Pitch, b.Pitch),
                Roll = Dominant(a.Roll, b.Roll),
                Yaw = Dominant(a.Yaw, b.Yaw),
                Throttle = Dominant(a.Throttle, b.Throttle),
                Boost = a.Boost || b.Boost,
                AirBrake = a.AirBrake || b.AirBrake,
            };
        }

        private static float Dominant(float a, float b) => Math.Abs(a) >= Math.Abs(b) ? a : b;
    }
}
