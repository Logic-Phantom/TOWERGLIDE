using System;
using TowerGlide.Audio;
using TowerGlide.Core.App;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;
using TowerGlide.Flight;
using TowerGlide.Platform;
using TowerGlide.Save;
using UnityEngine;

namespace TowerGlide.App
{
    /// <summary>
    /// 앱 수명 동안 유지되는 작은 컴포지션 루트(D-010). DI 프레임워크·범용 서비스 로케이터 대신
    /// 꼭 필요한 서비스(저장·진동·상태)만 명시적 프로퍼티로 노출한다.
    /// 어떤 씬에서 Play를 눌러도 자동 생성되므로 Boot 씬이 필수가 아니다.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public LocalSaveService Save { get; private set; }
        public IHapticService Haptics { get; private set; }
        public GameStateMachine State { get; private set; }
        /// <summary>격납고 기체 선택(애드온 명세 14).</summary>
        public AircraftSelectionService Aircraft { get; private set; }
        /// <summary>효과음·절차 합성 클립(Phase 3).</summary>
        public AudioManager Audio { get; private set; }

        /// <summary>설정 화면에서 값이 바뀌면 발생 — 비행 중이면 FlightSession이 즉시 반영.</summary>
        public event Action SettingsChanged;

        private DevLogFile _devLog;

        /// <summary>현재 기기 사양(SystemInfo) → Core 품질 자동 선택 입력.</summary>
        public static DeviceSpec CurrentDevice => new DeviceSpec
        {
            SystemMemoryMB = SystemInfo.systemMemorySize,
            ProcessorCount = SystemInfo.processorCount,
            IsMobile = Application.isMobilePlatform,
        };

        /// <summary>메인 메뉴 → 비행 씬으로 넘기는 모드 선택. 비행 씬을 직접 Play하면 기본값(Free Flight).</summary>
        public FlightMode PendingMode { get; set; } = FlightMode.FreeFlight;

        public SettingsData Settings => Save.Data.settings;
        public RecordsData Records => Save.Data.records;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameApp]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameApp>();
            Instance.Initialize();
        }

        private void Initialize()
        {
            State = new GameStateMachine();
            if (DevOverlay.Enabled)
            {
                // 실기기 테스트용: 로그 파일 + 측정 오버레이(릴리스 빌드에는 없음)
                try { _devLog = new DevLogFile(System.IO.Path.Combine(Application.persistentDataPath, "logs")); }
                catch (Exception e) { Debug.LogWarning($"[TOWERGLIDE] 개발 로그 파일 생성 실패: {e.GetType().Name}"); }
                gameObject.AddComponent<DevOverlay>();
            }
            Save = new LocalSaveService();
            Save.Load();
            // 첫 실행: 기기 사양에 맞는 그래픽 단계·프레임(명세 7.1). 이후엔 사용자 설정 우선
            if (QualityAutoSelector.ApplyOnce(Save.Data.settings, CurrentDevice))
            {
                Debug.Log($"[TOWERGLIDE] 기기 기본 품질: {(QualityTier)Save.Data.settings.qualityTier}, {Save.Data.settings.targetFrameRate}fps");
                Save.Save();
            }
            Haptics = new HapticService();
            Audio = gameObject.AddComponent<AudioManager>();
            Aircraft = new AircraftSelectionService(Save, Resources.Load<AircraftCatalog>(AircraftSelectionService.CatalogResourcePath));
            ApplySettings();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            State.TryTransition(GameState.MainMenu);
        }

        /// <summary>설정 변경 후 호출 — 프레임 제한·품질·진동 반영.</summary>
        public void ApplySettings()
        {
            SettingsData s = Settings;
            Application.targetFrameRate = s.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            QualityPresetApplier.Apply((QualityTier)s.qualityTier);
            Haptics.Enabled = s.hapticsEnabled;
            AudioListener.volume = s.masterVolume;
            if (Audio != null) Audio.SfxVolume = s.sfxVolume;
        }

        /// <summary>설정 화면 전용: 적용 + 저장 + 알림.</summary>
        public void CommitSettings()
        {
            Save.Data.settings = Save.Data.settings.Sanitized();
            QualityAutoSelector.ApplyOnce(Save.Data.settings, CurrentDevice); // 설정 초기화 직후면 기기 기본값 재계산
            ApplySettings();
            Save.Save();
            SettingsChanged?.Invoke();
        }

        public void SaveNow() => Save.Save();

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save.Save(); // 백그라운드 전환 시 저장(강제 종료 대비)
        }

        private void OnApplicationQuit()
        {
            Save.Save();
            _devLog?.Dispose();
        }
    }
}
