using System;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Flight
{
    [Serializable]
    public struct BoostSettings
    {
        /// <summary>가득 찬 게이지로 연속 사용 가능한 시간(초).</summary>
        public float CapacitySeconds;
        /// <summary>빈 게이지가 가득 차는 데 걸리는 시간(초).</summary>
        public float FullRechargeSeconds;
        /// <summary>부스트를 끈 뒤 충전이 시작되기까지의 지연(초).</summary>
        public float RechargeDelaySeconds;
        /// <summary>완전히 소진된 뒤 다시 켜려면 필요한 최소 게이지 비율(0..1). 깜빡임 방지.</summary>
        public float MinRatioToRestart;

        public static BoostSettings Default => new BoostSettings
        {
            CapacitySeconds = 3f,
            FullRechargeSeconds = 6f,
            RechargeDelaySeconds = 1f,
            MinRatioToRestart = 0.25f,
        };
    }

    /// <summary>
    /// 부스트 게이지 상태 기계. 무제한 부스트 금지(명세 2.2) — 소진되면 MinRatioToRestart까지 재충전돼야 다시 켜진다.
    /// FixedUpdate에서 Tick(fixedDeltaTime)으로 호출한다.
    /// </summary>
    public sealed class BoostMeter
    {
        private readonly BoostSettings _settings;
        private float _charge;          // 0..CapacitySeconds
        private float _sinceRelease;    // 마지막 사용 이후 경과
        private bool _depletedLock;     // 소진 후 재시작 잠금

        public BoostMeter(BoostSettings settings)
        {
            if (settings.CapacitySeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(settings), "CapacitySeconds must be > 0");
            if (settings.FullRechargeSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(settings), "FullRechargeSeconds must be > 0");
            _settings = settings;
            _charge = settings.CapacitySeconds;
            _sinceRelease = float.MaxValue;
        }

        public bool IsBoosting { get; private set; }
        public float Normalized => _charge / _settings.CapacitySeconds;
        public bool IsLocked => _depletedLock;

        public bool Tick(float deltaTime, bool wantsBoost)
        {
            if (deltaTime <= 0f) return IsBoosting;

            if (_depletedLock && Normalized >= _settings.MinRatioToRestart) _depletedLock = false;

            bool canBoost = wantsBoost && !_depletedLock && _charge > 0f;
            if (canBoost)
            {
                IsBoosting = true;
                _sinceRelease = 0f;
                _charge = Math.Max(0f, _charge - deltaTime);
                if (_charge <= 0f)
                {
                    _depletedLock = true;
                    IsBoosting = false;
                }
            }
            else
            {
                IsBoosting = false;
                _sinceRelease = _sinceRelease >= float.MaxValue - deltaTime ? float.MaxValue : _sinceRelease + deltaTime;
                if (_sinceRelease >= _settings.RechargeDelaySeconds)
                {
                    float rate = _settings.CapacitySeconds / _settings.FullRechargeSeconds;
                    _charge = Math.Min(_settings.CapacitySeconds, _charge + rate * deltaTime);
                }
            }

            _charge = FlightMath.Clamp(_charge, 0f, _settings.CapacitySeconds);
            return IsBoosting;
        }

        public void Refill()
        {
            _charge = _settings.CapacitySeconds;
            _depletedLock = false;
            IsBoosting = false;
        }
    }
}
