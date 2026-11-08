using System;
using System.Collections.Generic;
using TowerGlide.Core.Input;
using TowerGlide.Flight;
using UnityEngine;

namespace TowerGlide.Input
{
    /// <summary>
    /// 입력 장치와 게임플레이 사이의 유일한 중재자(명세 15-8).
    /// - 축 입력: 모든 IFlightInputSource를 합치고(InputShaping) Update마다 AircraftFlightController.SetInput.
    /// - 일회성 명령(카메라 전환·복귀·일시정지): UI 버튼과 키보드가 Request*를 호출 → 이벤트로 구독자에게 전달.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class FlightInputRouter : MonoBehaviour
    {
        [SerializeField] private AircraftFlightController aircraft;
        [Tooltip("IFlightInputSource를 구현한 컴포넌트들(터치, 키보드/게임패드). 비워 두면 자식·씬에서 자동 검색.")]
        [SerializeField] private MonoBehaviour[] sources = Array.Empty<MonoBehaviour>();

        private readonly List<IFlightInputSource> _sources = new List<IFlightInputSource>();
        private InputShapingSettings _shaping = InputShapingSettings.Default;
        private bool _enabled = true;

        public FlightInput LastInput { get; private set; }

        public event Action CameraToggleRequested;
        public event Action RecoverRequested;
        public event Action PauseRequested;

        public void SetShaping(InputShapingSettings shaping) => _shaping = shaping;

        /// <summary>일시정지·결과 화면·복귀 연출 중에는 입력을 중립으로 고정한다.</summary>
        public void SetFlightInputEnabled(bool value)
        {
            _enabled = value;
            if (!value && aircraft != null) aircraft.SetInput(FlightInput.Neutral);
        }

        public void RequestCameraToggle() => CameraToggleRequested?.Invoke();
        public void RequestRecover() => RecoverRequested?.Invoke();
        public void RequestPause() => PauseRequested?.Invoke();

        private void Awake()
        {
            foreach (MonoBehaviour mb in sources)
            {
                if (mb is IFlightInputSource s) _sources.Add(s);
                else if (mb != null) Debug.LogWarning($"[{nameof(FlightInputRouter)}] {mb.name}은 IFlightInputSource가 아닙니다.", mb);
            }
            if (_sources.Count == 0)
            {
                foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb is IFlightInputSource s) _sources.Add(s);
            }
            if (aircraft == null) Debug.LogError($"[{nameof(FlightInputRouter)}] aircraft가 연결되지 않았습니다.", this);
        }

        private void Update()
        {
            if (aircraft == null) return;
            if (!_enabled)
            {
                LastInput = FlightInput.Neutral;
                return;
            }

            FlightInput combined = FlightInput.Neutral;
            for (int i = 0; i < _sources.Count; i++)
            {
                IFlightInputSource s = _sources[i];
                if (s != null && s.IsActive) combined = FlightInput.Combine(combined, s.ReadRaw());
            }

            LastInput = InputShaping.Shape(combined, _shaping);
            aircraft.SetInput(LastInput);
        }
    }
}
