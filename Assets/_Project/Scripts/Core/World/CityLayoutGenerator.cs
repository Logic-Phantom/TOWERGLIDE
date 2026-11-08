using System;
using System.Collections.Generic;

namespace TowerGlide.Core.World
{
    public enum LotKind
    {
        Building,
        /// <summary>광장·공터(무작위) — 실루엣 다양화.</summary>
        Plaza,
        /// <summary>코스가 너무 낮게 지나가 건물을 세울 수 없는 자리.</summary>
        CourseClearance,
        /// <summary>크레인 등 장애물이 들어갈 공사 현장(예약).</summary>
        Reserved,
    }

    [Serializable]
    public struct LotPlacement
    {
        public int Ix;
        public int Iz;
        /// <summary>블록 중심(m).</summary>
        public float X;
        public float Z;
        public float Width;
        public float Depth;
        public float Height;
        public LotKind Kind;
        public BuildingArchetype Archetype;
        /// <summary>코스 때문에 높이가 낮아졌는지(디버그·검증용).</summary>
        public bool CappedByCourse;
        /// <summary>이 블록 전용 난수 시드(형상 세부 변형용, 결정적).</summary>
        public int DetailSeed;
    }

    public sealed class CityLayoutSettings
    {
        public int Seed = 20261108;
        public int LotsPerSide = 11;
        public float LotSpacing = 70f;
        public float MinFootprint = 26f;
        public float MaxFootprint = 40f;
        public float MinHeight = 35f;
        public float MaxHeight = 190f;
        public double TowerChance = 0.12;
        public float TowerMinHeight = 230f;
        public float TowerMaxHeight = 320f;
        public double PlazaChance = 0.08;
        /// <summary>코스 구간에서 건물 가장자리까지 수평 여유(m). 이 안쪽 건물은 높이 제한.</summary>
        public float CourseLateralClearance = 12f;
        /// <summary>코스 구간 아래로 남길 수직 여유(m).</summary>
        public float CourseVerticalClearance = 22f;
        /// <summary>이보다 낮아지면 건물 대신 공터.</summary>
        public float MinBuildingHeight = 15f;
        /// <summary>건물을 세우지 않는 예약 블록(블록 중심 좌표).</summary>
        public List<(float x, float z)> ReservedLots = new List<(float x, float z)>();
        /// <summary>최소 높이를 보장할 블록(스카이브리지 지지 건물 등).</summary>
        public List<(float x, float z, float minHeight)> MinHeightLots = new List<(float x, float z, float minHeight)>();

        public float CityHalfExtent => (LotsPerSide - 1) * 0.5f * LotSpacing;

        /// <summary>기본 다운타운: 크레인 공사 현장 2곳, 스카이브리지 지지 건물 2곳.</summary>
        public static CityLayoutSettings Downtown()
        {
            var s = new CityLayoutSettings();
            s.ReservedLots.Add((0f, 70f));      // 코스 크레인
            s.ReservedLots.Add((210f, -280f));  // 회전 크레인
            s.MinHeightLots.Add((210f, 140f, 120f));
            s.MinHeightLots.Add((210f, 210f, 120f));
            return s;
        }
    }

    /// <summary>
    /// 시드 고정 도시 격자 생성기(순수 로직 — 같은 입력이면 항상 같은 결과).
    /// 코스 구간 근처 건물은 구간 고도 − 수직 여유 이하로 자동 제한해 "항상 통과 가능한 코스"를 보장한다(D-022).
    /// </summary>
    public static class CityLayoutGenerator
    {
        public static List<LotPlacement> Generate(CityLayoutSettings settings, CourseDefinition course)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var rng = new Random(settings.Seed);
            var result = new List<LotPlacement>(settings.LotsPerSide * settings.LotsPerSide);
            float half = settings.CityHalfExtent;

