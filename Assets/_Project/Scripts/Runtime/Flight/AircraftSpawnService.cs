using TowerGlide.App;
using TowerGlide.Cameras;
using TowerGlide.Gameplay;
using TowerGlide.Input;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>
    /// 비행 씬 시작 시 선택 기체 프리팹을 스폰하고 라우터·카메라·세션에 연결한다(애드온 명세 14.4 AircraftSpawnService).
    /// 씬에 미리 놓인 기체(placeholder)는 선택 기체와 다르면 제거한다. 카탈로그가 없으면 placeholder로 그대로 비행.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AircraftSpawnService : MonoBehaviour
    {
        [SerializeField] private AircraftFlightController placeholderAircraft;
        [SerializeField] private string placeholderAircraftId = Core.Aircraft.AircraftIds.ScoutP1;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private FlightInputRouter router;
        [SerializeField] private ChaseCameraController chaseCamera;
        [SerializeField] private FlightSession session;

        public AircraftFlightController Spawned { get; private set; }
        public AircraftDefinition Definition { get; private set; }

        private void Awake()
        {
            AircraftDefinition def = GameApp.Instance != null ? GameApp.Instance.Aircraft?.Selected : null;
            Definition = def;

            AircraftFlightController aircraft = placeholderAircraft;
            if (def != null && def.prefab != null && def.id != placeholderAircraftId)
            {
                Vector3 pos = spawnPoint.position + Vector3.up * def.spawnHeightOffset;
                GameObject go = Instantiate(def.prefab, pos, spawnPoint.rotation);
                go.name = $"Aircraft ({def.displayName})";
                aircraft = go.GetComponent<AircraftFlightController>();
                if (aircraft == null)
                {
                    Debug.LogError($"[{nameof(AircraftSpawnService)}] {def.id} 프리팹에 AircraftFlightController가 없습니다 — 기본 기체 사용", go);
                    Destroy(go);
                    aircraft = placeholderAircraft;
                    def = null;
                }
                else if (placeholderAircraft != null)
                {
                    Destroy(placeholderAircraft.gameObject);
                }
            }

            Spawned = aircraft;
            if (aircraft == null)
            {
                Debug.LogError($"[{nameof(AircraftSpawnService)}] 비행할 기체가 없습니다.", this);
                return;
            }
            if (def != null && def.tuning != null) aircraft.SetTuning(def.tuning);

            router.SetAircraft(aircraft);
            chaseCamera.SetTarget(aircraft);
            if (def != null) chaseCamera.ApplyProfile(def.cameraProfile);
            session.BindAircraft(aircraft, def);
        }
    }
}
