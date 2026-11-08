using NUnit.Framework;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.Flight;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;

namespace TowerGlide.Tests
{
    public sealed class AircraftRulesTests
    {
        [Test]
        public void UnlockRulesUseLocalRecordsOnly()
        {
            var r = new RecordsData { totalDistanceMeters = 4000f, totalFlightSeconds = 500f, skylineCompletions = 1 };
            Assert.IsTrue(AircraftRules.IsUnlocked(UnlockRule.Always, r));
            Assert.IsTrue(AircraftRules.IsUnlocked(new UnlockRule { Kind = UnlockKind.TotalDistanceMeters, Threshold = 4000f }, r));
            Assert.IsFalse(AircraftRules.IsUnlocked(new UnlockRule { Kind = UnlockKind.TotalFlightSeconds, Threshold = 600f }, r));
            Assert.IsTrue(AircraftRules.IsUnlocked(new UnlockRule { Kind = UnlockKind.SkylineCompletions, Threshold = 1f }, r));
            Assert.IsFalse(AircraftRules.IsUnlocked(UnlockRule.ComingSoon, r), "미구현 기체는 절대 선택 불가");
            Assert.IsTrue(AircraftRules.IsUnlocked(UnlockRule.Always, null));
        }

        [Test]
        public void SkylineRunRequiresTightRouteAircraft()
        {
            Assert.IsTrue(AircraftRules.CanPlay(FlightMode.SkylineRun, AircraftTags.TightRoute | AircraftTags.Aerobatics, out _));
            Assert.IsFalse(AircraftRules.CanPlay(FlightMode.SkylineRun, AircraftTags.WideAirspace, out string reason));
            StringAssert.Contains("FREE FLIGHT", reason);
            Assert.IsTrue(AircraftRules.CanPlay(FlightMode.FreeFlight, AircraftTags.WideAirspace, out _), "FREE FLIGHT는 모든 기체");
        }

        [Test]
        public void SelectionFallsBackToDefaultForMissingLockedOrCorruptIds()
        {
            var catalog = new[]
            {
                (AircraftIds.ScoutP1, true),
                (AircraftIds.ViperF9, true),
                (AircraftIds.Cityliner180, true),
                (AircraftIds.Longhaul300, false),
            };
            Assert.AreEqual(AircraftIds.ViperF9, AircraftRules.ResolveSelection(AircraftIds.ViperF9, catalog, AircraftIds.Default));
            Assert.AreEqual(AircraftIds.Default, AircraftRules.ResolveSelection("no_such_plane", catalog, AircraftIds.Default));
            Assert.AreEqual(AircraftIds.Default, AircraftRules.ResolveSelection(AircraftIds.Longhaul300, catalog, AircraftIds.Default));
            Assert.AreEqual(AircraftIds.Default, AircraftRules.ResolveSelection(null, catalog, AircraftIds.Default));
            Assert.AreEqual(AircraftIds.ViperF9,
                AircraftRules.ResolveSelection(null, new[] { (AircraftIds.ScoutP1, false), (AircraftIds.ViperF9, true) }, AircraftIds.Default),
                "기본 기체도 못 쓰면 선택 가능한 첫 기체");
        }
    }

    public sealed class AircraftStatsTests
    {
        // 기본 3종의 튜닝 값(에디터가 만드는 에셋 초깃값과 같게 유지 — TowerglideAircraftFactory)
        private static readonly FlightProfileNumbers Scout = new FlightProfileNumbers
            { MaxSpeed = 55, MaxThrust = 18, MaxRollRate = 150, MaxPitchRate = 70, AngularResponse = 6, Wingspan = 8.4f, AirBrakeMultiplier = 4 };
        private static readonly FlightProfileNumbers Viper = new FlightProfileNumbers
            { MaxSpeed = 140, MaxThrust = 35, MaxRollRate = 220, MaxPitchRate = 60, AngularResponse = 7, Wingspan = 10f, AirBrakeMultiplier = 3 };
        private static readonly FlightProfileNumbers Liner = new FlightProfileNumbers
            { MaxSpeed = 95, MaxThrust = 9, MaxRollRate = 35, MaxPitchRate = 18, AngularResponse = 1.6f, Wingspan = 34f, AirBrakeMultiplier = 2.5f };

