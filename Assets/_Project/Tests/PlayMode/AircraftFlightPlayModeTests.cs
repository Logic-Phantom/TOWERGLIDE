using System.Collections;
using NUnit.Framework;
using TowerGlide.Core.Input;
using TowerGlide.Flight;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerGlide.Tests.PlayMode
{
    /// <summary>
    /// 씬 없이 기체만 만들어 물리 방향·안정화를 검증한다(명세 11.2). 수치가 아니라 "방향"을 본다 — 튜닝 변경에 강하게.
    /// </summary>
    public sealed class AircraftFlightPlayModeTests
    {
        private GameObject _go;
        private AircraftFlightController _aircraft;
        private AircraftFlightTuning _tuning;

        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<AircraftFlightTuning>();
            _go = new GameObject("TestAircraft");
            _go.SetActive(false); // 튜닝 연결 전에 Awake가 돌지 않도록
            _go.AddComponent<Rigidbody>();
            _go.AddComponent<CapsuleCollider>().direction = 2;
            _aircraft = _go.AddComponent<AircraftFlightController>();
            _aircraft.SetTuning(_tuning);
            _aircraft.ResetTo(new Vector3(0f, 500f, 0f), Quaternion.identity, _tuning.cruiseSpeed);
            _go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_go);
            Object.Destroy(_tuning);
        }

        private static IEnumerator Simulate(float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator SpawnsWithRigidbodyAndFliesForward()
        {
            Rigidbody rb = _go.GetComponent<Rigidbody>();
            Assert.IsFalse(rb.isKinematic);
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, rb.interpolation);
            float z0 = _go.transform.position.z;
            yield return Simulate(1f);
            Assert.Greater(_go.transform.position.z, z0 + 15f, "순항 속도로 전진해야 함");
        }

        [UnityTest]
        public IEnumerator HoldsAltitudeRoughlyWithNoInput()
        {
            float y0 = _go.transform.position.y;
            yield return Simulate(2f);
            Assert.AreEqual(y0, _go.transform.position.y, 25f, "입력 없이 순항 시 급강하/급상승하지 않아야 함");
        }

        [UnityTest]
        public IEnumerator PitchUpClimbs()
        {
            float y0 = _go.transform.position.y;
            for (int i = 0; i < 75; i++)
            {
                _aircraft.SetInput(new FlightInput { Pitch = 1f });
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(_go.transform.position.y, y0 + 5f);
        }

        [UnityTest]
        public IEnumerator RollRightBanksRightThenTurnsRight()
        {
            for (int i = 0; i < 40; i++)
            {
                _aircraft.SetInput(new FlightInput { Roll = 1f });
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(_aircraft.BankDeg, 10f, "오른쪽 입력 → 오른쪽으로 기울어야 함");
            _aircraft.SetInput(FlightInput.Neutral);
            yield return Simulate(1.5f);
            Assert.Greater(_go.transform.position.x, 1f, "기울기에 따라 오른쪽으로 선회해야 함");
        }

        [UnityTest]
        public IEnumerator AngularVelocitySettlesAfterRelease()
        {
            for (int i = 0; i < 30; i++)
            {
                _aircraft.SetInput(new FlightInput { Roll = 1f, Pitch = 0.5f });
                yield return new WaitForFixedUpdate();
            }
            _aircraft.SetInput(FlightInput.Neutral);
            yield return Simulate(2.5f);
            Rigidbody rb = _go.GetComponent<Rigidbody>();
            Assert.Less(rb.angularVelocity.magnitude, 0.6f, "손을 떼면 회전이 감쇠해야 함");
        }

        [UnityTest]
        public IEnumerator BoostIsLimited()
        {
            for (int i = 0; i < Mathf.CeilToInt((_tuning.boostCapacitySeconds + 0.5f) / Time.fixedDeltaTime); i++)
            {
                _aircraft.SetInput(new FlightInput { Boost = true });
                yield return new WaitForFixedUpdate();
            }
            Assert.IsFalse(_aircraft.IsBoosting, "게이지 소진 후 부스트는 꺼져야 함");
        }
    }
}
