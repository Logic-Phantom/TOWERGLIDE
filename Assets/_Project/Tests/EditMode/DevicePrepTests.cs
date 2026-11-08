using NUnit.Framework;
using TowerGlide.Core.App;
using TowerGlide.Core.Save;

namespace TowerGlide.Tests
{
    public sealed class QualityAutoSelectorTests
    {
        private static DeviceSpec Phone(int mb, int cores) => new DeviceSpec { SystemMemoryMB = mb, ProcessorCount = cores, IsMobile = true };

        [Test]
        public void LowMemoryOrFewCoresGetsPerformanceAt30Fps()
        {
            Assert.AreEqual(QualityTier.Performance, QualityAutoSelector.SelectTier(Phone(3000, 8)));
            Assert.AreEqual(QualityTier.Performance, QualityAutoSelector.SelectTier(Phone(6000, 4)));
            Assert.AreEqual(30, QualityAutoSelector.SelectFrameRate(QualityTier.Performance));
        }

        [Test]
        public void MidAndHighEndPhones()
        {
            Assert.AreEqual(QualityTier.Balanced, QualityAutoSelector.SelectTier(Phone(6000, 8)));
            Assert.AreEqual(QualityTier.Quality, QualityAutoSelector.SelectTier(Phone(12000, 8)));
            Assert.AreEqual(60, QualityAutoSelector.SelectFrameRate(QualityTier.Balanced));
            Assert.AreEqual(QualityTier.Quality, QualityAutoSelector.SelectTier(new DeviceSpec { SystemMemoryMB = 2000, ProcessorCount = 2, IsMobile = false }), "에디터/PC");
        }

        [Test]
        public void UnknownSpecFallsBackToBalanced()
        {
            Assert.AreEqual(QualityTier.Balanced, QualityAutoSelector.SelectTier(Phone(0, 0)));
        }

        [Test]
        public void ChunkBudgetShrinksOnLowTier()
        {
            Assert.Less(QualityAutoSelector.MaxResidentChunks(QualityTier.Performance), QualityAutoSelector.MaxResidentChunks(QualityTier.Quality));
        }

        [Test]
        public void AppliesOnlyOnceAndNeverOverridesUserChoiceAfterwards()
        {
            var s = new SettingsData();
            Assert.IsTrue(QualityAutoSelector.ApplyOnce(s, Phone(3000, 8)));
            Assert.AreEqual((int)QualityTier.Performance, s.qualityTier);
            Assert.AreEqual(30, s.targetFrameRate);
            s.qualityTier = (int)QualityTier.Quality; // 사용자가 직접 올림
            Assert.IsFalse(QualityAutoSelector.ApplyOnce(s, Phone(3000, 8)));
            Assert.AreEqual((int)QualityTier.Quality, s.qualityTier);
            Assert.IsTrue(s.Sanitized().qualityAutoApplied, "저장 검증에서도 플래그 유지");
            SettingsData reset = SettingsOptions.ResetKeepingTutorial(s);
            Assert.IsTrue(QualityAutoSelector.ApplyOnce(reset, Phone(3000, 8)), "초기화하면 기기 기본값을 다시 계산");
            Assert.AreEqual((int)QualityTier.Performance, reset.qualityTier);
        }
    }

    public sealed class FrameStatsTests
    {
        [Test]
        public void SteadySixtyFps()
        {
            var f = new FrameStats(100);
            for (int i = 0; i < 100; i++) f.Add(1f / 60f);
            Assert.AreEqual(60f, f.AverageFps, 0.1f);
            Assert.AreEqual(60f, f.OnePercentLowFps, 0.1f);
            Assert.AreEqual(0f, f.FractionSlowerThan(30f));
        }

        [Test]
        public void SpikesShowInOnePercentLowAndMax()
        {
            var f = new FrameStats(200);
            for (int i = 0; i < 198; i++) f.Add(1f / 60f);
            f.Add(0.1f);
            f.Add(0.1f);
            Assert.Greater(f.AverageFps, 50f);
            Assert.AreEqual(10f, f.OnePercentLowFps, 0.1f, "1% low = 가장 느린 프레임 쪽");
            Assert.AreEqual(100f, f.MaxFrameMs, 0.01f);
            Assert.AreEqual(0.01f, f.FractionSlowerThan(30f), 1e-4f);
        }

        [Test]
        public void RingBufferKeepsOnlyRecentWindowAndIgnoresInvalid()
        {
            var f = new FrameStats(10);
            for (int i = 0; i < 10; i++) f.Add(0.1f);
            for (int i = 0; i < 10; i++) f.Add(1f / 60f);
            f.Add(float.NaN);
            f.Add(-1f);
            Assert.AreEqual(10, f.Count);
            Assert.AreEqual(60f, f.AverageFps, 0.1f);
            f.Reset();
            Assert.AreEqual(0f, f.AverageFps);
        }
    }
}
