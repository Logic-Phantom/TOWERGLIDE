using UnityEngine;

namespace TowerGlide.World
{
    public enum BoundaryStatus
    {
        Inside,
        /// <summary>경고 구역 — HUD 안내만, 아직 조작 가능.</summary>
        Warning,
        /// <summary>완전 이탈 — 안전 지점으로 복귀.</summary>
        Outside,
    }

    /// <summary>
    /// 비행 가능 구역(명세 3.4). 텔레포트만 쓰지 않도록 Warning 단계(HUD 안내 + 화면 가장자리 효과)를 먼저 둔다.
    /// </summary>
    public sealed class FlightBoundary : MonoBehaviour
    {
        [Tooltip("비행 구역 크기(m). 중심은 이 오브젝트 위치.")]
        [SerializeField] private Vector3 size = new Vector3(800f, 500f, 800f);
        [Tooltip("구역 밖으로 이만큼 더 나가면 Outside.")]
        [SerializeField] private float warningMargin = 120f;
        [SerializeField] private float minAltitude = -5f;

        public Bounds InnerBounds => new Bounds(transform.position + Vector3.up * size.y * 0.5f, size);

        public BoundaryStatus Evaluate(Vector3 position)
        {
            if (position.y < minAltitude) return BoundaryStatus.Outside;
            Bounds inner = InnerBounds;
            if (inner.Contains(position)) return BoundaryStatus.Inside;
            Bounds outer = inner;
            outer.Expand(warningMargin * 2f);
            return outer.Contains(position) ? BoundaryStatus.Warning : BoundaryStatus.Outside;
        }

        /// <summary>구역 중심 방향(수평) — HUD 복귀 안내 화살표용.</summary>
        public Vector3 DirectionToCenter(Vector3 position)
        {
            Vector3 d = InnerBounds.center - position;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        private void OnDrawGizmosSelected()
        {
            Bounds b = InnerBounds;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            Gizmos.DrawWireCube(b.center, b.size + Vector3.one * warningMargin * 2f);
        }
    }
}
