using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TowerGlide.Core.World;
using TowerGlide.Core.World.Geo;
using TowerGlide.Core.World.Streaming;

namespace TowerGlide.Tests
{
    public sealed class CoordinateSystemTests
    {
        private static readonly TransverseMercator Tm = TransverseMercator.Korea2000CentralBelt2010();

        [Test]
        public void ProjectionOriginMapsToFalseOrigin()
        {
            ProjectedPoint p = Tm.Forward(new GeoPoint(38.0, 127.0));
            Assert.AreEqual(200000.0, p.Easting, 1e-3);
            Assert.AreEqual(600000.0, p.Northing, 1e-3);
        }

        [Test]
        public void RoundTripIsSubMillimetreAcrossSeoul()
        {
            for (double lat = 37.40; lat <= 37.72; lat += 0.04)
            for (double lon = 126.75; lon <= 127.20; lon += 0.05)
            {
                GeoPoint back = Tm.Inverse(Tm.Forward(new GeoPoint(lat, lon)));
                Assert.AreEqual(lat, back.Latitude, 1e-8, $"lat @ {lat},{lon}");
                Assert.AreEqual(lon, back.Longitude, 1e-8, $"lon @ {lat},{lon}");
            }
        }

        [Test]
        public void ProjectedDistanceMatchesGeodesicWithinHalfPercent()
        {
            // 위도 1분 ≈ 1852m(해리 정의). 서울 위도에서 경도 0.1° ≈ 8.83km(구면 근사)
            var c = new WorldCoordinateSystem(Tm, new GeoPoint(37.5665, 126.9780));
            LocalPoint a = c.GeoToLocal(new GeoPoint(37.5, 127.0));
            LocalPoint b = c.GeoToLocal(new GeoPoint(37.5 + 1.0 / 60.0, 127.0));
            Assert.AreEqual(1850.0, a.DistanceTo(b), 1850.0 * 0.005);

            LocalPoint e = c.GeoToLocal(new GeoPoint(37.5, 127.1));
            double expected = 6371008.8 * Math.Cos(37.5 * Math.PI / 180.0) * 0.1 * Math.PI / 180.0;
            Assert.AreEqual(expected, a.DistanceTo(e), expected * 0.005);
        }

        [Test]
        public void LocalAxesAreEastAndNorthWithOriginAtZero()
        {
            var c = new WorldCoordinateSystem(Tm, new GeoPoint(37.5665, 126.9780));
            LocalPoint o = c.GeoToLocal(c.Origin);
            Assert.AreEqual(0.0, o.X, 1e-6);
            Assert.AreEqual(0.0, o.Z, 1e-6);
            Assert.Greater(c.GeoToLocal(new GeoPoint(37.5665, 126.99)).X, 0.0, "동쪽 = +X");
            Assert.Greater(c.GeoToLocal(new GeoPoint(37.58, 126.9780)).Z, 0.0, "북쪽 = +Z");
        }

        [Test]
        public void RebaseShiftsUnityCoordinatesOnly()
        {
            var c = new WorldCoordinateSystem(Tm, new GeoPoint(37.5665, 126.9780)) { RebaseOffset = new LocalPoint(1000, -500) };
            var p = new LocalPoint(1200, -400);
            Float3 u = c.LocalToUnity(p, 50f);
            Assert.AreEqual(200f, u.X, 1e-3f);
            Assert.AreEqual(100f, u.Z, 1e-3f);
            LocalPoint back = c.UnityToLocal(u.X, u.Z);
            Assert.AreEqual(1200.0, back.X, 1e-3);
            Assert.AreEqual(-400.0, back.Z, 1e-3);
            Assert.AreEqual(p.X, c.GeoToLocal(c.LocalToGeo(p)).X, 1e-3, "로컬↔지리 변환은 rebase와 무관");
        }
    }

    public sealed class WorldDefinitionTests
    {
        [Test]
        public void PocGridCoversTestCityAndCourse()
        {
            SeoulWorldDefinition w = SeoulWorldDefinition.StreamingPoc();
            Assert.AreEqual(16, w.Grid.Count);
            foreach (Float3 p in new[] { SkylineCourses.Downtown().Spawn }.Concat(SkylineCourses.Downtown().Gates.Select(g => g.Position)))
                Assert.IsTrue(w.Grid.Contains(w.Grid.IdAt(p.X, p.Z)), $"코스 지점 {p}가 청크 격자 밖");
        }

