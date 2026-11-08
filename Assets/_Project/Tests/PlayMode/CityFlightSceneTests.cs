using System.Collections;
using NUnit.Framework;
using TowerGlide.App;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.App;
using TowerGlide.Core.Gameplay;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerGlide.Tests.PlayMode
{
    /// <summary>
    /// 실제 CityFlight 씬으로 흐름을 검증한다(명세 11.2: 스폰, 링 진행, 충돌 → 복귀, 일시정지/재개, 결과 → 재시작).
    /// 씬이 Build Settings에 없으면(TOWERGLIDE ▸ 2. 씬 생성 전) 건너뛴다.
    /// </summary>
    public sealed class CityFlightSceneTests
    {
        private FlightSession _session;
        private AircraftFlightController _aircraft;

        private static bool SceneAvailable() => SceneUtility.GetBuildIndexByScenePath("Assets/_Project/Scenes/CityFlight.unity") >= 0;

        private IEnumerator LoadSkylineRun() => LoadFlight(FlightMode.SkylineRun, AircraftIds.ScoutP1);

        /// <summary>지정 기체·모드로 CityFlight를 열고 청크 스트리밍 준비(시작 대기)가 끝날 때까지 기다린다.</summary>
        internal IEnumerator LoadFlight(FlightMode mode, string aircraftId)
        {
            if (!SceneAvailable()) Assert.Ignore("CityFlight 씬이 Build Settings에 없습니다 — TOWERGLIDE ▸ 2. 씬 생성 후 다시 실행");

            GameApp app = GameApp.Instance;
            Assert.IsNotNull(app, "GameApp은 BeforeSceneLoad에서 자동 생성돼야 함");
            if (app.Aircraft?.Catalog != null)
                Assert.IsTrue(app.Aircraft.Select(app.Aircraft.Catalog.Find(aircraftId)), $"{aircraftId} 선택 가능해야 함");
            app.PendingMode = mode;
            if (!app.State.TryTransition(GameState.Loading))
            {
                app.State.TryTransition(GameState.MainMenu);
                app.State.TryTransition(GameState.Loading);
            }

            yield return SceneManager.LoadSceneAsync("CityFlight");
            yield return null; // Start 실행

            _session = Object.FindFirstObjectByType<FlightSession>();
            Assert.IsNotNull(_session);
            float t = 0f;
            while ((_session.IsWaitingForWorld || _session.State != GameState.Flying) && t < 20f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(GameState.Flying, _session.State, "청크 준비 후 비행 시작");
            _aircraft = _session.Aircraft;
            Assert.IsNotNull(_aircraft);
            Session = _session;
        }

        internal FlightSession Session { get; private set; }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        [UnityTest]
        public IEnumerator SpawnsAircraftAndStartsSkylineRun()
        {
            yield return LoadSkylineRun();
            Assert.AreEqual(FlightMode.SkylineRun, _session.Mode);
            Assert.AreEqual(GameState.Flying, _session.State);
            Assert.IsFalse(_aircraft.Body.isKinematic);
            Assert.AreEqual(12, _session.Course.Count);
            Assert.AreEqual(0, _session.Course.Sequence.PassedCount);
        }

        [UnityTest]
        public IEnumerator FlyingThroughFirstGateAdvancesProgress()
        {
            yield return LoadSkylineRun();
            CheckpointGate first = _session.Course.FirstGate;
            Pose approach = first.ApproachPose; // 링 25m 앞, 링 방향
            _aircraft.ResetTo(approach.position, approach.rotation, 30f);

            float t = 0f;
            while (_session.Course.Sequence.PassedCount == 0 && t < 3f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(1, _session.Course.Sequence.PassedCount, "링 통과가 집계돼야 함");
            Assert.AreEqual(1, _session.Score.Breakdown.RingsPassed);

            // 같은 링을 다시 지나가도 중복 집계 없음
            _aircraft.ResetTo(approach.position, approach.rotation, 30f);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(1, _session.Score.Breakdown.RingsPassed);
        }

        [UnityTest]
        public IEnumerator CrashIsCountedThenAircraftRespawns()
        {
            yield return LoadSkylineRun();
            // 청크 스트리밍: 지금 로드된 청크의 건물 중 기체와 가장 가까운 것(실루엣은 충돌체가 없음)
            ObstacleMarker building = null;
            float best = float.MaxValue;
            foreach (ObstacleMarker m in Object.FindObjectsByType<ObstacleMarker>(FindObjectsSortMode.None))
            {
                if (m.Type != ObstacleType.Building) continue;
                float d = Vector3.Distance(m.transform.position, _aircraft.transform.position);
                if (d < best)
                {
                    best = d;
                    building = m;
                }
            }
            Assume.That(building != null, "건물이 없는 씬");

            Bounds b = default;
            bool hasBounds = false;
            foreach (Collider c in building.GetComponentsInChildren<Collider>())
            {
                if (!hasBounds) { b = c.bounds; hasBounds = true; }
                else b.Encapsulate(c.bounds);
            }
            Vector3 target = new Vector3(b.center.x, Mathf.Min(b.max.y - 5f, b.min.y + 20f), b.center.z);
            Vector3 start = target - Vector3.forward * (b.extents.z + 15f); // 거리 폭(≥30m) 안쪽에서 출발
            _aircraft.ResetTo(start, Quaternion.LookRotation(Vector3.forward), 35f);

            float t = 0f;
            while (_session.Score.Breakdown.Collisions == 0 && t < 4f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(_session.Score.Breakdown.Collisions, 1, "건물 충돌이 집계돼야 함");

            yield return new WaitForSeconds(2f); // 복귀 지연(1.2s) 이후
            Assert.Greater(Vector3.Distance(_aircraft.transform.position, target), 20f, "충돌 지점에서 복귀해야 함");
            Assert.AreEqual(GameState.Flying, _session.State);
        }

        [UnityTest]
        public IEnumerator PauseStopsTimeAndResumeRestores()
        {
            yield return LoadSkylineRun();
            _session.Pause();
            Assert.AreEqual(GameState.Paused, _session.State);
            Assert.AreEqual(0f, Time.timeScale);
            float left = _session.TimeLeft;
            yield return null;
            yield return null;
            Assert.AreEqual(left, _session.TimeLeft, "일시정지 중 타이머 정지");
            _session.Resume();
            Assert.AreEqual(GameState.Flying, _session.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator FinishShowsResultsAndRestartReloads()
        {
            yield return LoadSkylineRun();
            _session.FinishRun(completed: false);
            Assert.AreEqual(GameState.Results, _session.State);
            Assert.AreEqual(0f, Time.timeScale);

            _session.Restart();
            yield return null;
            yield return null;
            FlightSession fresh = Object.FindFirstObjectByType<FlightSession>();
            Assert.IsNotNull(fresh);
            float t = 0f;
            while (fresh.State != GameState.Flying && t < 20f) // 청크 준비 대기
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(GameState.Flying, fresh.State);
            Assert.AreEqual(FlightMode.SkylineRun, fresh.Mode, "재시작은 같은 모드");
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
