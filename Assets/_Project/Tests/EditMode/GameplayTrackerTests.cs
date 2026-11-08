using NUnit.Framework;
using TowerGlide.Core.App;
using TowerGlide.Core.Flight;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Util;

namespace TowerGlide.Tests
{
    public sealed class NearMissTrackerTests
    {
        [Test]
        public void CooldownPreventsDuplicateAwards()
        {
            var t = new NearMissTracker(cooldownSeconds: 3f);
            Assert.IsTrue(t.TryAward(7, 1f));
            Assert.IsFalse(t.TryAward(7, 2f));
            Assert.IsTrue(t.TryAward(8, 2f), "다른 장애물은 독립");
            Assert.IsTrue(t.TryAward(7, 4.1f));
        }

        [Test]
        public void RecentCollisionBlocksAward()
        {
            var t = new NearMissTracker(cooldownSeconds: 3f, collisionGraceSeconds: 2f);
            t.RegisterCollision(5, 10f);
            Assert.IsFalse(t.TryAward(5, 11f));
            Assert.IsTrue(t.TryAward(5, 12.5f));
        }
    }

    public sealed class FlightRecordTrackerTests
    {
        [Test]
        public void AccumulatesAndTracksMaxima()
        {
            var r = new FlightRecordTracker();
            r.Tick(1f, 30f, 30f, 100f);
            r.Tick(1f, 40f, 50f, 80f);
            Assert.AreEqual(2f, r.FlightSeconds, 1e-6f);
            Assert.AreEqual(70f, r.DistanceMeters, 1e-4f);
            Assert.AreEqual(180f, r.MaxSpeedKmh, 1e-3f);
            Assert.AreEqual(100f, r.MaxAltitudeMeters, 1e-6f);
        }

        [Test]
        public void TeleportJumpsAreNotCountedAsDistance()
        {
            var r = new FlightRecordTracker();
            r.Tick(0.02f, FlightRecordTracker.TeleportThresholdMeters + 1f, 30f, 50f);
            Assert.AreEqual(0f, r.DistanceMeters);
        }
    }

    public sealed class UnitConversionTests
    {
        [Test]
        public void SpeedConversionRoundTrips()
        {
            Assert.AreEqual(108f, FlightMath.MetersPerSecondToKmh(30f), 1e-4f);
            Assert.AreEqual(30f, FlightMath.KmhToMetersPerSecond(108f), 1e-4f);
        }
    }

    public sealed class AeroModelTests
    {
        [Test]
        public void LiftIsSymmetricAndDropsAfterStall()
        {
            float stall = 0.28f;
            float peak = AeroModel.LiftCoefficient(stall, 4f, stall);
            Assert.AreEqual(-peak, AeroModel.LiftCoefficient(-stall, 4f, stall), 1e-6f);
            Assert.Less(AeroModel.LiftCoefficient(stall * 1.5f, 4f, stall), peak);
            Assert.Greater(AeroModel.LiftCoefficient(stall * 1.5f, 4f, stall), 0f, "실속 후에도 양력이 0으로 급락하지 않음");
        }

        [Test]
        public void LiftFadesBelowMinimumFlightSpeed()
        {
            float atMin = AeroModel.LiftAcceleration(20f, 1f, 0.01f, 20f);
            float below = AeroModel.LiftAcceleration(10f, 1f, 0.01f, 20f);
            Assert.Less(below, atMin * 0.25f + 1e-4f);
        }

        [Test]
        public void AirBrakeIncreasesDrag()
        {
            Assert.Greater(AeroModel.DragAcceleration(40f, 0.003f, 4f, true), AeroModel.DragAcceleration(40f, 0.003f, 4f, false));
        }

        [Test]
        public void ThrustStopsAtTargetSpeed()
        {
            Assert.AreEqual(0f, AeroModel.ThrustAcceleration(31f, 30f, 18f, 1.2f));
            Assert.AreEqual(18f, AeroModel.ThrustAcceleration(0f, 30f, 18f, 1.2f));
        }

        [Test]
        public void ThrottleMapsToSpeedRange()
        {
            Assert.AreEqual(30f, AeroModel.TargetSpeed(0f, 20f, 30f, 60f), 1e-6f);
            Assert.AreEqual(60f, AeroModel.TargetSpeed(1f, 20f, 30f, 60f), 1e-6f);
            Assert.AreEqual(20f, AeroModel.TargetSpeed(-1f, 20f, 30f, 60f), 1e-6f);
        }

        [Test]
        public void ControlAuthorityReducedAtExtremes()
        {
            Assert.AreEqual(1f, AeroModel.ControlAuthority(30f, 20f, 30f, 60f, 0.3f, 0.6f), 1e-6f);
            Assert.Less(AeroModel.ControlAuthority(5f, 20f, 30f, 60f, 0.3f, 0.6f), 1f);
            Assert.AreEqual(0.6f, AeroModel.ControlAuthority(60f, 20f, 30f, 60f, 0.3f, 0.6f), 1e-5f);
        }
    }

    public sealed class GameStateMachineTests
    {
        [Test]
        public void AllowsNormalFlow()
        {
            var sm = new GameStateMachine();
            Assert.IsTrue(sm.TryTransition(GameState.MainMenu));
            Assert.IsTrue(sm.TryTransition(GameState.Loading));
            Assert.IsTrue(sm.TryTransition(GameState.Flying));
            Assert.IsTrue(sm.TryTransition(GameState.Paused));
            Assert.IsTrue(sm.TryTransition(GameState.Flying));
            Assert.IsTrue(sm.TryTransition(GameState.Results));
            Assert.IsTrue(sm.TryTransition(GameState.Loading));
        }

        [Test]
        public void RejectsInvalidTransitionWithoutChangingState()
        {
            var sm = new GameStateMachine(GameState.MainMenu);
            int changes = 0;
            sm.Changed += (_, __) => changes++;
            Assert.IsFalse(sm.TryTransition(GameState.Paused));
            Assert.IsFalse(sm.TryTransition(GameState.Results));
            Assert.AreEqual(GameState.MainMenu, sm.Current);
            Assert.AreEqual(0, changes);
        }
    }
}