        [Test]
        public void BarsDistinguishTheThreeBaselineAircraft()
        {
            AircraftStatBars[] b = AircraftStats.Compute(new[] { Scout, Viper, Liner });
            AircraftStatBars s = b[0], v = b[1], l = b[2];
            Assert.Greater(v.Speed, l.Speed);
            Assert.Greater(l.Speed, s.Speed);
            Assert.Greater(v.Acceleration, s.Acceleration);
            Assert.Greater(s.Acceleration, l.Acceleration);
            Assert.Greater(s.Agility, l.Agility);
            Assert.Greater(v.Agility, l.Agility);
            Assert.Greater(l.Stability, s.Stability, "여객기가 가장 안정적");
            Assert.Greater(l.Size, v.Size);
        }

        [Test]
        public void BarsStayWithinVisibleRange()
        {
            foreach (AircraftStatBars x in AircraftStats.Compute(new[] { Scout, Viper, Liner }))
                foreach (float f in new[] { x.Speed, x.Acceleration, x.Agility, x.Stability, x.Size })
                    Assert.That(f, Is.InRange(0.15f, 1f));
        }
    }

    public sealed class AircraftSaveTests
    {
        [Test]
        public void V1SaveMigratesWithDefaultAircraft()
        {
            var v1 = new SaveData { version = 1, aircraft = null, records = new RecordsData { bestSkylineScore = 900 } };
            SaveData d = SaveDataMigrator.Normalize(v1, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Migrated, status);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.AreEqual(AircraftIds.Default, d.aircraft.selectedAircraftId);
            Assert.AreEqual(900, d.records.bestSkylineScore, "기존 기록 보존");
            Assert.AreEqual(0f, d.records.totalDistanceMeters, "누적 기록은 0에서 시작(부풀리지 않음)");
        }

        [Test]
        public void BlankOrHugeAircraftIdIsReset()
        {
            Assert.AreEqual(AircraftIds.Default, new AircraftSaveData { selectedAircraftId = "  " }.Sanitized().selectedAircraftId);
            Assert.AreEqual(AircraftIds.Default, new AircraftSaveData { selectedAircraftId = new string('x', 200) }.Sanitized().selectedAircraftId);
            Assert.AreEqual(AircraftIds.ViperF9, new AircraftSaveData { selectedAircraftId = " viper_f9 " }.Sanitized().selectedAircraftId);
        }

        [Test]
        public void RunsAccumulateUnlockProgress()
        {
            var r = new RecordsData();
            r.Apply(new RunResult { Mode = FlightMode.FreeFlight, DistanceMeters = 1200f, FlightSeconds = 60f });
            r.Apply(new RunResult { Mode = FlightMode.SkylineRun, Completed = true, DistanceMeters = 3000f, FlightSeconds = 120f });
            Assert.AreEqual(4200f, r.totalDistanceMeters, 1e-3f);
            Assert.AreEqual(180f, r.totalFlightSeconds, 1e-3f);
            Assert.AreEqual(1, r.skylineCompletions);
        }
    }

    public sealed class ThrustCompensationTests
    {
        [Test]
        public void HoldsTargetSpeedAgainstDrag()
        {
            // 단순 적분: 항력 보상 추력이면 목표 속도 근처(±1 m/s)에 머문다
            foreach ((float target, float dragK, float maxThrust) in new[] { (30f, 0.0035f, 18f), (70f, 0.0012f, 35f), (65f, 0.0009f, 9f) })
            {
                float v = target * 0.5f;
                for (int i = 0; i < 3000; i++)
                {
                    float drag = AeroModel.DragAcceleration(v, dragK, 1f, false);
                    float thrust = AeroModel.ThrustWithDragCompensation(v, target, drag, maxThrust, 1.2f);
                    v += (thrust - drag) * 0.02f;
                }
                Assert.AreEqual(target, v, 1f, $"target {target}");
            }
        }

        [Test]
        public void NoThrustAboveTarget()
        {
            Assert.AreEqual(0f, AeroModel.ThrustWithDragCompensation(41f, 40f, 5f, 20f, 1f));
        }
    }
}
