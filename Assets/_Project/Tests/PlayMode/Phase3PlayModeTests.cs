using System.Collections;
using System.IO;
using NUnit.Framework;
using TowerGlide.App;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.Audio;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;
using TowerGlide.Save;
using TowerGlide.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerGlide.Tests.PlayMode
{
    /// <summary>로컬 저장 파일 단위 검증(명세 4.3·Phase 3 "설정과 기록이 앱 재실행 후 유지") — 임시 폴더 사용, 실제 저장 파일은 건드리지 않음.</summary>
    public sealed class LocalSaveServicePlayModeTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Application.temporaryCachePath, "towerglide-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void SettingsRecordsAndAircraftSurviveRestart()
        {
            var first = new LocalSaveService(_dir);
            Assert.AreEqual(SaveLoadStatus.Missing, first.LastLoadStatus);
            first.Load();
            first.Data.settings.invertPitch = true;
            first.Data.settings.sfxVolume = 0.3f;
            first.Data.records.bestSkylineScore = 4321;
            first.Data.aircraft.selectedAircraftId = AircraftIds.ViperF9;
            Assert.IsTrue(first.Save());

            var restarted = new LocalSaveService(_dir); // "앱 재실행"
            restarted.Load();
            Assert.AreEqual(SaveLoadStatus.Ok, restarted.LastLoadStatus);
            Assert.IsTrue(restarted.Data.settings.invertPitch);
            Assert.AreEqual(0.3f, restarted.Data.settings.sfxVolume, 1e-4f);
            Assert.AreEqual(4321, restarted.Data.records.bestSkylineScore);
            Assert.AreEqual(AircraftIds.ViperF9, restarted.Data.aircraft.selectedAircraftId);
            Assert.IsFalse(File.Exists(restarted.FilePath + ".tmp"), "원자적 쓰기 후 임시 파일 없음");
        }

        [Test]
        public void CorruptFileIsBackedUpAndDefaultsUsed()
        {
            File.WriteAllText(Path.Combine(_dir, "save.json"), "{ not json ]]");
            var s = new LocalSaveService(_dir);
            LogAssert.ignoreFailingMessages = true; // JsonUtility 파싱 경고 허용
            s.Load();
            Assert.AreEqual(SaveLoadStatus.Corrupted, s.LastLoadStatus);
            Assert.AreEqual(SaveData.CurrentVersion, s.Data.version);
            Assert.IsNotEmpty(Directory.GetFiles(_dir, "save.corrupt-*.json"), "손상 파일 백업");
            Assert.IsTrue(s.Save(), "기본값으로 계속 저장 가능");
        }

        [Test]
        public void NewerVersionFileIsNotOverwritten()
        {
            string path = Path.Combine(_dir, "save.json");
            File.WriteAllText(path, "{\"version\":99,\"settings\":{},\"records\":{}}");
            var s = new LocalSaveService(_dir);
            s.Load();
            Assert.IsTrue(s.IsReadOnly);
            Assert.IsFalse(s.Save());
            StringAssert.Contains("\"version\":99", File.ReadAllText(path));
        }
    }

    public sealed class AudioPlayModeTests
    {
        [Test]
        public void ProceduralClipsAreCreatedOnce()
        {
            Assert.IsNotNull(GameApp.Instance, "GameApp 자동 생성");
            var audio = GameApp.Instance.Audio;
            Assert.IsNotNull(audio);
            AudioClip crash = audio.GetSfx(SfxKind.Crash);
            Assert.Greater(crash.samples, 0);
            Assert.AreEqual(ProceduralAudio.DefaultSampleRate, crash.frequency);
            Assert.AreSame(crash, audio.GetSfx(SfxKind.Crash), "캐시");
            foreach (EngineSoundKind k in System.Enum.GetValues(typeof(EngineSoundKind)))
                Assert.Greater(audio.GetEngineLoop(k).length, 0.5f);
            Assert.DoesNotThrow(() => audio.PlaySfx(SfxKind.RingPass));
        }

        [Test]
        public void CommitSettingsAppliesVolumes()
        {
            GameApp app = GameApp.Instance;
            float master = app.Settings.masterVolume, sfx = app.Settings.sfxVolume;
            bool notified = false;
            app.SettingsChanged += () => notified = true;
            app.Settings.masterVolume = 0.4f;
            app.Settings.sfxVolume = 0.2f;
            app.CommitSettings();
            Assert.AreEqual(0.4f, AudioListener.volume, 1e-4f);
            Assert.AreEqual(0.2f, app.Audio.SfxVolume, 1e-4f);
            Assert.IsTrue(notified);
            app.Settings.masterVolume = master;
            app.Settings.sfxVolume = sfx;
            app.CommitSettings();
        }
    }

    public sealed class TutorialPlayModeTests
    {
        [UnityTest]
        public IEnumerator FirstFlightShowsTutorialAndSkipMarksSeen()
        {
            GameApp app = GameApp.Instance;
            bool before = app.Settings.tutorialSeen;
            app.Settings.tutorialSeen = false;

            var flow = new CityFlightSceneTests();
            yield return flow.LoadFlight(FlightMode.FreeFlight, AircraftIds.ScoutP1);
            yield return null;
            var tutorial = Object.FindFirstObjectByType<TutorialController>();
            Assert.IsNotNull(tutorial, "씬 생성 후 실행");
            Assert.IsTrue(tutorial.IsRunning, "첫 비행이면 튜토리얼 시작");
            tutorial.Skip();
            Assert.IsTrue(app.Settings.tutorialSeen, "건너뛰면 다시 나오지 않음");

            app.Settings.tutorialSeen = before;
        }
    }
}
