using System.IO;
using UnityEditor;
using UnityEngine;
using static TowerGlide.EditorTools.TowerglideEditorUtil;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// 프로젝트 공유 머티리얼·절차 텍스처(창문 패턴). 모두 직접 생성 — 외부 에셋 없음(docs/THIRD_PARTY_ASSETS.md).
    /// 머티리얼 공유 + GPU Instancing → 드로우콜 관리(명세 3.2, 10.2).
    /// </summary>
    internal sealed class TowerglideMaterials
    {
        public const string TexturesDir = Root + "/Art/Textures";

        public Material Ground, GlassFacade, ConcreteFacade, DarkFacade, Roof, AircraftBody, AircraftAccent, Canopy,
            Ring, Beacon, Hazard, Steel, Drone, Metal, Helipad, Balloon, Particle;

        public static TowerglideMaterials CreateAll()
        {
            Texture2D glassTex = WindowTexture("T_Window_Glass", WindowStyle.CurtainWall);
            Texture2D concreteTex = WindowTexture("T_Window_Concrete", WindowStyle.PunchedWindows);
            Texture2D darkTex = WindowTexture("T_Window_Ribbon", WindowStyle.Ribbon);

            return new TowerglideMaterials
            {
                Ground = GetOrCreateLit("M_Ground", new Color(0.33f, 0.36f, 0.38f), 0.1f),
                GlassFacade = Textured(GetOrCreateLit("M_Facade_Glass", new Color(0.62f, 0.76f, 0.9f), 0.85f, 0.35f), glassTex),
                ConcreteFacade = Textured(GetOrCreateLit("M_Facade_Concrete", new Color(0.86f, 0.84f, 0.8f), 0.25f), concreteTex),
                DarkFacade = Textured(GetOrCreateLit("M_Facade_Dark", new Color(0.45f, 0.5f, 0.6f), 0.7f, 0.4f), darkTex),
                Roof = GetOrCreateLit("M_Roof", new Color(0.45f, 0.46f, 0.48f), 0.15f),
                AircraftBody = GetOrCreateLit("M_Aircraft_White", new Color(0.95f, 0.95f, 0.96f), 0.7f, 0.2f),
                AircraftAccent = GetOrCreateLit("M_Aircraft_Red", new Color(0.86f, 0.12f, 0.12f), 0.7f, 0.2f),
                Canopy = GetOrCreateLit("M_Aircraft_Canopy", new Color(0.1f, 0.18f, 0.3f), 0.95f, 0.5f),
                Ring = GetOrCreateLit("M_Ring", new Color(1f, 0.62f, 0.1f), 0.5f, 0f, new Color(1f, 0.5f, 0.05f) * 1.5f),
                Beacon = GetOrCreateLit("M_Beacon", new Color(1f, 0.7f, 0.2f), 0f, 0f, new Color(1f, 0.55f, 0.1f) * 2.5f),
                Hazard = GetOrCreateLit("M_Hazard", new Color(1f, 0.35f, 0.08f), 0.4f, 0f, new Color(1f, 0.25f, 0.05f) * 0.8f),
                Steel = GetOrCreateLit("M_Steel_Yellow", new Color(0.95f, 0.75f, 0.12f), 0.35f, 0.5f),
                Drone = GetOrCreateLit("M_Drone", new Color(0.15f, 0.15f, 0.17f), 0.6f, 0.4f),
                Metal = GetOrCreateLit("M_Metal", new Color(0.8f, 0.82f, 0.85f), 0.4f, 0.6f),
                Helipad = GetOrCreateLit("M_Helipad", new Color(0.22f, 0.24f, 0.26f), 0.2f),
                Balloon = GetOrCreateLit("M_Balloon", new Color(0.95f, 0.95f, 0.98f), 0.5f, 0f, new Color(1f, 0.3f, 0.1f) * 0.3f),
                Particle = ParticleMaterial(),
            };
        }

        private static Material Textured(Material mat, Texture2D tex)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ParticleMaterial()
        {
            string path = $"{MaterialsDir}/M_Particle.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                mat = new Material(shader) { name = "M_Particle" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            // 반투명 알파 블렌드
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private enum WindowStyle
        {
            CurtainWall,
            PunchedWindows,
            Ribbon,
        }

        /// <summary>
        /// 창문 한 칸(가로 4m × 층고 3.6m) 회색조 패턴. 머티리얼 색으로 물들인다. 64px·mipmap·Repeat.
        /// </summary>
        private static Texture2D WindowTexture(string name, WindowStyle style)
        {
            EnsureFolder(TexturesDir);
            string path = $"{TexturesDir}/{name}.png";
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float c;
                switch (style)
                {
                    case WindowStyle.CurtainWall:
                        // 큰 유리, 얇은 멀리언, 아래쪽 층 슬래브 띠
                        bool mullion = u < 0.04f || u > 0.96f;
                        bool slab = v < 0.1f;
                        c = slab ? 0.55f : mullion ? 0.35f : 0.7f + 0.25f * v; // 위로 갈수록 하늘 반사
                        break;
                    case WindowStyle.PunchedWindows:
                        // 벽 안에 작은 창
                        bool window = u > 0.2f && u < 0.8f && v > 0.25f && v < 0.85f;
                        c = window ? 0.35f + 0.2f * v : 0.95f;
                        break;
                    default:
                        // 가로 띠 창
                        bool ribbon = v > 0.3f && v < 0.85f;
                        c = ribbon ? 0.45f + 0.35f * (v - 0.3f) : 0.8f;
                        break;
                }
                tex.SetPixel(x, y, new Color(c, c, c, 1f));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = 64;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
