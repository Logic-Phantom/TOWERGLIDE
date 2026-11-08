using System;
using System.Collections.Generic;

namespace TowerGlide.Core.World
{
    /// <summary>건물 형태(아키타입). 실제 형상은 Editor의 TowerglideBuildingFactory가 아키타입별 모듈로 만든다.</summary>
    public enum BuildingArchetype
    {
        GlassTower = 0,
        ConcreteOffice = 1,
        SetbackTower = 2,
        TwinTower = 3,
        SlimNeedle = 4,
        LowCommercial = 5,
        PodiumTower = 6,
        HelipadRoof = 7,
        WaterTankRoof = 8,
        DarkGlassSlab = 9,
        CrownTop = 10,
        SteppedLowRise = 11,
    }

    [Serializable]
    public readonly struct BuildingArchetypeInfo
    {
        public readonly BuildingArchetype Archetype;
        public readonly float MinHeight;
        public readonly float MaxHeight;
        /// <summary>선택 가중치(같은 높이에서 얼마나 자주 나오는지).</summary>
        public readonly int Weight;

        public BuildingArchetypeInfo(BuildingArchetype archetype, float minHeight, float maxHeight, int weight)
        {
            Archetype = archetype;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            Weight = weight;
        }

        public bool Fits(float height) => height >= MinHeight && height <= MaxHeight;
    }

    /// <summary>
    /// 아키타입별 허용 높이 — 형태가 어울리는 높이에서만 쓰이도록(예: 바늘형 타워는 180m 이상).
    /// 15m~340m 모든 높이에 최소 1개 이상 맞는 아키타입이 있어야 한다(테스트로 보장).
    /// </summary>
    public static class BuildingCatalog
    {
        public const float MinSupportedHeight = 15f;
        public const float MaxSupportedHeight = 340f;

        public static readonly IReadOnlyList<BuildingArchetypeInfo> All = new[]
        {
            new BuildingArchetypeInfo(BuildingArchetype.GlassTower, 80f, 340f, 4),
            new BuildingArchetypeInfo(BuildingArchetype.ConcreteOffice, 30f, 160f, 4),
            new BuildingArchetypeInfo(BuildingArchetype.SetbackTower, 120f, 340f, 3),
            new BuildingArchetypeInfo(BuildingArchetype.TwinTower, 100f, 260f, 2),
            new BuildingArchetypeInfo(BuildingArchetype.SlimNeedle, 180f, 340f, 2),
            new BuildingArchetypeInfo(BuildingArchetype.LowCommercial, 15f, 60f, 4),
            new BuildingArchetypeInfo(BuildingArchetype.PodiumTower, 90f, 260f, 3),
            new BuildingArchetypeInfo(BuildingArchetype.HelipadRoof, 60f, 200f, 2),
            new BuildingArchetypeInfo(BuildingArchetype.WaterTankRoof, 30f, 120f, 3),
            new BuildingArchetypeInfo(BuildingArchetype.DarkGlassSlab, 80f, 220f, 3),
            new BuildingArchetypeInfo(BuildingArchetype.CrownTop, 140f, 340f, 2),
            new BuildingArchetypeInfo(BuildingArchetype.SteppedLowRise, 15f, 90f, 3),
        };

        /// <summary>높이에 맞는 아키타입을 가중 무작위로 고른다. 맞는 것이 없으면 가장 가까운 범위의 것.</summary>
        public static BuildingArchetype Pick(float height, Random rng)
        {
            int total = 0;
            foreach (BuildingArchetypeInfo a in All)
                if (a.Fits(height)) total += a.Weight;

            if (total > 0)
            {
                int roll = rng.Next(total);
                foreach (BuildingArchetypeInfo a in All)
                {
                    if (!a.Fits(height)) continue;
                    if (roll < a.Weight) return a.Archetype;
                    roll -= a.Weight;
                }
            }

            BuildingArchetype best = BuildingArchetype.ConcreteOffice;
            float bestGap = float.MaxValue;
            foreach (BuildingArchetypeInfo a in All)
            {
                float gap = height < a.MinHeight ? a.MinHeight - height : height - a.MaxHeight;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = a.Archetype;
                }
            }
            return best;
        }

        public static BuildingArchetypeInfo Info(BuildingArchetype archetype)
        {
            foreach (BuildingArchetypeInfo a in All)
                if (a.Archetype == archetype) return a;
            throw new ArgumentOutOfRangeException(nameof(archetype));
        }
    }
}
