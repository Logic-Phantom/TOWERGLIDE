using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerGlide.Input
{
    /// <summary>
    /// uGUI 기반 터치 조이스틱. 이 컴포넌트가 붙은 RectTransform 전체가 터치 영역이다(화면 왼쪽 절반 권장).
    /// Floating = 처음 닿은 곳이 중심, Fixed = 배경 위치 고정. 손가락을 떼면 즉시 0(명세 16 모바일 UX).
    /// 멀티터치: 처음 잡은 pointerId만 추적한다(오른손 버튼과 간섭 방지).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [Tooltip("핸들 최대 이동 반경(캔버스 단위 px).")]
        [SerializeField] private float radius = 110f;
        [SerializeField] private bool floating = true;

        private RectTransform _area;
        private Canvas _canvas;
        private Vector2 _restPosition;
        private int _pointerId = int.MinValue;

        public Vector2 Value { get; private set; }
        public bool IsPressed => _pointerId != int.MinValue;

        public bool Floating
        {
            get => floating;
            set
            {
                floating = value;
                if (!IsPressed && background != null) background.anchoredPosition = _restPosition;
            }
        }

        private void Awake()
        {
            _area = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (background != null) _restPosition = background.anchoredPosition;
            if (background == null || handle == null) Debug.LogError($"[{nameof(VirtualJoystick)}] background/handle 미연결", this);
        }

        private void OnDisable() => Release();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsPressed) return;
            _pointerId = eventData.pointerId;
            if (floating && background != null && ScreenToArea(eventData.position, eventData.pressEventCamera, out Vector2 local))
                background.anchoredPosition = local;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId || background == null || handle == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            Vector2 clamped = Vector2.ClampMagnitude(local, radius);
            handle.anchoredPosition = clamped;
            Value = clamped / radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) Release();
        }

        private void Release()
        {
            _pointerId = int.MinValue;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (background != null) background.anchoredPosition = _restPosition;
        }

        private bool ScreenToArea(Vector2 screen, Camera cam, out Vector2 local)
        {
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, cam, out local);
            // background의 앵커가 area 중앙이 아닐 수 있으므로 anchoredPosition 기준으로 보정
            if (ok && background != null)
            {
                Vector2 anchorCenter = Vector2.Scale((background.anchorMin + background.anchorMax) * 0.5f - _area.pivot, _area.rect.size);
                local -= anchorCenter;
            }
            return ok && _canvas != null;
        }
    }
}
