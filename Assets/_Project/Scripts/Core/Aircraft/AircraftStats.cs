using System;
using System.Collections.Generic;

namespace TowerGlide.Core.Aircraft
{
    /// <summary>격납고 성능 표시에 필요한 기체 수치(게임 내 밸런스 값 — 실제 항공기 제원 아님).</summary>
    [Serializable]
    public struct FlightProfileNumbers
    {
        public float MaxSpeed;          // m/s (부스트 제외)
        public float MaxThrust;         // m/s²
        public float MaxRollRate;       // deg/s
        public float MaxPitchRate;      // deg/s
        public float AngularResponse;   // 1/s
        public float Wingspan;          // m
        public float AirBrakeMultiplier;
    }

    /// <summary>0..1 상대 지표. 서로 다른 단위를 하나의 점수로 합치지 않는다(애드온 명세 14.5).</summary>
    [Serializable]
    public struct AircraftStatBars
    {
        public float Speed;
        public float Acceleration;
        public float Agility;
        public float Stability;
        public float Size;
    }

    /// <summary>
    /// 라인업 안에서의 상대 성능 막대 계산. 최솟값 기체 = 0.15, 최댓값 = 1(막대가 비어 보이지 않게).
    /// - 기동성 = 롤·피치 각속도 × 반응 속도
    /// - 안정성 = 입력에 덜 출렁이는 정도(반응 속도의 역수) — 기동성과 반대 방향이라 "빠른 기체가 무조건 좋다"로 보이지 않는다.
    /// </summary>
    public static class AircraftStats
    {
        public static AircraftStatBars[] Compute(IReadOnlyList<FlightProfileNumbers> lineup)
        {
            int n = lineup.Count;
            var speed = new float[n];
            var accel = new float[n];
            var agility = new float[n];
            var stability = new float[n];
            var size = new float[n];
            for (int i = 0; i < n; i++)
            {
                FlightProfileNumbers p = lineup[i];
                speed[i] = p.MaxSpeed;
                accel[i] = p.MaxThrust;
                agility[i] = (p.MaxRollRate / 180f + p.MaxPitchRate / 90f) * 0.5f * Math.Min(1f, p.AngularResponse / 6f);
                stability[i] = 1f / Math.Max(0.1f, p.AngularResponse);
                size[i] = p.Wingspan;
            }

            Normalize(speed);
            Normalize(accel);
            Normalize(agility);
            Normalize(stability);
            Normalize(size);

            var bars = new AircraftStatBars[n];
            for (int i = 0; i < n; i++)
                bars[i] = new AircraftStatBars { Speed = speed[i], Acceleration = accel[i], Agility = agility[i], Stability = stability[i], Size = size[i] };
            return bars;
        }

        private static void Normalize(float[] v)
        {
            if (v.Length == 0) return;
            float min = float.MaxValue, max = float.MinValue;
            foreach (float x in v)
            {
                min = Math.Min(min, x);
                max = Math.Max(max, x);
            }
            for (int i = 0; i < v.Length; i++)
                v[i] = max - min < 1e-6f ? 0.6f : 0.15f + 0.85f * (v[i] - min) / (max - min);
        }
    }
}
