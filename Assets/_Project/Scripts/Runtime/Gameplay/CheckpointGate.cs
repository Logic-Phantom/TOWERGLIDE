using System;
using TowerGlide.Flight;
using UnityEngine;

namespace TowerGlide.Gameplay
{
    public enum GateVisualState
    {
        Inactive,
        Next,
        Passed,
    }

    /// <summary>
    /// 링(게이트) 통과 판정. 링 중앙의 트리거 박스에 기체가 transform.forward 방향으로 들어오면 통과로 본다.
    /// 순서·중복 판정은 CheckpointCourse/CheckpointSequence가 담당한다.
    /// 링 프레임(실제 충돌체)은 별도 자식 콜라이더로 둔다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CheckpointGate : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer[] frameRenderers = Array.Empty<Renderer>();
        [SerializeField] private Color inactiveColor = new Color(0.55f, 0.6f, 0.7f, 1f);
        [SerializeField] private Color nextColor = new Color(1f, 0.62f, 0.1f, 1f);
        [SerializeField] private Color passedColor = new Color(0.25f, 0.85f, 0.45f, 1f);
        [Tooltip("다음 링일 때만 켜지는 월드 마커(멀리서도 보이는 빛기둥). 명세 1.3 '다음 목표 방향을 월드 마커로 안내'.")]
        [SerializeField] private GameObject beacon;
        [Tooltip("복귀 시 링 앞(-forward) 몇 m에서 다시 시작할지.")]
        [SerializeField] private float respawnBackOffset = 25f;

        private MaterialPropertyBlock _mpb;

        public int Index { get; private set; } = -1;
        public event Action<CheckpointGate> AircraftPassed;

        /// <summary>이 링을 통과한 뒤 복귀할 자세: 링 중앙, 진행 방향.</summary>
        public Pose RespawnPoseAfterPass => new Pose(transform.position + transform.forward * 5f, transform.rotation);
        /// <summary>이 링을 향해 다시 시작할 자세.</summary>
        public Pose ApproachPose => new Pose(transform.position - transform.forward * respawnBackOffset, transform.rotation);

        public void Initialize(int index)
        {
            Index = index;
            GetComponent<Collider>().isTrigger = true;
        }

        public void SetVisualState(GateVisualState state)
        {
            if (beacon != null && beacon.activeSelf != (state == GateVisualState.Next)) beacon.SetActive(state == GateVisualState.Next);
            _mpb ??= new MaterialPropertyBlock();
            Color c = state == GateVisualState.Next ? nextColor : state == GateVisualState.Passed ? passedColor : inactiveColor;
            foreach (Renderer r in frameRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger) return; // 기체의 NearMissSensor(트리거)가 먼저 닿아 조기 통과되는 것 방지
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || !rb.TryGetComponent(out AircraftFlightController _)) return;
            if (Vector3.Dot(rb.linearVelocity, transform.forward) <= 0f) return; // 역방향 통과 무시
            AircraftPassed?.Invoke(this);
        }
    }
}
