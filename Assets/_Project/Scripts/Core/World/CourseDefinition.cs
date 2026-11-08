using System;
using System.Collections.Generic;

namespace TowerGlide.Core.World
{
    [Serializable]
    public readonly struct CourseWaypoint
    {
        public readonly string Name;
        public readonly Float3 Position;

        public CourseWaypoint(string name, Float3 position)
        {
            Name = name;
            Position = position;
        }
    }

    /// <summary>
    /// SKYLINE RUN 코스 데이터: 스폰 지점 + 순서대로 통과할 링 위치. 링 방향은 앞뒤 구간으로 자동 계산한다.
    /// 씬 생성기·도시 레이아웃(건물 높이 제한)·코스 검증이 모두 이 한 가지 데이터를 쓴다.
    /// </summary>
    public sealed class CourseDefinition
    {
        public CourseDefinition(string id, Float3 spawn, IReadOnlyList<CourseWaypoint> gates)
        {
            if (gates == null || gates.Count == 0) throw new ArgumentException("Course needs at least one gate", nameof(gates));
            Id = id;
            Spawn = spawn;
            Gates = gates;
        }

        public string Id { get; }
        public Float3 Spawn { get; }
        public IReadOnlyList<CourseWaypoint> Gates { get; }

        /// <summary>스폰 → 링1 → … → 마지막 링 구간들(시작점, 끝점).</summary>
        public IEnumerable<(Float3 from, Float3 to)> Segments()
        {
            Float3 prev = Spawn;
            for (int i = 0; i < Gates.Count; i++)
            {
                yield return (prev, Gates[i].Position);
                prev = Gates[i].Position;
            }
        }

        /// <summary>링 i의 진행 방향(정규화 전): 들어오는 방향 + 나가는 방향×0.6.</summary>
        public Float3 GateDirection(int i)
        {
            Float3 pos = Gates[i].Position;
            Float3 prev = i == 0 ? Spawn : Gates[i - 1].Position;
            Float3 next = i + 1 < Gates.Count ? Gates[i + 1].Position : pos + (pos - prev);
            Float3 a = Normalize(pos - prev);
            Float3 b = Normalize(next - pos);
            return Normalize(a + b * 0.6f);
        }

        internal static Float3 Normalize(Float3 v)
        {
            float l = v.Length;
            return l > 1e-5f ? v * (1f / l) : new Float3(0f, 0f, 1f);
        }
    }

    /// <summary>기본 코스(명세 3.3 구간 순서). 도시 격자: 블록 중심 -350..350(70m 간격), 거리 중심 = 블록 중심 ±35m.</summary>
    public static class SkylineCourses
    {
        public static CourseDefinition Downtown() => new CourseDefinition(
            "downtown-01",
            new Float3(-35f, 70f, -480f),
            new[]
            {
                new CourseWaypoint("01 Takeoff Zone", new Float3(-35f, 65f, -420f)),
                new CourseWaypoint("02 First Towers", new Float3(-35f, 55f, -315f)),
                new CourseWaypoint("03 Narrow Canyon A", new Float3(-35f, 50f, -175f)),
                new CourseWaypoint("04 Narrow Canyon B", new Float3(-35f, 50f, -35f)),
                new CourseWaypoint("05 Crane Crossing", new Float3(-35f, 55f, 105f)),
                new CourseWaypoint("06 Turn East", new Float3(35f, 60f, 175f)),
                new CourseWaypoint("07 Drone Corridor", new Float3(140f, 60f, 175f)),
                new CourseWaypoint("08 Skybridge Gate", new Float3(210f, 50f, 175f)),
                // 08 → 09는 z=175 거리를 그대로 따라가며 상승(대각선이면 스카이브리지 지지 건물을 깎게 됨 — CI 테스트로 발견)
                new CourseWaypoint("09 Climb Turn", new Float3(330f, 130f, 175f)),
                new CourseWaypoint("10 High Altitude", new Float3(315f, 230f, -35f)),
                new CourseWaypoint("11 Descent", new Float3(175f, 190f, -175f)),
                new CourseWaypoint("12 Final Gate", new Float3(10f, 110f, -340f)),
            });
    }
}
