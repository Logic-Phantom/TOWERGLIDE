using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerGlide.Input
{
    /// <summary>
    /// 누르고 있는 동안 IsHeld = true인 터치 버튼(부스트·에어브레이크), 또는 누를 때 Pressed 이벤트(카메라·복귀).
    /// 손가락이 버튼 밖으로 나가도 떼기 전까지 유지 — 엄지가 미끄러져도 부스트가 끊기지 않게 한다.
    /// </summary>
    public sealed class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic highlight;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color pressedColor = new Color(1f, 1f, 1f, 0.6f);

        private int _pointerId = int.MinValue;

        public bool IsHeld => _pointerId != int.MinValue;
        public event Action Pressed;

        private void OnDisable() => SetReleased();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld) return;
            _pointerId = eventData.pointerId;
            if (highlight != null) highlight.color = pressedColor;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) SetReleased();
        }

        private void SetReleased()
        {
            _pointerId = int.MinValue;
            if (highlight != null) highlight.color = normalColor;
        }
    }
}
