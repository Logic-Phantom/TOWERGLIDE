using System.Collections.Generic;

namespace TowerGlide.Core.Gameplay
{
    /// <summary>
    /// 근접 회피 중복 판정 방지(명세 4.2). 장애물별 쿨다운과 "최근 충돌한 장애물은 인정하지 않음" 규칙을 관리한다.
    /// 장애물은 정수 id(Unity InstanceID 등)로 식별한다.
    /// </summary>
    public sealed class NearMissTracker
    {
        private readonly float _cooldownSeconds;
        private readonly float _collisionGraceSeconds;
        private readonly Dictionary<int, float> _lastAwarded = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _lastCollision = new Dictionary<int, float>();

        public NearMissTracker(float cooldownSeconds = 3f, float collisionGraceSeconds = 2f)
        {
            _cooldownSeconds = cooldownSeconds;
            _collisionGraceSeconds = collisionGraceSeconds;
        }

        public void RegisterCollision(int obstacleId, float time) => _lastCollision[obstacleId] = time;

        /// <summary>근접 회피 인정 여부. 인정되면 쿨다운을 시작한다.</summary>
        public bool TryAward(int obstacleId, float time)
        {
            if (_lastCollision.TryGetValue(obstacleId, out float hit) && time - hit < _collisionGraceSeconds) return false;
            if (_lastAwarded.TryGetValue(obstacleId, out float last) && time - last < _cooldownSeconds) return false;
            _lastAwarded[obstacleId] = time;
            return true;
        }

        public void Clear()
        {
            _lastAwarded.Clear();
            _lastCollision.Clear();
        }
    }
}
