using System;
using System.Collections.Generic;
using TowerGlide.Flight;
using UnityEngine;

namespace TowerGlide.Gameplay
{
    /// <summary>
    /// 기체 자식의 구형 트리거. ObstacleMarker가 붙은 장애물이 반경 안에 들어왔다가 충돌 없이 빠져나가면
    /// 최근접 거리로 근접도(0..1)를 계산해 이벤트를 낸다. 쿨다운·충돌 제외는 Core의 NearMissTracker가 판단.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class NearMissSensor : MonoBehaviour
    {
        [SerializeField] private AircraftFlightController aircraft;
        [Tooltip("이 거리(m) 이내를 스쳐야 근접 회피로 본다. 트리거 반경과 같게 둔다.")]
        [SerializeField] private float radius = 7f;
        [Tooltip("이 속도(m/s) 미만에서는 근접 회피를 인정하지 않는다(저속 '줍기' 방지).")]
        [SerializeField] private float minSpeed = 20f;

        private readonly Dictionary<Collider, Tracking> _inside = new Dictionary<Collider, Tracking>();

        private struct Tracking
        {
            public ObstacleMarker Marker;
            public float MinDistance;
        }

        /// <summary>(장애물, 근접도 0..1)</summary>
        public event Action<ObstacleMarker, float> NearMiss;

        private void Awake()
        {
            var sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = radius;
        }

        /// <summary>충돌하면 그 장애물의 추적을 버린다(충돌한 장애물은 근접 회피 아님).</summary>
        public void DiscardObstacle(ObstacleMarker marker)
        {
            if (marker == null) return;
            var remove = ListPool.Get();
            foreach (KeyValuePair<Collider, Tracking> kv in _inside)
                if (kv.Value.Marker == marker) remove.Add(kv.Key);
            foreach (Collider c in remove) _inside.Remove(c);
            ListPool.Release(remove);
        }

        public void ClearAll() => _inside.Clear();

        private void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger) return;
            ObstacleMarker marker = other.GetComponentInParent<ObstacleMarker>();
            if (marker == null || !marker.CountsForNearMiss) return;
            _inside[other] = new Tracking { Marker = marker, MinDistance = float.MaxValue };
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_inside.TryGetValue(other, out Tracking tr)) return;
            Vector3 p = transform.position;
            float d = Vector3.Distance(p, other.ClosestPoint(p));
            if (d < tr.MinDistance)
            {
                tr.MinDistance = d;
                _inside[other] = tr;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_inside.TryGetValue(other, out Tracking tr)) return;
            _inside.Remove(other);
            if (aircraft == null || aircraft.Speed < minSpeed || tr.MinDistance > radius) return;
            float proximity = 1f - Mathf.Clamp01(tr.MinDistance / radius);
            NearMiss?.Invoke(tr.Marker, proximity);
        }

        /// <summary>프레임마다 리스트를 새로 만들지 않기 위한 아주 작은 풀.</summary>
        private static class ListPool
        {
            private static readonly Stack<List<Collider>> Pool = new Stack<List<Collider>>();
            public static List<Collider> Get() => Pool.Count > 0 ? Pool.Pop() : new List<Collider>(4);
            public static void Release(List<Collider> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
