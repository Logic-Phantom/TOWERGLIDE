using System;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Input
{
    /// <summary>
    /// 사용자 설정(감도·데드존·반전)에 따라 원시 축 입력을 다듬는다.
    /// </summary>
    [Serializable]
    public struct InputShapingSettings
    {
        /// <summary>0..0.5. 이 값 이하의 입력은 0으로 본다.</summary>
        public float DeadZone;
        /// <summary>0..1. 0 = 선형, 1 = 3차 곡선(중앙에서 둔감, 끝에서 민감).</summary>
        public float Expo;
        /// <summary>0.25..2. 최종 배율.</summary>
        public float Sensitivity;
        public bool InvertPitch;

        public static InputShapingSettings Default => new InputShapingSettings
        {
            DeadZone = 0.08f,
            Expo = 0.35f,
            Sensitivity = 1f,
            InvertPitch = false,
        };
    }

    public static class InputShaping
    {
        /// <summary>
        /// 데드존을 적용하고 남은 구간을 0..1로 다시 펴서(rescale) 데드존 경계에서 값이 튀지 않게 한다.
        /// </summary>
        public static float ApplyDeadZone(float value, float deadZone)
        {
            value = FlightMath.Sanitize(value);
            deadZone = FlightMath.Clamp(deadZone, 0f, 0.95f);
            float magnitude = Math.Abs(value);
            if (magnitude <= deadZone) return 0f;
            float rescaled = (Math.Min(magnitude, 1f) - deadZone) / (1f - deadZone);
            return Math.Sign(value) * rescaled;
        }

        /// <summary>선형과 3차 곡선을 expo 비율로 섞는다. 부호와 끝값(±1)은 유지된다.</summary>
        public static float ApplyExpo(float value, float expo)
        {
            expo = FlightMath.Clamp01(expo);
            return (1f - expo) * value + expo * value * value * value;
        }

        public static float ShapeAxis(float raw, InputShapingSettings settings)
        {
            float v = ApplyDeadZone(raw, settings.DeadZone);
            v = ApplyExpo(v, settings.Expo);
            float sensitivity = FlightMath.Clamp(settings.Sensitivity <= 0f ? 1f : settings.Sensitivity, 0.25f, 2f);
            return FlightMath.Clamp(v * sensitivity, -1f, 1f);
        }

        public static FlightInput Shape(FlightInput raw, InputShapingSettings settings)
        {
            raw = raw.Sanitized();
            float pitch = ShapeAxis(raw.Pitch, settings);
            return new FlightInput
            {
                Pitch = settings.InvertPitch ? -pitch : pitch,
                Roll = ShapeAxis(raw.Roll, settings),
                Yaw = ShapeAxis(raw.Yaw, settings),
                Throttle = ApplyDeadZone(raw.Throttle, settings.DeadZone),
                Boost = raw.Boost,
                AirBrake = raw.AirBrake,
            };
        }
    }
}
