using System;
using NUnit.Framework;
using TowerGlide.Core.Audio;
using TowerGlide.Core.Input;
using TowerGlide.Core.Save;
using TowerGlide.Core.Tutorial;

namespace TowerGlide.Tests
{
    public sealed class ProceduralAudioTests
    {
        private static void AssertClean(float[] s, float peak)
        {
            Assert.IsNotEmpty(s);
            float max = 0f;
            foreach (float v in s)
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                max = Math.Max(max, Math.Abs(v));
            }
            Assert.LessOrEqual(max, peak + 1e-4f, "정규화 상한");
            Assert.Greater(max, 0.05f, "무음이 아님");
        }

        [Test]
        public void AllSfxAreCleanAndShort()
        {
            foreach (SfxKind k in Enum.GetValues(typeof(SfxKind)))
            {
                float[] s = ProceduralAudio.Sfx(k);
                AssertClean(s, 0.9f);
                Assert.LessOrEqual(s.Length, ProceduralAudio.DefaultSampleRate, $"{k}: 1초 이하(짧은 피드백)");
            }
        }

        [Test]
        public void EngineLoopsAreCleanAndSeamless()
        {
            foreach (EngineSoundKind k in Enum.GetValues(typeof(EngineSoundKind)))
            {
                float[] s = ProceduralAudio.EngineLoop(k);
                AssertClean(s, 0.8f);
                AssertSeamless(s, k.ToString());
            }
            float[] wind = ProceduralAudio.WindLoop();
            AssertClean(wind, 0.7f);
            AssertSeamless(wind, "wind");
        }

        /// <summary>루프 이음매(마지막→첫 샘플) 차이가 신호 안의 일반적인 인접 차이보다 크게 튀지 않아야 함.</summary>
        private static void AssertSeamless(float[] s, string label)
        {
            float typical = 0f;
            for (int i = 1; i < s.Length; i++) typical = Math.Max(typical, Math.Abs(s[i] - s[i - 1]));
            float seam = Math.Abs(s[0] - s[s.Length - 1]);
            Assert.LessOrEqual(seam, typical * 1.5f + 1e-4f, $"{label} 이음매 클릭");
        }

        [Test]
        public void DifferentEnginesSoundDifferent()
        {
            float[] p = ProceduralAudio.EngineLoop(EngineSoundKind.Propeller);
            float[] j = ProceduralAudio.EngineLoop(EngineSoundKind.Jet);
            int n = Math.Min(p.Length, j.Length);
            double diff = 0;
            for (int i = 0; i < n; i++) diff += Math.Abs(p[i] - j[i]);
            Assert.Greater(diff / n, 0.05);
        }

        [Test]
        public void IsDeterministic()
        {
            CollectionAssert.AreEqual(ProceduralAudio.Sfx(SfxKind.Crash), ProceduralAudio.Sfx(SfxKind.Crash));
        }

        [Test]
        public void FitPutsWholeCyclesInLoop()
        {
            float hz = ProceduralAudio.Fit(91.3f, 2f);
            Assert.AreEqual(Math.Round(hz * 2f), hz * 2f, 1e-4);
            Assert.AreEqual(0f, ProceduralAudio.Fit(0f, 2f));
        }

