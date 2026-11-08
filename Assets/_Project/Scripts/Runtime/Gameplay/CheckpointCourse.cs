using System;
using System.Collections.Generic;
using TowerGlide.Core.Gameplay;
using UnityEngine;

namespace TowerGlide.Gameplay
{
    /// <summary>
    /// SKYLINE RUN 코스. 자식 CheckpointGate들을 계층 순서대로 번호 매기고 CheckpointSequence로 진행을 관리한다.
    /// 코스 데이터 = 씬 계층(위치·회전). 코스 순서를 바꾸려면 자식 순서를 바꾼다.
    /// </summary>
    public sealed class CheckpointCourse : MonoBehaviour
    {
        private readonly List<CheckpointGate> _gates = new List<CheckpointGate>();
        private CheckpointSequence _sequence;

        /// <summary>(통과한 링, 결과)</summary>
        public event Action<CheckpointGate, CheckpointPassResult> GateProcessed;

        public int Count => _gates.Count;
        public CheckpointSequence Sequence => _sequence;
        public CheckpointGate NextGate => _sequence != null && !_sequence.IsComplete ? _gates[_sequence.NextIndex] : null;
        public CheckpointGate LastPassedGate => _sequence != null && _sequence.LastPassedIndex >= 0 ? _gates[_sequence.LastPassedIndex] : null;
        public CheckpointGate FirstGate => _gates.Count > 0 ? _gates[0] : null;

        private void Awake()
        {
            GetComponentsInChildren(true, _gates);
            if (_gates.Count == 0)
            {
                Debug.LogError($"[{nameof(CheckpointCourse)}] 자식 CheckpointGate가 없습니다.", this);
                return;
            }
            for (int i = 0; i < _gates.Count; i++)
            {
                _gates[i].Initialize(i);
                _gates[i].AircraftPassed += OnGatePassed;
            }
            _sequence = new CheckpointSequence(_gates.Count);
            RefreshVisuals();
        }

        private void OnDestroy()
        {
            foreach (CheckpointGate g in _gates)
                if (g != null) g.AircraftPassed -= OnGatePassed;
        }

        public void ResetCourse()
        {
            _sequence?.Reset();
            RefreshVisuals();
        }

        public void SetCourseVisible(bool visible) => gameObject.SetActive(visible);

        private void OnGatePassed(CheckpointGate gate)
        {
            if (_sequence == null) return;
            CheckpointPassResult result = _sequence.TryPass(gate.Index);
            if (result == CheckpointPassResult.Passed || result == CheckpointPassResult.Completed) RefreshVisuals();
            GateProcessed?.Invoke(gate, result);
        }

        private void RefreshVisuals()
        {
            if (_sequence == null) return;
            for (int i = 0; i < _gates.Count; i++)
            {
                GateVisualState s = i < _sequence.NextIndex ? GateVisualState.Passed
                    : i == _sequence.NextIndex ? GateVisualState.Next
                    : GateVisualState.Inactive;
                _gates[i].SetVisualState(s);
            }
        }
    }
}
