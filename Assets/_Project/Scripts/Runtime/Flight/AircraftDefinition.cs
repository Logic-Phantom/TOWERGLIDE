using System;
using TowerGlide.Core.Aircraft;
using UnityEngine;

namespace TowerGlide.Flight
{
    /// <summary>기체 크기에 맞춘 추적 카메라 설정(애드온 명세 14.8). 큰 기체일수록 멀리·높이, 충돌 반경 크게.</summary>
    [Serializable]
    public struct AircraftCameraProfile
    {
        public Vector3 chaseOffset;
        public float lookAhead;
        public float baseFov;
        public float maxExtraFov;
        public float fovMaxAtSpeed;
        public float collisionRadius;
        public float minDistance;
        public Vector3 cockpitOffset;

        public static AircraftCameraProfile ForSize(float length, float wingspan, float maxSpeed)
        {
            float s = Mathf.Max(length, wingspan * 0.8f);
            return new AircraftCameraProfile
            {
                chaseOffset = new Vector3(0f, 1.2f + s * 0.3f, -(4f + s * 1.0f)),
                lookAhead = 4f + s * 0.6f,
                baseFov = 60f,
                maxExtraFov = 12f,
                fovMaxAtSpeed = maxSpeed,
                collisionRadius = Mathf.Clamp(s * 0.08f, 0.6f, 3f),
                minDistance = 2f + s * 0.25f,
                cockpitOffset = new Vector3(0f, 0.65f, length * 0.12f),
            };
        }
    }

    /// <summary>
    /// 기체 1종의 정의(애드온 명세 14.4). 물리는 공통 AircraftFlightController + 기종별 AircraftFlightTuning — 컨트롤러 복사·기종 분기 없음.
    /// 새 기체 = 이 에셋 + 튜닝 에셋 + 프리팹을 만들어 AircraftCatalog에 등록.
    /// </summary>
    [CreateAssetMenu(menuName = "TOWERGLIDE/Aircraft Definition", fileName = "AircraftDefinition")]
    public sealed class AircraftDefinition : ScriptableObject
    {
        [Tooltip("저장 데이터에 들어가는 고유 ID. 출시 후 바꾸지 않는다(Core AircraftIds).")]
        public string id = AircraftIds.ScoutP1;
        public string displayName = "Scout P-1";
        public AircraftCategory category = AircraftCategory.LightProp;
        [TextArea] public string description;

        [Header("Assets")]
        public GameObject prefab;
        public AircraftFlightTuning tuning;
        [Tooltip("격납고 카드 색(미리보기 이미지가 없을 때).")]
        public Color accentColor = new Color(0.86f, 0.12f, 0.12f);

        [Header("Size (m, 게임 월드 단위)")]
        public float length = 7.5f;
        public float wingspan = 8.4f;
        public float height = 2.4f;

        [Header("Spawn / Camera")]
        [Tooltip("공중 스폰 시 초기 속도(m/s). 0이면 튜닝의 순항 속도.")]
        public float spawnSpeed;
        [Tooltip("스폰 고도 추가(m) — 큰 기체는 더 높이에서 시작.")]
        public float spawnHeightOffset;
        public AircraftCameraProfile cameraProfile = AircraftCameraProfile.ForSize(7.5f, 8.4f, 80f);

        [Header("Availability")]
        public AircraftTags tags = AircraftTags.TightRoute;
        public UnlockRule unlock = UnlockRule.Always;

        public bool IsImplemented => prefab != null && tuning != null && unlock.Kind != UnlockKind.ComingSoon;
        public float SpawnSpeed => spawnSpeed > 0f ? spawnSpeed : tuning != null ? tuning.cruiseSpeed : 30f;

        public FlightProfileNumbers ProfileNumbers => tuning != null
            ? tuning.ToProfileNumbers(wingspan)
            : new FlightProfileNumbers { Wingspan = wingspan };
    }
}
