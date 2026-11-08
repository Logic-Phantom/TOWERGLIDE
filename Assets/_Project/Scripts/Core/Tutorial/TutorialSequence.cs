using System;
using System.Collections.Generic;
using TowerGlide.Core.Input;

namespace TowerGlide.Core.Tutorial
{
    public enum TutorialStep
    {
        Roll,
        Turn,
        Pitch,
        Boost,
        Done,
        Complete,
    }

    /// <summary>
    /// 첫 비행 조작 튜토리얼(명세 2.2 "초보자도 1~2분 안에 상승·하강·선회 이해", 5.5 "튜토리얼 재생").
    /// 각 단계는 실제 조작을 일정 시간 유지해야 넘어간다(설명만 읽고 넘기지 않게). 언제든 건너뛸 수 있다.
    /// 시간은 누적(손을 떼도 진행이 0으로 돌아가지 않음 — 조급함 방지).
    /// </summary>
    public sealed class TutorialSequence
    {
        private static readonly Dictionary<TutorialStep, (string text, float seconds)> Steps = new Dictionary<TutorialStep, (string, float)>
        {
            { TutorialStep.Roll, ("왼쪽 화면을 좌우로 밀어 기체를 기울여 보세요", 1.0f) },
            { TutorialStep.Turn, ("기울인 채로 유지하면 그 방향으로 선회합니다", 1.5f) },
            { TutorialStep.Pitch, ("위로 밀면 상승, 아래로 밀면 하강합니다", 1.0f) },
            { TutorialStep.Boost, ("오른쪽 BOOST 버튼을 누르고 있으면 가속합니다", 0.5f) },
            { TutorialStep.Done, ("준비 완료! 자유롭게 비행하세요", 2.0f) },
        };

        private readonly bool _boostAvailable;
        private float _held;

        public TutorialSequence(bool boostAvailable)
        {
            _boostAvailable = boostAvailable;
            Current = TutorialStep.Roll;
        }

        public TutorialStep Current { get; private set; }
        public bool IsComplete => Current == TutorialStep.Complete;
        public string Instruction => IsComplete ? string.Empty : Steps[Current].text;
        /// <summary>현재 단계 진행률 0..1.</summary>
        public float StepProgress => IsComplete ? 1f : Math.Min(1f, _held / Steps[Current].seconds);

        /// <returns>단계가 바뀌었으면 true</returns>
        public bool Tick(float deltaTime, FlightInput input, float bankDeg, bool isBoosting)
        {
            if (IsComplete || deltaTime <= 0f) return false;
            if (Satisfied(input, bankDeg, isBoosting)) _held += deltaTime;
            if (_held < Steps[Current].seconds) return false;
            Advance();
            return true;
        }

        public void Skip()
        {
            Current = TutorialStep.Complete;
            _held = 0f;
        }

        private bool Satisfied(FlightInput input, float bankDeg, bool isBoosting)
        {
            switch (Current)
            {
                case TutorialStep.Roll: return Math.Abs(input.Roll) > 0.5f;
                case TutorialStep.Turn: return Math.Abs(bankDeg) > 25f;
                case TutorialStep.Pitch: return Math.Abs(input.Pitch) > 0.5f;
                case TutorialStep.Boost: return isBoosting;
                case TutorialStep.Done: return true;
                default: return false;
            }
        }

        private void Advance()
        {
            _held = 0f;
            Current = Current + 1;
            if (Current == TutorialStep.Boost && !_boostAvailable) Current = TutorialStep.Done; // 부스트 없는 기체(여객기)
        }
    }
}