        [Test]
        public void CrossfadeShortensByFadeLength()
        {
            var s = new float[100];
            Assert.AreEqual(90, ProceduralAudio.CrossfadeLoop(s, 10).Length);
            Assert.AreEqual(100, ProceduralAudio.CrossfadeLoop(s, 0).Length);
        }
    }

    public sealed class EngineSoundModelTests
    {
        [Test]
        public void PitchAndWindRiseWithSpeed()
        {
            foreach (EngineSoundKind k in Enum.GetValues(typeof(EngineSoundKind)))
                Assert.Greater(EngineSoundModel.Pitch(k, 1f, 0f, false), EngineSoundModel.Pitch(k, 0f, 0f, false));
            Assert.Greater(EngineSoundModel.WindVolume(1f), EngineSoundModel.WindVolume(0.5f));
            Assert.AreEqual(0f, EngineSoundModel.WindVolume(0f));
        }

        [Test]
        public void TurbofanIsDeeperRangeAndIgnoresBoost()
        {
            float jetRange = EngineSoundModel.Pitch(EngineSoundKind.Jet, 1f, 0f, false) - EngineSoundModel.Pitch(EngineSoundKind.Jet, 0f, 0f, false);
            float fanRange = EngineSoundModel.Pitch(EngineSoundKind.Turbofan, 1f, 0f, false) - EngineSoundModel.Pitch(EngineSoundKind.Turbofan, 0f, 0f, false);
            Assert.Greater(jetRange, fanRange);
            Assert.AreEqual(EngineSoundModel.Pitch(EngineSoundKind.Turbofan, 0.5f, 0f, true), EngineSoundModel.Pitch(EngineSoundKind.Turbofan, 0.5f, 0f, false));
        }

        [Test]
        public void VolumesStayInRange()
        {
            Assert.That(EngineSoundModel.EngineVolume(2f, 1f, true), Is.InRange(0f, 1f));
            Assert.AreEqual(0f, EngineSoundModel.Speed01(10f, 20f, 60f));
            Assert.AreEqual(1f, EngineSoundModel.Speed01(80f, 20f, 60f));
        }
    }

    public sealed class TutorialSequenceTests
    {
        private static void Run(TutorialSequence t, float seconds, FlightInput input, float bank = 0f, bool boost = false)
        {
            for (float e = 0f; e < seconds; e += 0.02f) t.Tick(0.02f, input, bank, boost);
        }

        [Test]
        public void AdvancesOnlyWhenActionIsHeld()
        {
            var t = new TutorialSequence(boostAvailable: true);
            Run(t, 3f, FlightInput.Neutral);
            Assert.AreEqual(TutorialStep.Roll, t.Current, "손 놓고 기다리면 진행 안 됨");
            Run(t, 1.1f, new FlightInput { Roll = 0.8f });
            Assert.AreEqual(TutorialStep.Turn, t.Current);
            Run(t, 1.6f, FlightInput.Neutral, bank: 40f);
            Assert.AreEqual(TutorialStep.Pitch, t.Current);
            Run(t, 1.1f, new FlightInput { Pitch = -0.9f });
            Assert.AreEqual(TutorialStep.Boost, t.Current);
            Run(t, 0.6f, FlightInput.Neutral, boost: true);
            Assert.AreEqual(TutorialStep.Done, t.Current);
            Run(t, 2.1f, FlightInput.Neutral);
            Assert.IsTrue(t.IsComplete);
            Assert.AreEqual(string.Empty, t.Instruction);
        }

        [Test]
        public void ProgressIsCumulativeNotReset()
        {
            var t = new TutorialSequence(true);
            Run(t, 0.6f, new FlightInput { Roll = 1f });
            Run(t, 1f, FlightInput.Neutral);
            Assert.AreEqual(0.6f, t.StepProgress, 0.05f);
            Run(t, 0.5f, new FlightInput { Roll = -1f });
            Assert.AreEqual(TutorialStep.Turn, t.Current);
        }

        [Test]
        public void BoostStepSkippedForAircraftWithoutBoost()
        {
            var t = new TutorialSequence(boostAvailable: false);
            Run(t, 1.1f, new FlightInput { Roll = 1f });
            Run(t, 1.6f, FlightInput.Neutral, bank: 30f);
            Run(t, 1.1f, new FlightInput { Pitch = 1f });
            Assert.AreEqual(TutorialStep.Done, t.Current);
        }

        [Test]
        public void SkipCompletesImmediately()
        {
            var t = new TutorialSequence(true);
            t.Skip();
            Assert.IsTrue(t.IsComplete);
            Assert.IsFalse(t.Tick(0.1f, new FlightInput { Roll = 1f }, 0f, false));
        }
    }

    public sealed class SettingsOptionsTests
    {
        [Test]
        public void CyclesQualityAndFrameRate()
        {
            Assert.AreEqual(1, SettingsOptions.NextQuality(0));
            Assert.AreEqual(0, SettingsOptions.NextQuality(2));
            Assert.AreEqual(30, SettingsOptions.NextFrameRate(60));
            Assert.AreEqual(60, SettingsOptions.NextFrameRate(30));
            Assert.AreEqual("Quality", SettingsOptions.QualityLabel(9));
        }

        [Test]
        public void SnapsSensitivityToStepsWithinRange()
        {
            Assert.AreEqual(0.7f, SettingsOptions.SnapSensitivity(0.73f), 1e-5f);
            Assert.AreEqual(1.5f, SettingsOptions.SnapSensitivity(9f), 1e-5f);
            Assert.AreEqual(0.5f, SettingsOptions.SnapSensitivity(-1f), 1e-5f);
            Assert.AreEqual(1f, SettingsOptions.SnapSensitivity(float.NaN), 1e-5f);
        }

        [Test]
        public void ResetKeepsTutorialFlagButRestoresDefaults()
        {
            var cur = new SettingsData { tutorialSeen = true, invertPitch = true, masterVolume = 0.1f };
            SettingsData r = SettingsOptions.ResetKeepingTutorial(cur);
            Assert.IsTrue(r.tutorialSeen);
            Assert.IsFalse(r.invertPitch);
            Assert.AreEqual(1f, r.masterVolume);
        }

        [Test]
        public void LabelsAreReadable()
        {
            Assert.AreEqual("70%", SettingsOptions.Percent(0.7f));
            Assert.AreEqual("켜짐", SettingsOptions.OnOff(true));
        }
    }
}
