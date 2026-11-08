using NUnit.Framework;
using TowerGlide.Core.Input;

namespace TowerGlide.Tests
{
    public sealed class InputShapingTests
    {
        [Test]
        public void DeadZone_ZeroesSmallInput()
        {
            Assert.AreEqual(0f, InputShaping.ApplyDeadZone(0.05f, 0.1f));
            Assert.AreEqual(0f, InputShaping.ApplyDeadZone(-0.1f, 0.1f));
        }

        [Test]
        public void DeadZone_RescalesSoEdgeIsContinuousAndFullIsOne()
        {
            Assert.AreEqual(0f, InputShaping.ApplyDeadZone(0.1000001f, 0.1f), 1e-4f);
            Assert.AreEqual(1f, InputShaping.ApplyDeadZone(1f, 0.1f), 1e-6f);
            Assert.AreEqual(-1f, InputShaping.ApplyDeadZone(-1f, 0.1f), 1e-6f);
            Assert.AreEqual(0.5f, InputShaping.ApplyDeadZone(0.55f, 0.1f), 1e-5f);
        }

        [Test]
        public void DeadZone_ClampsOverRangeAndNaN()
        {
            Assert.AreEqual(1f, InputShaping.ApplyDeadZone(3f, 0.1f), 1e-6f);
            Assert.AreEqual(0f, InputShaping.ApplyDeadZone(float.NaN, 0.1f));
        }

        [Test]
        public void Expo_KeepsEndpointsAndSignButSoftensCenter()
        {
            Assert.AreEqual(1f, InputShaping.ApplyExpo(1f, 0.5f), 1e-6f);
            Assert.AreEqual(-1f, InputShaping.ApplyExpo(-1f, 0.5f), 1e-6f);
            Assert.Less(InputShaping.ApplyExpo(0.5f, 0.5f), 0.5f);
            Assert.Greater(InputShaping.ApplyExpo(0.5f, 0.5f), 0f);
        }

        [Test]
        public void Shape_InvertPitchFlipsOnlyPitch()
        {
            var settings = InputShapingSettings.Default;
            settings.InvertPitch = true;
            settings.Expo = 0f;
            settings.DeadZone = 0f;
            FlightInput shaped = InputShaping.Shape(new FlightInput { Pitch = 0.5f, Roll = 0.5f }, settings);
            Assert.AreEqual(-0.5f, shaped.Pitch, 1e-5f);
            Assert.AreEqual(0.5f, shaped.Roll, 1e-5f);
        }

        [Test]
        public void Shape_SensitivityIsClampedToUnitRange()
        {
            var settings = InputShapingSettings.Default;
            settings.Sensitivity = 2f;
            settings.Expo = 0f;
            settings.DeadZone = 0f;
            FlightInput shaped = InputShaping.Shape(new FlightInput { Roll = 0.9f }, settings);
            Assert.AreEqual(1f, shaped.Roll, 1e-6f);
        }

        [Test]
        public void Combine_TakesDominantAxisAndOrsButtons()
        {
            var a = new FlightInput { Pitch = 0.2f, Roll = -0.9f, Boost = true };
            var b = new FlightInput { Pitch = -0.6f, Roll = 0.3f, AirBrake = true };
            FlightInput c = FlightInput.Combine(a, b);
            Assert.AreEqual(-0.6f, c.Pitch);
            Assert.AreEqual(-0.9f, c.Roll);
            Assert.IsTrue(c.Boost);
            Assert.IsTrue(c.AirBrake);
        }

        [Test]
        public void Sanitized_ClampsAndRemovesNaN()
        {
            FlightInput s = new FlightInput { Pitch = 5f, Roll = float.NaN, Yaw = float.NegativeInfinity }.Sanitized();
            Assert.AreEqual(1f, s.Pitch);
            Assert.AreEqual(0f, s.Roll);
            Assert.AreEqual(0f, s.Yaw);
        }
    }
}
