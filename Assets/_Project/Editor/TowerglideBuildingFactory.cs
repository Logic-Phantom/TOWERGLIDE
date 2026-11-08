using System.Collections.Generic;
using TowerGlide.Core.World;
using TowerGlide.Gameplay;
using TowerGlide.World;
using UnityEditor;
using UnityEngine;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// 건물 아키타입 12종(BuildingCatalog)을 블록 크기·높이에 맞춰 조립하는 모듈(D-024).
    /// - 크기가 블록마다 달라 고정 프리팹 대신 "매개변수 모듈"로 만든다(늘린 프리팹은 옥상 설비가 찌그러짐).
    /// - 외벽은 미터 UV 메시 → 창문 간격이 모든 건물에서 같다.
    /// - 콜라이더는 큰 덩어리마다 BoxCollider 하나(명세 2.4: 장식 MeshCollider 금지).
    /// - LODGroup: LOD0 = 전체, LOD1 = 본체만, 그 아래 컬링(명세 3.2, 10.2).
    /// - 높이는 lot.Height를 넘지 않는다(코스 여유 보장, D-022). 옥상 설비는 +6m 이내.
    /// </summary>
    internal static class TowerglideBuildingFactory
    {
        private sealed class Ctx
        {
            public Transform Root;
            public TowerglideMaterials M;
            public readonly List<Renderer> Body = new List<Renderer>();
            public readonly List<Renderer> Detail = new List<Renderer>();
        }

        public static GameObject Build(LotPlacement lot, TowerglideMaterials m, Transform parent)
        {
            var go = new GameObject($"B{lot.Ix:00}_{lot.Iz:00}_{lot.Archetype}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(lot.X, 0f, lot.Z);
            go.AddComponent<ObstacleMarker>().Configure(ObstacleType.Building);
            var ctx = new Ctx { Root = go.transform, M = m };
            var rng = new System.Random(lot.DetailSeed);

            float w = lot.Width, d = lot.Depth, h = lot.Height;
            Material facade = PickFacade(lot.Archetype, rng, m);

            switch (lot.Archetype)
            {
                case BuildingArchetype.GlassTower:
                    Box(ctx, "Body", 0f, 0f, 0f, w, h, d, m.GlassFacade);
                    RoofUnits(ctx, h, w, d, rng, 2);
                    break;

                case BuildingArchetype.ConcreteOffice:
                    Box(ctx, "Body", 0f, 0f, 0f, w, h, d, m.ConcreteFacade);
                    RoofUnits(ctx, h, w, d, rng, 3);
                    break;

                case BuildingArchetype.SetbackTower:
                {
                    float h1 = h * 0.5f, h2 = h * 0.3f, h3 = h - h1 - h2;
                    Box(ctx, "Tier1", 0f, 0f, 0f, w, h1, d, facade);
                    Box(ctx, "Tier2", 0f, h1, 0f, w * 0.75f, h2, d * 0.75f, facade);
                    Box(ctx, "Tier3", 0f, h1 + h2, 0f, w * 0.5f, h3, d * 0.5f, facade);
                    break;
                }

                case BuildingArchetype.TwinTower:
                {
                    float podium = Mathf.Min(24f, h * 0.15f);
                    Box(ctx, "Podium", 0f, 0f, 0f, w, podium, d, m.ConcreteFacade);
                    float tw = w * 0.4f, offset = w * 0.28f;
                    float hB = h - podium;
                    float hA = hB * (0.8f + 0.2f * (float)rng.NextDouble());
                    Box(ctx, "TowerA", -offset, podium, 0f, tw, hA, d * 0.8f, facade);
                    Box(ctx, "TowerB", offset, podium, 0f, tw, hB, d * 0.8f, facade);
                    break;
                }

                case BuildingArchetype.SlimNeedle:
                {
                    float bodyH = h * 0.86f;
                    Box(ctx, "Body", 0f, 0f, 0f, w * 0.6f, bodyH, d * 0.6f, m.GlassFacade);
                    Box(ctx, "Crown", 0f, bodyH, 0f, w * 0.35f, h * 0.06f, d * 0.35f, m.DarkFacade);
                    Box(ctx, "Spire", 0f, bodyH + h * 0.06f, 0f, 1.6f, h * 0.08f, 1.6f, m.Metal, detail: false);
                    WarningLight(ctx, new Vector3(0f, h, 0f));
                    break;
                }

                case BuildingArchetype.LowCommercial:
                    Box(ctx, "Body", 0f, 0f, 0f, w, h, d, m.ConcreteFacade);
                    RoofUnits(ctx, h, w, d, rng, 4);
                    break;

                case BuildingArchetype.PodiumTower:
                {
                    float podium = Mathf.Min(25f, h * 0.2f);
                    Box(ctx, "Podium", 0f, 0f, 0f, w, podium, d, m.ConcreteFacade);
                    float ox = (float)(rng.NextDouble() - 0.5) * w * 0.25f;
                    Box(ctx, "Tower", ox, podium, 0f, w * 0.6f, h - podium, d * 0.6f, facade);
                    break;
                }

                case BuildingArchetype.HelipadRoof:
                {
                    Box(ctx, "Body", 0f, 0f, 0f, w, h - 1f, d, facade);
                    float r = Mathf.Min(w, d) * 0.7f;
                    Cylinder(ctx, "Helipad", new Vector3(0f, h - 1f, 0f), r, 1f, m.Helipad);
                    break;
                }

                case BuildingArchetype.WaterTankRoof:
                    Box(ctx, "Body", 0f, 0f, 0f, w, h, d, m.ConcreteFacade);
                    Cylinder(ctx, "WaterTank", new Vector3(w * 0.2f, h, d * 0.15f), 6f, 6f, m.Steel);
                    RoofUnits(ctx, h, w, d, rng, 1);
                    break;

                case BuildingArchetype.DarkGlassSlab:
                    Box(ctx, "Slab", 0f, 0f, 0f, w, h, d * 0.55f, m.DarkFacade);
                    Box(ctx, "Fin", w * 0.5f - 1f, 0f, 0f, 2f, h, d * 0.7f, m.Metal);
                    break;

                case BuildingArchetype.CrownTop:
                {
                    float bodyH = h * 0.9f, crownH = h - bodyH;
                    Box(ctx, "Body", 0f, 0f, 0f, w, bodyH, d, m.GlassFacade);
                    // 열린 왕관 프레임: 모서리 기둥 4 + 윗 테두리
                    float px = w * 0.5f - 1f, pz = d * 0.5f - 1f;
                    for (int i = 0; i < 4; i++)
                        Box(ctx, $"CrownPost{i}", i < 2 ? -px : px, bodyH, i % 2 == 0 ? -pz : pz, 2f, crownH, 2f, m.Metal);
                    Box(ctx, "CrownRingN", 0f, h - 2f, pz, w, 2f, 2f, m.Metal);
                    Box(ctx, "CrownRingS", 0f, h - 2f, -pz, w, 2f, 2f, m.Metal);
                    WarningLight(ctx, new Vector3(px, h, pz));
                    break;
                }

                default: // SteppedLowRise
                {
                    float h1 = h * 0.5f, h2 = h * 0.3f, h3 = h - h1 - h2;
                    Box(ctx, "Step1", 0f, 0f, 0f, w, h1, d, m.ConcreteFacade);
                    Box(ctx, "Step2", 0f, h1, -d * 0.12f, w * 0.75f, h2, d * 0.75f, m.ConcreteFacade);
                    Box(ctx, "Step3", 0f, h1 + h2, -d * 0.25f, w * 0.5f, h3, d * 0.5f, m.ConcreteFacade);
                    break;
                }
            }

            if (h > 150f && lot.Archetype != BuildingArchetype.SlimNeedle && lot.Archetype != BuildingArchetype.CrownTop)
                WarningLight(ctx, new Vector3(w * 0.4f, h, d * 0.4f));

            SetupLod(go, ctx);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        private static Material PickFacade(BuildingArchetype a, System.Random rng, TowerglideMaterials m)
        {
            int r = rng.Next(3);
            return r == 0 ? m.GlassFacade : r == 1 ? m.DarkFacade : m.ConcreteFacade;
        }

        /// <summary>바닥 중앙 기준 박스(미터 UV). y0 = 바닥 높이.</summary>
        private static void Box(Ctx ctx, string name, float x, float y0, float z, float w, float h, float d, Material facade, bool detail = false)
        {
            if (h <= 0.1f || w <= 0.1f || d <= 0.1f) return;
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Root, false);
            go.transform.localPosition = new Vector3(x, y0, z);
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.CreateMetricBox(new Vector3(w, h, d));
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { facade, ctx.M.Roof };
            BoxCollider c = go.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, h * 0.5f, 0f);
            c.size = new Vector3(w, h, d);
            (detail ? ctx.Detail : ctx.Body).Add(r);
        }

        private static void Cylinder(Ctx ctx, string name, Vector3 bottomCenter, float radius, float height, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(ctx.Root, false);
            go.transform.localPosition = bottomCenter + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            // 원기둥 프리미티브의 캡슐 콜라이더는 납작한 원판에서 구처럼 부풀어 오르므로 박스로 대체
            BoxCollider c = go.AddComponent<BoxCollider>();
            c.size = new Vector3(1f, 2f, 1f);
            ctx.Detail.Add(go.GetComponent<Renderer>());
        }

        private static void RoofUnits(Ctx ctx, float h, float w, float d, System.Random rng, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float uw = 3f + (float)rng.NextDouble() * 5f;
                float ud = 3f + (float)rng.NextDouble() * 5f;
                float uh = 2f + (float)rng.NextDouble() * 3f;
                float x = ((float)rng.NextDouble() - 0.5f) * (w - uw - 2f);
                float z = ((float)rng.NextDouble() - 0.5f) * (d - ud - 2f);
                Box(ctx, $"RoofUnit{i}", x, h, z, uw, uh, ud, ctx.M.Roof, detail: true);
            }
        }

        private static void WarningLight(Ctx ctx, Vector3 localPos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WarningLight";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(ctx.Root, false);
            go.transform.localPosition = localPos + Vector3.up * 0.8f;
            go.transform.localScale = Vector3.one * 1.6f;
            go.GetComponent<Renderer>().sharedMaterial = ctx.M.Hazard;
            ctx.Detail.Add(go.GetComponent<Renderer>());
        }

        private static void SetupLod(GameObject go, Ctx ctx)
        {
            LODGroup lod = go.AddComponent<LODGroup>();
            var all = new List<Renderer>(ctx.Body);
            all.AddRange(ctx.Detail);
            lod.SetLODs(new[]
            {
                new LOD(0.25f, all.ToArray()),
                new LOD(0.015f, ctx.Body.ToArray()),
            });
            lod.RecalculateBounds();
        }
    }
}
