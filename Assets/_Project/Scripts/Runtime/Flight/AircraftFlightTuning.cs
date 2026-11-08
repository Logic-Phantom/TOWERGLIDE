using TowerGlide.Core.Flight;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>
    /// 비행 파라미터. 모든 속도는 m/s, 가속도는 m/s²(질량 무관, ForceMode.Acceleration — D-008), 각속도는 deg/s.
    /// 값은 시작점일 뿐이며 실제 플레이로 조정한다(docs/FLIGHT_PHYSICS.md).
    /// 생성: Assets ▸ Create ▸ TOWERGLIDE ▸ Aircraft Flight Tuning (또는 TOWERGLIDE ▸ 1. 프로젝트 설정 적용 메뉴가 기본 에셋 생성)
    /// </summary>
    [CreateAssetMenu(menuName = "TOWERGLIDE/Aircraft Flight Tuning", fileName = "AircraftFlightTuning")]
    public sealed class AircraftFlightTuning : ScriptableObject
    {
        [Header("Speed (m/s) — 30 m/s ≈ 108 km/h")]
        [Tooltip("이 속도 미만에서 양력이 줄고 기수가 처진다(실속 표현).")]
        [Min(1f)] public float minFlightSpeed = 22f;
        [Tooltip("스로틀 입력이 없을 때 유지하는 자동 순항 속도.")]
        [Min(1f)] public float cruiseSpeed = 30f;
        [Tooltip("스로틀 최대 시 목표 속도(부스트 제외).")]
        [Min(1f)] public float maxSpeed = 65f;
        [Tooltip("부스트 중 목표 속도.")]
        [Min(1f)] public float boostMaxSpeed = 80f;

        [Header("Thrust")]
        [Min(0f)] public float maxThrustAcceleration = 18f;
        [Tooltip("목표 속도와의 차이(m/s)당 추력 가속도. 클수록 빠르게 목표 속도에 도달.")]
        [Min(0f)] public float thrustResponsiveness = 1.2f;
        [Min(0f)] public float boostExtraAcceleration = 14f;

        [Header("Lift / Drag")]
        [Tooltip("최소 비행 속도 이상에서 중력을 상쇄하는 기본 양력 비율(1 = 수평 비행 유지).")]
        [Range(0f, 1.5f)] public float gravityCompensation = 1f;
        [Tooltip("받음각 1 rad당 양력 계수.")]
        [Min(0f)] public float liftSlope = 4f;
        [Range(5f, 30f)] public float stallAngleDeg = 16f;
        [Tooltip("받음각 양력 = Cl × liftFactor × v².")]
        [Min(0f)] public float liftFactor = 0.004f;
        [Tooltip("항력 = dragFactor × v². 최고 속도에서 추력과 균형이 맞도록 조정.")]
        [Min(0f)] public float dragFactor = 0.0035f;
        [Min(1f)] public float airBrakeDragMultiplier = 4f;
        [Tooltip("속도 벡터를 기수 방향으로 끌어당기는 비율(rad/s). 아케이드 조작감의 핵심 — 높을수록 '레일 위' 느낌.")]
        [Range(0f, 5f)] public float velocityAlignment = 1.6f;

        [Header("Rotation rates (deg/s) — 목표 각속도")]
        [Min(0f)] public float maxPitchRate = 75f;
        [Min(0f)] public float maxRollRate = 160f;
        [Min(0f)] public float maxYawRate = 30f;
        [Tooltip("목표 각속도로 수렴하는 세기. 클수록 즉각적, 작을수록 묵직.")]
        [Min(0.1f)] public float angularResponse = 6f;

        [Header("Assists (초보자 보조)")]
        [Tooltip("기울기(bank)에 비례해 기수를 자동으로 돌리는 협조 선회 yaw (deg/s at 90° bank).")]
        [Min(0f)] public float coordinatedTurnYawRate = 55f;
        [Tooltip("선회 중 기수를 살짝 당겨 고도 손실을 줄이는 보조 (deg/s at 90° bank).")]
        [Min(0f)] public float turnPitchAssistRate = 18f;
        [Tooltip("Roll 입력이 없을 때 수평으로 되돌리는 세기 (deg/s per 90° bank). 0 = 끔.")]
        [Min(0f)] public float autoLevelRate = 40f;
        [Tooltip("초보자 최대 기울기(deg). 180 = 제한 없음.")]
        [Range(30f, 180f)] public float maxBankAngleDeg = 75f;
        [Tooltip("실속 시 기수가 내려가는 속도(deg/s at 0 speed).")]
        [Min(0f)] public float stallNoseDropRate = 25f;

        [Header("Control authority by speed (0..1)")]
        [Range(0.1f, 1f)] public float lowSpeedAuthority = 0.35f;
        [Range(0.1f, 1f)] public float highSpeedAuthority = 0.65f;

        [Header("Boost")]
        [Min(0.1f)] public float boostCapacitySeconds = 3f;
        [Min(0.1f)] public float boostFullRechargeSeconds = 6f;
        [Min(0f)] public float boostRechargeDelaySeconds = 1f;
        [Range(0f, 1f)] public float boostMinRatioToRestart = 0.25f;

        public BoostSettings ToBoostSettings() => new BoostSettings
        {
            CapacitySeconds = boostCapacitySeconds,
            FullRechargeSeconds = boostFullRechargeSeconds,
            RechargeDelaySeconds = boostRechargeDelaySeconds,
            MinRatioToRestart = boostMinRatioToRestart,
        };

        private void OnValidate()
        {
            cruiseSpeed = Mathf.Max(cruiseSpeed, minFlightSpeed);
            maxSpeed = Mathf.Max(maxSpeed, cruiseSpeed);
            boostMaxSpeed = Mathf.Max(boostMaxSpeed, maxSpeed);
        }
    }
}