            for (int ix = 0; ix < settings.LotsPerSide; ix++)
            for (int iz = 0; iz < settings.LotsPerSide; iz++)
            {
                // 블록마다 같은 개수의 난수를 소비 → 한 블록 규칙을 바꿔도 다른 블록 결과가 흔들리지 않음
                double plazaRoll = rng.NextDouble();
                float w = Lerp(settings.MinFootprint, settings.MaxFootprint, rng.NextDouble());
                float d = Lerp(settings.MinFootprint, settings.MaxFootprint, rng.NextDouble());
                bool tower = rng.NextDouble() < settings.TowerChance;
                double heightRoll = rng.NextDouble();
                int detailSeed = rng.Next();
                int archetypeSeed = rng.Next();

                var lot = new LotPlacement
                {
                    Ix = ix,
                    Iz = iz,
                    X = -half + ix * settings.LotSpacing,
                    Z = -half + iz * settings.LotSpacing,
                    Width = w,
                    Depth = d,
                    DetailSeed = detailSeed,
                };

                if (IsListed(settings.ReservedLots, lot.X, lot.Z))
                {
                    lot.Kind = LotKind.Reserved;
                    result.Add(lot);
                    continue;
                }

                float h = tower
                    ? Lerp(settings.TowerMinHeight, settings.TowerMaxHeight, heightRoll)
                    : Lerp(settings.MinHeight, settings.MaxHeight, heightRoll);
                bool forcedTall = false;
                foreach ((float x, float z, float minHeight) m in settings.MinHeightLots)
                {
                    if (Same(m.x, lot.X) && Same(m.z, lot.Z))
                    {
                        h = Math.Max(h, m.minHeight);
                        forcedTall = true;
                    }
                }

                if (!forcedTall && plazaRoll < settings.PlazaChance)
                {
                    lot.Kind = LotKind.Plaza;
                    result.Add(lot);
                    continue;
                }

                if (course != null)
                {
                    float cap = HeightCap(course, lot.X, lot.Z, Math.Max(w, d) * 0.5f, settings);
                    if (cap < h)
                    {
                        h = cap;
                        lot.CappedByCourse = true;
                    }
                }

                if (h < settings.MinBuildingHeight)
                {
                    lot.Kind = LotKind.CourseClearance;
                    result.Add(lot);
                    continue;
                }

                lot.Kind = LotKind.Building;
                lot.Height = h;
                lot.Archetype = BuildingCatalog.Pick(h, new Random(archetypeSeed));
                result.Add(lot);
            }
            return result;
        }

        /// <summary>블록 주변을 지나는 코스 구간들 중 가장 낮은 고도 − 여유. 영향 없으면 float.MaxValue.</summary>
        public static float HeightCap(CourseDefinition course, float x, float z, float halfSize, CityLayoutSettings settings)
        {
            float cap = float.MaxValue;
            foreach ((Float3 from, Float3 to) in course.Segments())
            {
                float dist = DistancePointSegment2D(x, z, from.X, from.Z, to.X, to.Z);
                if (dist < halfSize + settings.CourseLateralClearance)
                    cap = Math.Min(cap, Math.Min(from.Y, to.Y) - settings.CourseVerticalClearance);
            }
            return cap;
        }

        public static float DistancePointSegment2D(float px, float pz, float ax, float az, float bx, float bz)
        {
            float abx = bx - ax, abz = bz - az;
            float len2 = abx * abx + abz * abz;
            float t = len2 < 1e-6f ? 0f : ((px - ax) * abx + (pz - az) * abz) / len2;
            t = Math.Max(0f, Math.Min(1f, t));
            float cx = ax + abx * t - px, cz = az + abz * t - pz;
            return (float)Math.Sqrt(cx * cx + cz * cz);
        }

        private static bool IsListed(List<(float x, float z)> list, float x, float z)
        {
            foreach ((float lx, float lz) in list)
                if (Same(lx, x) && Same(lz, z)) return true;
            return false;
        }

        private static bool Same(float a, float b) => Math.Abs(a - b) < 0.01f;

        private static float Lerp(float a, float b, double t) => a + (float)t * (b - a);
    }
}