        [Test]
        public void PlannedDistrictsAreNotMarkedAsVerifiedRealData()
        {
            foreach (SeoulDistrictDefinition d in SeoulWorldDefinition.PlannedDistricts())
            {
                Assert.IsFalse(d.CoordinatesVerified, $"{d.Id}: 실제 데이터 확인 전 좌표를 검증됨으로 표시하지 않음");
                Assert.AreNotEqual(DistrictDataStatus.Imported, d.Status);
            }
        }
    }

    public sealed class ChunkGridTests
    {
        [Test]
        public void IdAtUsesFloorForNegativeCoordinates()
        {
            var g = new WorldChunkGrid(250f, -2, -2, 1, 1);
            Assert.AreEqual(new WorldChunkId(0, 0), g.IdAt(10f, 10f));
            Assert.AreEqual(new WorldChunkId(-1, -1), g.IdAt(-0.1f, -0.1f));
            Assert.AreEqual(new WorldChunkId(-2, 1), g.IdAt(-499f, 499f));
            Assert.IsFalse(g.Contains(g.IdAt(600f, 0f)));
        }

        [Test]
        public void DistanceIsZeroInsideAndPositiveOutside()
        {
            var g = new WorldChunkGrid(250f, -2, -2, 1, 1);
            Assert.AreEqual(0f, g.DistanceTo(new WorldChunkId(0, 0), 100f, 100f));
            Assert.AreEqual(50f, g.DistanceTo(new WorldChunkId(0, 0), 300f, 100f), 1e-4f);
        }

        [Test]
        public void KeysAreStableAndUnique()
        {
            var g = new WorldChunkGrid(250f, -2, -2, 1, 1);
            List<string> keys = g.All().Select(i => i.Key).ToList();
            Assert.AreEqual(keys.Count, keys.Distinct().Count());
            Assert.AreEqual("x-2_z+1", new WorldChunkId(-2, 1).Key);
            Assert.AreEqual("x+0_z+0", new WorldChunkId(0, 0).Key);
        }
    }

    public sealed class ChunkStateTrackerTests
    {
        [Test]
        public void FollowsLifecycleAndRejectsDuplicates()
        {
            var t = new ChunkStateTracker();
            var id = new WorldChunkId(0, 0);
            Assert.IsTrue(t.BeginLoad(id));
            Assert.IsFalse(t.BeginLoad(id), "로딩 중 중복 요청 거부");
            Assert.IsFalse(t.BeginUnload(id), "로딩 중 언로드 거부");
            Assert.IsTrue(t.CompleteLoad(id));
            Assert.IsFalse(t.BeginLoad(id), "이미 로드됨");
            Assert.IsTrue(t.BeginUnload(id));
            Assert.IsFalse(t.BeginUnload(id));
            Assert.IsTrue(t.CompleteUnload(id));
            Assert.AreEqual(WorldChunkState.Unloaded, t.Get(id));
        }

        [Test]
        public void FailedChunkCanRetryAndCountsFailures()
        {
            var t = new ChunkStateTracker();
            var id = new WorldChunkId(1, 0);
            t.BeginLoad(id);
            Assert.IsTrue(t.FailLoad(id, 5f));
            Assert.AreEqual(1, t.FailCount(id));
            Assert.IsTrue(t.BeginLoad(id));
            t.CompleteLoad(id);
            Assert.AreEqual(0, t.FailCount(id), "성공하면 실패 횟수 초기화");
        }
    }

    public sealed class ChunkStreamingPlannerTests
    {
        private static readonly WorldChunkGrid Grid = new WorldChunkGrid(250f, -2, -2, 1, 1);

        private static StreamingSettings Settings(int concurrent = 32, int budget = 32)
        {
            StreamingSettings s = StreamingSettings.Default;
            s.MaxConcurrentLoads = concurrent;
            s.MaxResidentChunks = budget;
            return s;
        }

        [Test]
        public void StationaryLoadsCurrentChunkFirst()
        {
            var t = new ChunkStateTracker();
            StreamingPlan p = ChunkStreamingPlanner.Plan(Grid, t, Settings(), 125f, 125f, 0f, 0f, false, 0f);
            Assert.AreEqual(new WorldChunkId(0, 0), p.ToLoad[0]);
            Assert.IsTrue(p.ToLoad.Contains(new WorldChunkId(1, 0)));
            Assert.IsFalse(p.ToLoad.Contains(new WorldChunkId(-2, -2)), "멀리 떨어진 청크는 로드하지 않음");
        }

