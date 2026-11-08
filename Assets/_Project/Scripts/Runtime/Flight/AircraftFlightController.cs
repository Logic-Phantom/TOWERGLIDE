using TowerGlide.Core.Flight;
using TowerGlide.Core.Input;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>
    /// Rigidbody 기반 아케이드 비행 물리. 입력은 FlightInputRouter가 SetInput으로만 넣는다(입력 장치·UI를 모름).
    /// 모든 힘은 FixedUpdate에서 ForceMode.Acceleration으로 적용한다.
    ///
    /// 축 규칙(Unity 왼손 좌표): +Pitch 입력 = 기수 상승(local -X 회전), +Roll = 오른쪽으로 기울기(local -Z), +Yaw = 오른쪽(local +Y).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class AircraftFlightController : MonoBehaviour
    {
        [SerializeField] private AircraftFlightTuning tuning;
        [Tooltip("고도 0 기준 높이(지면).")]
        [SerializeField] private float seaLevel;

        private Rigidbody _rb;
        private BoostMeter _boost;
        private FlightInput _input;
        private float _controlLockUntil;

        public AircraftFlightTuning Tuning => tuning;
        public Rigidbody Body => _rb;
        public float Speed { get; private set; }
        public float SpeedKmh => Speed * 3.6f;
        public float Altitude => transform.position.y - seaLevel;
        public float AngleOfAttackDeg { get; private set; }
        /// <summary>기울기(deg). 오른쪽 기울기가 양수.</summary>
        public float BankDeg { get; private set; }
        public bool IsBoosting => _boost != null && _boost.IsBoosting;
        public float BoostNormalized => _boost?.Normalized ?? 1f;
        public bool IsBoostLocked => _boost != null && _boost.IsLocked;
        public bool ControlsLocked => Time.time < _controlLockUntil;

        public void SetTuning(AircraftFlightTuning value)
        {
            tuning = value;
            _boost = tuning != null ? new BoostMeter(tuning.ToBoostSettings()) : null;
        }

        /// <summary>Update 단계에서 호출. 실제 소비는 FixedUpdate.</summary>
        public void SetInput(in FlightInput input) => _input = input.Sanitized();

        /// <summary>충돌 직후 등 짧은 시간 조작을 무시한다.</summary>
        public void LockControls(float seconds) => _controlLockUntil = Mathf.Max(_controlLockUntil, Time.time + seconds);

        /// <summary>복귀/재시작: 위치·자세·속도를 즉시 설정한다(보간 튐 방지를 위해 Transform도 같이 이동).</summary>
        public void ResetTo(Vector3 position, Quaternion rotation, float initialSpeed)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.position = position;
            _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _rb.linearVelocity = rotation * Vector3.forward * Mathf.Max(0f, initialSpeed);
            _rb.angularVelocity = Vector3.zero;
            _input = FlightInput.Neutral;
            _boost?.Refill();
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = true;
            _rb.linearDamping = 0f;   // 항력은 직접 계산
            _rb.angularDamping = 0f;  // 각속도 감쇠는 목표 각속도 추종으로 처리
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 10f;
            if (tuning != null && _boost == null) _boost = new BoostMeter(tuning.ToBoostSettings());
            if (tuning == null) Debug.LogError($"[{nameof(AircraftFlightController)}] AircraftFlightTuning이 연결되지 않았습니다.", this);
        }

        private void FixedUpdate()
        {
            if (tuning == null || _rb.isKinematic) return;

            float dt = Time.fixedDeltaTime;
            FlightInput input = ControlsLocked ? FlightInput.Neutral : _input;

            Vector3 velocity = _rb.linearVelocity;
            float speed = velocity.magnitude;
            Speed = speed;
            Transform t = transform;
            Vector3 forward = t.forward;
            Vector3 localVelocity = t.InverseTransformDirection(velocity);

            float aoa = speed > 0.5f ? Mathf.Atan2(-localVelocity.y, localVelocity.z) : 0f;
            AngleOfAttackDeg = aoa * Mathf.Rad2Deg;
            float bankRad = Mathf.Asin(Mathf.Clamp(-t.right.y, -1f, 1f));
            // 뒤집힌 상태(up.y < 0)에서는 asin이 90° 이상을 표현하지 못하므로 보정
            if (t.up.y < 0f) bankRad = Mathf.Sign(bankRad == 0f ? 1f : bankRad) * Mathf.PI - bankRad;
            BankDeg = bankRad * Mathf.Rad2Deg;

            // ---- 추력 / 부스트 ----
            bool boosting = _boost.Tick(dt, input.Boost);
            float targetSpeed = boosting
                ? tuning.boostMaxSpeed
                : AeroModel.TargetSpeed(input.Throttle, tuning.minFlightSpeed, tuning.cruiseSpeed, tuning.maxSpeed);
            float forwardSpeed = Vector3.Dot(velocity, forward);
            float maxThrust = tuning.maxThrustAcceleration + (boosting ? tuning.boostExtraAcceleration : 0f);
            float thrust = AeroModel.ThrustAcceleration(forwardSpeed, targetSpeed, maxThrust, tuning.thrustResponsiveness);
            if (input.AirBrake) thrust = 0f;
            _rb.AddForce(forward * thrust, ForceMode.Acceleration);

            if (speed > 0.5f)
            {
                Vector3 velocityDir = velocity / speed;

                // ---- 양력: 속도에 수직, 날개 평면 위쪽. 기울이면 양력도 기울어 선회가 생긴다 ----
                Vector3 liftDir = Vector3.Cross(velocityDir, t.right).normalized;
                float speedRatio = Mathf.Clamp01(speed / tuning.minFlightSpeed);
                float baseLift = -Physics.gravity.y * tuning.gravityCompensation * speedRatio * speedRatio;
                float cl = AeroModel.LiftCoefficient(aoa, tuning.liftSlope, tuning.stallAngleDeg * Mathf.Deg2Rad);
                float aoaLift = AeroModel.LiftAcceleration(speed, cl, tuning.liftFactor, tuning.minFlightSpeed);
                _rb.AddForce(liftDir * (baseLift + aoaLift), ForceMode.Acceleration);

                // ---- 항력 (+ 최고 속도 초과분 소프트 캡) ----
                float drag = AeroModel.DragAcceleration(speed, tuning.dragFactor, tuning.airBrakeDragMultiplier, input.AirBrake);
                float cap = boosting ? tuning.boostMaxSpeed : tuning.maxSpeed;
                if (speed > cap) drag += (speed - cap) * 2f;
                _rb.AddForce(-velocityDir * drag, ForceMode.Acceleration);

                // ---- 속도 벡터를 기수 방향으로 정렬(아케이드 보조) ----
                float authorityAlign = AeroModel.ControlAuthority(speed, tuning.minFlightSpeed, tuning.cruiseSpeed,
                    tuning.boostMaxSpeed, tuning.lowSpeedAuthority, 1f);
                Vector3 aligned = Vector3.RotateTowards(velocity, forward * speed, tuning.velocityAlignment * authorityAlign * dt, 0f);
                _rb.AddForce((aligned - velocity) / dt, ForceMode.Acceleration);
            }

            // ---- 회전: 목표 각속도 추종 (손을 떼면 0으로 수렴 = 자연 감쇠) ----
            float authority = AeroModel.ControlAuthority(speed, tuning.minFlightSpeed, tuning.cruiseSpeed,
                tuning.maxSpeed, tuning.lowSpeedAuthority, tuning.highSpeedAuthority);
            float bank01 = Mathf.Clamp(bankRad / (Mathf.PI * 0.5f), -1f, 1f);
            float absBankSin = Mathf.Abs(Mathf.Sin(bankRad));

            float pitchRate = -input.Pitch * tuning.maxPitchRate * authority;            // 기수 상승 = -X
            pitchRate -= absBankSin * tuning.turnPitchAssistRate * Mathf.Abs(input.Roll) * authority;
            if (speed < tuning.minFlightSpeed)
                pitchRate += tuning.stallNoseDropRate * (1f - speed / tuning.minFlightSpeed); // 실속 시 기수 처짐

            float rollRate = -input.Roll * tuning.maxRollRate * authority;               // 오른쪽 기울기 = -Z
            if (Mathf.Abs(input.Roll) < 0.05f) rollRate += bank01 * tuning.autoLevelRate; // 자동 수평
            float bankLimitRad = tuning.maxBankAngleDeg * Mathf.Deg2Rad;
            if (tuning.maxBankAngleDeg < 179f && Mathf.Abs(bankRad) >= bankLimitRad && Mathf.Sign(-rollRate) == Mathf.Sign(bankRad))
                rollRate = bank01 * tuning.autoLevelRate;                               // 최대 기울기 제한

            float yawRate = input.Yaw * tuning.maxYawRate * authority
                            + bank01 * tuning.coordinatedTurnYawRate * authority;       // 협조 선회

            Vector3 targetLocalAngular = new Vector3(pitchRate, yawRate, rollRate) * Mathf.Deg2Rad;
            Vector3 currentLocalAngular = t.InverseTransformDirection(_rb.angularVelocity);
            Vector3 angularAccel = (targetLocalAngular - currentLocalAngular) * tuning.angularResponse;
            _rb.AddRelativeTorque(angularAccel, ForceMode.Acceleration);
        }
    }
}
