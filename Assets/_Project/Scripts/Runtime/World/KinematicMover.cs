using UnityEngine;

namespace TowerGlide.World
{
    /// <summary>
    /// 이동·회전 장애물(드론, 크레인 붐, 화물). Kinematic Rigidbody + MovePosition/MoveRotation으로 움직여
    /// 물리 충돌이 정확하고 예측 가능하게 한다(명세 2.5: 피할 수 있어야 함 — 속도·경로가 일정).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KinematicMover : MonoBehaviour
    {
        [Header("Path (local offsets from start, ping-pong)")]
        [SerializeField] private Vector3[] waypoints = { Vector3.zero, new Vector3(0f, 0f, 40f) };
        [Min(0f)] [SerializeField] private float speed = 8f;

        [Header("Rotation")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [Tooltip("deg/s. 0 = 회전 없음.")]
        [SerializeField] private float rotationSpeed;

        private Rigidbody _rb;
        private Vector3 _origin;
        private Quaternion _startRotation;
        private int _target = 1;
        private int _direction = 1;
        private float _angle;

        public void Configure(Vector3[] localWaypoints, float moveSpeed, Vector3 axis, float degPerSecond)
        {
            waypoints = localWaypoints;
            speed = moveSpeed;
            rotationAxis = axis;
            rotationSpeed = degPerSecond;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _origin = transform.position;
            _startRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (waypoints != null && waypoints.Length >= 2 && speed > 0f)
            {
                Vector3 goal = _origin + waypoints[_target];
                Vector3 next = Vector3.MoveTowards(_rb.position, goal, speed * dt);
                _rb.MovePosition(next);
                if ((next - goal).sqrMagnitude < 0.01f)
                {
                    if (_target + _direction >= waypoints.Length || _target + _direction < 0) _direction = -_direction;
                    _target += _direction;
                }
            }

            if (Mathf.Abs(rotationSpeed) > 0.001f)
            {
                _angle = Mathf.Repeat(_angle + rotationSpeed * dt, 360f);
                _rb.MoveRotation(_startRotation * Quaternion.AngleAxis(_angle, rotationAxis.normalized));
            }
        }
    }
}
