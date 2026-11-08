using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TowerGlide.Core.World;

namespace TowerGlide.Tests
{
    public sealed class CourseValidatorTests
    {
        [Test]
        public void DefaultCoursePassesValidation()
        {
            List<string> issues = CourseValidator.Validate(SkylineCourses.Downtown(), CourseRules.Default);
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void DefaultCourseHasTwelveGatesInSpecOrder()
        {
            CourseDefinition c = SkylineCourses.Downtown();
            Assert.AreEqual(12, c.Gates.Count);
            StringAssert.Contains("Takeoff", c.Gates[0].Name);
            StringAssert.Contains("Final", c.Gates[c.Gates.Count - 1].Name);
        }

        [Test]
        public void DetectsTooCloseTooLowAndSharpTurn()
        {
            var bad = new CourseDefinition("bad", new Float3(0f, 60f, -100f), new[]
            {
                new CourseWaypoint("A", new Float3(0f, 60f, 0f)),
                new CourseWaypoint("B", new Float3(0f, 10f, 20f)),     // 가깝고(54m) 낮고 급강하
                new CourseWaypoint("C", new Float3(0f, 60f, -60f)),    // 급반전
            });
            List<string> issues = CourseValidator.Validate(bad, CourseRules.Default);
            Assert.IsTrue(issues.Any(i => i.StartsWith("B") && i.Contains("최소")), "간격");
            Assert.IsTrue(issues.Any(i => i.StartsWith("B") && i.Contains("고도")), "고도");
            Assert.IsTrue(issues.Any(i => i.StartsWith("C") && i.Contains("방향")), "급선회");
        }

        [Test]
        public void HorizontalAngleBasics()
        {
            Assert.AreEqual(0f, CourseValidator.HorizontalAngle(new Float3(0, 0, 1), new Float3(0, 5, 2)), 1e-3f);
            Assert.AreEqual(90f, CourseValidator.HorizontalAngle(new Float3(0, 0, 1), new Float3(1, 0, 0)), 1e-3f);
            Assert.AreEqual(180f, CourseValidator.HorizontalAngle(new Float3(0, 0, 1), new Float3(0, 0, -1)), 1e-3f);
        }

        [Test]
        public void GateDirectionPointsAlongTravel()
        {
            CourseDefinition c = SkylineCourses.Downtown();
            Float3 d = c.GateDirection(0); // 스폰에서 북쪽(+Z)으로 진입
            Assert.Greater(d.Z, 0.9f);
            Assert.AreEqual(1f, d.Length, 1e-4f);
        }
    }

    public sealed class BuildingCatalogTests
    {
        [Test]
        public void HasAtLeastTenArchetypes()
        {
            Assert.GreaterOrEqual(BuildingCatalog.All.Count, 10);
            Assert.AreEqual(BuildingCatalog.All.Count, BuildingCatalog.All.Select(a => a.Archetype).Distinct().Count());
        }

        [Test]
        public void EverySupportedHeightHasAnArchetype()
        {
            for (float h = BuildingCatalog.MinSupportedHeight; h <= BuildingCatalog.MaxSupportedHeight; h += 1f)
                Assert.IsTrue(BuildingCatalog.All.Any(a => a.Fits(h)), $"{h}m에 맞는 아키타입 없음");
        }

        [Test]
        public void PickReturnsFittingArchetype()
        {
            var rng = new Random(1);
            for (int i = 0; i < 200; i++)
            {
                float h = 15f + (float)rng.NextDouble() * 320f;
                BuildingArchetype a = BuildingCatalog.Pick(h, rng);
                Assert.IsTrue(BuildingCatalog.Info(a).Fits(h), $"{a} @ {h}");
            }
        }
    }

    public sealed class CityLayoutGeneratorTests
    {
        private static List<LotPlacement> Generate() =>
            CityLayoutGenerator.Generate(CityLayoutSettings.Downtown(), SkylineCourses.Downtown());

        [Test]
        public void IsDeterministicForSameSeed()
        {
            List<LotPlacement> a = Generate();
            List<LotPlacement> b = Generate();
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Height, b[i].Height);
                Assert.AreEqual(a[i].Archetype, b[i].Archetype);
            }
        }

        [Test]
        public void DifferentSeedChangesCity()
        {
            CityLayoutSettings s = CityLayoutSettings.Downtown();
            s.Seed = 7;
            List<LotPlacement> other = CityLayoutGenerator.Generate(s, SkylineCourses.Downtown());
            List<LotPlacement> baseline = Generate();
            Assert.IsTrue(other.Where((l, i) => Math.Abs(l.Height - baseline[i].Height) > 0.01f).Any());
        }

        [Test]
        public void CoversFullGrid()
        {
            Assert.AreEqual(121, Generate().Count);
        }

        [Test]
        public void NoBuildingBlocksTheCourse()
        {
            CityLayoutSettings s = CityLayoutSettings.Downtown();
            CourseDefinition course = SkylineCourses.Downtown();
            foreach (LotPlacement lot in Generate().Where(l => l.Kind == LotKind.Building))
            {
                float half = Math.Max(lot.Width, lot.Depth) * 0.5f;
                foreach ((Float3 from, Float3 to) in course.Segments())
                {
                    float dist = CityLayoutGenerator.DistancePointSegment2D(lot.X, lot.Z, from.X, from.Z, to.X, to.Z);
                    if (dist < half + s.CourseLateralClearance)
                        Assert.LessOrEqual(lot.Height, Math.Min(from.Y, to.Y) - s.CourseVerticalClearance + 1e-3f,
                            $"블록({lot.Ix},{lot.Iz}) 높이 {lot.Height:0}m가 코스 구간을 막음");
                }
            }
        }

        [Test]
        public void CanyonWallsStayTall()
        {
            // 협곡 구간(x = -35 거리) 양옆 블록 중 적어도 일부는 코스 고도보다 높아야 "협곡"이 된다
            List<LotPlacement> lots = Generate();
            int tallWalls = lots.Count(l => l.Kind == LotKind.Building && (Math.Abs(l.X + 70f) < 0.1f || Math.Abs(l.X) < 0.1f)
                                            && l.Z > -200f && l.Z < 0f && l.Height > 60f);
            Assert.GreaterOrEqual(tallWalls, 2);
        }

        [Test]
        public void ReservedAndMinHeightLotsAreRespected()
        {
            List<LotPlacement> lots = Generate();
            LotPlacement crane = lots.Single(l => Math.Abs(l.X) < 0.1f && Math.Abs(l.Z - 70f) < 0.1f);
            Assert.AreEqual(LotKind.Reserved, crane.Kind);
            foreach (LotPlacement anchor in lots.Where(l => Math.Abs(l.X - 210f) < 0.1f && (Math.Abs(l.Z - 140f) < 0.1f || Math.Abs(l.Z - 210f) < 0.1f)))
            {
                Assert.AreEqual(LotKind.Building, anchor.Kind);
                Assert.GreaterOrEqual(anchor.Height, 120f);
            }
        }

        [Test]
        public void BuildingsUseFittingArchetypesAndManyVariants()
        {
            List<LotPlacement> buildings = Generate().Where(l => l.Kind == LotKind.Building).ToList();
            foreach (LotPlacement b in buildings)
                Assert.IsTrue(BuildingCatalog.Info(b.Archetype).Fits(b.Height), $"{b.Archetype} @ {b.Height:0}m");
            Assert.GreaterOrEqual(buildings.Select(b => b.Archetype).Distinct().Count(), 8, "도시에 형태가 다양해야 함");
        }
    }
}
