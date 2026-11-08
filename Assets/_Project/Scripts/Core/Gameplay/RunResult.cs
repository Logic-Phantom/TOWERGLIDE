using System;

namespace TowerGlide.Core.Gameplay
{
    public enum FlightMode
    {
        FreeFlight = 0,
        SkylineRun = 1,
    }

    /// <summary>결과 화면·기록 저장에 쓰는 한 판의 결과.</summary>
    [Serializable]
    public struct RunResult
    {
        public FlightMode Mode;
        public bool Completed;
        public float FlightSeconds;
        public float DistanceMeters;
        public float MaxSpeedKmh;
        public float MaxAltitudeMeters;
        public int CheckpointsPassed;
        public int CheckpointsTotal;
        public ScoreBreakdown Score;
    }
}