        [Test]
        public void MovingEastPrioritisesChunksAhead()
        {
            var t = new ChunkStateTracker();
            // 서쪽 끝(-450, 125)에서 동쪽으로 80 m/s
            StreamingPlan p = ChunkStreamingPlanner.Plan(Grid, t, Settings(), -450f, 125f, 80f, 0f, false, 0f);
            int ahead = p.Wanted.IndexOf(new WorldChunkId(0, 0));
            Assert.GreaterOrEqual(ahead, 0, "4초 앞(−130m 지점) 청크가 미리 포함돼야 함");
            StreamingPlan still = ChunkStreamingPlanner.Plan(Grid, new ChunkStateTracker(), Settings(), -450f, 125f, 0f, 0f, false, 0f);
            Assert.IsFalse(still.Wanted.Contains(new WorldChunkId(1, 0)));
            StreamingPlan fast = ChunkStreamingPlanner.Plan(Grid, new ChunkStateTracker(), Settings(), -450f, 125f, 80f, 0f, true, 0f);
            Assert.GreaterOrEqual(fast.Wanted.Count, p.Wanted.Count, "부스트 중 더 멀리 프리로드");
        }

        [Test]
        public void ConcurrentLoadLimitAndNoDuplicateRequests()
        {
            var t = new ChunkStateTracker();
            StreamingPlan p1 = ChunkStreamingPlanner.Plan(Grid, t, Settings(concurrent: 2), 0f, 0f, 0f, 0f, false, 0f);
            Assert.AreEqual(2, p1.ToLoad.Count);
            foreach (WorldChunkId id in p1.ToLoad) t.BeginLoad(id);
            StreamingPlan p2 = ChunkStreamingPlanner.Plan(Grid, t, Settings(concurrent: 2), 0f, 0f, 0f, 0f, false, 0f);
            Assert.AreEqual(0, p2.ToLoad.Count, "로딩 중인 2개가 끝날 때까지 새 요청 없음");
        }

        [Test]
        public void UnloadsOnlyBeyondHysteresisRadius()
        {
            var t = new ChunkStateTracker();
            var far = new WorldChunkId(-2, -2);   // (−500..−250)
            var mid = new WorldChunkId(-1, 0);    // (−250..0, 0..250)
            foreach (WorldChunkId id in new[] { far, mid })
            {
                t.BeginLoad(id);
                t.CompleteLoad(id);
            }
            // 위치 (400, 400)은 격자 밖 동북쪽 — far까지 ~1000m, mid까지 ~400m
            StreamingPlan p = ChunkStreamingPlanner.Plan(Grid, t, Settings(), 400f, 400f, 0f, 0f, false, 0f);
            Assert.IsTrue(p.ToUnload.Contains(far));
            Assert.IsFalse(p.ToUnload.Contains(mid), "로드 반경(300) 밖이지만 언로드 반경(550) 안 → 유지(깜빡임 방지)");
        }

        [Test]
        public void BudgetEvictsFarthestNonWantedFirst()
        {
            var t = new ChunkStateTracker();
            foreach (WorldChunkId id in Grid.All())
            {
                t.BeginLoad(id);
                t.CompleteLoad(id);
            }
            StreamingPlan p = ChunkStreamingPlanner.Plan(Grid, t, Settings(budget: 6), 125f, 125f, 0f, 0f, false, 0f);
            Assert.IsNotEmpty(p.ToUnload);
            Assert.IsFalse(p.ToUnload.Any(id => p.Wanted.Contains(id)), "필요한 청크는 절대 언로드하지 않음");
            Assert.AreEqual(new WorldChunkId(-2, -2), p.ToUnload[0], "가장 먼 청크부터");
        }

        [Test]
        public void FailedChunkWaitsBeforeRetry()
        {
            var t = new ChunkStateTracker();
            var id = new WorldChunkId(0, 0);
            t.BeginLoad(id);
            t.FailLoad(id, 10f);
            Assert.IsFalse(ChunkStreamingPlanner.Plan(Grid, t, Settings(), 125f, 125f, 0f, 0f, false, 10.5f).ToLoad.Contains(id));
            Assert.IsTrue(ChunkStreamingPlanner.Plan(Grid, t, Settings(), 125f, 125f, 0f, 0f, false, 12.5f).ToLoad.Contains(id));
        }

        [Test]
        public void ReadyOnlyWhenNearestWantedAreLoaded()
        {
            var t = new ChunkStateTracker();
            StreamingPlan p = ChunkStreamingPlanner.Plan(Grid, t, Settings(), 125f, 125f, 0f, 0f, false, 0f);
            Assert.IsFalse(ChunkStreamingPlanner.IsReady(p, t, 1));
            t.BeginLoad(p.Wanted[0]);
            t.CompleteLoad(p.Wanted[0]);
            Assert.IsTrue(ChunkStreamingPlanner.IsReady(p, t, 1));
        }
    }
}
