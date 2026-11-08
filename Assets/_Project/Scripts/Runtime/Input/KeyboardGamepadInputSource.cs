using TowerGlide.Core.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerGlide.Input
{
    /// <summary>
    /// 에디터/PC 테스트용 키보드 + 게임패드 입력(Input System 직접 폴링 — 액션 에셋 없이 동작).
    /// 키보드: W/S 피치(W = 기수 상승), A/D 롤, Q/E 요, ↑/↓ 스로틀, Shift 부스트, Space 에어브레이크,
    ///         C 카메라, R 복귀, Esc/P 일시정지.
    /// 게임패드: 왼쪽 스틱 롤/피치, 숄더 요, 오른쪽 트리거 스로틀/왼쪽 트리거 에어브레이크, South 부스트,
    ///         North 카메라, West 복귀, Start 일시정지.
    /// </summary>
    public sealed class KeyboardGamepadInputSource : MonoBehaviour, IFlightInputSource
    {
        [SerializeField] private FlightInputRouter router;

        public bool IsActive => isActiveAndEnabled;

        private void Update()
        {
            if (router == null) return;
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;

            if ((kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))
                router.RequestCameraToggle();
            if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))
                router.RequestRecover();
            if ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) ||
                (pad != null && pad.startButton.wasPressedThisFrame))
                router.RequestPause();
        }

        public FlightInput ReadRaw()
        {
            FlightInput input = FlightInput.Neutral;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                input.Pitch = Axis(kb.sKey.isPressed, kb.wKey.isPressed);
                input.Roll = Axis(kb.aKey.isPressed, kb.dKey.isPressed);
                input.Yaw = Axis(kb.qKey.isPressed, kb.eKey.isPressed);
                input.Throttle = Axis(kb.downArrowKey.isPressed, kb.upArrowKey.isPressed);
                input.Boost = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                input.AirBrake = kb.spaceKey.isPressed;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                var padInput = new FlightInput
                {
                    Roll = ls.x,
                    Pitch = ls.y,
                    Yaw = Axis(pad.leftShoulder.isPressed, pad.rightShoulder.isPressed),
                    Throttle = pad.rightTrigger.ReadValue(),
                    Boost = pad.buttonSouth.isPressed,
                    AirBrake = pad.leftTrigger.ReadValue() > 0.5f,
                };
                input = FlightInput.Combine(input, padInput);
            }
            return input;
        }

        private static float Axis(bool negative, bool positive) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
