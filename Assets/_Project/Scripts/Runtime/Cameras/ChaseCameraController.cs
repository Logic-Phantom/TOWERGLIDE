using TowerGlide.Flight;
using UnityEngine;

// 주의: 네임스페이스를 'TowerGlide.Camera'로 두면 TowerGlide.* 전체에서 UnityEngine.Camera가 가려진다 → 'Cameras' 사용.
namespace TowerGlide.Cameras
{
    public enum CameraMode
    {
        Chase = 0,
        Cockpit = 1,
    }

    /// <summary>
    /// 3인칭 추적 카메라(명세 2.3). LateUpdate에서 보간된 기체 Transform을 부드럽게 따라간다.
    /// - 기체 기울기 일부만 카메라에 반영(bankFollow) → 속도감은 살리고 멀미는 줄인다.
    /// - 속도에 따라 FOV 소폭 증가, 충돌·부스트 시에만 짧은 흔들림(설정의 강도 배율 적용).
    /// - 기체 → 카메라 사이를 SphereCast해 건물 안으로 파고들지 않게 당긴다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCameraController : MonoBehaviour
    {
        [SerializeField] private AircraftFlightController target;

        [Header("Chase")]
        [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 3.2f, -11f);
        [SerializeField] private float lookAhead = 8f;
        [Tooltip("위치 추적 민첩도(클수록 바짝 붙음).")]
        [SerializeField] private float positionSharpness = 8f;
        [SerializeField] private float rotationSharpness = 10f;
        [Tooltip("기체 기울기를 카메라 롤에 반영하는 비율(0 = 수평 고정, 1 = 기체와 동일).")]
        [Range(0f, 1f)] [SerializeField] private float bankFollow = 0.35f;

        [Header("Cockpit")]
        [SerializeField] private Vector3 cockpitOffset = new Vector3(0f, 0.65f, 0.4f);

        [Header("FOV")]
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float maxExtraFov = 12f;
        [Tooltip("이 속도(m/s)에서 FOV 증가가 최대.")]
        [SerializeField] private float fovMaxAtSpeed = 80f;

        [Header("Collision")]
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField] private float collisionRadius = 0.6f;
        [SerializeField] private float minDistance = 2.5f;

        [Header("Shake")]
        [SerializeField] private float shakeDecay = 4f;
        [SerializeField] private float shakeFrequency = 22f;
        [SerializeField] private float maxShakeAngle = 2.5f;

        private Camera _camera;
        private float _shake;          // 0..1
        private float _shakeIntensity = 1f;
        private float _currentDistanceFactor = 1f;

        public CameraMode Mode { get; private set; } = CameraMode.Chase;
        public AircraftFlightController Target => target;

        public void SetTarget(AircraftFlightController aircraft)
        {
            target = aircraft;
            SnapToTarget();
        }

        /// <summary>설정 화면의 흔들림 강도(0..1).</summary>
        public void SetShakeIntensity(float value) => _shakeIntensity = Mathf.Clamp01(value);

        public void AddShake(float amount) => _shake = Mathf.Clamp01(_shake + amount);

        public void ToggleMode() => SetMode(Mode == CameraMode.Chase ? CameraMode.Cockpit : CameraMode.Chase);

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
            SnapToTarget();
        }

        /// <summary>복귀/재시작 직후 카메라를 즉시 제자리로(긴 보간으로 건물을 통과하는 장면 방지).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            ComputeDesired(out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (target == null) Debug.LogError($"[{nameof(ChaseCameraController)}] target 미연결", this);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // 일시정지(timeScale 0) 중에는 고정

            ComputeDesired(out Vector3 desiredPos, out Quaternion desiredRot);

            if (Mode == CameraMode.Chase)
            {
                float pk = 1f - Mathf.Exp(-positionSharpness * dt);
                float rk = 1f - Mathf.Exp(-rotationSharpness * dt);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(transform.position, desiredPos, pk),
                    Quaternion.Slerp(transform.rotation, desiredRot, rk));
            }
            else
            {
                transform.SetPositionAndRotation(desiredPos, desiredRot);
            }

            // 흔들림: 위치는 건드리지 않고 작은 회전만(멀미·충돌 판정 영향 최소화)
            if (_shake > 0.001f && _shakeIntensity > 0f)
            {
                float t = Time.time * shakeFrequency;
                float a = maxShakeAngle * _shake * _shake * _shakeIntensity;
                transform.rotation *= Quaternion.Euler(
                    (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * a,
                    (Mathf.PerlinNoise(0.2f, t) - 0.5f) * 2f * a,
                    0f);
            }
            _shake = Mathf.MoveTowards(_shake, 0f, shakeDecay * dt * Mathf.Max(0.25f, _shake));

            float speed01 = Mathf.Clamp01(target.Speed / Mathf.Max(1f, fovMaxAtSpeed));
            float fov = baseFov + maxExtraFov * speed01 * speed01;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
        }

        private void ComputeDesired(out Vector3 position, out Quaternion rotation)
        {
            Transform t = target.transform;
            if (Mode == CameraMode.Cockpit)
            {
                position = t.TransformPoint(cockpitOffset);
                rotation = t.rotation;
                return;
            }

            Vector3 up = Vector3.Slerp(Vector3.up, t.up, bankFollow);
            Vector3 forward = t.forward;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f) up = t.up; // 수직 상승/하강 시 특이점 회피
            Quaternion frame = Quaternion.LookRotation(forward, up);

            Vector3 pivot = t.position;
            Vector3 desired = pivot + frame * chaseOffset;

            // 카메라 충돌: 기체에서 원하는 위치까지 SphereCast
            Vector3 toCam = desired - pivot;
            float fullDistance = toCam.magnitude;
            float allowed = fullDistance;
            if (fullDistance > 0.01f &&
                Physics.SphereCast(pivot, collisionRadius, toCam / fullDistance, out RaycastHit hit, fullDistance, obstructionMask, QueryTriggerInteraction.Ignore) &&
                hit.rigidbody != target.Body)
            {
                allowed = Mathf.Max(minDistance, hit.distance);
            }
            float targetFactor = fullDistance > 0.01f ? allowed / fullDistance : 1f;
            // 당길 때는 즉시, 다시 멀어질 때는 천천히(튐 방지)
            _currentDistanceFactor = targetFactor < _currentDistanceFactor
                ? targetFactor
                : Mathf.MoveTowards(_currentDistanceFactor, targetFactor, Time.deltaTime * 1.5f);
            position = pivot + toCam * _currentDistanceFactor;

            Vector3 lookPoint = pivot + forward * lookAhead;
            rotation = Quaternion.LookRotation(lookPoint - position, up);
        }
    }
}
