#if UNITY_ANDROID
// Android Build Support 모듈이 있고 빌드 대상이 Android일 때만 컴파일(UnityEditor.Android는 모듈 전용 어셈블리)
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// Android Gradle 프로젝트 생성 직후 처리.
    /// 1) 한국어 기기 홈 화면 이름 "타워글라이드"(D-003): launcher 모듈 res/values-ko/strings.xml의 app_name.
    ///    기본(영문) 이름은 PlayerSettings.productName = "TOWERGLIDE".
    /// 2) 권한 점검(명세 6.3·13 "필요 없는 권한 금지"): 생성된 매니페스트의 uses-permission을 모아
    ///    Builds/Android/permissions-report.txt로 남기고, 허용 목록 밖 권한은 경고(릴리스 AAB는 오류 로그).
    ///    최종 병합 매니페스트(라이브러리 포함)는 빌드 후 AAB에서 다시 확인한다(docs/DEVICE_TEST_GUIDE.md).
    /// </summary>
    public sealed class TowerglideAndroidPostProcess : IPostGenerateGradleAndroidProject
    {
        /// <summary>오프라인 게임이 써도 되는 권한. VIBRATE = 햅틱(일반 권한, 런타임 동의 없음).</summary>
        private static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "android.permission.VIBRATE",
        };

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string root = Path.GetFullPath(Path.Combine(unityLibraryPath, ".."));
            string launcher = Path.Combine(root, "launcher");
            WriteKoreanAppName(Directory.Exists(launcher) ? launcher : unityLibraryPath);
            AuditPermissions(root);
        }

        private static void WriteKoreanAppName(string module)
        {
            string dir = Path.Combine(module, "src", "main", "res", "values-ko");
            Directory.CreateDirectory(dir);
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n" +
                         $"    <string name=\"app_name\">{TowerglideEditorUtil.KoreanAppName}</string>\n</resources>\n";
            File.WriteAllText(Path.Combine(dir, "strings.xml"), xml, new UTF8Encoding(false));
        }

        private static void AuditPermissions(string gradleRoot)
        {
            var found = new SortedDictionary<string, string>();
            foreach (string manifest in Directory.GetFiles(gradleRoot, "AndroidManifest.xml", SearchOption.AllDirectories))
            {
                if (manifest.Contains(Path.DirectorySeparatorChar + "build" + Path.DirectorySeparatorChar)) continue;
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(manifest);
                    foreach (XmlNode n in doc.GetElementsByTagName("uses-permission"))
                    {
                        string name = n.Attributes?["android:name"]?.Value;
                        if (!string.IsNullOrEmpty(name)) found[name] = manifest.Substring(gradleRoot.Length).TrimStart('\\', '/');
                    }
                }
                catch (XmlException e)
                {
                    Debug.LogWarning($"[TOWERGLIDE] 매니페스트 읽기 실패: {manifest} ({e.Message})");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"TOWERGLIDE Android 권한 점검 {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"applicationId: {PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android)}");
            var unexpected = new List<string>();
            foreach (KeyValuePair<string, string> kv in found)
            {
                bool ok = Allowed.Contains(kv.Key);
                if (!ok) unexpected.Add(kv.Key);
                sb.AppendLine($"{(ok ? "OK " : "?? ")} {kv.Key}  ({kv.Value})");
            }
            if (found.Count == 0) sb.AppendLine("(uses-permission 없음)");

            Directory.CreateDirectory(Path.Combine("Builds", "Android"));
            File.WriteAllText(Path.Combine("Builds", "Android", "permissions-report.txt"), sb.ToString(), new UTF8Encoding(false));

            if (unexpected.Count > 0)
            {
                string msg = "[TOWERGLIDE] 허용 목록 밖 권한: " + string.Join(", ", unexpected) +
                             " — 필요 없는 권한이면 원인(패키지·설정)을 제거하세요. Play 데이터 보안 답변과 맞아야 합니다.";
                if (EditorUserBuildSettings.buildAppBundle && !EditorUserBuildSettings.development) Debug.LogError(msg);
                else Debug.LogWarning(msg);
            }
            else
            {
                Debug.Log("[TOWERGLIDE] 권한 점검 통과 — Builds/Android/permissions-report.txt");
            }
        }
    }
}
#endif
