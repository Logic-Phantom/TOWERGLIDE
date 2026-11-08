using TowerGlide.Core.Aircraft;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using UnityEditor;
using UnityEngine;
using static TowerGlide.EditorTools.TowerglideEditorUtil;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// 기체 라인업 생성(애드온 명세 14): 튜닝 에셋·프리팹·기체 정의·카탈로그(Resources).
    /// - 공통 AircraftFlightController + 기종별 튜닝 데이터. 컨트롤러 복사·기종 분기 없음.
    /// - 튜닝 에셋은 **없을 때만** 초깃값으로 만든다 → 사용자가 조정한 값을 씬 재생성이 덮어쓰지 않음.
    /// - 모델은 실제 기체를 복제하지 않은 독자 그레이박스 실루엣(실제 제조사·항공사 표식 없음).
    /// </summary>
    internal static class TowerglideAircraftFactory
    {
        public const string TuningDir = SettingsDir + "/Aircraft";
        public const string DefinitionsDir = SettingsDir + "/Aircraft";
        public const string ResourcesDir = Root + "/Resources";
        public const string CatalogPath = ResourcesDir + "/AircraftCatalog.asset";

        public sealed class Result
        {
            public GameObject ScoutPrefab, ViperPrefab, LinerPrefab;
            public AircraftCatalog Catalog;
        }

        public static Result CreateAll(TowerglideMaterials m, AircraftFlightTuning scoutTuning)
        {
            EnsureFolder(TuningDir);
            EnsureFolder(ResourcesDir);
            EnsureFolder(TowerglidePrefabFactory.AircraftDir);

            AircraftFlightTuning viperTuning = EnsureTuning("Tuning_ViperF9", SeedViper);
            AircraftFlightTuning linerTuning = EnsureTuning("Tuning_Cityliner180", SeedLiner);

            Material jetGray = GetOrCreateLit("M_Jet_Gray", new Color(0.42f, 0.46f, 0.5f), 0.55f, 0.35f);
            Material linerWhite = GetOrCreateLit("M_Liner_White", new Color(0.93f, 0.94f, 0.96f), 0.6f, 0.2f);
            Material linerBlue = GetOrCreateLit("M_Liner_Blue", new Color(0.12f, 0.32f, 0.62f), 0.6f, 0.2f);
            Material exhaust = GetOrCreateLit("M_Exhaust", new Color(1f, 0.55f, 0.2f), 0f, 0f, new Color(1f, 0.45f, 0.1f) * 2f);

            var r = new Result
            {
                ScoutPrefab = Save(BuildScout(m, scoutTuning), "Aircraft_ScoutP1"),
                ViperPrefab = Save(BuildViper(m, viperTuning, jetGray, exhaust), "Aircraft_ViperF9"),
                LinerPrefab = Save(BuildLiner(m, linerTuning, linerWhite, linerBlue), "Aircraft_Cityliner180"),
            };

            AircraftDefinition scout = Definition(AircraftIds.ScoutP1, "Scout P-1", AircraftCategory.LightProp,
                "가볍고 저속에서도 안정적인 소형 프로펠러기. 좁은 빌딩 사이와 저고도 탐험의 기준 기체.",
                r.ScoutPrefab, scoutTuning, new Color(0.86f, 0.12f, 0.12f), 7.5f, 8.4f, 2.4f, 0f, AircraftTags.TightRoute, UnlockRule.Always);
            AircraftDefinition viper = Definition(AircraftIds.ViperF9, "Viper F-9", AircraftCategory.FighterJet,
                "높은 추력과 최고 속도의 제트. 고속에서는 선회 반경과 감속 거리가 커서 속도 관리가 중요하다. 무기 없음.",
                r.ViperPrefab, viperTuning, new Color(0.42f, 0.46f, 0.5f), 15f, 10f, 4f, 60f, AircraftTags.HighSpeed | AircraftTags.WideAirspace, UnlockRule.Always);
            AircraftDefinition liner = Definition(AircraftIds.Cityliner180, "Cityliner 180", AircraftCategory.Airliner,
                "큰 동체와 날개, 높은 관성의 중형 여객기. 롤이 느리고 선회 반경이 넓다 — 도심 위 고고도·넓은 공간용.",
                r.LinerPrefab, linerTuning, new Color(0.12f, 0.32f, 0.62f), 38f, 34f, 11f, 260f, AircraftTags.WideAirspace, UnlockRule.Always);

            AircraftDefinition aero = Definition(AircraftIds.AeroS2, "Aero S-2", AircraftCategory.Aerobatic,
                "롤·피치 반응이 빠른 곡예기(개발 예정).", null, null, Color.gray, 6.5f, 7.5f, 2.2f, 0f,
                AircraftTags.TightRoute | AircraftTags.Aerobatics, UnlockRule.ComingSoon);
            AircraftDefinition metro = Definition(AircraftIds.Metro70, "Metro 70", AircraftCategory.RegionalAirliner,
                "쌍발 지역 여객기(개발 예정).", null, null, Color.gray, 27f, 27f, 8f, 0f, AircraftTags.WideAirspace, UnlockRule.ComingSoon);
            AircraftDefinition longhaul = Definition(AircraftIds.Longhaul300, "Longhaul 300", AircraftCategory.WidebodyAirliner,
                "대형 장거리 여객기(개발 예정).", null, null, Color.gray, 64f, 60f, 18f, 0f, AircraftTags.WideAirspace, UnlockRule.ComingSoon);
            AircraftDefinition cargo = Definition(AircraftIds.CargoLift220, "CargoLift 220", AircraftCategory.Cargo,
                "고익 화물기(개발 예정).", null, null, Color.gray, 45f, 42f, 14f, 0f, AircraftTags.WideAirspace, UnlockRule.ComingSoon);

            var catalog = AssetDatabase.LoadAssetAtPath<AircraftCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AircraftCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.SetEntries(new[] { scout, viper, liner, aero, metro, longhaul, cargo }, AircraftIds.Default);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            r.Catalog = catalog;
            return r;
        }

        // ------------------------------------------------------------------ 튜닝 초깃값(시작점 — docs/AIRCRAFT.md)

        private static void SeedViper(AircraftFlightTuning t)
        {
            t.mass = 9000f;
            t.minFlightSpeed = 45f; t.cruiseSpeed = 70f; t.maxSpeed = 140f; t.boostMaxSpeed = 170f;
            t.maxThrustAcceleration = 35f; t.thrustResponsiveness = 1.5f; t.boostExtraAcceleration = 25f;
            t.liftFactor = 0.0015f; t.dragFactor = 0.0012f; t.airBrakeDragMultiplier = 3f; t.velocityAlignment = 1.2f;
            t.maxPitchRate = 60f; t.maxRollRate = 220f; t.maxYawRate = 25f; t.angularResponse = 7f;
            t.coordinatedTurnYawRate = 45f; t.turnPitchAssistRate = 20f; t.autoLevelRate = 30f; t.maxBankAngleDeg = 85f;
            t.stallNoseDropRate = 30f; t.lowSpeedAuthority = 0.3f; t.highSpeedAuthority = 0.45f;
            t.boostEnabled = true; t.boostCapacitySeconds = 4f; t.boostFullRechargeSeconds = 8f;
        }

        private static void SeedLiner(AircraftFlightTuning t)
        {
            t.mass = 70000f;
            t.minFlightSpeed = 50f; t.cruiseSpeed = 65f; t.maxSpeed = 95f; t.boostMaxSpeed = 95f;
            t.maxThrustAcceleration = 9f; t.thrustResponsiveness = 0.4f; t.boostExtraAcceleration = 0f;
            t.liftFactor = 0.0012f; t.dragFactor = 0.0009f; t.airBrakeDragMultiplier = 2.5f; t.velocityAlignment = 0.8f;
            t.maxPitchRate = 18f; t.maxRollRate = 35f; t.maxYawRate = 8f; t.angularResponse = 1.6f;
            t.coordinatedTurnYawRate = 14f; t.turnPitchAssistRate = 5f; t.autoLevelRate = 25f; t.maxBankAngleDeg = 35f;
            t.stallNoseDropRate = 12f; t.lowSpeedAuthority = 0.3f; t.highSpeedAuthority = 0.8f;
            t.boostEnabled = false;
        }

        private static AircraftFlightTuning EnsureTuning(string name, System.Action<AircraftFlightTuning> seed)
        {
            string path = $"{TuningDir}/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<AircraftFlightTuning>(path);
            if (t != null) return t; // 사용자 튜닝 보존
            t = ScriptableObject.CreateInstance<AircraftFlightTuning>();
            seed(t);
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        private static AircraftDefinition Definition(string id, string displayName, AircraftCategory category, string description,
            GameObject prefab, AircraftFlightTuning tuning, Color accent, float length, float wingspan, float height, float spawnHeightOffset,
            AircraftTags tags, UnlockRule unlock)
        {
            string path = $"{DefinitionsDir}/Aircraft_{id}.asset";
            var d = AssetDatabase.LoadAssetAtPath<AircraftDefinition>(path);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<AircraftDefinition>();
                AssetDatabase.CreateAsset(d, path);
            }
            d.id = id;
            d.displayName = displayName;
            d.category = category;
            d.description = description;
            d.prefab = prefab;
            d.tuning = tuning;
            d.accentColor = accent;
            d.length = length;
            d.wingspan = wingspan;
            d.height = height;
            d.spawnHeightOffset = spawnHeightOffset;
            d.tags = tags;
            d.unlock = unlock;
            float vmax = tuning != null ? Mathf.Max(tuning.maxSpeed, tuning.boostMaxSpeed) : 80f;
            d.cameraProfile = AircraftCameraProfile.ForSize(length, wingspan, vmax);
            EditorUtility.SetDirty(d);
            return d;
        }

        private static GameObject Save(GameObject go, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{TowerglidePrefabFactory.AircraftDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ 공통 조립

        private static GameObject CreateRoot(string name, AircraftFlightTuning tuning, float sensorRadius, out Transform visual)
        {
            var root = new GameObject(name);
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = tuning != null ? tuning.mass : 900f;
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            AircraftFlightController controller = root.AddComponent<AircraftFlightController>();
            SetRef(controller, "tuning", tuning);
            root.AddComponent<AircraftCollisionHandler>();

            var sensorGo = new GameObject("NearMissSensor");
            sensorGo.transform.SetParent(root.transform, false);
            SphereCollider sphere = sensorGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = sensorRadius;
            NearMissSensor sensor = sensorGo.AddComponent<NearMissSensor>();
            SetRef(sensor, "aircraft", controller);
            SetFloat(sensor, "radius", sensorRadius);
            return root;
        }

        private static void Capsule(GameObject root, float radius, float length)
        {
            CapsuleCollider c = root.AddComponent<CapsuleCollider>();
            c.direction = 2;
            c.radius = radius;
            c.height = length;
        }

        private static void Box(GameObject root, Vector3 center, Vector3 size)
        {
            BoxCollider b = root.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
        }

        // ------------------------------------------------------------------ 기체 3종

        /// <summary>소형 프로펠러기(기존 POC 기체와 같은 형상·크기).</summary>
        private static GameObject BuildScout(TowerglideMaterials m, AircraftFlightTuning tuning)
        {
            GameObject root = CreateRoot("Aircraft_ScoutP1", tuning, 7f, out Transform v);
            Capsule(root, 0.8f, 7f);
            Box(root, new Vector3(0f, 0f, 0.3f), new Vector3(8.4f, 0.35f, 1.8f));
            Visual(PrimitiveType.Capsule, "Fuselage", v, Vector3.zero, new Vector3(1.4f, 3.5f, 1.4f), m.AircraftBody, new Vector3(90f, 0f, 0f));
            Visual(PrimitiveType.Sphere, "Canopy", v, new Vector3(0f, 0.65f, 0.6f), new Vector3(0.9f, 0.7f, 1.8f), m.Canopy);
            Visual(PrimitiveType.Cube, "Wing", v, new Vector3(0f, 0f, 0.3f), new Vector3(8.4f, 0.18f, 1.7f), m.AircraftAccent);
            Visual(PrimitiveType.Cube, "WingStripe", v, new Vector3(0f, 0.1f, 0.3f), new Vector3(2.2f, 0.05f, 1.72f), m.AircraftBody);
            Visual(PrimitiveType.Cube, "TailPlane", v, new Vector3(0f, 0.15f, -3.1f), new Vector3(3.2f, 0.12f, 0.9f), m.AircraftAccent);
            Visual(PrimitiveType.Cube, "TailFin", v, new Vector3(0f, 0.9f, -3.1f), new Vector3(0.12f, 1.5f, 1.0f), m.AircraftAccent);
            Visual(PrimitiveType.Cube, "Propeller", v, new Vector3(0f, 0f, 3.6f), new Vector3(2.2f, 0.18f, 0.05f), m.Drone);
            return root;
        }

        /// <summary>전투기형 제트: 길이 15m·날개폭 10m, 삼각익 근사·쌍수직미익·배기 발광. 무장 없음.</summary>
        private static GameObject BuildViper(TowerglideMaterials m, AircraftFlightTuning tuning, Material body, Material exhaust)
        {
            GameObject root = CreateRoot("Aircraft_ViperF9", tuning, 9f, out Transform v);
            Capsule(root, 1.0f, 15f);
            Box(root, new Vector3(0f, 0f, -2f), new Vector3(10f, 0.5f, 5f));
            Visual(PrimitiveType.Capsule, "Fuselage", v, Vector3.zero, new Vector3(1.7f, 7.5f, 1.9f), body, new Vector3(90f, 0f, 0f));
            Visual(PrimitiveType.Sphere, "Canopy", v, new Vector3(0f, 0.85f, 3.2f), new Vector3(0.9f, 0.8f, 2.6f), m.Canopy);
            Visual(PrimitiveType.Cube, "WingL", v, new Vector3(-2.7f, -0.1f, -2.2f), new Vector3(5f, 0.16f, 4.6f), body, new Vector3(0f, -28f, 0f));
            Visual(PrimitiveType.Cube, "WingR", v, new Vector3(2.7f, -0.1f, -2.2f), new Vector3(5f, 0.16f, 4.6f), body, new Vector3(0f, 28f, 0f));
            Visual(PrimitiveType.Cube, "StripeL", v, new Vector3(-3.4f, 0f, -2.4f), new Vector3(2.4f, 0.17f, 0.5f), m.AircraftAccent, new Vector3(0f, -28f, 0f));
            Visual(PrimitiveType.Cube, "StripeR", v, new Vector3(3.4f, 0f, -2.4f), new Vector3(2.4f, 0.17f, 0.5f), m.AircraftAccent, new Vector3(0f, 28f, 0f));
            Visual(PrimitiveType.Cube, "FinL", v, new Vector3(-1.1f, 1.3f, -5.8f), new Vector3(0.14f, 2.4f, 2.2f), body, new Vector3(0f, 0f, 14f));
            Visual(PrimitiveType.Cube, "FinR", v, new Vector3(1.1f, 1.3f, -5.8f), new Vector3(0.14f, 2.4f, 2.2f), body, new Vector3(0f, 0f, -14f));
            Visual(PrimitiveType.Cube, "Stabilizer", v, new Vector3(0f, 0f, -6.4f), new Vector3(5.2f, 0.12f, 1.6f), body);
            Visual(PrimitiveType.Sphere, "Exhaust", v, new Vector3(0f, 0f, -7.6f), new Vector3(1.1f, 1.1f, 0.6f), exhaust);
            return root;
        }

        /// <summary>중형 여객기: 길이 38m·날개폭 34m, 후퇴익·날개 아래 엔진 2기. 가상 도장(실제 항공사 아님).</summary>
        private static GameObject BuildLiner(TowerglideMaterials m, AircraftFlightTuning tuning, Material white, Material blue)
        {
            GameObject root = CreateRoot("Aircraft_Cityliner180", tuning, 18f, out Transform v);
            Capsule(root, 2.2f, 38f);
            Box(root, new Vector3(0f, -0.6f, -1f), new Vector3(34f, 1.2f, 7f));
            Box(root, new Vector3(0f, 4f, -17f), new Vector3(0.8f, 7f, 5f));
            Visual(PrimitiveType.Capsule, "Fuselage", v, Vector3.zero, new Vector3(4f, 19f, 4.2f), white, new Vector3(90f, 0f, 0f));
            Visual(PrimitiveType.Cube, "WindowBand", v, new Vector3(0f, 0.8f, 1f), new Vector3(4.05f, 0.5f, 28f), m.Canopy);
            Visual(PrimitiveType.Cube, "Cheatline", v, new Vector3(0f, -0.2f, 0f), new Vector3(4.06f, 0.35f, 34f), blue);
            Visual(PrimitiveType.Cube, "WingL", v, new Vector3(-9f, -0.8f, -1f), new Vector3(17f, 0.5f, 5f), white, new Vector3(0f, -20f, 0f));
            Visual(PrimitiveType.Cube, "WingR", v, new Vector3(9f, -0.8f, -1f), new Vector3(17f, 0.5f, 5f), white, new Vector3(0f, 20f, 0f));
            Visual(PrimitiveType.Cylinder, "EngineL", v, new Vector3(-7f, -2.2f, 2f), new Vector3(2.2f, 2.4f, 2.2f), blue, new Vector3(90f, 0f, 0f));
            Visual(PrimitiveType.Cylinder, "EngineR", v, new Vector3(7f, -2.2f, 2f), new Vector3(2.2f, 2.4f, 2.2f), blue, new Vector3(90f, 0f, 0f));
            Visual(PrimitiveType.Cube, "TailFin", v, new Vector3(0f, 4.5f, -16.5f), new Vector3(0.4f, 7f, 5f), blue, new Vector3(-15f, 0f, 0f));
            Visual(PrimitiveType.Cube, "Stabilizer", v, new Vector3(0f, 1.2f, -17f), new Vector3(12f, 0.3f, 3f), white);
            return root;
        }
    }
}
