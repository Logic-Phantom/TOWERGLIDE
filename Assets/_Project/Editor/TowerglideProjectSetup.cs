using TowerGlide.App;
using TowerGlide.Flight;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerGlide.EditorTools
{
    /// <summary>
    /// TOWERGLIDE ▸ 1. 프로젝트 설정 적용 — 여러 번 실행해도 결과가 같다(멱등).
    /// Player(Android·iOS 공통 식별자/버전/가로 화면), 입력 시스템, URP 3단계 품질, 비행 튜닝 에셋.
    /// </summary>
    public static class TowerglideProjectSetup
    {
        private static readonly (string name, float renderScale, int msaa, float shadowDistance, int cascades, bool hdr)[] Tiers =
        {
            ("Performance", 0.75f, 1, 120f, 1, false),
            ("Balanced", 0.9f, 2, 220f, 2, false),
            ("Quality", 1.0f, 4, 350f, 2, true),
        };

        [MenuItem("TOWERGLIDE/1. 프로젝트 설정 적용", priority = 1)]
        public static void ApplyAll()
        {
            EnsureFolders();
            ApplyPlayerSettings();
            ApplyInputHandling();
            EnsureRenderPipelines();
            EnsureTuningAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TOWERGLIDE] 프로젝트 설정 적용 완료. 입력 처리 방식이 바뀌었다면 Unity가 재시작을 요청합니다.");
        }

        public static void EnsureFolders()
        {
            string[] folders =
            {
                "Art/Aircraft", "Art/Buildings", "Art/Obstacles", "Art/Materials", "Art/Textures", "Art/VFX", "Art/Audio",
                "Prefabs/Aircraft", "Prefabs/City", "Prefabs/Obstacles", "Prefabs/UI", "Prefabs/VFX",
                "Scenes", "Settings/Rendering",
            };
            foreach (string f in folders) TowerglideEditorUtil.EnsureFolder($"{TowerglideEditorUtil.Root}/{f}");
            TowerglideEditorUtil.EnsureFolder("Assets/ThirdParty");
        }

        public static void ApplyPlayerSettings()
        {
            TowerglideEditorUtil.VersionInfo v = TowerglideEditorUtil.ReadVersion();

            PlayerSettings.companyName = TowerglideEditorUtil.CompanyName;
            PlayerSettings.productName = TowerglideEditorUtil.ProductName;
            PlayerSettings.bundleVersion = v.version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, TowerglideEditorUtil.ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, TowerglideEditorUtil.ApplicationId);

            // 가로 화면 고정(양방향 허용)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Unity 6 Personal은 스플래시 로고를 끌 수 있다(출시 전 현재 약관 재확인)
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // ---- Android (Google Play) ----
            PlayerSettings.Android.bundleVersionCode = v.build;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;    // Android 8.0 (D-005)
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36; // Play 신규 앱 요건(제출 직전 재확인)
            PlayerSettings.Android.forceInternetPermission = false;           // 오프라인 게임
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.optimizedFramePacing = true;

            // ---- iOS (App Store) ----
            PlayerSettings.iOS.buildNumber = v.build.ToString();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true; // 가로 전용 게임: iPad 멀티태스킹 제외
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All; // 가장자리 스와이프 오작동 방지
        }

        /// <summary>Active Input Handling = Input System Package (New). 0 = Old, 1 = New, 2 = Both.</summary>
        public static void ApplyInputHandling()
        {
            Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (playerSettings == null) return;
            var so = new SerializedObject(playerSettings);
            SerializedProperty p = so.FindProperty("activeInputHandler");
            if (p == null)
            {
                Debug.LogWarning("[TOWERGLIDE] activeInputHandler 속성을 찾지 못했습니다. Project Settings ▸ Player ▸ Active Input Handling을 'Input System Package (New)'로 직접 설정하세요.");
                return;
            }
            if (p.intValue != 1)
            {
                p.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[TOWERGLIDE] Active Input Handling → Input System Package (New). 에디터 재시작 필요.");
            }
        }

        public static void EnsureRenderPipelines()
        {
            TowerglideEditorUtil.EnsureFolder(TowerglideEditorUtil.RenderingDir);
            string rendererPath = $"{TowerglideEditorUtil.RenderingDir}/URP_Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            var assets = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                var t = Tiers[i];
                string path = $"{TowerglideEditorUtil.RenderingDir}/URP_{t.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    asset = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.renderScale = t.renderScale;
                asset.msaaSampleCount = t.msaa;
                asset.shadowDistance = t.shadowDistance;
                asset.shadowCascadeCount = t.cascades;
                asset.supportsHDR = t.hdr;
                EditorUtility.SetDirty(asset);
                assets[i] = asset;
            }

            GraphicsSettings.defaultRenderPipeline = assets[1];
            ConfigureQualityLevels(assets);
        }

        /// <summary>Quality Level을 Performance/Balanced/Quality 3개로 맞추고 각 URP 에셋을 연결한다.</summary>
        private static void ConfigureQualityLevels(UniversalRenderPipelineAsset[] assets)
        {
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (objs == null || objs.Length == 0)
            {
                Debug.LogWarning("[TOWERGLIDE] QualitySettings.asset을 찾지 못했습니다. 품질 단계는 수동 설정하세요(docs/PERFORMANCE.md).");
                return;
            }
            var so = new SerializedObject(objs[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                Debug.LogWarning("[TOWERGLIDE] m_QualitySettings 속성을 찾지 못했습니다. 품질 단계는 수동 설정하세요.");
                return;
            }
            levels.arraySize = Tiers.Length;
            for (int i = 0; i < Tiers.Length; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = QualityPresetApplier.LevelNames[i];
                SerializedProperty rp = level.FindPropertyRelative("customRenderPipeline");
                if (rp != null) rp.objectReferenceValue = assets[i];
                SerializedProperty vsync = level.FindPropertyRelative("vSyncCount");
                if (vsync != null) vsync.intValue = 0; // 프레임 제한은 Application.targetFrameRate로
            }
            SerializedProperty current = so.FindProperty("m_CurrentQuality");
            if (current != null) current.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static AircraftFlightTuning EnsureTuningAsset()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<AircraftFlightTuning>(TowerglideEditorUtil.TuningPath);
            if (tuning != null) return tuning;
            TowerglideEditorUtil.EnsureFolder(TowerglideEditorUtil.SettingsDir);
            tuning = ScriptableObject.CreateInstance<AircraftFlightTuning>();
            AssetDatabase.CreateAsset(tuning, TowerglideEditorUtil.TuningPath);
            return tuning;
        }
    }
}
