using System.Collections.Generic;
using TowerGlide.Cameras;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using TowerGlide.Input;
using TowerGlide.UI;
using TowerGlide.World;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TowerGlide.EditorTools.TowerglideEditorUtil;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// TOWERGLIDE ▸ 2. 씬 생성 — MainMenu, CityFlight(그레이박스 도시 + 코스 + 기체 + HUD)를 매번 새로 만든다(멱등: 같은 시드 → 같은 결과).
    /// 수작업 수정이 필요해지면 이 메뉴를 다시 실행하지 말고 씬을 직접 편집한다(docs/ARCHITECTURE.md ▸ 씬 생성).
    /// </summary>
    public static class TowerglideSceneBuilder
    {
        private const int CitySeed = 20261108;
        private const float LotSpacing = 70f;     // 블록 중심 간격(m)
        private const int LotsPerSide = 11;       // -350 .. 350
        private const float CityHalf = 350f;
        private const float RingInnerRadius = 8f;

        // SKYLINE RUN 코스(명세 3.3 구간 순서). 링은 다음 지점을 향하도록 자동 회전.
        private static readonly (string name, Vector3 pos)[] CoursePoints =
        {
            ("01 Takeoff Zone", new Vector3(-35f, 65f, -420f)),
            ("02 First Towers", new Vector3(-35f, 55f, -315f)),
            ("03 Narrow Canyon A", new Vector3(-35f, 50f, -175f)),
            ("04 Narrow Canyon B", new Vector3(-35f, 50f, -35f)),
            ("05 Crane Crossing", new Vector3(-35f, 55f, 105f)),
            ("06 Turn East", new Vector3(35f, 60f, 175f)),
            ("07 Drone Corridor", new Vector3(140f, 60f, 175f)),
            ("08 Skybridge Gate", new Vector3(210f, 50f, 175f)),
            ("09 Climb Turn", new Vector3(315f, 140f, 105f)),
            ("10 High Altitude", new Vector3(245f, 240f, -105f)),
            ("11 Descent", new Vector3(105f, 190f, -245f)),
            ("12 Final Gate", new Vector3(-35f, 90f, -385f)),
        };

        private static readonly Vector3 SpawnPosition = new Vector3(-35f, 70f, -480f);

        private sealed class Mats
        {
            public Material Ground, Glass, Concrete, Dark, Roof, AircraftBody, AircraftAccent, Canopy, Ring, Hazard, Steel, Drone, Ui;
        }

        [MenuItem("TOWERGLIDE/2. 씬 생성 (MainMenu + CityFlight)", priority = 2)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TowerglideProjectSetup.EnsureFolders();
            AircraftFlightTuning tuning = TowerglideProjectSetup.EnsureTuningAsset();
            Mats m = CreateMaterials();

            string flightPath = BuildCityFlight(m, tuning);
            string menuPath = BuildMainMenu(m);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene(flightPath, true),
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(flightPath);
            Debug.Log("[TOWERGLIDE] 씬 생성 완료: MainMenu, CityFlight (Build Settings 등록). CityFlight에서 Play → 키보드 W/S/A/D, 모바일은 Device Simulator 사용.");
        }

        private static Mats CreateMaterials()
        {
            return new Mats
            {
                Ground = GetOrCreateLit("M_Ground", new Color(0.33f, 0.36f, 0.38f), 0.1f),
                Glass = GetOrCreateLit("M_Building_Glass", new Color(0.36f, 0.5f, 0.64f), 0.85f, 0.4f),
                Concrete = GetOrCreateLit("M_Building_Concrete", new Color(0.72f, 0.7f, 0.66f), 0.2f),
                Dark = GetOrCreateLit("M_Building_Dark", new Color(0.2f, 0.23f, 0.28f), 0.6f, 0.3f),
                Roof = GetOrCreateLit("M_Roof", new Color(0.45f, 0.46f, 0.48f), 0.15f),
                AircraftBody = GetOrCreateLit("M_Aircraft_White", new Color(0.95f, 0.95f, 0.96f), 0.7f, 0.2f),
                AircraftAccent = GetOrCreateLit("M_Aircraft_Red", new Color(0.86f, 0.12f, 0.12f), 0.7f, 0.2f),
                Canopy = GetOrCreateLit("M_Aircraft_Canopy", new Color(0.1f, 0.18f, 0.3f), 0.95f, 0.5f),
                Ring = GetOrCreateLit("M_Ring", new Color(1f, 0.62f, 0.1f), 0.5f, 0f, new Color(1f, 0.5f, 0.05f) * 1.5f),
                Hazard = GetOrCreateLit("M_Hazard", new Color(1f, 0.35f, 0.08f), 0.4f, 0f, new Color(1f, 0.25f, 0.05f) * 0.8f),
                Steel = GetOrCreateLit("M_Steel_Yellow", new Color(0.95f, 0.75f, 0.12f), 0.35f, 0.5f),
                Drone = GetOrCreateLit("M_Drone", new Color(0.15f, 0.15f, 0.17f), 0.6f, 0.4f),
                Ui = GetOrCreateLit("M_Antenna", new Color(0.8f, 0.82f, 0.85f), 0.4f, 0.6f),
            };
        }

        // =====================================================================
        // CityFlight
        // =====================================================================

        private static string BuildCityFlight(Mats m, AircraftFlightTuning tuning)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            Transform world = new GameObject("World").transform;
            GameObject ground = Solid(PrimitiveType.Plane, "Ground", world, Vector3.zero, new Vector3(200f, 1f, 200f), m.Ground);
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);

            BuildCity(world, m);
            BuildObstacles(world, m);
            CheckpointCourse course = BuildCourse(world, m);

            var boundaryGo = new GameObject("FlightBoundary");
            boundaryGo.transform.SetParent(world, false);
            FlightBoundary boundary = boundaryGo.AddComponent<FlightBoundary>();
            SetVector3(boundary, "size", new Vector3(1100f, 520f, 1100f));

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetPositionAndRotation(SpawnPosition, Quaternion.LookRotation(CoursePoints[0].pos - SpawnPosition));

            AircraftFlightController aircraft = BuildAircraft(m, tuning, spawn);

            // ---- Camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1600f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            ChaseCameraController chase = camGo.AddComponent<ChaseCameraController>();
            SetRef(chase, "target", aircraft);
            camGo.transform.SetPositionAndRotation(SpawnPosition + new Vector3(0f, 3f, -11f), spawn.rotation);

            // ---- Systems ----
            var systems = new GameObject("FlightSystems");
            FlightInputRouter router = systems.AddComponent<FlightInputRouter>();
            KeyboardGamepadInputSource keyboard = systems.AddComponent<KeyboardGamepadInputSource>();
            SetRef(keyboard, "router", router);
            FlightSession session = systems.AddComponent<FlightSession>();

            // ---- UI ----
            FlightHud hud = BuildFlightUi(router, session, cam, out TouchFlightInputSource touch);

            SetRef(router, "aircraft", aircraft);
            SetRefs(router, "sources", new Object[] { touch, keyboard });

            SetRef(session, "aircraft", aircraft);
            SetRef(session, "collisionHandler", aircraft.GetComponent<AircraftCollisionHandler>());
            SetRef(session, "nearMissSensor", aircraft.GetComponentInChildren<NearMissSensor>());
            SetRef(session, "inputRouter", router);
            SetRef(session, "chaseCamera", chase);
            SetRef(session, "course", course);
            SetRef(session, "boundary", boundary);
            SetRef(session, "spawnPoint", spawn);
            SetRef(session, "hud", hud);

            string path = $"{ScenesDir}/CityFlight.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void SetupEnvironment()
        {
            var sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            // 원근 안개 = 도시 깊이감 + 원거리 디테일 생략(명세 1.2, 10.2)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.86f);
            RenderSettings.fogStartDistance = 280f;
            RenderSettings.fogEndDistance = 1300f;
        }

        // ---------------------------------------------------------------------
        // 도시: 시드 고정 격자. 코스 구간 주변 건물은 자동으로 낮추거나 비워 "항상 통과 가능한 코스"를 보장.
        // ---------------------------------------------------------------------
        private static void BuildCity(Transform world, Mats m)
        {
            var city = new GameObject("City").transform;
            city.SetParent(world, false);
            var rng = new System.Random(CitySeed);
            Material[] facades = { m.Glass, m.Concrete, m.Dark, m.Glass };
            var tallRoofs = new List<Transform>();

            for (int ix = 0; ix < LotsPerSide; ix++)
            for (int iz = 0; iz < LotsPerSide; iz++)
            {
                float x = -CityHalf + ix * LotSpacing;
                float z = -CityHalf + iz * LotSpacing;
                var center = new Vector2(x, z);

                if (rng.NextDouble() < 0.08) continue; // 광장·공터 → 실루엣 다양화

                float w = Lerp(rng, 26f, 40f);
                float d = Lerp(rng, 26f, 40f);
                float h = rng.NextDouble() < 0.12 ? Lerp(rng, 230f, 320f) : Lerp(rng, 35f, 190f);

                // 스카이브리지 지지 건물은 충분히 높게
                if ((Mathf.Approximately(x, 210f) && (Mathf.Approximately(z, 140f) || Mathf.Approximately(z, 210f))))
                    h = Mathf.Max(h, 120f);
                // 크레인 공사 현장 부지
                if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 70f)) continue;
                if (Mathf.Approximately(x, 210f) && Mathf.Approximately(z, -280f)) continue;

                float cap = CourseHeightCap(center, Mathf.Max(w, d) * 0.5f);
                if (cap < h) h = cap;
                if (h < 15f) continue;

                Material facade = facades[rng.Next(facades.Length)];
                GameObject b = Solid(PrimitiveType.Cube, $"Building_{ix:00}_{iz:00}", city, new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), facade);
                b.AddComponent<ObstacleMarker>().Configure(ObstacleType.Building);
                GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

                // 옥상 설비(시각 전용) — 실루엣 구분
                if (h > 60f && rng.NextDouble() < 0.6)
                {
                    Visual(PrimitiveType.Cube, "RoofUnit", b.transform, new Vector3(0f, 0.5f + 3f / h, 0f),
                        new Vector3(0.45f, 6f / h, 0.45f), m.Roof);
                }
                if (h > 200f) tallRoofs.Add(b.transform);
            }

            // Antenna Array: 초고층 옥상 통신 안테나(충돌 대상, 잘 보이는 흰색 + 경고등)
            int antennaCount = 0;
            foreach (Transform roof in tallRoofs)
            {
                if (antennaCount++ >= 6) break;
                float top = roof.localScale.y;
                Vector3 basePos = new Vector3(roof.position.x, top, roof.position.z);
                var array = new GameObject($"AntennaArray_{antennaCount}").transform;
                array.SetParent(city, false);
                array.position = basePos;
                array.gameObject.AddComponent<ObstacleMarker>().Configure(ObstacleType.AntennaArray);
                for (int i = 0; i < 3; i++)
                {
                    float hh = 18f + i * 9f;
                    Vector3 p = basePos + new Vector3((i - 1) * 6f, hh * 0.5f, (i % 2) * 5f);
                    Solid(PrimitiveType.Cylinder, $"Mast_{i}", array, p, new Vector3(0.9f, hh * 0.5f, 0.9f), m.Ui);
                    Visual(PrimitiveType.Sphere, "WarningLight", array, p + Vector3.up * hh * 0.5f - basePos, Vector3.one * 1.6f, m.Hazard);
                }
            }
        }

        /// <summary>건물 위치가 코스 구간과 가까우면 그 구간 고도 - 여유보다 낮게 제한.</summary>
        private static float CourseHeightCap(Vector2 lot, float halfSize)
        {
            float cap = float.MaxValue;
            Vector3 prev = SpawnPosition;
            foreach (var (_, pos) in CoursePoints)
            {
                Vector2 a = new Vector2(prev.x, prev.z);
                Vector2 b = new Vector2(pos.x, pos.z);
                float dist = DistancePointSegment(lot, a, b);
                if (dist < halfSize + 12f) cap = Mathf.Min(cap, Mathf.Min(prev.y, pos.y) - 22f);
                prev = pos;
            }
            return cap;
        }

        private static float DistancePointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Lerp(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------------
        // 장애물 6종+: 크레인, 드론, 광고판 프레임, 스카이브리지, 케이블 존, (안테나는 BuildCity), 링 프레임(BuildCourse)
        // ---------------------------------------------------------------------
        private static void BuildObstacles(Transform world, Mats m)
        {
            var root = new GameObject("Obstacles").transform;
            root.SetParent(world, false);

            // Construction Crane (코스 05): 기둥은 블록 안, 붐은 거리 위 높이 95m → 아래로 통과
            BuildCrane(root, m, "Crane_CourseCrossing", new Vector3(0f, 0f, 70f), 110f, 95f, -Vector3.right, rotateDegPerSec: 0f);
            // 천천히 도는 크레인(자유 비행 구역)
            BuildCrane(root, m, "Crane_Rotating", new Vector3(210f, 0f, -280f), 150f, 132f, Vector3.forward, rotateDegPerSec: 6f);

            // Survey Drone ×3 (코스 07): 거리를 가로질러 일정 속도로 왕복 → 예측 가능
            for (int i = 0; i < 3; i++)
            {
                Vector3 start = new Vector3(90f + i * 30f, 54f + i * 6f, 160f);
                BuildDrone(root, m, $"SurveyDrone_{i}", start, new[] { Vector3.zero, new Vector3(0f, 0f, 30f) }, 6f + i);
            }

            // Billboard Frame (코스 03~04 사이): 거리를 가로지르는 틀, 가운데 40×38m 통과 구역
            {
                var bb = new GameObject("BillboardFrame").transform;
                bb.SetParent(root, false);
                bb.position = new Vector3(-35f, 0f, -105f);
                bb.gameObject.AddComponent<ObstacleMarker>().Configure(ObstacleType.BillboardFrame);
                Solid(PrimitiveType.Cube, "PostL", bb, new Vector3(-58f, 45f, -105f), new Vector3(2f, 90f, 2f), m.Steel);
                Solid(PrimitiveType.Cube, "PostR", bb, new Vector3(-12f, 45f, -105f), new Vector3(2f, 90f, 2f), m.Steel);
                Solid(PrimitiveType.Cube, "Panel", bb, new Vector3(-35f, 80f, -105f), new Vector3(48f, 14f, 1.2f), m.Dark);
                Solid(PrimitiveType.Cube, "LowerBeam", bb, new Vector3(-35f, 30f, -105f), new Vector3(48f, 2f, 2f), m.Steel);
            }

            // Skybridge (코스 08): 거리 위 72~80m, 아래로 통과
            {
                var sb = new GameObject("Skybridge").transform;
                sb.SetParent(root, false);
                sb.position = new Vector3(210f, 76f, 175f);
                sb.gameObject.AddComponent<ObstacleMarker>().Configure(ObstacleType.Skybridge);
                Solid(PrimitiveType.Cube, "Deck", sb, new Vector3(210f, 76f, 175f), new Vector3(12f, 8f, 70f), m.Glass);
                Visual(PrimitiveType.Cube, "Stripe", sb, new Vector3(0f, -4.2f, 0f), new Vector3(12.2f, 0.6f, 70.2f), m.Hazard);
            }

            // Cable Zone: 경고 구슬이 달린 케이블 — 코스 고도보다 확실히 위/아래, 잘 보이게
            BuildCable(root, m, "CableZone_High", new Vector3(-58f, 88f, -250f), new Vector3(-12f, 88f, -250f));
            BuildCable(root, m, "CableZone_Low", new Vector3(-58f, 26f, 30f), new Vector3(-12f, 26f, 30f));
        }

        private static void BuildCrane(Transform parent, Mats m, string name, Vector3 basePos, float mastHeight, float boomHeight,
            Vector3 boomDir, float rotateDegPerSec)
        {
            var crane = new GameObject(name).transform;
            crane.SetParent(parent, false);
            crane.position = basePos;
            crane.gameObject.AddComponent<ObstacleMarker>().Configure(ObstacleType.ConstructionCrane);
            Solid(PrimitiveType.Cube, "Mast", crane, basePos + Vector3.up * mastHeight * 0.5f, new Vector3(4f, mastHeight, 4f), m.Steel);

            var boomPivot = new GameObject("BoomPivot").transform;
            boomPivot.SetParent(crane, false);
            boomPivot.position = basePos + Vector3.up * boomHeight;
            boomPivot.rotation = Quaternion.LookRotation(boomDir.normalized, Vector3.up);
            const float boomLength = 62f;
            Solid(PrimitiveType.Cube, "Boom", boomPivot, boomPivot.position + boomPivot.forward * boomLength * 0.5f,
                new Vector3(2.4f, 2.4f, boomLength), m.Steel, boomPivot.rotation);
            Solid(PrimitiveType.Cube, "CounterJib", boomPivot, boomPivot.position - boomPivot.forward * 10f,
                new Vector3(2.4f, 2.4f, 20f), m.Steel, boomPivot.rotation);
            Vector3 hookTop = boomPivot.position + boomPivot.forward * 35f; // 코스 크레인: 거리 중앙(x=-35) 위
            Solid(PrimitiveType.Cube, "HookCable", boomPivot, hookTop + Vector3.down * 7f, new Vector3(0.4f, 14f, 0.4f), m.Dark);
            Solid(PrimitiveType.Cube, "HookBlock", boomPivot, hookTop + Vector3.down * 15f, new Vector3(3f, 3f, 3f), m.Hazard);

            if (rotateDegPerSec != 0f)
            {
                boomPivot.gameObject.AddComponent<Rigidbody>().isKinematic = true;
                KinematicMover mover = boomPivot.gameObject.AddComponent<KinematicMover>();
                mover.Configure(new Vector3[0], 0f, Vector3.up, rotateDegPerSec);
            }
        }

        private static void BuildDrone(Transform parent, Mats m, string name, Vector3 pos, Vector3[] path, float speed)
        {
            GameObject drone = Solid(PrimitiveType.Cube, name, parent, pos, new Vector3(2.6f, 0.8f, 2.6f), m.Drone);
            drone.AddComponent<ObstacleMarker>().Configure(ObstacleType.SurveyDrone);
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = new Vector3(i < 2 ? -0.6f : 0.6f, 0.7f, i % 2 == 0 ? -0.6f : 0.6f);
                Visual(PrimitiveType.Cylinder, "Rotor", drone.transform, local, new Vector3(0.45f, 0.05f, 0.45f), m.Hazard);
            }
            drone.AddComponent<Rigidbody>().isKinematic = true;
            drone.AddComponent<KinematicMover>().Configure(path, speed, Vector3.up, 0f);
        }

        private static void BuildCable(Transform parent, Mats m, string name, Vector3 a, Vector3 b)
        {
            var cable = new GameObject(name).transform;
            cable.SetParent(parent, false);
            cable.position = (a + b) * 0.5f;
            cable.gameObject.AddComponent<ObstacleMarker>().Configure(ObstacleType.CableZone);
            float len = Vector3.Distance(a, b);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            GameObject line = Solid(PrimitiveType.Cylinder, "Cable", cable, (a + b) * 0.5f, new Vector3(0.5f, len * 0.5f, 0.5f), m.Dark, rot);
            // 판정 굵기 = 경고 구슬 크기(지름 2.2m). 보이는 것보다 얇은 판정은 불공정하므로 구슬 기준으로 맞춘다.
            // (CapsuleCollider 반지름은 로컬 단위 — 스케일 0.5 → 2.2 × 0.5 = 월드 1.1m)
            line.GetComponent<CapsuleCollider>().radius = 2.2f;
            for (float t = 0f; t <= 1.001f; t += 6f / len)
                Visual(PrimitiveType.Sphere, "Marker", cable, Vector3.Lerp(a, b, t) - cable.position, Vector3.one * 2.2f, m.Hazard);
        }

        // ---------------------------------------------------------------------
        // 코스(링)
        // ---------------------------------------------------------------------
        private static CheckpointCourse BuildCourse(Transform world, Mats m)
        {
            var courseGo = new GameObject("SkylineCourse");
            courseGo.transform.SetParent(world, false);

            for (int i = 0; i < CoursePoints.Length; i++)
            {
                Vector3 pos = CoursePoints[i].pos;
                Vector3 prev = i == 0 ? SpawnPosition : CoursePoints[i - 1].pos;
                Vector3 next = i + 1 < CoursePoints.Length ? CoursePoints[i + 1].pos : pos + (pos - prev);
                Vector3 dir = ((pos - prev).normalized + (next - pos).normalized * 0.6f).normalized;
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                var gateGo = new GameObject($"Gate {CoursePoints[i].name}");
                gateGo.transform.SetParent(courseGo.transform, false);
                gateGo.transform.SetPositionAndRotation(pos, rot);
                BoxCollider trigger = gateGo.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(RingInnerRadius * 2f, RingInnerRadius * 2f, 3f);
                CheckpointGate gate = gateGo.AddComponent<CheckpointGate>();

                // 링 프레임: 16조각(충돌 대상, ObstacleMarker = 링 프레임, 근접 회피 제외)
                var frame = new GameObject("Frame").transform;
                frame.SetParent(gateGo.transform, false);
                frame.gameObject.AddComponent<ObstacleMarker>().Configure(ObstacleType.CheckpointRingFrame, nearMiss: false);
                const int segments = 16;
                float r = RingInnerRadius + 0.9f;
                float segLen = 2f * Mathf.PI * r / segments + 0.2f;
                var renderers = new Renderer[segments];
                for (int s = 0; s < segments; s++)
                {
                    float ang = s * Mathf.PI * 2f / segments;
                    var local = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"Seg_{s:00}";
                    seg.transform.SetParent(frame, false);
                    seg.transform.localPosition = local;
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg + 90f);
                    seg.transform.localScale = new Vector3(segLen, 1.4f, 1.4f);
                    renderers[s] = seg.GetComponent<Renderer>();
                    renderers[s].sharedMaterial = m.Ring;
                }
                SetRefs(gate, "frameRenderers", renderers);
            }

            return courseGo.AddComponent<CheckpointCourse>();
        }

        // ---------------------------------------------------------------------
        // 기체(임시 그레이박스 모델 — 실제 모델로 교체 시 루트 컴포넌트·콜라이더 유지)
        // ---------------------------------------------------------------------
        private static AircraftFlightController BuildAircraft(Mats m, AircraftFlightTuning tuning, Transform spawn)
        {
            var root = new GameObject("Aircraft");
            root.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 900f;
            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.radius = 0.8f;
            body.height = 7f;
            BoxCollider wings = root.AddComponent<BoxCollider>();
            wings.center = new Vector3(0f, 0f, 0.3f);
            wings.size = new Vector3(8.4f, 0.35f, 1.8f);

            Transform v = new GameObject("Visual").transform;
            v.SetParent(root.transform, false);
            Visual(PrimitiveType.Capsule, "Fuselage", v, Vector3.zero, new Vector3(1.4f, 3.5f, 1.4f), m.AircraftBody, new Vector3(90f, 0f, 0f));
            Visual(PrimitiveType.Sphere, "Canopy", v, new Vector3(0f, 0.65f, 0.6f), new Vector3(0.9f, 0.7f, 1.8f), m.Canopy);
            Visual(PrimitiveType.Cube, "Wing", v, new Vector3(0f, 0f, 0.3f), new Vector3(8.4f, 0.18f, 1.7f), m.AircraftAccent);
            Visual(PrimitiveType.Cube, "WingStripe", v, new Vector3(0f, 0.1f, 0.3f), new Vector3(2.2f, 0.05f, 1.72f), m.AircraftBody);
            Visual(PrimitiveType.Cube, "TailPlane", v, new Vector3(0f, 0.15f, -3.1f), new Vector3(3.2f, 0.12f, 0.9f), m.AircraftAccent);
            Visual(PrimitiveType.Cube, "TailFin", v, new Vector3(0f, 0.9f, -3.1f), new Vector3(0.12f, 1.5f, 1.0f), m.AircraftAccent);
            Visual(PrimitiveType.Cube, "Propeller", v, new Vector3(0f, 0f, 3.6f), new Vector3(2.2f, 0.18f, 0.05f), m.Drone);

            AircraftFlightController controller = root.AddComponent<AircraftFlightController>();
            SetRef(controller, "tuning", tuning);
            root.AddComponent<AircraftCollisionHandler>();

            var sensorGo = new GameObject("NearMissSensor");
            sensorGo.transform.SetParent(root.transform, false);
            SphereCollider sphere = sensorGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 7f;
            NearMissSensor sensor = sensorGo.AddComponent<NearMissSensor>();
            SetRef(sensor, "aircraft", controller);
            return controller;
        }

        // =====================================================================
        // UI
        // =====================================================================

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        private static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            return canvas;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, int fontSize,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            RectTransform rt = Rect(name, parent, anchor, anchor, pos, size);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            Shadow shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static Image Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
        {
            RectTransform rt = Rect(name, parent, anchorMin, anchorMax, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            if (sprite != null) img.type = Image.Type.Sliced;
            return img;
        }

        private static TouchHoldButton HoldButton(string name, Transform parent, Vector2 anchor, Vector2 pos, float size, string label, int fontSize = 30)
        {
            Image img = Panel(name, parent, anchor, anchor, pos, new Vector2(size, size), new Color(1f, 1f, 1f, 0.25f), Knob);
            img.type = Image.Type.Simple;
            TouchHoldButton b = img.gameObject.AddComponent<TouchHoldButton>();
            SetRef(b, "highlight", img);
            Label("Label", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), label, fontSize);
            return b;
        }

        private static Button MenuButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, UnityAction onClick)
        {
            Image img = Panel(name, parent, anchor, anchor, pos, size, new Color(0.08f, 0.12f, 0.2f, 0.85f), RoundedSprite);
            Button btn = img.gameObject.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(btn.onClick, onClick);
            Label("Label", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, label, 34);
            return btn;
        }

        private static FlightHud BuildFlightUi(FlightInputRouter router, FlightSession session, Camera cam, out TouchFlightInputSource touch)
        {
            Canvas canvas = CreateCanvas("FlightCanvas");
            RectTransform safe = Rect("SafeArea", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ---- 왼쪽: 플로팅 조이스틱(화면 왼쪽 45% 전체가 터치 영역) ----
            Image area = Panel("JoystickArea", safe, new Vector2(0f, 0f), new Vector2(0.45f, 0.78f), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
            Image bg = Panel("Background", area.transform, new Vector2(0.4f, 0.35f), new Vector2(0.4f, 0.35f), Vector2.zero, new Vector2(260f, 260f), new Color(1f, 1f, 1f, 0.12f), Knob);
            bg.type = Image.Type.Simple;
            bg.raycastTarget = false;
            Image handle = Panel("Handle", bg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f), new Color(1f, 1f, 1f, 0.45f), Knob);
            handle.type = Image.Type.Simple;
            handle.raycastTarget = false;
            VirtualJoystick stick = area.gameObject.AddComponent<VirtualJoystick>();
            SetRef(stick, "background", bg.rectTransform);
            SetRef(stick, "handle", handle.rectTransform);

            // ---- 오른쪽: 엄지 도달 범위 버튼 ----
            Vector2 br = new Vector2(1f, 0f);
            TouchHoldButton boost = HoldButton("BoostButton", safe, br, new Vector2(-150f, 160f), 210f, "BOOST", 34);
            TouchHoldButton brake = HoldButton("AirBrakeButton", safe, br, new Vector2(-380f, 110f), 160f, "BRAKE", 28);
            TouchHoldButton camBtn = HoldButton("CameraButton", safe, br, new Vector2(-130f, 380f), 120f, "CAM", 26);
            TouchHoldButton recover = HoldButton("RecoverButton", safe, br, new Vector2(-300f, 330f), 120f, "RESET", 24);
            TouchHoldButton pause = HoldButton("PauseButton", safe, new Vector2(1f, 1f), new Vector2(-80f, -70f), 100f, "II", 36);

            var touchGo = new GameObject("TouchFlightInput");
            touchGo.transform.SetParent(canvas.transform, false);
            touch = touchGo.AddComponent<TouchFlightInputSource>();
            SetRef(touch, "stick", stick);
            SetRef(touch, "boostButton", boost);
            SetRef(touch, "airBrakeButton", brake);
            SetRef(touch, "cameraButton", camBtn);
            SetRef(touch, "recoverButton", recover);
            SetRef(touch, "pauseButton", pause);
            SetRef(touch, "router", router);

            // ---- HUD (얇게, 화면 가장자리) ----
            Vector2 tl = new Vector2(0f, 1f), tc = new Vector2(0.5f, 1f);
            Text speed = Label("Speed", safe, tl, new Vector2(170f, -60f), new Vector2(300f, 60f), "0 km/h", 44, TextAnchor.MiddleLeft);
            Text alt = Label("Altitude", safe, tl, new Vector2(170f, -115f), new Vector2(300f, 50f), "0 m", 34, TextAnchor.MiddleLeft);
            Image boostBg = Panel("BoostBar", safe, br, br, new Vector2(-150f, 290f), new Vector2(200f, 16f), new Color(0f, 0f, 0f, 0.4f));
            Image boostFill = Panel("Fill", boostBg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.3f, 0.8f, 1f, 0.9f), RoundedSprite);
            boostFill.type = Image.Type.Filled;
            boostFill.fillMethod = Image.FillMethod.Horizontal;

            Text mode = Label("Mode", safe, tc, new Vector2(0f, -40f), new Vector2(500f, 44f), "FREE FLIGHT", 30);
            Text progress = Label("Progress", safe, tc, new Vector2(-180f, -90f), new Vector2(300f, 50f), "RING 0 / 0", 34);
            Text timer = Label("Timer", safe, tc, new Vector2(180f, -90f), new Vector2(220f, 50f), "3:00", 40);
            Text score = Label("Score", safe, new Vector2(1f, 1f), new Vector2(-330f, -70f), new Vector2(300f, 50f), "0 pts", 36, TextAnchor.MiddleRight);
            Text arrowText = Label("GateArrow", safe, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(80f, 80f), "▲", 64);
            arrowText.color = new Color(1f, 0.65f, 0.15f);
            Text gateDist = Label("GateDistance", arrowText.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(200f, 40f), "", 26);
            Text toast = Label("Toast", safe, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 60f), "", 40);
            Text warning = Label("BoundaryWarning", safe, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1000f, 60f), "비행 구역을 벗어나고 있습니다 — 돌아가세요", 36);
            warning.color = new Color(1f, 0.45f, 0.3f);

            Button endFree = MenuButton("EndFlightButton", safe, new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(160f, 80f), "END", session.EndFreeFlight);

            // ---- 일시정지 패널 ----
            Image pausePanel = Panel("PausePanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            Label("Title", pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(600f, 90f), "PAUSED", 64);
            MenuButton("Resume", pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(460f, 100f), "RESUME", session.Resume);
            MenuButton("Restart", pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(460f, 100f), "RESTART", session.Restart);
            MenuButton("Menu", pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(460f, 100f), "MAIN MENU", session.GoToMainMenu);

            // ---- 결과 패널 ----
            Image resultPanel = Panel("ResultPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0.02f, 0.06f, 0.75f));
            Text resultText = Label("ResultText", resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1200f, 420f), "", 38);
            MenuButton("Retry", resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-250f, -230f), new Vector2(420f, 100f), "RETRY", session.Restart);
            MenuButton("Menu", resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(250f, -230f), new Vector2(420f, 100f), "MAIN MENU", session.GoToMainMenu);

            var hudGo = new GameObject("FlightHud");
            hudGo.transform.SetParent(canvas.transform, false);
            FlightHud hud = hudGo.AddComponent<FlightHud>();
            SetRef(hud, "speedText", speed);
            SetRef(hud, "altitudeText", alt);
            SetRef(hud, "boostFill", boostFill);
            SetRef(hud, "modeText", mode);
            SetRef(hud, "progressText", progress);
            SetRef(hud, "timerText", timer);
            SetRef(hud, "scoreText", score);
            SetRef(hud, "gateArrow", arrowText.rectTransform);
            SetRef(hud, "gateDistanceText", gateDist);
            SetRef(hud, "toastText", toast);
            SetRef(hud, "boundaryWarning", warning.gameObject);
            SetRef(hud, "pausePanel", pausePanel.gameObject);
            SetRef(hud, "resultPanel", resultPanel.gameObject);
            SetRef(hud, "resultText", resultText);
            SetRef(hud, "endFreeFlightButton", endFree.gameObject);
            SetRef(hud, "joystick", stick);
            SetRef(hud, "worldCamera", cam);
            return hud;
        }

        // =====================================================================
        // MainMenu
        // =====================================================================

        private static string BuildMainMenu(Mats m)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            // 배경: 정적인 빌딩 몇 동 + 기체(메뉴 연출은 가볍게 — 명세 5.2)
            var bg = new GameObject("Backdrop").transform;
            Solid(PrimitiveType.Plane, "Ground", bg, Vector3.zero, new Vector3(60f, 1f, 60f), m.Ground);
            var rng = new System.Random(CitySeed);
            Material[] facades = { m.Glass, m.Concrete, m.Dark };
            for (int i = 0; i < 14; i++)
            {
                float x = -160f + i * 25f + Lerp(rng, -5f, 5f);
                float z = 120f + Lerp(rng, 0f, 140f);
                float h = Lerp(rng, 60f, 240f);
                Solid(PrimitiveType.Cube, $"Tower_{i}", bg, new Vector3(x, h * 0.5f, z), new Vector3(Lerp(rng, 18f, 30f), h, Lerp(rng, 18f, 30f)), facades[rng.Next(facades.Length)]);
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 1200f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 45f, -40f), Quaternion.Euler(-12f, 0f, 0f));

            Canvas canvas = CreateCanvas("MenuCanvas");
            RectTransform safe = Rect("SafeArea", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var controllerGo = new GameObject("MainMenu");
            MainMenuController menu = controllerGo.AddComponent<MainMenuController>();

            Vector2 left = new Vector2(0f, 0.5f);
            Label("Logo", safe, left, new Vector2(420f, 330f), new Vector2(760f, 140f), "TOWERGLIDE", 110, TextAnchor.MiddleLeft);
            Label("Tagline", safe, left, new Vector2(420f, 245f), new Vector2(760f, 50f), "빌딩 사이, 한계 없이.", 36, TextAnchor.MiddleLeft, FontStyle.Normal);
            MenuButton("SkylineRun", safe, left, new Vector2(300f, 110f), new Vector2(480f, 110f), "SKYLINE RUN", menu.StartSkylineRun);
            MenuButton("FreeFlight", safe, left, new Vector2(300f, -20f), new Vector2(480f, 110f), "FREE FLIGHT", menu.StartFreeFlight);
            MenuButton("Hangar", safe, left, new Vector2(180f, -150f), new Vector2(230f, 90f), "HANGAR", menu.OpenHangar);
            MenuButton("Settings", safe, left, new Vector2(420f, -150f), new Vector2(230f, 90f), "SETTINGS", menu.OpenSettings);
            Text records = Label("Records", safe, new Vector2(0f, 0f), new Vector2(560f, 90f), new Vector2(1000f, 100f), "", 28, TextAnchor.MiddleLeft, FontStyle.Normal);
            Text notice = Label("Notice", safe, left, new Vector2(300f, -250f), new Vector2(600f, 50f), "", 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            SetRef(menu, "recordsText", records);
            SetRef(menu, "noticeText", notice);

            string path = $"{ScenesDir}/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
