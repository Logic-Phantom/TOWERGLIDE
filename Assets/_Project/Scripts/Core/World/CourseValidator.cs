using System;
using System.Collections.Generic;

namespace TowerGlide.Core.World
{
    [Serializable]
    public struct CourseRules
    {
        /// <summary>링 사이 최소 거리(m). 너무 가까우면 다음 링을 볼 시간이 없다.</summary>
        public float MinGateSpacing;
        /// <summary>링 사이 최대 거리(m). 너무 멀면 방향을 잃는다.</summary>
        public float MaxGateSpacing;
        public float MinAltitude;
        public float MaxAltitude;
        /// <summary>연속 구간 사이 최대 수평 방향 변화(도). 이보다 급하면 링을 놓치게 된다.</summary>
        public float MaxTurnDegrees;
        /// <summary>구간 최대 상승/하강각(도).</summary>
        public float MaxClimbDegrees;
        /// <summary>링 안쪽 반지름(m) — 링끼리 겹침 검사용.</summary>
        public float RingRadius;

        public static CourseRules Default => new CourseRules
        {
            MinGateSpacing = 55f,
            MaxGateSpacing = 260f,
            MinAltitude = 25f,
            MaxAltitude = 400f,
            MaxTurnDegrees = 100f,
            MaxClimbDegrees = 40f,
            RingRadius = 8f,
        };
    }

    /// <summary>
    /// 코스 데이터의 정적 검증(명세 2.5 "피할 공간이 없는 배치 금지", 3.3 "필수 링 순서 명확").
    /// 건물과의 실제 충돌 검사는 에디터 메뉴(TOWERGLIDE ▸ 3. 코스 검증)가 물리 쿼리로 한다.
    /// </summary>
    public static class CourseValidator
    {
        public static List<string> Validate(CourseDefinition course, CourseRules rules)
        {
            var issues = new List<string>();
            IReadOnlyList<CourseWaypoint> g = course.Gates;

            for (int i = 0; i < g.Count; i++)
            {
                Float3 p = g[i].Position;
                if (p.Y < rules.MinAltitude || p.Y > rules.MaxAltitude)
                    issues.Add($"{g[i].Name}: 고도 {p.Y:0}m가 허용 범위({rules.MinAltitude:0}~{rules.MaxAltitude:0}m) 밖");
            }

            int index = 0;
            Float3? prevDir = null;
            foreach ((Float3 from, Float3 to) in course.Segments())
            {
                string name = g[index].Name;
                Float3 d = to - from;
                float dist = d.Length;
                bool firstSegment = index == 0;
                if (!firstSegment && dist < rules.MinGateSpacing)
                    issues.Add($"{name}: 이전 링과 {dist:0}m — 최소 {rules.MinGateSpacing:0}m");
                if (dist > rules.MaxGateSpacing)
                    issues.Add($"{name}: 이전 지점과 {dist:0}m — 최대 {rules.MaxGateSpacing:0}m");

                float horizontal = d.HorizontalLength;
                float climb = (float)(Math.Atan2(Math.Abs(d.Y), Math.Max(horizontal, 1e-3f)) * 180.0 / Math.PI);
                if (climb > rules.MaxClimbDegrees)
                    issues.Add($"{name}: 상승/하강각 {climb:0}° — 최대 {rules.MaxClimbDegrees:0}°");

                if (prevDir.HasValue && horizontal > 1e-3f)
                {
                    float turn = HorizontalAngle(prevDir.Value, d);
                    if (turn > rules.MaxTurnDegrees)
                        issues.Add($"{name}: 진입 방향 변화 {turn:0}° — 최대 {rules.MaxTurnDegrees:0}°");
                }
                prevDir = d;
                index++;
            }

            for (int i = 0; i < g.Count; i++)
            for (int j = i + 1; j < g.Count; j++)
                if (Float3.Distance(g[i].Position, g[j].Position) < rules.RingRadius * 4f)
                    issues.Add($"{g[i].Name} ↔ {g[j].Name}: 링이 너무 가까워 순서가 헷갈림");

            return issues;
        }

        /// <summary>두 방향의 수평(XZ) 사이각(도).</summary>
        public static float HorizontalAngle(Float3 a, Float3 b)
        {
            double la = a.HorizontalLength, lb = b.HorizontalLength;
            if (la < 1e-5 || lb < 1e-5) return 0f;
            double cos = (a.X * b.X + a.Z * b.Z) / (la * lb);
            cos = Math.Max(-1.0, Math.Min(1.0, cos));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }
    }
}
