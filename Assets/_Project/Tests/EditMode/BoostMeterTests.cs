using System;
using NUnit.Framework;
using TowerGlide.Core.Flight;

namespace TowerGlide.Tests
{
    public sealed class BoostMeterTests
    {
        private static BoostSettings Settings => new BoostSettings
        {
            CapacitySeconds = 2f,
            FullRechargeSeconds = 4f,
            RechargeDelaySeconds = 1f,
            MinRatioToRestart = 0.25f,
        };

        private static void Run(BoostMeter m, float seconds, bool wants, float step = 0.02f)
        {
            int n = (int)Math.Round(seconds / step);
            for (int i = 0; i < n; i++) m.Tick(step, wants);
        }

        [Test]
        public void StartsFull()
        {
            var m = new BoostMeter(Settings);
            Assert.AreEqual(1f, m.Normalized, 1e-6f);
            Assert.IsFalse(m.IsBoosting);
        }

        [Test]
        public void DrainsWhileHeld()
        {
            var m = new BoostMeter(Settings);
            Run(m, 1f, true);
            Assert.IsTrue(m.IsBoosting);
            Assert.AreEqual(0.5f, m.Normalized, 0.02f);
        }

        [Test]
        public void IsNotUnlimited_DepletesAndLocks()
        {
            var m = new BoostMeter(Settings);
            Run(m, 3f, true);
            Assert.IsFalse(m.IsBoosting);
            Assert.IsTrue(m.IsLocked);
            Assert.AreEqual(0f, m.Normalized, 0.02f);
        }

        [Test]
        public void RechargeWaitsForDelay()
        {
            var m = new BoostMeter(Settings);
            Run(m, 1f, true);
            float after = m.Normalized;
            Run(m, 0.9f, false);
            Assert.AreEqual(after, m.Normalized, 1e-4f, "지연 시간 안에는 충전되지 않아야 함");
            Run(m, 1f, false);
            Assert.Greater(m.Normalized, after);
        }

        [Test]
        public void LockedUntilMinRatioReached()
        {
            var m = new BoostMeter(Settings);
            Run(m, 2.02f, true);              // 2초에 소진 → 잠금
            Assert.IsTrue(m.IsLocked);
            Run(m, 1.5f, false);              // 지연 1s + 약 0.5s 충전 ≈ 0.125
            Assert.IsFalse(m.Tick(0.02f, true), "최소 비율 전에는 다시 켜지지 않음");
            Run(m, 1f, false);                // 0.375 >= 0.25
            Assert.IsTrue(m.Tick(0.02f, true));
        }

        [Test]
        public void NeverExceedsCapacity()
        {
            var m = new BoostMeter(Settings);
            Run(m, 30f, false);
            Assert.AreEqual(1f, m.Normalized, 1e-6f);
        }

        [Test]
        public void InvalidSettingsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoostMeter(new BoostSettings { CapacitySeconds = 0f, FullRechargeSeconds = 1f }));
        }
    }
}
