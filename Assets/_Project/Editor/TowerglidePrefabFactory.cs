using TowerGlide.Flight;
using TowerGlide.Gameplay;
using TowerGlide.World;
using UnityEditor;
using UnityEngine;
using static TowerGlide.EditorTools.TowerglideEditorUtil;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// 장애물·기체·링·VFX 프리팹 생성(명세 8 Prefabs/*). 크기가 고정된 것만 프리팹으로 만들고 씬에서는 인스턴스로 배치한다.
    /// 같은 경로에 다시 저장하므로 GUID가 유지되고, 씬의 프리팹 연결도 유지된다(멱등).
    /// 축 규칙: 프리팹의 +Z = 진행/통과 방향(링, 광고판, 케이블), 원점 = 바닥(크레인) 또는 중심(드론·링).
    /// </summary>
    internal static class TowerglidePrefabFactory
    {
        public const string ObstaclesDir = Root + "/Prefabs/Obstacles";
        public const string AircraftDir = Root + "/Prefabs/Aircraft";
        public const string VfxDir = Root + "/Prefabs/VFX";
        public const float RingInnerRadius = 8f;
        /// <summary>도시 거리 폭(건물 사이) 기준 — 광고판·케이블 스팬.</summary>
        public const float StreetSpan = 46f;

        public sealed class Set
        {
            public GameObject Gate, CraneStatic, CraneRotating, Drone, Billboard, Skybridge, Cable, Antenna, CargoBalloon;
            public ParticleSystem FxCrash, FxRing, FxNearMiss;
        }

        public static Set CreateAll(TowerglideMaterials m)
        {
            EnsureFolder(ObstaclesDir);
            EnsureFolder(AircraftDir);
            EnsureFolder(VfxDir);
            return new Set
            {
                Gate = Save(BuildGate(m), $"{ObstaclesDir}/CheckpointRing.prefab"),
                CraneStatic = Save(BuildCrane(m, "Crane_Static", 110f, 95f, hookAt: 35f, rotateDegPerSec: 0f), $"{ObstaclesDir}/Crane_Static.prefab"),
                CraneRotating = Save(BuildCrane(m, "Crane_Rotating", 150f, 132f, hookAt: 56f, rotateDegPerSec: 6f), $"{ObstaclesDir}/Crane_Rotating.prefab"),
                Drone = Save(BuildDrone(m), $"{ObstaclesDir}/SurveyDrone.prefab"),
                Billboard = Save(BuildBillboard(m), $"{ObstaclesDir}/BillboardFrame.prefab"),
                Skybridge = Save(BuildSkybridge(m), $"{ObstaclesDir}/Skybridge.prefab"),
                Cable = Save(BuildCable(m), $"{ObstaclesDir}/CableZone.prefab"),
                Antenna = Save(BuildAntenna(m), $"{ObstaclesDir}/AntennaArray.prefab"),
                CargoBalloon = Save(BuildCargoBalloon(m), $"{ObstaclesDir}/CargoBalloon.prefab"),
                FxCrash = Save(BuildFx(m, FxKind.Crash), $"{VfxDir}/FX_Crash.prefab").GetComponent<ParticleSystem>(),
                FxRing = Save(BuildFx(m, FxKind.Ring), $"{VfxDir}/FX_RingPass.prefab").GetComponent<ParticleSystem>(),
                FxNearMiss = Save(BuildFx(m, FxKind.NearMiss), $"{VfxDir}/FX_NearMiss.prefab").GetComponent<ParticleSystem>(),
            };
        }

        private static GameObject Save(GameObject go, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static GameObject Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, rotation);
            if (name != null) go.name = name;
            return go;
        }

        // 기체 프리팹은 TowerglideAircraftFactory(기종별 라인업)가 만든다.

        // ------------------------------------------------------------------ 링

        private static GameObject BuildGate(TowerglideMaterials m)
        {
            var gateGo = new GameObject("CheckpointRing");
            BoxCollider trigger = gateGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(RingInnerRadius * 2f, RingInnerRadius * 2f, 3f);
            CheckpointGate gate = gateGo.AddComponent<CheckpointGate>();

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
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Seg_{s:00}";
                seg.transform.SetParent(frame, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg + 90f);
                seg.transform.localScale = new Vector3(segLen, 1.4f, 1.4f);
                renderers[s] = seg.GetComponent<Renderer>();
                renderers[s].sharedMaterial = m.Ring;
            }

            // 월드 마커: 링 위로 솟은 빛기둥(다음 링일 때만 켜짐, 충돌 없음)
            GameObject beacon = Visual(PrimitiveType.Cylinder, "Beacon", gateGo.transform, new Vector3(0f, RingInnerRadius + 40f, 0f),
                new Vector3(0.8f, 30f, 0.8f), m.Beacon);
            beacon.SetActive(false);

            SetRefs(gate, "frameRenderers", renderers);
            SetRef(gate, "beacon", beacon);
            return gateGo;
        }

        // ------------------------------------------------------------------ 장애물

        /// <summary>크레인: 원점 = 마스트 바닥, 붐 = +X 방향(인스턴스 회전으로 방향 지정).</summary>
        private static GameObject BuildCrane(TowerglideMaterials m, string name, float mastHeight, float boomHeight, float hookAt, float rotateDegPerSec)
        {
            var crane = new GameObject(name);
            crane.AddComponent<ObstacleMarker>().Configure(ObstacleType.ConstructionCrane);
            Child(PrimitiveType.Cube, "Mast", crane.transform, new Vector3(0f, mastHeight * 0.5f, 0f), new Vector3(4f, mastHeight, 4f), m.Steel);

            var pivot = new GameObject("BoomPivot").transform;
            pivot.SetParent(crane.transform, false);
            pivot.localPosition = new Vector3(0f, boomHeight, 0f);
            const float boomLength = 62f;
            Child(PrimitiveType.Cube, "Boom", pivot, new Vector3(boomLength * 0.5f, 0f, 0f), new Vector3(boomLength, 2.4f, 2.4f), m.Steel);
            Child(PrimitiveType.Cube, "CounterJib", pivot, new Vector3(-10f, 0f, 0f), new Vector3(20f, 2.4f, 2.4f), m.Steel);
            Child(PrimitiveType.Cube, "CounterWeight", pivot, new Vector3(-17f, -2.5f, 0f), new Vector3(5f, 4f, 4f), m.Drone);
            Child(PrimitiveType.Cube, "HookCable", pivot, new Vector3(hookAt, -7f, 0f), new Vector3(0.4f, 14f, 0.4f), m.Drone);
            Child(PrimitiveType.Cube, "HookBlock", pivot, new Vector3(hookAt, -15f, 0f), new Vector3(3f, 3f, 3f), m.Hazard);
            Visual(PrimitiveType.Sphere, "TipLight", pivot, new Vector3(boomLength, 1.6f, 0f), Vector3.one * 1.6f, m.Hazard);

            if (Mathf.Abs(rotateDegPerSec) > 0.01f)
            {
                Rigidbody rb = pivot.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                pivot.gameObject.AddComponent<KinematicMover>().Configure(new Vector3[0], 0f, Vector3.up, rotateDegPerSec);
            }
            return crane;
        }

        /// <summary>드론: 경로는 인스턴스에서 KinematicMover.Configure로 지정.</summary>
        private static GameObject BuildDrone(TowerglideMaterials m)
        {
            var drone = new GameObject("SurveyDrone");
            drone.AddComponent<ObstacleMarker>().Configure(ObstacleType.SurveyDrone);
            BoxCollider c = drone.AddComponent<BoxCollider>();
            c.size = new Vector3(3.4f, 1.2f, 3.4f); // 로터까지 포함(보이는 것 = 판정)
            Visual(PrimitiveType.Cube, "Body", drone.transform, Vector3.zero, new Vector3(2.6f, 0.8f, 2.6f), m.Drone);
            for (int i = 0; i < 4; i++)
            {
                var local = new Vector3(i < 2 ? -1.2f : 1.2f, 0.45f, i % 2 == 0 ? -1.2f : 1.2f);
                Visual(PrimitiveType.Cylinder, $"Rotor{i}", drone.transform, local, new Vector3(1.1f, 0.04f, 1.1f), m.Hazard);
            }
            Visual(PrimitiveType.Sphere, "StatusLight", drone.transform, new Vector3(0f, -0.5f, 1.2f), Vector3.one * 0.5f, m.Beacon);
            Rigidbody rb = drone.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            drone.AddComponent<KinematicMover>();
            return drone;
        }

        /// <summary>광고판 프레임: 원점 = 바닥 중앙, +Z 통과. 통과 구역 약 44 × 42m(아래 보 31m ~ 패널 73m).</summary>
        private static GameObject BuildBillboard(TowerglideMaterials m)
        {
            var bb = new GameObject("BillboardFrame");
            bb.AddComponent<ObstacleMarker>().Configure(ObstacleType.BillboardFrame);
            float half = StreetSpan * 0.5f;
            Child(PrimitiveType.Cube, "PostL", bb.transform, new Vector3(-half, 45f, 0f), new Vector3(2f, 90f, 2f), m.Steel);
            Child(PrimitiveType.Cube, "PostR", bb.transform, new Vector3(half, 45f, 0f), new Vector3(2f, 90f, 2f), m.Steel);
            Child(PrimitiveType.Cube, "Panel", bb.transform, new Vector3(0f, 80f, 0f), new Vector3(StreetSpan + 2f, 14f, 1.2f), m.DarkFacade);
            Child(PrimitiveType.Cube, "LowerBeam", bb.transform, new Vector3(0f, 30f, 0f), new Vector3(StreetSpan + 2f, 2f, 2f), m.Steel);
            return bb;
        }

        /// <summary>스카이브리지: 원점 = 데크 중심, 다리 길이 = Z축 70m.</summary>
        private static GameObject BuildSkybridge(TowerglideMaterials m)
        {
            var sb = new GameObject("Skybridge");
            sb.AddComponent<ObstacleMarker>().Configure(ObstacleType.Skybridge);
            Child(PrimitiveType.Cube, "Deck", sb.transform, Vector3.zero, new Vector3(12f, 8f, 70f), m.GlassFacade);
            Visual(PrimitiveType.Cube, "Stripe", sb.transform, new Vector3(0f, -4.2f, 0f), new Vector3(12.2f, 0.6f, 70.2f), m.Hazard);
            return sb;
        }

        /// <summary>케이블 존: 원점 = 케이블 중앙, 스팬 = X축. 경고 구슬 6m 간격, 판정 = 구슬 크기(D-023).</summary>
        private static GameObject BuildCable(TowerglideMaterials m)
        {
            var cable = new GameObject("CableZone");
            cable.AddComponent<ObstacleMarker>().Configure(ObstacleType.CableZone);
            GameObject line = Visual(PrimitiveType.Cylinder, "Cable", cable.transform, Vector3.zero, new Vector3(0.5f, StreetSpan * 0.5f, 0.5f), m.Drone,
                new Vector3(0f, 0f, 90f));
            CapsuleCollider col = line.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height = 2f;
            col.radius = 2.2f; // 로컬 → 월드 1.1m
            for (float x = -StreetSpan * 0.5f; x <= StreetSpan * 0.5f + 0.01f; x += 6f)
                Visual(PrimitiveType.Sphere, "Marker", cable.transform, new Vector3(x, 0f, 0f), Vector3.one * 2.2f, m.Hazard);
            return cable;
        }

        /// <summary>안테나 어레이: 원점 = 옥상 바닥.</summary>
        private static GameObject BuildAntenna(TowerglideMaterials m)
        {
            var array = new GameObject("AntennaArray");
            array.AddComponent<ObstacleMarker>().Configure(ObstacleType.AntennaArray);
            for (int i = 0; i < 3; i++)
            {
                float hh = 18f + i * 9f;
                var p = new Vector3((i - 1) * 6f, hh * 0.5f, (i % 2) * 5f);
                Child(PrimitiveType.Cylinder, $"Mast_{i}", array.transform, p, new Vector3(0.9f, hh * 0.5f, 0.9f), m.Metal);
                Visual(PrimitiveType.Sphere, $"WarningLight_{i}", array.transform, p + Vector3.up * hh * 0.5f, Vector3.one * 1.6f, m.Hazard);
            }
            return array;
        }

        /// <summary>화물 기구(8번째 유형, 명세 2.5-8): 천천히 오르내리는 풍선 + 매달린 화물. 경로는 인스턴스에서 지정.</summary>
        private static GameObject BuildCargoBalloon(TowerglideMaterials m)
        {
            var go = new GameObject("CargoBalloon");
            go.AddComponent<ObstacleMarker>().Configure(ObstacleType.MovingCargo);
            Child(PrimitiveType.Sphere, "Envelope", go.transform, new Vector3(0f, 6f, 0f), new Vector3(9f, 11f, 9f), m.Balloon);
            Child(PrimitiveType.Cube, "Rope", go.transform, new Vector3(0f, -2f, 0f), new Vector3(0.3f, 6f, 0.3f), m.Drone);
            Child(PrimitiveType.Cube, "Cargo", go.transform, new Vector3(0f, -6f, 0f), new Vector3(4f, 3f, 4f), m.Steel);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            go.AddComponent<KinematicMover>().Configure(new[] { Vector3.zero, new Vector3(0f, 40f, 0f) }, 3f, Vector3.up, 4f);
            return go;
        }

        private static GameObject Child(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ VFX

        private enum FxKind
        {
            Crash,
            Ring,
            NearMiss,
        }

        private static GameObject BuildFx(TowerglideMaterials m, FxKind kind)
        {
            var go = new GameObject(kind == FxKind.Crash ? "FX_Crash" : kind == FxKind.Ring ? "FX_RingPass" : "FX_NearMiss");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Callback;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m.Particle;

            var fade = new Gradient();
            switch (kind)
            {
                case FxKind.Crash: // 짧은 불꽃 + 파편(0.7초 이내, 과하지 않게)
                    main.duration = 0.5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 22f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.4f));
                    main.gravityModifier = 1f;
                    main.maxParticles = 50;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 40f;
                    shape.radius = 0.4f;
                    fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                    break;

                case FxKind.Ring: // 링 테두리에서 퍼지는 고리
                    main.duration = 0.4f;
                    main.startLifetime = 0.5f;
                    main.startSpeed = 3f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                    main.startColor = new Color(1f, 0.7f, 0.2f);
                    main.gravityModifier = 0f;
                    main.maxParticles = 40;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = RingInnerRadius + 0.9f;
                    shape.radiusThickness = 0f;
                    fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                    break;

                default: // 근접 회피: 기체 뒤로 짧은 흰 줄무늬
                    main.duration = 0.25f;
                    main.startLifetime = 0.25f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 18f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                    main.startColor = new Color(0.85f, 0.95f, 1f, 0.8f);
                    main.gravityModifier = 0f;
                    main.maxParticles = 16;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 12f;
                    shape.radius = 3f;
                    shape.rotation = new Vector3(0f, 180f, 0f); // 뒤쪽으로
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.velocityScale = 0.08f;
                    r.lengthScale = 2f;
                    fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                    break;
            }

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);
            return go;
        }
    }
}
