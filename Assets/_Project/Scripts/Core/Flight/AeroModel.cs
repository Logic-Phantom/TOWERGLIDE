using System;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Flight
{
    /// <summary>
    /// 아케이드 비행용 간소화 공력 계산(순수 함수). 결과는 질량 무관 가속도(m/s²)이며
    /// Runtime의 AircraftFlightController가 ForceMode.Acceleration으로 적용한다 (D-008).
    /// </summary>
    public static class AeroModel
    {
        /// <summary>
        /// 받음각(라디안)에 대한 양력 계수. 실속각까지 선형 증가, 이후 완만히 감소(급격한 실속 없음 — 명세 2.2).
        /// </summary>
        public static float LiftCoefficient(float angleOfAttackRad, float liftSlope, float stallAngleRad)
        {
            float a = Math.Abs(angleOfAttackRad);
            float sign = Math.Sign(angleOfAttackRad);
            if (stallAngleRad <= 0f) return 0f;
            if (a <= stallAngleRad) return sign * liftSlope * a;

            // 실속 이후: 최대값에서 실속각의 2배 지점까지 40%로 선형 감소, 그 이후 40% 유지
            float peak = liftSlope * stallAngleRad;
            float t = FlightMath.Clamp01((a - stallAngleRad) / stallAngleRad);
            return sign * FlightMath.Lerp(peak, peak * 0.4f, t);
        }

        /// <summary>
        /// 양력 가속도 크기. 속도 제곱에 비례하되, 최소 비행 속도 미만에서는 추가로 감쇠해 "저속에서 기수가 처진다".
        /// </summary>
        public static float LiftAcceleration(float airspeed, float liftCoefficient, float liftFactor, float minFlightSpeed)
        {
            if (airspeed <= 0f) return 0f;
            float lowSpeedFade = minFlightSpeed > 0f ? FlightMath.Clamp01(airspeed / minFlightSpeed) : 1f;
            return liftCoefficient * liftFactor * airspeed * airspeed * lowSpeedFade;
        }

        /// <summary>항력 가속도 크기(속도 제곱 비례). 에어브레이크는 계수를 곱한다.</summary>
        public static float DragAcceleration(float airspeed, float dragFactor, float airBrakeMultiplier, bool airBrake)
        {
            float k = dragFactor * (airBrake ? Math.Max(1f, airBrakeMultiplier) : 1f);
            return k * airspeed * airspeed;
        }

        /// <summary>
        /// 속도별 조종 권한(0..1). 너무 느리면 둔해지고(최소 maxAuthorityLow), 아주 빠르면 과민해지지 않도록 줄인다.
        /// </summary>
        public static float ControlAuthority(float airspeed, float minFlightSpeed, float cruiseSpeed, float maxSpeed,
            float lowSpeedAuthority, float highSpeedAuthority)
        {
            if (airspeed <= minFlightSpeed) return FlightMath.Lerp(lowSpeedAuthority, 1f, airspeed / Math.Max(0.01f, minFlightSpeed));
            if (airspeed <= cruiseSpeed) return 1f;
            float t = FlightMath.InverseLerp(cruiseSpeed, maxSpeed, airspeed);
            return FlightMath.Lerp(1f, highSpeedAuthority, t);
        }

        /// <summary>
        /// 자동 스로틀: 목표 속도로 수렴하는 추력 가속도. 목표 이상이면 0(감속은 항력/에어브레이크 담당).
        /// </summary>
        public static float ThrustAcceleration(float airspeed, float targetSpeed, float maxThrustAccel, float responsiveness)
        {
            if (targetSpeed <= 0f || airspeed >= targetSpeed) return 0f;
            float error = targetSpeed - airspeed;
            return Math.Min(maxThrustAccel, error * responsiveness);
        }

        /// <summary>스로틀 입력(-1..1)을 목표 속도로 변환. 0 = 순항.</summary>
        public static float TargetSpeed(float throttle, float minFlightSpeed, float cruiseSpeed, float maxSpeed)
        {
            throttle = FlightMath.Clamp(throttle, -1f, 1f);
            return throttle >= 0f
                ? FlightMath.Lerp(cruiseSpeed, maxSpeed, throttle)
                : FlightMath.Lerp(cruiseSpeed, minFlightSpeed, -throttle);
        }
    }
}
