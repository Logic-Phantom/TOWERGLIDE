using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TowerGlide.App;
using TowerGlide.Cameras;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Input;
using TowerGlide.Core.World.Streaming;
using TowerGlide.Flight;
using TowerGlide.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerGlide.Tests.PlayMode
{
    /// <summary>
    /// 기체 3종 비행 특성 비교(애드온 명세 14.11 "같은 입력에 기종별 움직임이 실제로 구분") — 씬 없이 프리팹만 띄워 측정.
    /// 숫자가 아니라 순서(제트 > 여객기 > 소형기 속도, 소형기 > 여객기 기동성)를 검증해 튜닝 변경에 견고하게.
    /// 카탈로그(Resources/AircraftCatalog)는 TOWERGLIDE ▸ 2. 씬 생성이 만든다 — 없으면 Ignore.
    /// </summary>
    public sealed class AircraftComparisonPlayModeTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject g in _spawned) if (g != null) Object.Destroy(g);
            _spawned.Clear();
        }

        private AircraftFlightController Spawn(string id, Vector3 position)
        {
            var catalog = Resources.Load<AircraftCatalog>(AircraftSelectionService.CatalogResourcePath);
            if (catalog == null) Assert.Ignore("AircraftCatalog 없음 — 씬 생성 후 실행");
            AircraftDefinition def = catalog.Find(id);
            Assert.IsNotNull(def, id);
            Assert.IsTrue(def.IsImplemented, $"{id} 구현됨");
            GameObject go = Object.Instantiate(def.prefab, position, Quaternion.identity);
            _spawned.Add(go);
            var a = go.GetComponent<AircraftFlightController>();
            Assert.AreSame(def.tuning, a.Tuning, "프리팹이 기종 튜닝을 참조해야 함(컨트롤러 공통, 데이터만 다름)");
            a.ResetTo(position, Quaternion.identity, def.SpawnSpeed);
            return a;
        }

        private static IEnumerator Hold(IEnumerable<AircraftFlightController> all, FlightInput input, float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
            for (int i = 0; i < frames; i++)
            {
                foreach (AircraftFlightController a in all) a.SetInput(input);
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator SameControllerDifferentTuningPerAircraft()
        {
            AircraftFlightController scout = Spawn(AircraftIds.ScoutP1, new Vector3(0f, 800f, 0f));
            AircraftFlightController viper = Spawn(AircraftIds.ViperF9, new Vector3(2000f, 800f, 0f));
            AircraftFlightController liner = Spawn(AircraftIds.Cityliner180, new Vector3(4000f, 800f, 0f));
            Assert.AreNotSame(scout.Tuning, viper.Tuning);
            Assert.AreNotSame(viper.Tuning, liner.Tuning);
            Assert.Greater(liner.Body.mass, viper.Body.mass);
            Assert.Greater(viper.Body.mass, scout.Body.mass);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullThrottleTopSpeedOrdersJetThenLinerThenScout()
        {
            AircraftFlightController scout = Spawn(AircraftIds.ScoutP1, new Vector3(0f, 800f, 0f));
            AircraftFlightController viper = Spawn(AircraftIds.ViperF9, new Vector3(2000f, 800f, 0f));
            AircraftFlightController liner = Spawn(AircraftIds.Cityliner180, new Vector3(4000f, 800f, 0f));
            yield return Hold(new[] { scout, viper, liner }, new FlightInput { Throttle = 1f }, 10f);
            Assert.Greater(viper.Speed, liner.Speed, "제트가 가장 빠름");
            Assert.Greater(liner.Speed, scout.Speed, "여객기 순항·최고 속도 > 소형 프로펠러기");
        }

        [UnityTest]
        public IEnumerator ScoutBanksAndTurnsFasterThanAirliner()
        {
            AircraftFlightController scout = Spawn(AircraftIds.ScoutP1, new Vector3(0f, 800f, 0f));
            AircraftFlightController liner = Spawn(AircraftIds.Cityliner180, new Vector3(4000f, 800f, 0f));
            yield return Hold(new[] { scout, liner }, new FlightInput { Roll = 1f }, 0.6f);
            Assert.Greater(scout.BankDeg, liner.BankDeg + 10f, "같은 입력 0.6초 — 여객기는 롤이 느림(관성)");

            float scoutHeading0 = scout.transform.eulerAngles.y, linerHeading0 = liner.transform.eulerAngles.y;
            yield return Hold(new[] { scout, liner }, new FlightInput { Roll = 1f }, 3f);
            float scoutTurn = Mathf.Abs(Mathf.DeltaAngle(scoutHeading0, scout.transform.eulerAngles.y));
            float linerTurn = Mathf.Abs(Mathf.DeltaAngle(linerHeading0, liner.transform.eulerAngles.y));
            Assert.Greater(scoutTurn, linerTurn, "여객기의 선회 반경이 더 넓다");
        }

        [UnityTest]
        public IEnumerator AirlinerHasNoBoost()
        {
            AircraftFlightController liner = Spawn(AircraftIds.Cityliner180, new Vector3(4000f, 800f, 0f));
            yield return Hold(new[] { liner }, new FlightInput { Boost = true }, 1f);
            Assert.IsFalse(liner.IsBoosting);
        }
    }

    /// <summary>선택·저장·스폰·모드 호환성·청크 스트리밍을 실제 CityFlight 씬으로 검증(명세 14.11, 애드온 11.1).</summary>
    public sealed class OpenWorldFlowPlayModeTests
    {
        private static bool Available() =>
            SceneUtility.GetBuildIndexByScenePath("Assets/_Project/Scenes/CityFlight.unity") >= 0 &&
            Resources.Load<AircraftCatalog>(AircraftSelectionService.CatalogResourcePath) != null;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameApp.Instance?.Aircraft?.Select(GameApp.Instance.Aircraft.Catalog?.Find(AircraftIds.ScoutP1));
        }

        [UnityTest]
        public IEnumerator SelectedAircraftSpawnsWithItsTuningAndCamera()
        {
            if (!Available()) Assert.Ignore("씬 생성 전");
            var flow = new CityFlightSceneTests();
            yield return flow.LoadFlight(FlightMode.FreeFlight, AircraftIds.ViperF9);
            AircraftDefinition viper = GameApp.Instance.Aircraft.Catalog.Find(AircraftIds.ViperF9);
            Assert.AreSame(viper.tuning, flow.Session.Aircraft.Tuning, "선택한 제트의 튜닝으로 비행");
            Assert.AreEqual(viper, flow.Session.AircraftDefinition);
            Assert.AreEqual(1, Object.FindObjectsByType<AircraftFlightController>(FindObjectsSortMode.None).Length, "placeholder 제거 — 기체 1대");
            var cam = Object.FindFirstObjectByType<ChaseCameraController>();
            Assert.AreSame(flow.Session.Aircraft, cam.Target);
            Assert.AreEqual(AircraftIds.ViperF9, GameApp.Instance.Save.Data.aircraft.selectedAircraftId, "선택이 저장됨");
        }

        [UnityTest]
        public IEnumerator AirlinerCannotBeForcedIntoSkylineRun()
        {
            if (!Available()) Assert.Ignore("씬 생성 전");
            var flow = new CityFlightSceneTests();
            yield return flow.LoadFlight(FlightMode.SkylineRun, AircraftIds.Cityliner180);
            Assert.AreEqual(FlightMode.FreeFlight, flow.Session.Mode, "여객기는 소형기 전용 링 코스 대신 FREE FLIGHT");
        }

        [UnityTest]
        public IEnumerator ChunksStreamInAheadAndUnloadBehindWithoutDuplicates()
        {
            if (!Available()) Assert.Ignore("씬 생성 전");
            var flow = new CityFlightSceneTests();
            yield return flow.LoadFlight(FlightMode.FreeFlight, AircraftIds.ScoutP1);
            var streaming = Object.FindFirstObjectByType<WorldStreamingManager>();
            Assert.IsNotNull(streaming);
            ChunkStateTracker tracker = streaming.Loader.Tracker;
            WorldChunkGrid grid = streaming.Grid;

            AircraftFlightController a = flow.Session.Aircraft;
            WorldChunkId start = grid.IdAt(a.transform.position.x, a.transform.position.z);
            start = new WorldChunkId(Mathf.Clamp(start.X, grid.MinX, grid.MaxX), Mathf.Clamp(start.Z, grid.MinZ, grid.MaxZ));
            Assert.AreEqual(WorldChunkState.Loaded, tracker.Get(start), "시작 청크는 비행 전에 로드됨");

            // 반대편 북동쪽 구석으로 이동(복귀 텔레포트와 같은 경로) → 그쪽 청크가 로드되고 남서쪽은 언로드
            var far = new Vector3(400f, 200f, 400f);
            a.ResetTo(far, Quaternion.LookRotation(Vector3.forward), 30f);
            flow.Session.Pause(); // 물리 정지(충돌·경계 복귀 방지). 스트리밍은 unscaled 시간·비동기 로드로 계속 진행
            WorldChunkId target = new WorldChunkId(1, 1);
            float t = 0f;
            while (tracker.Get(target) != WorldChunkState.Loaded && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(WorldChunkState.Loaded, tracker.Get(target), "새 위치의 청크가 10초 안에 로드");

            t = 0f;
            while (tracker.Get(new WorldChunkId(-2, -2)) != WorldChunkState.Unloaded && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(WorldChunkState.Unloaded, tracker.Get(new WorldChunkId(-2, -2)), "먼 청크 언로드");

            // 같은 청크 씬이 두 번 로드되지 않음
            var seen = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                Assert.IsTrue(seen.Add(s.path), $"중복 로드: {s.path}");
            }
        }
    }
}
