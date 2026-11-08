using TowerGlide.Flight;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerGlide.UI
{
    /// <summary>
    /// 격납고 3D 미리보기(애드온 명세 14.5): 선택 기체 프리팹의 "보이는 부분만" 받침대 위에 띄워 천천히 돌린다.
    /// 이 컴포넌트가 붙은 UI 영역을 드래그하면 직접 돌려 볼 수 있다. 물리·입력·센서 컴포넌트는 제거해 비행 로직이 돌지 않게 한다.
    /// </summary>
    public sealed class AircraftPreviewController : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Transform pedestal;
        [SerializeField] private float autoRotateDegPerSec = 18f;
        [SerializeField] private float dragDegPerPixel = 0.3f;
        [Tooltip("받침대 위 기체 크기를 맞출 목표 길이(m) — 여객기도 화면 안에 들어오게.")]
        [SerializeField] private float targetSize = 12f;

        private GameObject _current;
        private bool _dragging;

        public void Show(AircraftDefinition def)
        {
            if (_current != null) Destroy(_current);
            if (def == null || def.prefab == null || pedestal == null) return;

            var holder = new GameObject($"Preview_{def.id}");
            holder.SetActive(false); // 자식 Awake 실행 전에 컴포넌트를 제거하기 위해 비활성 상태로 생성
            holder.transform.SetParent(pedestal, false);
            GameObject model = Instantiate(def.prefab, holder.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // MonoBehaviour 먼저(RequireComponent 의존), 그다음 물리
            foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);

            float size = Mathf.Max(def.length, def.wingspan, 0.1f);
            holder.transform.localScale = Vector3.one * (targetSize / size);
            holder.SetActive(true);
            _current = holder;
        }

        private void Update()
        {
            if (pedestal != null && !_dragging) pedestal.Rotate(0f, autoRotateDegPerSec * Time.unscaledDeltaTime, 0f, Space.World);
        }

        public void OnPointerDown(PointerEventData eventData) => _dragging = true;

        public void OnPointerUp(PointerEventData eventData) => _dragging = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (pedestal != null) pedestal.Rotate(0f, -eventData.delta.x * dragDegPerPixel, 0f, Space.World);
        }
    }
}
