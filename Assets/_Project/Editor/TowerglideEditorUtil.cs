using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerGlide.EditorTools
{
    /// <summary>에디터 자동화 공통 도우미: 경로, 버전 파일, 머티리얼, 직렬화 필드 설정.</summary>
    internal static class TowerglideEditorUtil
    {
        public const string Root = "Assets/_Project";
        public const string ScenesDir = Root + "/Scenes";
        public const string SettingsDir = Root + "/Settings";
        public const string RenderingDir = SettingsDir + "/Rendering";
        public const string MaterialsDir = Root + "/Art/Materials";
        public const string TuningPath = SettingsDir + "/AircraftFlightTuning.asset";
        public const string VersionFile = "build/version.json";
        /// <summary>UI 폰트: Pretendard v1.3.9(SIL OFL 1.1) — docs/THIRD_PARTY_ASSETS.md.</summary>
        public const string FontRegularPath = Root + "/Art/Fonts/Pretendard-Regular.otf";
        public const string FontBoldPath = Root + "/Art/Fonts/Pretendard-Bold.otf";

        public const string CompanyName = "Logic Phantom";
        public const string ProductName = "TOWERGLIDE";
        /// <summary>한국어 기기의 홈 화면 이름(D-003) — Android values-ko / iOS ko.lproj 후처리에서 사용.</summary>
        public const string KoreanAppName = "타워글라이드";
        /// <summary>스토어 등록 후에는 바꿀 수 없다. 첫 업로드 전 확정(D-004).</summary>
        public const string ApplicationId = "com.logicphantom.towerglide";

        [System.Serializable]
        public sealed class VersionInfo
        {
            public string version = "0.1.0";
            public int build = 1;
        }

        public static VersionInfo ReadVersion()
        {
            try
            {
                if (File.Exists(VersionFile)) return JsonUtility.FromJson<VersionInfo>(File.ReadAllText(VersionFile)) ?? new VersionInfo();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TOWERGLIDE] {VersionFile} 읽기 실패: {e.Message}");
            }
            return new VersionInfo();
        }

        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        public static Material GetOrCreateLit(string name, Color color, float smoothness = 0.3f, float metallic = 0f, Color? emission = null)
        {
            EnsureFolder(MaterialsDir);
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.enableInstancing = true; // 건물·장애물 반복 → GPU Instancing (명세 10.2)
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[TOWERGLIDE] {target.GetType().Name}.{field} 필드를 찾지 못했습니다."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetRefs(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray) { Debug.LogError($"[TOWERGLIDE] {target.GetType().Name}.{field} 배열 필드를 찾지 못했습니다."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[TOWERGLIDE] {target.GetType().Name}.{field} 필드를 찾지 못했습니다."); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector3(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[TOWERGLIDE] {target.GetType().Name}.{field} 필드를 찾지 못했습니다."); return; }
            p.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>렌더링 전용 자식(콜라이더 제거)으로 기본 도형을 만든다.</summary>
        public static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, Vector3? localEuler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            go.transform.localScale = localScale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>콜라이더가 있는 기본 도형(건물·장애물).</summary>
        public static GameObject Solid(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Vector3 scale,
            Material mat, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
