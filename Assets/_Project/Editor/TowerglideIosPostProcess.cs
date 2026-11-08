#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// iOS Xcode 프로젝트 후처리(iOS Build Support 모듈이 있고 빌드 대상이 iOS일 때만 컴파일됨).
    /// - ITSAppUsesNonExemptEncryption = NO: 암호화 기능 없음(HTTPS 통신도 없음) → App Store 수출 규정 질문 자동 처리.
    /// - 한국어 홈 화면 이름 "타워글라이드": ko.lproj/InfoPlist.strings(CFBundleDisplayName), 기본은 "TOWERGLIDE".
    /// - CFBundleLocalizations: ko, en.
    /// </summary>
    public static class TowerglideIosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict rootDict = plist.root;
            rootDict.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            rootDict.SetString("CFBundleDevelopmentRegion", "ko");
            PlistElementArray locs = rootDict.CreateArray("CFBundleLocalizations");
            locs.AddString("ko");
            locs.AddString("en");
            plist.WriteToFile(plistPath);

            string koDir = Path.Combine(pathToBuiltProject, "ko.lproj");
            Directory.CreateDirectory(koDir);
            File.WriteAllText(Path.Combine(koDir, "InfoPlist.strings"),
                $"\"CFBundleDisplayName\" = \"{TowerglideEditorUtil.KoreanAppName}\";\n\"CFBundleName\" = \"{TowerglideEditorUtil.KoreanAppName}\";\n",
                new System.Text.UTF8Encoding(false));

            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string mainTarget = proj.GetUnityMainTargetGuid();
            string fileGuid = proj.AddFolderReference(koDir, "ko.lproj");
            proj.AddFileToBuild(mainTarget, fileGuid);
            proj.WriteToFile(projPath);
        }
    }
}
#endif
