using TowerGlide.App;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;
using UnityEngine;
using UnityEngine.UI;

namespace TowerGlide.UI
{
    /// <summary>
    /// 메인 메뉴(명세 5.2). MVP: FREE FLIGHT / SKYLINE RUN 진입 + 최고 기록 표시.
    /// HANGAR·SETTINGS 화면은 Phase 3에서 구현(현재 버튼은 '준비 중' 안내).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Text recordsText;
        [SerializeField] private Text noticeText;

        private void Start()
        {
            Time.timeScale = 1f;
            RefreshRecords();
            if (noticeText != null) noticeText.text = string.Empty;
        }

        public void StartFreeFlight() => StartMode(FlightMode.FreeFlight);

        public void StartSkylineRun() => StartMode(FlightMode.SkylineRun);

        public void OpenHangar() => ShowNotice("HANGAR는 준비 중입니다.");

        public void OpenSettings() => ShowNotice("SETTINGS는 준비 중입니다.");

        private void StartMode(FlightMode mode)
        {
            if (GameApp.Instance != null) GameApp.Instance.PendingMode = mode;
            SceneFlow.LoadFlight();
        }

        private void ShowNotice(string message)
        {
            if (noticeText != null) noticeText.text = message;
        }

        private void RefreshRecords()
        {
            if (recordsText == null || GameApp.Instance == null) return;
            RecordsData r = GameApp.Instance.Records;
            string best = r.bestSkylineSeconds > 0f
                ? $"{Mathf.FloorToInt(r.bestSkylineSeconds) / 60}:{Mathf.FloorToInt(r.bestSkylineSeconds) % 60:00}"
                : "-";
            recordsText.text =
                $"BEST SCORE  {r.bestSkylineScore}   BEST TIME  {best}\n" +
                $"TOP SPEED  {Mathf.RoundToInt(r.topSpeedKmh)} km/h   LONGEST  {Mathf.RoundToInt(r.longestFlightMeters)} m";
        }
    }
}
