using System;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>
    /// 충돌 감지만 담당한다. 복귀·점수 처리는 FlightSession이 이벤트를 받아 결정한다(책임 분리).
    /// 같은 충돌로 이벤트가 연속 발생하지 않도록 쿨다운을 둔다.
    /// </summary>
    [RequireComponent(typeof(AircraftFlightController))]
    public sealed class AircraftCollisionHandler : MonoBehaviour
    {
        [Tooltip("이 상대 속도(m/s) 미만의 접촉은 '스침'으로 보고 무시한다.")]
        [SerializeField] private float minImpactSpeed = 6f;
        [SerializeField] private float cooldownSeconds = 1.5f;
        [Tooltip("충돌 직후 조작을 잠그는 시간(끼임·연속 튕김 방지).")]
        [SerializeField] private float controlLockSeconds = 0.4f;

        private AircraftFlightController _aircraft;
        private float _lastCrashTime = float.NegativeInfinity;

        /// <summary>(충돌 지점, 충돌한 콜라이더, 충격 속도 m/s)</summary>
        public event Action<Vector3, Collider, float> Crashed;

        private void Awake() => _aircraft = GetComponent<AircraftFlightController>();

        private void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            if (impact < minImpactSpeed) return;
            if (Time.time - _lastCrashTime < cooldownSeconds) return;
            _lastCrashTime = Time.time;

            _aircraft.LockControls(controlLockSeconds);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Crashed?.Invoke(point, collision.collider, impact);
        }
    }
}
