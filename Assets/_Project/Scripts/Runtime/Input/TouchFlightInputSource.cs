using TowerGlide.Core.Input;
using UnityEngine;

namespace TowerGlide.Input
{
    /// <summary>
    /// 모바일 터치 조작: 왼쪽 조이스틱 X = Roll, Y = Pitch(위로 밀면 기수 상승 — 반전은 설정).
    /// 오른쪽 버튼: 부스트(홀드)·에어브레이크(홀드)·카메라·복귀(탭) — 탭 버튼은 Router의 Request*로 전달.
    /// </summary>
    public sealed class TouchFlightInputSource : MonoBehaviour, IFlightInputSource
    {
        [SerializeField] private VirtualJoystick stick;
        [SerializeField] private TouchHoldButton boostButton;
        [SerializeField] private TouchHoldButton airBrakeButton;
        [SerializeField] private TouchHoldButton cameraButton;
        [SerializeField] private TouchHoldButton recoverButton;
        [SerializeField] private TouchHoldButton pauseButton;
        [SerializeField] private FlightInputRouter router;

        public bool IsActive => isActiveAndEnabled;

        private void OnEnable()
        {
            if (router == null) return;
            if (cameraButton != null) cameraButton.Pressed += router.RequestCameraToggle;
            if (recoverButton != null) recoverButton.Pressed += router.RequestRecover;
            if (pauseButton != null) pauseButton.Pressed += router.RequestPause;
        }

        private void OnDisable()
        {
            if (router == null) return;
            if (cameraButton != null) cameraButton.Pressed -= router.RequestCameraToggle;
            if (recoverButton != null) recoverButton.Pressed -= router.RequestRecover;
            if (pauseButton != null) pauseButton.Pressed -= router.RequestPause;
        }

        public FlightInput ReadRaw()
        {
            Vector2 v = stick != null ? stick.Value : Vector2.zero;
            return new FlightInput
            {
                Roll = v.x,
                Pitch = v.y,
                Boost = boostButton != null && boostButton.IsHeld,
                AirBrake = airBrakeButton != null && airBrakeButton.IsHeld,
            };
        }
    }
}
