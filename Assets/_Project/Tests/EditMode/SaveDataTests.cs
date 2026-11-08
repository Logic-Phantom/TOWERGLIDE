using NUnit.Framework;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;

namespace TowerGlide.Tests
{
    public sealed class SaveDataTests
    {
        [Test]
        public void NullLoadReturnsDefaultsAsCorrupted()
        {
            SaveData d = SaveDataMigrator.Normalize(null, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Corrupted, status);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsNotNull(d.settings);
            Assert.IsNotNull(d.records);
        }

        [Test]
        public void MissingSectionsAreRestored()
        {
            var loaded = new SaveData { version = SaveData.CurrentVersion, settings = null, records = null };
            SaveData d = SaveDataMigrator.Normalize(loaded, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Ok, status);
            Assert.IsNotNull(d.settings);
            Assert.IsNotNull(d.records);
        }

        [Test]
        public void OutOfRangeValuesAreClamped()
        {
            var loaded = new SaveData
            {
                settings = new SettingsData
                {
                    qualityTier = 9,
                    cameraShake = 5f,
                    controlSensitivity = float.NaN,
                    masterVolume = -1f,
                    targetFrameRate = 144,
                },
                records = new RecordsData { bestSkylineScore = -10, topSpeedKmh = float.PositiveInfinity, totalRuns = -3 },
            };
            SaveData d = SaveDataMigrator.Normalize(loaded, out _);
            Assert.AreEqual(2, d.settings.qualityTier);
            Assert.AreEqual(1f, d.settings.cameraShake);
            Assert.AreEqual(1f, d.settings.controlSensitivity);
            Assert.AreEqual(0f, d.settings.masterVolume);
            Assert.AreEqual(60, d.settings.targetFrameRate);
            Assert.AreEqual(0, d.records.bestSkylineScore);
            Assert.AreEqual(0f, d.records.topSpeedKmh);
            Assert.AreEqual(0, d.records.totalRuns);
        }

        [Test]
        public void VersionZeroIsMigrated()
        {
            SaveDataMigrator.Normalize(new SaveData { version = 0 }, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Migrated, status);
        }

        [Test]
        public void NewerVersionIsFlaggedAndVersionKept()
        {
            SaveData d = SaveDataMigrator.Normalize(new SaveData { version = SaveData.CurrentVersion + 5 }, out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.NewerVersion, status);
            Assert.AreEqual(SaveData.CurrentVersion + 5, d.version);
        }

        [Test]
        public void RecordsApplyTracksBestScoreAndTime()
        {
            var r = new RecordsData();
            var run = new RunResult
            {
                Mode = FlightMode.SkylineRun,
                Completed = true,
                FlightSeconds = 120f,
                DistanceMeters = 3000f,
                MaxSpeedKmh = 200f,
                Score = new ScoreBreakdown { RingPoints = 1200 },
            };
            Assert.IsTrue(r.Apply(run));
            Assert.AreEqual(1200, r.bestSkylineScore);
            Assert.AreEqual(120f, r.bestSkylineSeconds);

            run.FlightSeconds = 150f;
            run.Score = new ScoreBreakdown { RingPoints = 500 };
            Assert.IsFalse(r.Apply(run), "더 느리고 낮은 점수는 갱신 아님");
            Assert.AreEqual(120f, r.bestSkylineSeconds);
            Assert.AreEqual(2, r.totalRuns);
        }

        [Test]
        public void FreeFlightUpdatesOnlyGeneralRecords()
        {
            var r = new RecordsData();
            bool best = r.Apply(new RunResult { Mode = FlightMode.FreeFlight, DistanceMeters = 5000f, MaxAltitudeMeters = 300f });
            Assert.IsFalse(best);
            Assert.AreEqual(5000f, r.longestFlightMeters);
            Assert.AreEqual(300f, r.maxAltitudeMeters);
            Assert.AreEqual(0, r.bestSkylineScore);
        }

        [Test]
        public void IncompleteRunDoesNotSetBestTime()
        {
            var r = new RecordsData();
            r.Apply(new RunResult { Mode = FlightMode.SkylineRun, Completed = false, FlightSeconds = 30f });
            Assert.AreEqual(0f, r.bestSkylineSeconds);
        }
    }
}
