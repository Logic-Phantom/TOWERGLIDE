using System.Collections.Generic;
using System.IO;
using TowerGlide.Audio;
using TowerGlide.Cameras;
using TowerGlide.Core.World;
using TowerGlide.Core.World.Streaming;
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
    /// TOWERGLIDE ▸ 2. 씬 생성 — 프리팹(장애물·기체·링·VFX)을 갱신하고 MainMenu, CityFlight를 매번 새로 만든다(멱등).
    /// 도시 배치·코스는 Core 데이터(CityLayoutGenerator, SkylineCourses)에서 오므로 씬 생성기는 "조립"만 한다.
    /// 씬을 손으로 다듬기 시작하면 이 메뉴를 다시 실행하지 않는다(덮어씀).
    /// </summary>
    public static class TowerglideSceneBuilder
    {
        private const string CityMeshesPath = Root + "/Art/Buildings/CityMeshes.asset";
        private const string EdgeGlowPath = Root + "/Art/Textures/T_EdgeGlow.png";
        private const string ChunkScenesDir = ScenesDir + "/Chunks";
        private const string SkylineMeshesPath = Root + "/Art/Buildings/SkylineMeshes.asset";
        public const string ChunkCatalogPath = SettingsDir + "/World/WorldChunkCatalog.asset";

        [MenuItem("TOWERGLIDE/2. 씬 생성 (MainMenu + CityFlight)", priority = 2)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TowerglideProjectSetup.EnsureFolders();
            AircraftFlightTuning tuning = TowerglideProjectSetup.EnsureTuningAsset();
            TowerglideProjectSetup.EnsureFontImport();
            TowerglideMaterials m = TowerglideMaterials.CreateAll();
            TowerglidePrefabFactory.Set prefabs = TowerglidePrefabFactory.CreateAll(m);
            TowerglideAircraftFactory.Result aircraftSet = TowerglideAircraftFactory.CreateAll(m, tuning);
            AssetDatabase.SaveAssets();

            string flightPath = BuildCityFlight(m, prefabs, aircraftSet, out List<string> chunkScenes);
            string menuPath = BuildMainMenu(m);

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene(flightPath, true),
            };
            foreach (string c in chunkScenes) scenes.Add(new EditorBuildSettingsScene(c, true)); // Additive 로드 대상(애드온 명세 5.3)
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(flightPath);
            TowerglideCourseValidator.ValidateOpenScene(showDialog: false);
            Debug.Log("[TOWERGLIDE] 씬 생성 완료: MainMenu, CityFlight (Build Settings 등록). CityFlight에서 Play → 키보드 W/S/A/D, 모바일은 Device Simulator.");
        }

        private static Vector3 V(Float3 f) => new Vector3(f.X, f.Y, f.Z);

        // =====================================================================
        // CityFlight
        // =====================================================================

        private static string BuildCityFlight(TowerglideMaterials m, TowerglidePrefabFactory.Set prefabs,
            TowerglideAircraftFactory.Result aircraftSet, out List<string> chunkScenePaths)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            CourseDefinition course = SkylineCourses.Downtown();
            CityLayoutSettings layoutSettings = CityLayoutSettings.Downtown();
            List<LotPlacement> lots = CityLayoutGenerator.Generate(layoutSettings, course);

            Transform world = new GameObject("World").transform;
            GameObject ground = Solid(PrimitiveType.Plane, "Ground", world, Vector3.zero, new Vector3(200f, 1f, 200f), m.Ground);
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            Transform cityRoot = BuildCity(world, m, lots);
            Transform obstaclesRoot = BuildObstacles(world, prefabs, lots, course, layoutSettings);
            CheckpointCourse courseComponent = BuildCourse(world, prefabs, course);

            var boundaryGo = new GameObject("FlightBoundary");
            boundaryGo.transform.SetParent(world, false);
            FlightBoundary boundary = boundaryGo.AddComponent<FlightBoundary>();
            SetVector3(boundary, "size", new Vector3(1100f, 520f, 1100f));

            Vector3 spawnPos = V(course.Spawn);
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(V(course.Gates[0].Position) - spawnPos));

            // 씬에 놓이는 기본 기체(placeholder). 실행 시 AircraftSpawnService가 격납고 선택 기체로 바꾼다.
            GameObject aircraftGo = TowerglidePrefabFactory.Place(aircraftSet.ScoutPrefab, null, spawn.position, spawn.rotation, "Aircraft");
            var aircraft = aircraftGo.GetComponent<AircraftFlightController>();

            // ---- Camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1600f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            ChaseCameraController chase = camGo.AddComponent<ChaseCameraController>();
            SetRef(chase, "target", aircraft);
            camGo.transform.SetPositionAndRotation(spawnPos + spawn.rotation * new Vector3(0f, 3.2f, -11f), spawn.rotation);

            // ---- Systems ----
            var systems = new GameObject("FlightSystems");
            FlightInputRouter router = systems.AddComponent<FlightInputRouter>();
            KeyboardGamepadInputSource keyboard = systems.AddComponent<KeyboardGamepadInputSource>();
            SetRef(keyboard, "router", router);
            FlightSession session = systems.AddComponent<FlightSession>();
            FlightAudioController flightAudio = systems.AddComponent<FlightAudioController>();
            SetRef(flightAudio, "session", session);
            var fxGo = new GameObject("FeedbackFx");
            fxGo.transform.SetParent(systems.transform, false);
            FeedbackFx fx = fxGo.AddComponent<FeedbackFx>();
            SetRef(fx, "crashPrefab", prefabs.FxCrash);
            SetRef(fx, "ringPassPrefab", prefabs.FxRing);
            SetRef(fx, "nearMissPrefab", prefabs.FxNearMiss);

            // ---- UI ----
            FlightHud hud = BuildFlightUi(router, session, cam, out TouchFlightInputSource touch);

            SetRef(router, "aircraft", aircraft);
            SetRefs(router, "sources", new Object[] { touch, keyboard });

            SetRef(session, "aircraft", aircraft);
            SetRef(session, "collisionHandler", aircraftGo.GetComponent<AircraftCollisionHandler>());
            SetRef(session, "nearMissSensor", aircraftGo.GetComponentInChildren<NearMissSensor>());
            SetRef(session, "inputRouter", router);
            SetRef(session, "chaseCamera", chase);
            SetRef(session, "course", courseComponent);
            SetRef(session, "boundary", boundary);
            SetRef(session, "spawnPoint", spawn);
            SetRef(session, "hud", hud);
            SetRef(session, "feedbackFx", fx);

            // ---- 기체 선택 스폰(애드온 명세 14) ----
            AircraftSpawnService spawnService = systems.AddComponent<AircraftSpawnService>();
            SetRef(spawnService, "placeholderAircraft", aircraft);
            SetRef(spawnService, "spawnPoint", spawn);
            SetRef(spawnService, "router", router);
            SetRef(spawnService, "chaseCamera", chase);
            SetRef(spawnService, "session", session);

            // ---- 청크 스트리밍(OW1): 도시·장애물 → 250m 청크 씬, 비행 씬에는 지면·코스·원거리 실루엣만 상주 ----
            WorldChunkGrid grid = SeoulWorldDefinition.StreamingPoc().Grid;
            var streamingGo = new GameObject("WorldStreaming");
            streamingGo.transform.SetParent(systems.transform, false);
            WorldChunkLoader loader = streamingGo.AddComponent<WorldChunkLoader>();
            WorldStreamingManager streamingManager = streamingGo.AddComponent<WorldStreamingManager>();
            SkylineImpostorController skyline = streamingGo.AddComponent<SkylineImpostorController>();
            WorldChunkCatalog catalog = SplitIntoChunks(scene, grid, cityRoot, obstaclesRoot, out chunkScenePaths,
                out SkylineImpostorController.ChunkImpostor[] impostors);
            SetRef(loader, "catalog", catalog);
            SetRef(streamingManager, "loader", loader);
            SetRef(streamingManager, "session", session);
            skyline.Configure(loader, impostors);
            EditorUtility.SetDirty(skyline);
            SetRef(session, "streaming", streamingManager);
            SetRef(hud, "streaming", streamingManager);

            string path = $"{ScenesDir}/CityFlight.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>
        /// 건물·장애물을 위치 기준 청크로 나눠 각각의 씬(Scenes/Chunks/Chunk_키.unity)으로 저장하고,
        /// 청크마다 건물 외곽 상자를 합친 원거리 실루엣(충돌 없음)을 비행 씬에 남긴다(애드온 명세 5.1·5.4).
        /// 격자 밖 오브젝트는 가장 가까운 가장자리 청크에 넣는다(누락 방지).
        /// </summary>
        private static WorldChunkCatalog SplitIntoChunks(Scene mainScene, WorldChunkGrid grid,
            Transform cityRoot, Transform obstaclesRoot, out List<string> scenePaths, out SkylineImpostorController.ChunkImpostor[] impostors)
        {
            AssetDatabase.DeleteAsset(ChunkScenesDir);
            EnsureFolder(ChunkScenesDir);
            EnsureFolder(SettingsDir + "/World");
            AssetDatabase.DeleteAsset(SkylineMeshesPath);
            var skylineContainer = new Mesh { name = "SkylineMeshes" };
            AssetDatabase.CreateAsset(skylineContainer, SkylineMeshesPath);
            Material skylineMat = GetOrCreateLit("M_Skyline", new Color(0.5f, 0.56f, 0.64f), 0.3f);

            var buckets = new Dictionary<WorldChunkId, List<Transform>>();
            void Add(Transform root)
            {
                var items = new List<Transform>();
                foreach (Transform t in root) items.Add(t);
                foreach (Transform t in items)
                {
                    WorldChunkId id = grid.IdAt(t.position.x, t.position.z);
                    id = new WorldChunkId(Mathf.Clamp(id.X, grid.MinX, grid.MaxX), Mathf.Clamp(id.Z, grid.MinZ, grid.MaxZ));
                    if (!buckets.TryGetValue(id, out List<Transform> list)) buckets[id] = list = new List<Transform>();
                    list.Add(t);
                }
            }
            Add(cityRoot);
            Add(obstaclesRoot);

            var skylineRoot = new GameObject("Skyline").transform;
            var entries = new List<WorldChunkCatalog.Entry>();
            var impostorList = new List<SkylineImpostorController.ChunkImpostor>();
            scenePaths = new List<string>();

            foreach (WorldChunkId id in grid.All())
            {
                if (!buckets.TryGetValue(id, out List<Transform> items) || items.Count == 0) continue;

                // 원거리 실루엣: 건물 렌더러 범위를 상자로 합침
                var combine = new List<CombineInstance>();
                int buildings = 0;
                foreach (Transform t in items)
                {
                    ObstacleMarker marker = t.GetComponent<ObstacleMarker>();
                    if (marker == null || marker.Type != ObstacleType.Building) continue;
                    Renderer[] rs = t.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    Bounds b = rs[0].bounds;
                    foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                    combine.Add(new CombineInstance
                    {
                        mesh = ProceduralMeshes.CreateMetricBox(b.size),
                        transform = Matrix4x4.Translate(new Vector3(b.center.x, b.min.y, b.center.z)),
                    });
                    buildings++;
                }
                if (combine.Count > 0)
                {
                    var mesh = new Mesh { name = $"Skyline_{id.Key}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.CombineMeshes(combine.ToArray(), true, true);
                    foreach (CombineInstance ci in combine) Object.DestroyImmediate(ci.mesh);
                    AssetDatabase.AddObjectToAsset(mesh, skylineContainer);
                    var imp = new GameObject($"Skyline_{id.Key}");
                    imp.transform.SetParent(skylineRoot, false);
                    imp.AddComponent<MeshFilter>().sharedMesh = mesh;
                    imp.AddComponent<MeshRenderer>().sharedMaterial = skylineMat;
                    GameObjectUtility.SetStaticEditorFlags(imp, StaticEditorFlags.BatchingStatic);
                    impostorList.Add(new SkylineImpostorController.ChunkImpostor { x = id.X, z = id.Z, root = imp });
                }

                // 상세 청크 씬
                var chunkRoot = new GameObject($"Chunk_{id.Key}");
                foreach (Transform t in items) t.SetParent(chunkRoot.transform, true);
                Scene chunkScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.MoveGameObjectToScene(chunkRoot, chunkScene);
                string path = $"{ChunkScenesDir}/Chunk_{id.Key}.unity";
                EditorSceneManager.SaveScene(chunkScene, path);
                EditorSceneManager.CloseScene(chunkScene, true);
                SceneManager.SetActiveScene(mainScene);

                scenePaths.Add(path);
                entries.Add(new WorldChunkCatalog.Entry { x = id.X, z = id.Z, scenePath = path, buildingCount = buildings });
            }
            AssetDatabase.SaveAssets();

            var catalog = AssetDatabase.LoadAssetAtPath<WorldChunkCatalog>(ChunkCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldChunkCatalog>();
                AssetDatabase.CreateAsset(catalog, ChunkCatalogPath);
            }
            catalog.Configure(grid.ChunkSize, grid.MinX, grid.MinZ, grid.MaxX, grid.MaxZ, entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            impostors = impostorList.ToArray();
            Debug.Log($"[TOWERGLIDE] 청크 {entries.Count}개 씬 생성(크기 {grid.ChunkSize}m), 원거리 실루엣 {impostors.Length}개");
            return catalog;
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
        // 도시: Core 레이아웃 → 아키타입 모듈. 절차 메시는 하나의 에셋 파일에 모아 저장(씬 비대화·누락 방지).
        // ---------------------------------------------------------------------
        private static Transform BuildCity(Transform world, TowerglideMaterials m, List<LotPlacement> lots)
        {
            var city = new GameObject("City").transform;
            city.SetParent(world, false);
            foreach (LotPlacement lot in lots)
                if (lot.Kind == LotKind.Building) TowerglideBuildingFactory.Build(lot, m, city);
            SaveProceduralMeshes(city);
            return city;
        }

        private static void SaveProceduralMeshes(Transform root)
        {
            EnsureFolder(Path.GetDirectoryName(CityMeshesPath)?.Replace('\\', '/'));
            AssetDatabase.DeleteAsset(CityMeshesPath);
            var container = new Mesh { name = "CityMeshes" };
            AssetDatabase.CreateAsset(container, CityMeshesPath);
            int n = 0;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))) continue; // 내장·기존 에셋 메시는 제외
                mesh.name = $"{mf.transform.parent.name}_{mf.name}";
                AssetDatabase.AddObjectToAsset(mesh, container);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TOWERGLIDE] 건물 메시 {n}개 → {CityMeshesPath}");
        }

        // ---------------------------------------------------------------------
        // 장애물(프리팹 인스턴스). 위치는 코스 구간 기준 — docs/GAME_DESIGN.md 표와 같게 유지.
        // ---------------------------------------------------------------------
        private static Transform BuildObstacles(Transform world, TowerglidePrefabFactory.Set p, List<LotPlacement> lots,
            CourseDefinition course, CityLayoutSettings settings)
        {
            var root = new GameObject("Obstacles").transform;
            root.SetParent(world, false);

            // 05 Crane Crossing: 공사 블록(0,70)에 마스트, 붐은 -X(거리 x=-35 위 95m), 훅은 거리 중앙 위
            TowerglidePrefabFactory.Place(p.CraneStatic, root, new Vector3(0f, 0f, 70f), Quaternion.Euler(0f, 180f, 0f), "Crane_CourseCrossing");
            // 자유 비행 구역의 느리게 도는 크레인
            TowerglidePrefabFactory.Place(p.CraneRotating, root, new Vector3(210f, 0f, -280f), Quaternion.identity, "Crane_Rotating");

            // 07 Drone Corridor: z=175 거리를 가로질러 왕복(일정 속도 → 예측 가능)
            for (int i = 0; i < 3; i++)
            {
                GameObject drone = TowerglidePrefabFactory.Place(p.Drone, root, new Vector3(90f + i * 30f, 54f + i * 6f, 160f), Quaternion.identity, $"SurveyDrone_{i}");
                var mover = drone.GetComponent<KinematicMover>();
                mover.Configure(new[] { Vector3.zero, new Vector3(0f, 0f, 30f) }, 6f + i, Vector3.up, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mover); // 프리팹 인스턴스 오버라이드로 저장
            }

            // 03~04 Narrow Canyon: 광고판 프레임(통과) + 위·아래 케이블 존
            TowerglidePrefabFactory.Place(p.Billboard, root, new Vector3(-35f, 0f, -105f), Quaternion.identity, "BillboardFrame");
            TowerglidePrefabFactory.Place(p.Cable, root, new Vector3(-35f, 88f, -250f), Quaternion.identity, "CableZone_High");
            TowerglidePrefabFactory.Place(p.Cable, root, new Vector3(-35f, 26f, 30f), Quaternion.identity, "CableZone_Low");

            // 08 Skybridge Gate: 지지 건물(210,140)·(210,210) 사이, 72~80m
            TowerglidePrefabFactory.Place(p.Skybridge, root, new Vector3(210f, 76f, 175f), Quaternion.identity, "Skybridge");

            // 안테나: 코스와 무관한 초고층 옥상(안테나 높이 +40m를 더해도 코스 여유 유지)
            int antennas = 0;
            foreach (LotPlacement lot in lots)
            {
                if (antennas >= 6 || lot.Kind != LotKind.Building || lot.Height < 200f) continue;
                if (lot.Archetype == BuildingArchetype.SlimNeedle || lot.Archetype == BuildingArchetype.CrownTop) continue;
                float cap = CityLayoutGenerator.HeightCap(course, lot.X, lot.Z, Mathf.Max(lot.Width, lot.Depth) * 0.5f, settings);
                if (cap < lot.Height + 40f) continue;
                TowerglidePrefabFactory.Place(p.Antenna, root, new Vector3(lot.X, lot.Height, lot.Z), Quaternion.identity, $"AntennaArray_{antennas++}");
            }

            // 화물 기구: 코스 영향이 없는 광장 위에서 천천히 오르내림(자유 비행용)
            int balloons = 0;
            foreach (LotPlacement lot in lots)
            {
                if (balloons >= 3 || lot.Kind != LotKind.Plaza) continue;
                if (CityLayoutGenerator.HeightCap(course, lot.X, lot.Z, 25f, settings) < float.MaxValue) continue;
                TowerglidePrefabFactory.Place(p.CargoBalloon, root, new Vector3(lot.X, 60f, lot.Z), Quaternion.identity, $"CargoBalloon_{balloons++}");
            }
            return root;
        }

        // ---------------------------------------------------------------------
        // 코스(링 프리팹). 자식 순서 = 통과 순서.
        // ---------------------------------------------------------------------
        private static CheckpointCourse BuildCourse(Transform world, TowerglidePrefabFactory.Set p, CourseDefinition course)
        {
            var courseGo = new GameObject("SkylineCourse");
            courseGo.transform.SetParent(world, false);
            for (int i = 0; i < course.Gates.Count; i++)
            {
                Vector3 dir = V(course.GateDirection(i));
                TowerglidePrefabFactory.Place(p.Gate, courseGo.transform, V(course.Gates[i].Position),
                    Quaternion.LookRotation(dir, Vector3.up), $"Gate {course.Gates[i].Name}");
            }
            return courseGo.AddComponent<CheckpointCourse>();
        }

        // =====================================================================
        // UI
        // =====================================================================

        // UI 폰트 = Pretendard(OFL). 파일이 없으면 Unity 기본 폰트로 대체(D-049)
        private static Font UiFont =>
            AssetDatabase.LoadAssetAtPath<Font>(FontRegularPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Font UiFontBold => AssetDatabase.LoadAssetAtPath<Font>(FontBoldPath);
        private static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        private static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static UiClickSound _click;

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
            _click = go.AddComponent<UiClickSound>();

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
            // 굵게는 실제 Bold 폰트 파일로(합성 볼드보다 선명). Bold 파일이 없으면 합성 볼드
            Font bold = style == FontStyle.Bold ? UiFontBold : null;
            t.font = bold != null ? bold : UiFont;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = bold != null ? FontStyle.Normal : style;
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
            if (_click != null) UnityEventTools.AddPersistentListener(btn.onClick, _click.Play); // 클릭음(Phase 3)
            Label("Label", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, label, 34);
            return btn;
        }

        /// <summary>가장자리만 밝은 비네트 스프라이트(근접 회피 플래시용, 직접 생성).</summary>
        private static Sprite EdgeGlowSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f;
                float v = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                float edge = Mathf.Max(u, v);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, edge));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            EnsureFolder(Path.GetDirectoryName(EdgeGlowPath)?.Replace('\\', '/'));
            File.WriteAllBytes(EdgeGlowPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(EdgeGlowPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(EdgeGlowPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(EdgeGlowPath);
        }

        private static FlightHud BuildFlightUi(FlightInputRouter router, FlightSession session, Camera cam, out TouchFlightInputSource touch)
        {
            Canvas canvas = CreateCanvas("FlightCanvas");

            // 근접 회피 가장자리 플래시(맨 뒤, 터치 막지 않음)
            Image flash = Panel("NearMissFlash", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.75f, 0.92f, 1f, 1f), EdgeGlowSprite());
            flash.type = Image.Type.Simple;
            flash.raycastTarget = false;

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
            Vector2 tl = new Vector2(0f, 1f), tc = new Vector2(0.5f, 1f), center = new Vector2(0.5f, 0.5f);
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
            Text arrowText = Label("GateArrow", safe, center, new Vector2(0f, 250f), new Vector2(80f, 80f), "▲", 64);
            arrowText.color = new Color(1f, 0.65f, 0.15f);
            Text gateDist = Label("GateDistance", arrowText.transform, center, new Vector2(0f, -60f), new Vector2(200f, 40f), "", 26);
            Text toast = Label("Toast", safe, center, new Vector2(0f, 150f), new Vector2(900f, 60f), "", 40);
            Text warning = Label("BoundaryWarning", safe, center, new Vector2(0f, 60f), new Vector2(1000f, 60f), "비행 구역을 벗어나고 있습니다 — 화살표 방향으로 돌아가세요", 34);
            warning.color = new Color(1f, 0.45f, 0.3f);
            Text boundaryArrow = Label("BoundaryArrow", safe, center, new Vector2(0f, -40f), new Vector2(100f, 100f), "▲", 80);
            boundaryArrow.color = new Color(1f, 0.45f, 0.3f);

            Button endFree = MenuButton("EndFlightButton", safe, new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(160f, 80f), "END", session.EndFreeFlight);

            // ---- 일시정지 패널 ----
            Image pausePanel = Panel("PausePanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            Label("Title", pausePanel.transform, center, new Vector2(0f, 220f), new Vector2(600f, 90f), "PAUSED", 64);
            MenuButton("Resume", pausePanel.transform, center, new Vector2(0f, 80f), new Vector2(460f, 100f), "RESUME", session.Resume);
            MenuButton("Restart", pausePanel.transform, center, new Vector2(0f, -40f), new Vector2(460f, 100f), "RESTART", session.Restart);
            MenuButton("Menu", pausePanel.transform, center, new Vector2(0f, -160f), new Vector2(460f, 100f), "MAIN MENU", session.GoToMainMenu);

            // ---- 결과 패널 ----
            Image resultPanel = Panel("ResultPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0.02f, 0.06f, 0.75f));
            Text resultText = Label("ResultText", resultPanel.transform, center, new Vector2(0f, 110f), new Vector2(1200f, 420f), "", 38);
            MenuButton("Retry", resultPanel.transform, center, new Vector2(-250f, -230f), new Vector2(420f, 100f), "RETRY", session.Restart);
            MenuButton("Menu", resultPanel.transform, center, new Vector2(250f, -230f), new Vector2(420f, 100f), "MAIN MENU", session.GoToMainMenu);

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
            SetRef(hud, "boundaryArrow", boundaryArrow.rectTransform);
            SetRef(hud, "nearMissFlash", flash);
            // ---- 설정(일시정지 화면에서 열기) · 튜토리얼 ----
            SettingsPanelController settingsPanel = BuildSettingsUi(canvas);
            MenuButton("Settings", pausePanel.transform, center, new Vector2(0f, -280f), new Vector2(460f, 100f), "SETTINGS", settingsPanel.Show);
            BuildTutorialUi(safe, session);

            SetRef(hud, "pausePanel", pausePanel.gameObject);
            SetRef(hud, "resultPanel", resultPanel.gameObject);
            SetRef(hud, "resultText", resultText);
            SetRef(hud, "endFreeFlightButton", endFree.gameObject);
            SetRef(hud, "joystick", stick);
            SetRef(hud, "worldCamera", cam);
            Text compass = Label("Compass", safe, tc, new Vector2(0f, -140f), new Vector2(260f, 44f), "N 000°", 30);
            Text streamingDebug = Label("StreamingDebug", safe, new Vector2(0f, 0f), new Vector2(560f, 30f), new Vector2(1100f, 36f), "", 20, TextAnchor.MiddleLeft, FontStyle.Normal);
            streamingDebug.color = new Color(0.8f, 1f, 0.8f, 0.85f);
            SetRef(hud, "compassText", compass);
            SetRef(hud, "streamingDebugText", streamingDebug);
            return hud;
        }

        // =====================================================================
        // MainMenu
        // =====================================================================

        private static string BuildMainMenu(TowerglideMaterials m)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupEnvironment();

            // 배경: 아키타입 건물 몇 동(정적) — 메뉴 연출은 가볍게(명세 5.2)
            var bg = new GameObject("Backdrop").transform;
            Solid(PrimitiveType.Plane, "Ground", bg, Vector3.zero, new Vector3(60f, 1f, 60f), m.Ground);
            var rng = new System.Random(20261108);
            for (int i = 0; i < 12; i++)
            {
                float h = 50f + (float)rng.NextDouble() * 220f;
                var lot = new LotPlacement
                {
                    Ix = i,
                    X = -170f + i * 30f + (float)rng.NextDouble() * 8f,
                    Z = 130f + (float)rng.NextDouble() * 150f,
                    Width = 20f + (float)rng.NextDouble() * 10f,
                    Depth = 20f + (float)rng.NextDouble() * 10f,
                    Height = h,
                    Kind = LotKind.Building,
                    Archetype = BuildingCatalog.Pick(h, rng),
                    DetailSeed = rng.Next(),
                };
                TowerglideBuildingFactory.Build(lot, m, bg);
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

            RectTransform group = Rect("MenuGroup", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Vector2 left = new Vector2(0f, 0.5f);
            Label("Logo", group, left, new Vector2(420f, 330f), new Vector2(760f, 140f), "TOWERGLIDE", 110, TextAnchor.MiddleLeft);
            Label("Tagline", group, left, new Vector2(420f, 245f), new Vector2(760f, 50f), "빌딩 사이, 한계 없이.", 36, TextAnchor.MiddleLeft, FontStyle.Normal);
            MenuButton("SkylineRun", group, left, new Vector2(300f, 110f), new Vector2(480f, 110f), "SKYLINE RUN", menu.StartSkylineRun);
            MenuButton("FreeFlight", group, left, new Vector2(300f, -20f), new Vector2(480f, 110f), "FREE FLIGHT", menu.StartFreeFlight);
            MenuButton("Hangar", group, left, new Vector2(180f, -150f), new Vector2(230f, 90f), "HANGAR", menu.OpenHangar);
            MenuButton("Settings", group, left, new Vector2(420f, -150f), new Vector2(230f, 90f), "SETTINGS", menu.OpenSettings);
            Text records = Label("Records", group, new Vector2(0f, 0f), new Vector2(560f, 90f), new Vector2(1000f, 100f), "", 28, TextAnchor.MiddleLeft, FontStyle.Normal);
            Text notice = Label("Notice", safe, left, new Vector2(300f, -250f), new Vector2(600f, 50f), "", 28, TextAnchor.MiddleCenter, FontStyle.Normal);
            SetRef(menu, "recordsText", records);
            SetRef(menu, "noticeText", notice);
            Text aircraftLabel = Label("Aircraft", group, left, new Vector2(420f, 190f), new Vector2(760f, 40f), "", 28, TextAnchor.MiddleLeft, FontStyle.Normal);
            Text credits = Label("Credits", safe, new Vector2(1f, 0f), new Vector2(-330f, 30f), new Vector2(620f, 30f), "", 18, TextAnchor.MiddleRight, FontStyle.Normal);
            SetRef(menu, "aircraftText", aircraftLabel);
            SetRef(menu, "creditsText", credits);

            // 격납고(애드온 명세 14.5): 3D 미리보기 받침대는 메뉴 카메라 정면
            var pedestal = new GameObject("HangarPedestal").transform;
            pedestal.position = camGo.transform.position + camGo.transform.forward * 26f + Vector3.down * 2f;
            HangarController hangar = BuildHangarUi(canvas, menu, group.gameObject, pedestal);
            SetRef(menu, "hangar", hangar);
            SetRef(menu, "settings", BuildSettingsUi(canvas));

            // 메뉴 배경 건물 메시도 에셋으로 저장
            SaveMenuMeshes(bg);

            string path = $"{ScenesDir}/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static HangarController BuildHangarUi(Canvas canvas, MainMenuController menu, GameObject menuGroup, Transform pedestal)
        {
            RectTransform panel = Rect("HangarPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            HangarController hangar = panel.gameObject.AddComponent<HangarController>();

            // 가운데: 드래그로 돌리는 미리보기 영역(투명) — 3D 기체는 메뉴 카메라 앞 받침대에 표시
            Image dragArea = Panel("PreviewDrag", panel, new Vector2(0.28f, 0.15f), new Vector2(0.62f, 0.95f), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
            AircraftPreviewController preview = dragArea.gameObject.AddComponent<AircraftPreviewController>();
            SetRef(preview, "pedestal", pedestal);

            // 왼쪽: 기체 카드 목록(카탈로그에서 실행 시 생성)
            Image left = Panel("CardColumn", panel, new Vector2(0f, 0f), new Vector2(0.27f, 1f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.04f, 0.08f, 0.8f));
            Label("Title", left.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(460f, 60f), "HANGAR", 48);
            RectTransform list = Rect("Cards", left.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-40f, -160f));
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            Image cardBg = Panel("CardTemplate", list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(440f, 96f), new Color(0.08f, 0.12f, 0.2f, 0.85f), RoundedSprite);
            Button cardTemplate = cardBg.gameObject.AddComponent<Button>();
            Label("Name", cardBg.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(420f, 44f), "", 32);
            Label("Status", cardBg.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(420f, 32f), "", 22, TextAnchor.MiddleCenter, FontStyle.Normal);

            // 오른쪽: 상세·상대 성능 막대·버튼
            Image right = Panel("DetailColumn", panel, new Vector2(0.63f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.04f, 0.08f, 0.8f));
            Vector2 top = new Vector2(0.5f, 1f);
            Text nameText = Label("Name", right.transform, top, new Vector2(0f, -60f), new Vector2(640f, 56f), "", 38);
            Text detail = Label("Detail", right.transform, top, new Vector2(0f, -150f), new Vector2(640f, 110f), "", 24, TextAnchor.UpperLeft, FontStyle.Normal);
            string[] statNames = { "속도", "가속", "기동성", "안정성", "크기" };
            var bars = new Image[statNames.Length];
            for (int i = 0; i < statNames.Length; i++)
            {
                float y = -250f - i * 46f;
                Label($"StatLabel{i}", right.transform, top, new Vector2(-230f, y), new Vector2(160f, 40f), statNames[i], 26, TextAnchor.MiddleLeft, FontStyle.Normal);
                Image bg = Panel($"StatBg{i}", right.transform, top, top, new Vector2(70f, y), new Vector2(400f, 18f), new Color(1f, 1f, 1f, 0.15f));
                Image fill = Panel("Fill", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.6f, 0.15f, 0.95f), RoundedSprite);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                bars[i] = fill;
            }
            Text unlock = Label("Unlock", right.transform, top, new Vector2(0f, -500f), new Vector2(640f, 44f), "", 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            Text notice = Label("Notice", right.transform, top, new Vector2(0f, -545f), new Vector2(640f, 44f), "", 22, TextAnchor.MiddleCenter, FontStyle.Normal);
            notice.color = new Color(1f, 0.75f, 0.4f);
            Vector2 bottom = new Vector2(0.5f, 0f);
            Button select = MenuButton("Select", right.transform, bottom, new Vector2(-170f, 250f), new Vector2(300f, 90f), "SELECT", hangar.SelectViewing);
            Button flyFree = MenuButton("FlyFree", right.transform, bottom, new Vector2(170f, 250f), new Vector2(300f, 90f), "FREE FLIGHT", hangar.FlyFree);
            Button flySkyline = MenuButton("FlySkyline", right.transform, bottom, new Vector2(170f, 150f), new Vector2(300f, 90f), "SKYLINE RUN", hangar.FlySkyline);
            MenuButton("Back", right.transform, bottom, new Vector2(-170f, 150f), new Vector2(300f, 90f), "BACK", hangar.Hide);

            SetRef(hangar, "panel", panel.gameObject);
            SetRef(hangar, "menuGroup", menuGroup);
            SetRef(hangar, "cardContainer", list);
            SetRef(hangar, "cardTemplate", cardTemplate);
            SetRef(hangar, "nameText", nameText);
            SetRef(hangar, "detailText", detail);
            SetRef(hangar, "unlockText", unlock);
            SetRefs(hangar, "statBars", bars);
            SetRef(hangar, "selectButton", select);
            SetRef(hangar, "selectButtonLabel", select.GetComponentInChildren<Text>());
            SetRef(hangar, "flyFreeButton", flyFree);
            SetRef(hangar, "flySkylineButton", flySkyline);
            SetRef(hangar, "noticeText", notice);
            SetRef(hangar, "preview", preview);
            SetRef(hangar, "menu", menu);
            return hangar;
        }

        private static DefaultControls.Resources UiResources => new DefaultControls.Resources
        {
            standard = RoundedSprite,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = Knob,
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        };

        private static Slider SettingSlider(Transform parent, string name, Vector2 pos, out Text label, float min = 0f, float max = 1f)
        {
            label = Label($"{name}Label", parent, new Vector2(0.5f, 1f), pos + new Vector2(-200f, 0f), new Vector2(380f, 44f), name, 28, TextAnchor.MiddleLeft, FontStyle.Normal);
            GameObject go = DefaultControls.CreateSlider(UiResources);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos + new Vector2(200f, 0f);
            rt.sizeDelta = new Vector2(380f, 36f); // 터치하기 쉬운 높이
            Slider s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            return s;
        }

        private static Toggle SettingToggle(Transform parent, string label, Vector2 pos)
        {
            GameObject go = DefaultControls.CreateToggle(UiResources);
            go.name = label;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(380f, 48f);
            Text t = go.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.text = label;
                t.font = UiFont;
                t.fontSize = 28;
                t.color = Color.white;
            }
            Transform bg = go.transform.Find("Background");
            if (bg != null) ((RectTransform)bg).sizeDelta = new Vector2(40f, 40f);
            return go.GetComponent<Toggle>();
        }

        /// <summary>설정 화면(명세 5.5). 메인 메뉴·일시정지 화면 공용.</summary>
        private static SettingsPanelController BuildSettingsUi(Canvas canvas)
        {
            Image panel = Panel("SettingsPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.03f, 0.07f, 0.92f));
            SettingsPanelController c = panel.gameObject.AddComponent<SettingsPanelController>();
            Transform p = panel.transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Label("Title", p, top, new Vector2(0f, -60f), new Vector2(800f, 70f), "SETTINGS", 52);

            // 왼쪽 열: 그래픽·조작
            float lx = -470f, rx = 470f;
            Button quality = MenuButton("Quality", p, top, new Vector2(lx, -160f), new Vector2(420f, 80f), "그래픽", c.CycleQuality);
            Button frame = MenuButton("FrameRate", p, top, new Vector2(lx, -255f), new Vector2(420f, 80f), "프레임", c.CycleFrameRate);
            Slider sens = SettingSlider(p, "조작 감도", new Vector2(lx, -350f), out Text sensLabel, 0.5f, 1.5f);
            Slider shake = SettingSlider(p, "카메라 흔들림", new Vector2(lx, -430f), out Text shakeLabel);
            Toggle invert = SettingToggle(p, "피치 반전(위로 밀면 하강)", new Vector2(lx, -510f));
            Toggle floating = SettingToggle(p, "플로팅 조이스틱", new Vector2(lx, -570f));

            // 오른쪽 열: 소리·진동·기타
            Slider master = SettingSlider(p, "전체 음량", new Vector2(rx, -160f), out Text masterLabel);
            Slider music = SettingSlider(p, "음악", new Vector2(rx, -240f), out Text musicLabel);
            Slider sfx = SettingSlider(p, "효과음", new Vector2(rx, -320f), out Text sfxLabel);
            Toggle haptics = SettingToggle(p, "진동", new Vector2(rx, -400f));
            MenuButton("Tutorial", p, top, new Vector2(rx - 115f, -490f), new Vector2(210f, 80f), "튜토리얼", c.ReplayTutorial);
            MenuButton("Reset", p, top, new Vector2(rx + 115f, -490f), new Vector2(210f, 80f), "초기화", c.ResetToDefaults);
            MenuButton("Licenses", p, top, new Vector2(rx - 115f, -585f), new Vector2(210f, 80f), "라이선스", c.ToggleLicenses);
            MenuButton("Close", p, top, new Vector2(rx + 115f, -585f), new Vector2(210f, 80f), "닫기", c.Hide);
            Text notice = Label("Notice", p, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1200f, 44f), "", 26, TextAnchor.MiddleCenter, FontStyle.Normal);
            notice.color = new Color(1f, 0.8f, 0.45f);

            // 라이선스 고지 오버레이
            Image lic = Panel("LicensePanel", p, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero, new Color(0.04f, 0.07f, 0.12f, 0.98f), RoundedSprite);
            // 스크롤 가능한 고지 본문(OFL 전문 포함)
            RectTransform viewport = Rect("Viewport", lic.transform, Vector2.zero, Vector2.one, new Vector2(0f, 40f), new Vector2(-60f, -140f));
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 600f));
            content.pivot = new Vector2(0.5f, 1f);
            Text licText = content.gameObject.AddComponent<Text>();
            licText.font = UiFont;
            licText.fontSize = 24;
            licText.color = Color.white;
            licText.alignment = TextAnchor.UpperLeft;
            licText.horizontalOverflow = HorizontalWrapMode.Wrap;
            licText.verticalOverflow = VerticalWrapMode.Overflow;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = lic.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            MenuButton("CloseLicenses", lic.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(260f, 80f), "닫기", c.ToggleLicenses);

            SetRef(c, "panel", panel.gameObject);
            SetRef(c, "qualityLabel", quality.GetComponentInChildren<Text>());
            SetRef(c, "frameRateLabel", frame.GetComponentInChildren<Text>());
            SetRef(c, "sensitivity", sens);
            SetRef(c, "sensitivityLabel", sensLabel);
            SetRef(c, "cameraShake", shake);
            SetRef(c, "cameraShakeLabel", shakeLabel);
            SetRef(c, "invertPitch", invert);
            SetRef(c, "floatingJoystick", floating);
            SetRef(c, "haptics", haptics);
            SetRef(c, "masterVolume", master);
            SetRef(c, "masterLabel", masterLabel);
            SetRef(c, "musicVolume", music);
            SetRef(c, "musicLabel", musicLabel);
            SetRef(c, "sfxVolume", sfx);
            SetRef(c, "sfxLabel", sfxLabel);
            SetRef(c, "noticeText", notice);
            SetRef(c, "licensePanel", lic.gameObject);
            SetRef(c, "licenseText", licText);
            return c;
        }

        /// <summary>첫 비행 조작 튜토리얼: 화면 위쪽 안내 + 진행 막대 + 건너뛰기.</summary>
        private static void BuildTutorialUi(Transform safe, FlightSession session)
        {
            Image box = Panel("Tutorial", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(980f, 120f), new Color(0f, 0f, 0f, 0.55f), RoundedSprite);
            Text text = Label("Instruction", box.transform, new Vector2(0.5f, 0.5f), new Vector2(-60f, 14f), new Vector2(820f, 56f), "", 34);
            Image bar = Panel("ProgressBg", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -34f), new Vector2(700f, 10f), new Color(1f, 1f, 1f, 0.2f));
            Image fill = Panel("Fill", bar.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.65f, 0.15f, 1f), RoundedSprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            var ctrlGo = new GameObject("TutorialController");
            ctrlGo.transform.SetParent(safe, false);
            TutorialController ctrl = ctrlGo.AddComponent<TutorialController>();
            MenuButton("Skip", box.transform, new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(130f, 70f), "SKIP", ctrl.Skip);
            SetRef(ctrl, "session", session);
            SetRef(ctrl, "root", box.gameObject);
            SetRef(ctrl, "instruction", text);
            SetRef(ctrl, "progress", fill);
        }

        private static void SaveMenuMeshes(Transform root)
        {
            const string path = Root + "/Art/Buildings/MenuMeshes.asset";
            AssetDatabase.DeleteAsset(path);
            var container = new Mesh { name = "MenuMeshes" };
            AssetDatabase.CreateAsset(container, path);
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))) continue; // 내장·기존 에셋 메시는 제외
                mesh.name = $"{mf.transform.parent.name}_{mf.name}";
                AssetDatabase.AddObjectToAsset(mesh, container);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
