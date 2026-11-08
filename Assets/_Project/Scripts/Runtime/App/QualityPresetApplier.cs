using TowerGlide.Core.Save;
using UnityEngine;

namespace TowerGlide.App
{
    /// <summary>
    /// 그래픽 프리셋(Performance / Balanced / Quality) → Unity Quality Level.
    /// TOWERGLIDE ▸ 1. 프로젝트 설정 적용 메뉴가 같은 이름의 Quality Level과 URP 에셋 3종을 만든다.
    /// 이름이 없으면(설정 전) 아무것도 바꾸지 않는다.
    /// </summary>
    public static class QualityPresetApplier
    {
        public static readonly string[] LevelNames = { "Performance", "Balanced", "Quality" };

        public static void Apply(QualityTier tier)
        {
            string wanted = LevelNames[Mathf.Clamp((int)tier, 0, LevelNames.Length - 1)];
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != wanted) continue;
                if (QualitySettings.GetQualityLevel() != i) QualitySettings.SetQualityLevel(i, applyExpensiveChanges: true);
                return;
            }
        }
    }
}
