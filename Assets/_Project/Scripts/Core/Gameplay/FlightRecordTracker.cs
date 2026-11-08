using System;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Gameplay
{
    /// <summary>비행 시간·거리·최고 속도·최고 고도 누적(FREE FLIGHT 기록, 결과 화면).</summary>
    public sealed class FlightRecordTracker
    {
        /// <summary>한 틱 이동이 이 값보다 크면 텔레포트(복귀)로 보고 거리에 넣지 않는다(m).</summary>
        public const float TeleportThresholdMeters = 50f;

        public float FlightSeconds { get; private set; }
        public float DistanceMeters { get; private set; }
        public float MaxSpeedKmh { get; private set; }
        public float MaxAltitudeMeters { get; private set; }

        public void Tick(float deltaTime, float movedMeters, float speedMps, float altitudeMeters)
        {
            if (deltaTime <= 0f) return;
            FlightSeconds += deltaTime;
            movedMeters = FlightMath.Sanitize(movedMeters);
            if (movedMeters > 0f && movedMeters < TeleportThresholdMeters) DistanceMeters += movedMeters;
            MaxSpeedKmh = Math.Max(MaxSpeedKmh, FlightMath.MetersPerSecondToKmh(FlightMath.Sanitize(speedMps)));
            MaxAltitudeMeters = Math.Max(MaxAltitudeMeters, FlightMath.Sanitize(altitudeMeters));
        }

        public void Reset()
        {
            FlightSeconds = 0f;
            DistanceMeters = 0f;
            MaxSpeedKmh = 0f;
            MaxAltitudeMeters = 0f;
        }
    }
}
