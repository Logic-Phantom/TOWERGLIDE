using System;
using NUnit.Framework;
using TowerGlide.Core.Gameplay;

namespace TowerGlide.Tests
{
    public sealed class CheckpointSequenceTests
    {
        [Test]
        public void PassesInOrderAndReportsProgress()
        {
            var s = new CheckpointSequence(4);
            Assert.AreEqual(CheckpointPassResult.Passed, s.TryPass(0));
            Assert.AreEqual(CheckpointPassResult.Passed, s.TryPass(1));
            Assert.AreEqual(0.5f, s.Progress, 1e-6f);
            Assert.AreEqual(1, s.LastPassedIndex);
        }

        [Test]
        public void DuplicatePassIsIgnored()
        {
            var s = new CheckpointSequence(3);
            s.TryPass(0);
            Assert.AreEqual(CheckpointPassResult.AlreadyPassed, s.TryPass(0));
            Assert.AreEqual(1, s.PassedCount);
        }

        [Test]
        public void SkippingIsOutOfOrder()
        {
            var s = new CheckpointSequence(3);
            Assert.AreEqual(CheckpointPassResult.OutOfOrder, s.TryPass(2));
            Assert.AreEqual(0, s.PassedCount);
        }

        [Test]
        public void LastRingCompletesAndFurtherPassesAreIgnored()
        {
            var s = new CheckpointSequence(2);
            s.TryPass(0);
            Assert.AreEqual(CheckpointPassResult.Completed, s.TryPass(1));
            Assert.IsTrue(s.IsComplete);
            Assert.AreEqual(1f, s.Progress, 1e-6f);
            Assert.AreEqual(CheckpointPassResult.Ignored, s.TryPass(1));
            Assert.AreEqual(CheckpointPassResult.Ignored, s.TryPass(-1));
        }

        [Test]
        public void ResetStartsOver()
        {
            var s = new CheckpointSequence(2);
            s.TryPass(0);
            s.Reset();
            Assert.AreEqual(0, s.NextIndex);
            Assert.AreEqual(-1, s.LastPassedIndex);
        }

        [Test]
        public void EmptyCourseThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckpointSequence(0));
        }
    }
}
