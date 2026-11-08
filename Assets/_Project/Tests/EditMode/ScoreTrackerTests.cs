using NUnit.Framework;
using TowerGlide.Core.Gameplay;

namespace TowerGlide.Tests
{
    public sealed class ScoreTrackerTests
    {
        private static ScoreRules Rules => ScoreRules.Default;

        [Test]
        public void RingWithoutComboGivesBasePoints()
        {
            var t = new ScoreTracker(Rules);
            Assert.AreEqual(Rules.RingPoints, t.OnRingPassed(1f));
            Assert.AreEqual(0, t.CurrentCombo);
        }

        [Test]
        public void ChainedRingsBuildCombo()
        {
            var t = new ScoreTracker(Rules);
            t.OnRingPassed(1f);
            int second = t.OnRingPassed(1f + Rules.ComboWindowSeconds - 0.1f);
            Assert.AreEqual(Rules.RingPoints + Rules.ComboStepBonus, second);
            Assert.AreEqual(1, t.Breakdown.BestCombo);
        }

        [Test]
        public void ComboBreaksAfterWindow()
        {
            var t = new ScoreTracker(Rules);
            t.OnRingPassed(1f);
            t.OnRingPassed(2f);
            t.OnRingPassed(2f + Rules.ComboWindowSeconds + 0.5f);
            Assert.AreEqual(0, t.CurrentCombo);
        }

        [Test]
        public void ComboIsCapped()
        {
            var t = new ScoreTracker(Rules);
            for (int i = 0; i < 30; i++) t.OnRingPassed(i * 0.5f);
            Assert.AreEqual(Rules.MaxComboSteps, t.CurrentCombo);
        }

        [Test]
        public void CollisionPenalizesAndBreaksCombo()
        {
            var t = new ScoreTracker(Rules);
            t.OnRingPassed(1f);
            t.OnRingPassed(2f);
            t.OnCollision();
            Assert.AreEqual(0, t.CurrentCombo);
            Assert.AreEqual(1, t.Breakdown.Collisions);
            Assert.AreEqual(Rules.CollisionPenalty, t.Breakdown.CollisionPenalty);
        }

        [Test]
        public void TotalNeverNegative()
        {
            var t = new ScoreTracker(Rules);
            for (int i = 0; i < 5; i++) t.OnCollision();
            Assert.AreEqual(0, t.Total);
        }

        [Test]
        public void NearMissScalesWithProximityAndSpeed()
        {
            var t = new ScoreTracker(Rules);
            int far = t.OnNearMiss(0f, 300f);
            int close = t.OnNearMiss(1f, Rules.NearMissSpeedForMaxBonusKmh);
            int slow = t.OnNearMiss(1f, 0f);
            Assert.AreEqual(Rules.NearMissBasePoints, far);
            Assert.AreEqual(Rules.NearMissBasePoints + Rules.NearMissMaxBonus, close);
            Assert.AreEqual(Rules.NearMissBasePoints, slow);
            Assert.AreEqual(3, t.Breakdown.NearMisses);
        }

        [Test]
        public void FinishAddsBonusesOnlyWhenCompletedAndOnlyOnce()
        {
            var t = new ScoreTracker(Rules);
            t.OnRingPassed(1f);
            ScoreBreakdown b = t.Finish(true, 10.7f);
            Assert.AreEqual(Rules.CompletionBonus, b.CompletionBonus);
            Assert.AreEqual(10 * Rules.PointsPerRemainingSecond, b.TimeBonus);
            int total = b.Total;
            t.Finish(true, 100f);
            t.OnRingPassed(2f);
            Assert.AreEqual(total, t.Total, "종료 후 점수 변화 없음");

            var t2 = new ScoreTracker(Rules);
            ScoreBreakdown b2 = t2.Finish(false, 50f);
            Assert.AreEqual(0, b2.CompletionBonus);
            Assert.AreEqual(0, b2.TimeBonus);
        }
    }
}
