using UnityEngine;

namespace TowerGlide.Gameplay
{
    public enum ObstacleType
    {
        Building = 0,
        CheckpointRingFrame = 1,
        ConstructionCrane = 2,
        SurveyDrone = 3,
        BillboardFrame = 4,
        AntennaArray = 5,
        Skybridge = 6,
        CableZone = 7,
        MovingCargo = 8,
    }

    /// <summary>
    /// 근접 회피·충돌 통계 대상 표시. 장애물 루트에 붙이면 자식 콜라이더 전부가 이 장애물로 집계된다.
    /// 지면·경계 등 근접 회피 대상이 아닌 것에는 붙이지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObstacleMarker : MonoBehaviour
    {
        [SerializeField] private ObstacleType type = ObstacleType.Building;
        [Tooltip("false면 충돌은 하되 근접 회피 점수 대상에서 제외.")]
        [SerializeField] private bool countsForNearMiss = true;

        public ObstacleType Type => type;
        public bool CountsForNearMiss => countsForNearMiss;
        public int Id => GetInstanceID();

        public void Configure(ObstacleType obstacleType, bool nearMiss = true)
        {
            type = obstacleType;
            countsForNearMiss = nearMiss;
        }
    }
}
