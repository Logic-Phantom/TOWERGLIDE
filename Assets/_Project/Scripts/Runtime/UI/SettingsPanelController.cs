using TowerGlide.App;
using TowerGlide.Core.Save;
using UnityEngine;
using UnityEngine.UI;

namespace TowerGlide.UI
{
    /// <summary>
    /// 설정 화면(명세 5.5): 그래픽·프레임·조작 감도·피치 반전·조이스틱 방식·카메라 흔들림·볼륨 3종·진동·튜토리얼 다시 보기·초기화·라이선스.
    /// 값이 바뀌면 즉시 적용·저장(GameApp.CommitSettings) → 비행 중이면 FlightSession이 바로 반영.
    /// 메인 메뉴와 일시정지 화면에서 같은 컴포넌트를 쓴다. 자이로는 후속 기능이라 항목 없음(D-006).
    /// </summary>
    public sealed class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text qualityLabel;
        [SerializeField] private Text frameRateLabel;
        [SerializeField] private Slider sensitivity;
        [SerializeField] private Text sensitivityLabel;
        [SerializeField] private Slider cameraShake;
        [SerializeField] private Text cameraShakeLabel;
        [SerializeField] private Toggle invertPitch;
        [SerializeField] private Toggle floatingJoystick;
        [SerializeField] private Toggle haptics;
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Text masterLabel;
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Text musicLabel;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private Text sfxLabel;
        [SerializeField] private Text noticeText;
        [SerializeField] private GameObject licensePanel;
        [SerializeField] private Text licenseText;

        private bool _binding;

        private static SettingsData S => GameApp.Instance != null ? GameApp.Instance.Settings : null;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (licensePanel != null) licensePanel.SetActive(false);
            if (licenseText != null) licenseText.text = LicenseNotices.Text;

            Hook(sensitivity, v => { S.controlSensitivity = SettingsOptions.SnapSensitivity(v); });
            Hook(cameraShake, v => { S.cameraShake = v; });
            Hook(masterVolume, v => { S.masterVolume = v; });
            Hook(musicVolume, v => { S.musicVolume = v; });
            Hook(sfxVolume, v => { S.sfxVolume = v; });
            Hook(invertPitch, v => { S.invertPitch = v; });
            Hook(floatingJoystick, v => { S.floatingJoystick = v; });
            Hook(haptics, v => { S.hapticsEnabled = v; });
        }

        public void Show()
        {
            if (S == null) return;
            if (panel != null) panel.SetActive(true);
            if (noticeText != null) noticeText.text = string.Empty;
            Refresh();
        }

        public void Hide()
        {
            if (licensePanel != null) licensePanel.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        public void CycleQuality()
        {
            S.qualityTier = SettingsOptions.NextQuality(S.qualityTier);
            Commit();
        }

        public void CycleFrameRate()
        {
            S.targetFrameRate = SettingsOptions.NextFrameRate(S.targetFrameRate);
            Commit();
        }

        public void ReplayTutorial()
        {
            S.tutorialSeen = false;
            Commit();
            if (noticeText != null) noticeText.text = "다음 비행을 시작하면 조작 튜토리얼이 나옵니다.";
        }

        public void ResetToDefaults()
        {
            GameApp.Instance.Save.Data.settings = SettingsOptions.ResetKeepingTutorial(S);
            Commit();
            if (noticeText != null) noticeText.text = "기본 설정으로 되돌렸습니다.";
        }

        public void ToggleLicenses()
        {
            if (licensePanel != null) licensePanel.SetActive(!licensePanel.activeSelf);
        }

        private void Commit()
        {
            GameApp.Instance?.CommitSettings();
            Refresh();
        }

        private void Refresh()
        {
            SettingsData s = S;
            if (s == null) return;
            _binding = true;
            Set(sensitivity, s.controlSensitivity);
            Set(cameraShake, s.cameraShake);
            Set(masterVolume, s.masterVolume);
            Set(musicVolume, s.musicVolume);
            Set(sfxVolume, s.sfxVolume);
            if (invertPitch != null) invertPitch.isOn = s.invertPitch;
            if (floatingJoystick != null) floatingJoystick.isOn = s.floatingJoystick;
            if (haptics != null) haptics.isOn = s.hapticsEnabled;
            _binding = false;

            if (qualityLabel != null) qualityLabel.text = $"그래픽  {SettingsOptions.QualityLabel(s.qualityTier)}";
            if (frameRateLabel != null) frameRateLabel.text = $"프레임  {s.targetFrameRate} FPS";
            if (sensitivityLabel != null) sensitivityLabel.text = $"조작 감도  {s.controlSensitivity:0.0}";
            if (cameraShakeLabel != null) cameraShakeLabel.text = $"카메라 흔들림  {SettingsOptions.Percent(s.cameraShake)}";
            if (masterLabel != null) masterLabel.text = $"전체 음량  {SettingsOptions.Percent(s.masterVolume)}";
            if (musicLabel != null) musicLabel.text = $"음악  {SettingsOptions.Percent(s.musicVolume)}";
            if (sfxLabel != null) sfxLabel.text = $"효과음  {SettingsOptions.Percent(s.sfxVolume)}";
        }

        private void Hook(Slider slider, System.Action<float> apply)
        {
            if (slider == null) return;
            slider.onValueChanged.AddListener(v =>
            {
                if (_binding || S == null) return;
                apply(v);
                Commit();
            });
        }

        private void Hook(Toggle toggle, System.Action<bool> apply)
        {
            if (toggle == null) return;
            toggle.onValueChanged.AddListener(v =>
            {
                if (_binding || S == null) return;
                apply(v);
                Commit();
            });
        }

        private static void Set(Slider s, float v)
        {
            if (s != null) s.SetValueWithoutNotify(v);
        }
    }
}
