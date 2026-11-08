using System;

namespace TowerGlide.Core.Save
{
    /// <summary>
    /// 설정 화면 선택지(명세 5.5)와 표시 문구. UI는 값 → 문구 변환·다음 값 순환만 여기서 가져간다(테스트 가능).
    /// </summary>
    public static class SettingsOptions
    {
        public static readonly string[] QualityLabels = { "Performance", "Balanced", "Quality" };
        public static readonly int[] FrameRates = { 30, 60 };
        public const float SensitivityMin = 0.5f;
        public const float SensitivityMax = 1.5f;
        public const float SensitivityStep = 0.1f;

        public static int NextQuality(int current) => ((current < 0 ? 0 : current) + 1) % QualityLabels.Length;

        public static string QualityLabel(int tier) => QualityLabels[Math.Max(0, Math.Min(QualityLabels.Length - 1, tier))];

        public static int NextFrameRate(int current) => current >= 60 ? 30 : 60;

        /// <summary>슬라이더 값을 0.1 단위로 맞춘다(저장값이 0.73 같은 애매한 값이 되지 않게).</summary>
        public static float SnapSensitivity(float v)
        {
            if (float.IsNaN(v)) return 1f;
            v = Math.Max(SensitivityMin, Math.Min(SensitivityMax, v));
            return (float)Math.Round(v / SensitivityStep) * SensitivityStep;
        }

        public static string Percent(float v01) => $"{(int)Math.Round(Math.Max(0f, Math.Min(1f, v01)) * 100)}%";

        public static string OnOff(bool v) => v ? "켜짐" : "꺼짐";

        /// <summary>설정 초기화: 기본값으로 되돌리되 튜토리얼 완료 여부는 유지(초기화할 때마다 튜토리얼이 다시 뜨지 않게).</summary>
        public static SettingsData ResetKeepingTutorial(SettingsData current)
        {
            var d = new SettingsData { tutorialSeen = current != null && current.tutorialSeen };
            return d;
        }
    }
}
