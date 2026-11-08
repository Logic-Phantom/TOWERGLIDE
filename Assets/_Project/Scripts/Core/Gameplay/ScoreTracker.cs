using System;
using TowerGlide.Core.Util;

namespace TowerGlide.Core.Gameplay
{
    [Serializable]
    public struct ScoreRules
    {
        public int RingPoints;
        /// <summary>콤보 1단계당 추가 점수(링 점수에 더함).</summary>
        public int ComboStepBonus;
        public int MaxComboSteps;
        /// <summary>이 시간 안에 다음 링을 통과해야 콤보 유지(초).</summary>
        public float ComboWindowSeconds;
        public int NearMissBasePoints;
        /// <summary>근접도(0=경계, 1=스치듯)와 속도에 따른 최대 추가 점수.</summary>
        public int NearMissMaxBonus;
        public float NearMissSpeedForMaxBonusKmh;
        public int CollisionPenalty;
        public int CompletionBonus;
        public int PointsPerRemainingSecond;

        public static ScoreRules Default => new ScoreRules
        {
            RingPoints = 100,
            ComboStepBonus = 25,
            MaxComboSteps = 8,
            ComboWindowSeconds = 6f,
            NearMissBasePoints = 50,
            NearMissMaxBonus = 100,
            NearMissSpeedForMaxBonusKmh = 250f,
            CollisionPenalty = 150,
            CompletionBonus = 1000,
            PointsPerRemainingSecond = 20,
        };
    }

    /// <summary>점수 항목별 합계. 결과 화면과 테스트가 그대로 사용한다.</summary>
    [Serializable]
    public struct ScoreBreakdown
    {
        public int RingPoints;
        public int ComboPoints;
        public int NearMissPoints;
        public int CollisionPenalty;
        public int CompletionBonus;
        public int TimeBonus;
        public int RingsPassed;
        public int NearMisses;
        public int Collisions;
        public int BestCombo;

        /// <summary>총점은 0 미만이 되지 않는다.</summary>
        public int Total => Math.Max(0, RingPoints + ComboPoints + NearMissPoints + CompletionBonus + TimeBonus - CollisionPenalty);
    }

    /// <summary>
    /// 런 중 이벤트를 받아 점수를 누적한다. 시간은 호출자가 넘기는 런 경과 시간(초)을 사용(테스트 가능).
    /// </summary>
    public sealed class ScoreTracker
    {
        private readonly ScoreRules _rules;
        private ScoreBreakdown _b;
        private int _combo;
        private float _lastRingTime = float.NegativeInfinity;
        private bool _finished;

        public ScoreTracker(ScoreRules rules) { _rules = rules; }

        public ScoreBreakdown Breakdown => _b;
        public int Total => _b.Total;
        public int CurrentCombo => _combo;

        /// <returns>이번 링으로 얻은 점수</returns>
        public int OnRingPassed(float runTime)
        {
            if (_finished) return 0;
            bool chained = runTime - _lastRingTime <= _rules.ComboWindowSeconds;
            _combo = chained ? Math.Min(_combo + 1, _rules.MaxComboSteps) : 0;
            _lastRingTime = runTime;

            int comboPoints = _combo * _rules.ComboStepBonus;
            _b.RingPoints += _rules.RingPoints;
            _b.ComboPoints += comboPoints;
            _b.RingsPassed++;
            _b.BestCombo = Math.Max(_b.BestCombo, _combo);
            return _rules.RingPoints + comboPoints;
        }

        /// <param name="proximity01">0 = 판정 반경 끝, 1 = 거의 접촉</param>
        /// <returns>이번 근접 회피로 얻은 점수</returns>
        public int OnNearMiss(float proximity01, float speedKmh)
        {
            if (_finished) return 0;
            float p = FlightMath.Clamp01(proximity01);
            float s = _rules.NearMissSpeedForMaxBonusKmh > 0f ? FlightMath.Clamp01(speedKmh / _rules.NearMissSpeedForMaxBonusKmh) : 0f;
            int points = _rules.NearMissBasePoints + (int)Math.Round(_rules.NearMissMaxBonus * p * s);
            _b.NearMissPoints += points;
            _b.NearMisses++;
            return points;
        }

        public void OnCollision()
        {
            if (_finished) return;
            _combo = 0;
            _lastRingTime = float.NegativeInfinity;
            _b.Collisions++;
            _b.CollisionPenalty += _rules.CollisionPenalty;
        }

        /// <summary>런 종료. 완주 시 완주 보너스와 남은 시간 보너스를 더한다. 두 번 호출해도 한 번만 반영.</summary>
        public ScoreBreakdown Finish(bool completed, float remainingSeconds)
        {
            if (_finished) return _b;
            _finished = true;
            if (completed)
            {
                _b.CompletionBonus = _rules.CompletionBonus;
                _b.TimeBonus = (int)Math.Floor(Math.Max(0f, remainingSeconds)) * _rules.PointsPerRemainingSecond;
            }
            return _b;
        }
    }
}
