using TowerGlide.Core.App;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;
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
            Save = new LocalSaveService();
            Save.Load();
            Haptics = new HapticService();
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
        }

        public void SaveNow() => Save.Save();

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save.Save(); // 백그라운드 전환 시 저장(강제 종료 대비)
        }

        private void OnApplicationQuit() => Save.Save();
    }
}
