using TowerGlide.App;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;
using TowerGlide.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace TowerGlide.UI
{
    /// <summary>
    /// 메인 메뉴(명세 5.2): FREE FLIGHT / SKYLINE RUN / HANGAR(기체 선택, 애드온 명세 14.5) / SETTINGS + 최고 기록·현재 기체 표시.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Text recordsText;
        [SerializeField] private Text noticeText;
        [SerializeField] private Text aircraftText;
        [SerializeField] private HangarController hangar;
        [SerializeField] private SettingsPanelController settings;
        [Tooltip("오픈 데이터 출처 표시(애드온 명세 3.3·8). 실제 지도 데이터를 쓰기 전에는 비어 있다.")]
        [SerializeField] private Text creditsText;

        private void Start()
        {
            Time.timeScale = 1f;
            if (noticeText != null) noticeText.text = string.Empty;
            if (creditsText != null) creditsText.text = string.Empty; // OW2에서 OSM 등 사용 시 "© OpenStreetMap contributors" 표시
            Refresh();
        }

        public void Refresh()
        {
            RefreshRecords();
            AircraftDefinition def = GameApp.Instance?.Aircraft?.Selected;
            if (aircraftText != null)
                aircraftText.text = def != null ? $"AIRCRAFT  {def.displayName}  ·  {HangarController.CategoryLabel(def.category)}" : string.Empty;
        }

        public void StartFreeFlight() => StartMode(FlightMode.FreeFlight);

        public void StartSkylineRun() => StartMode(FlightMode.SkylineRun);

        public void OpenHangar()
        {
            if (hangar != null) hangar.Show();
            else ShowNotice("HANGAR를 불러오지 못했습니다.");
        }

        public void OpenSettings()
        {
            if (settings != null) settings.Show();
            else ShowNotice("SETTINGS를 불러오지 못했습니다.");
        }

        private void StartMode(FlightMode mode)
        {
            AircraftDefinition def = GameApp.Instance?.Aircraft?.Selected;
            if (def != null && !AircraftRules.CanPlay(mode, def.tags, out string reason))
            {
                ShowNotice(reason);
                return;
            }
            if (GameApp.Instance != null) GameApp.Instance.PendingMode = mode;
            SceneFlow.LoadFlight();
        }

        public void ShowNotice(string message)
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
                $"TOP SPEED  {Mathf.RoundToInt(r.topSpeedKmh)} km/h   TOTAL  {r.totalDistanceMeters / 1000f:0.0} km";
        }
    }
}
