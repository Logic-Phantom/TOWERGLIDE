using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// 빌드 메뉴 + 배치 모드 진입점. 버전은 build/version.json 하나로 Android·iOS를 함께 관리한다.
    ///
    /// 배치 모드 예(Windows):
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TowerGlide.EditorTools.TowerglideBuild.BuildAndroidAab -logFile -
    ///
    /// 업로드 키(Play App Signing의 upload key)는 저장소에 넣지 않고 환경 변수로만 전달한다:
    ///   TOWERGLIDE_KEYSTORE_PATH, TOWERGLIDE_KEYSTORE_PASS, TOWERGLIDE_KEY_ALIAS, TOWERGLIDE_KEY_ALIAS_PASS
    /// </summary>
    public static class TowerglideBuild
    {
        private const string OutputRoot = "Builds";
        private const int MinTargetSdk = 36;

        [MenuItem("TOWERGLIDE/Build/설정 검증", priority = 100)]
        public static void ValidateMenu()
        {
            List<string> problems = Validate(BuildTarget.Android, release: true);
            problems.AddRange(Validate(BuildTarget.iOS, release: true).Where(p => !problems.Contains(p)));
            if (problems.Count == 0) Debug.Log("[TOWERGLIDE] 빌드 설정 검증 통과");
            else Debug.LogWarning("[TOWERGLIDE] 빌드 설정 문제:\n- " + string.Join("\n- ", problems));
        }

        [MenuItem("TOWERGLIDE/Build/Android AAB (Google Play 릴리스)", priority = 110)]
        public static void BuildAndroidAab() => BuildAndroid(release: true);

        [MenuItem("TOWERGLIDE/Build/Android APK (개발·실기기 테스트)", priority = 111)]
        public static void BuildAndroidDevApk() => BuildAndroid(release: false);

        [MenuItem("TOWERGLIDE/Build/iOS Xcode 프로젝트 (macOS에서 Archive)", priority = 120)]
        public static void BuildIosXcode()
        {
            TowerglideProjectSetup.ApplyPlayerSettings();
            if (!Check(Validate(BuildTarget.iOS, release: true))) return;
            TowerglideEditorUtil.VersionInfo v = TowerglideEditorUtil.ReadVersion();
            string output = Path.Combine(OutputRoot, "iOS", $"TOWERGLIDE-{v.version}-{v.build}");
            Run(BuildTarget.iOS, output, BuildOptions.None);
        }

        private static void BuildAndroid(bool release)
        {
            TowerglideProjectSetup.ApplyPlayerSettings();
            if (!Check(Validate(BuildTarget.Android, release))) return;

            TowerglideEditorUtil.VersionInfo v = TowerglideEditorUtil.ReadVersion();
            EditorUserBuildSettings.buildAppBundle = release;
            string ext = release ? "aab" : "apk";
            string output = Path.Combine(OutputRoot, "Android", $"TOWERGLIDE-{v.version}-{v.build}{(release ? string.Empty : "-dev")}.{ext}");

            bool usedKeystore = false;
            try
            {
                if (release)
                {
                    if (!ApplyUploadKeystore())
                    {
                        Fail("릴리스 AAB에는 업로드 키가 필요합니다. 환경 변수 TOWERGLIDE_KEYSTORE_PATH/PASS, TOWERGLIDE_KEY_ALIAS/_PASS를 설정하세요(docs/RELEASE_CHECKLIST.md).");
                        return;
                    }
                    usedKeystore = true;
                }
                else
                {
                    PlayerSettings.Android.useCustomKeystore = false; // 디버그 키(개인 테스트용, 스토어 업로드 불가)
                }

                BuildOptions options = release ? BuildOptions.None : BuildOptions.Development;
                Run(BuildTarget.Android, output, options);
            }
            finally
            {
                if (usedKeystore)
                {
                    // 비밀번호를 프로젝트 설정에 남기지 않는다
                    PlayerSettings.Android.keystorePass = string.Empty;
                    PlayerSettings.Android.keyaliasPass = string.Empty;
                }
            }
        }

        private static bool ApplyUploadKeystore()
        {
            string path = Environment.GetEnvironmentVariable("TOWERGLIDE_KEYSTORE_PATH");
            string pass = Environment.GetEnvironmentVariable("TOWERGLIDE_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("TOWERGLIDE_KEY_ALIAS");
            string aliasPass = Environment.GetEnvironmentVariable("TOWERGLIDE_KEY_ALIAS_PASS");
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(alias)) return false;

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = path;
            PlayerSettings.Android.keystorePass = pass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(aliasPass) ? pass : aliasPass;
            return true;
        }

        public static List<string> Validate(BuildTarget target, bool release)
        {
            var problems = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            if (scenes.Length == 0) problems.Add("Build Settings에 씬이 없습니다 — TOWERGLIDE ▸ 2. 씬 생성 실행");
            foreach (EditorBuildSettingsScene s in scenes)
                if (!File.Exists(s.path)) problems.Add($"씬 파일 없음: {s.path}");

            if (target == BuildTarget.Android)
            {
                if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                    problems.Add("Android 스크립팅 백엔드가 IL2CPP가 아닙니다");
                if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                    problems.Add("ARM64가 빠져 있습니다(Play 64비트 요건)");
                int targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
                if (targetSdk != 0 && targetSdk < MinTargetSdk)
                    problems.Add($"targetSdk {targetSdk} < {MinTargetSdk} (Play 신규 앱 요건 — 제출 직전 공식 정책 재확인)");
                if (PlayerSettings.Android.forceInternetPermission)
                    problems.Add("INTERNET 권한 강제가 켜져 있습니다(오프라인 게임 — 불필요한 권한 금지)");
            }
            if (PlayerSettings.GetApplicationIdentifier(target == BuildTarget.iOS ? NamedBuildTarget.iOS : NamedBuildTarget.Android) != TowerglideEditorUtil.ApplicationId)
                problems.Add($"applicationIdentifier가 {TowerglideEditorUtil.ApplicationId}가 아닙니다");
            if (release && EditorUserBuildSettings.development)
                problems.Add("Development Build가 켜져 있습니다(릴리스 금지)");
            return problems;
        }

        private static bool Check(List<string> problems)
        {
            if (problems.Count == 0) return true;
            Fail("빌드 중단 — 설정 문제:\n- " + string.Join("\n- ", problems));
            return false;
        }

        private static void Run(BuildTarget target, string output, BuildOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? OutputRoot);
            var bpo = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = options,
            };
            BuildReport report = BuildPipeline.BuildPlayer(bpo);
            BuildSummary sum = report.summary;
            if (sum.result == BuildResult.Succeeded)
            {
                Debug.Log($"[TOWERGLIDE] 빌드 성공: {sum.outputPath} ({sum.totalSize / (1024f * 1024f):F1} MB, {sum.totalTime.TotalSeconds:F0}s)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Fail($"빌드 실패: {sum.result}, 오류 {sum.totalErrors}개");
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[TOWERGLIDE] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
