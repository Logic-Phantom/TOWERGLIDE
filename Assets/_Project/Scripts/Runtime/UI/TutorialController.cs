using TowerGlide.App;
using TowerGlide.Core.App;
using TowerGlide.Core.Tutorial;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace TowerGlide.UI
{
    /// <summary>
    /// 첫 비행 조작 튜토리얼 표시(Core TutorialSequence). 설정의 tutorialSeen이 false면 비행 시작 후 자동으로 나온다.
    /// 화면 위쪽 가운데에 한 줄 안내 + 진행 막대 + 건너뛰기. 완료·건너뛰기 시 tutorialSeen = true 저장.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        [SerializeField] private FlightSession session;
        [SerializeField] private GameObject root;
        [SerializeField] private Text instruction;
        [SerializeField] private Image progress;

        private TutorialSequence _sequence;

        public bool IsRunning => _sequence != null && !_sequence.IsComplete;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (session == null) return;
            if (_sequence == null)
            {
                if (session.State != GameState.Flying || session.Aircraft == null) return;
                GameApp app = GameApp.Instance;
                if (app == null || app.Settings.tutorialSeen) { enabled = false; return; }
                AircraftFlightController a = session.Aircraft;
                _sequence = new TutorialSequence(boostAvailable: a.Tuning == null || a.Tuning.boostEnabled);
                if (root != null) root.SetActive(true);
            }
            if (_sequence.IsComplete || session.State != GameState.Flying) return;

            AircraftFlightController aircraft = session.Aircraft;
            _sequence.Tick(Time.deltaTime, session.InputRouter != null ? session.InputRouter.LastInput : default,
                aircraft.BankDeg, aircraft.IsBoosting);
            if (instruction != null) instruction.text = _sequence.Instruction;
            if (progress != null) progress.fillAmount = _sequence.StepProgress;
            if (_sequence.IsComplete) Finish();
        }

        /// <summary>건너뛰기 버튼.</summary>
        public void Skip()
        {
            _sequence ??= new TutorialSequence(true);
            _sequence.Skip();
            Finish();
        }

        private void Finish()
        {
            if (root != null) root.SetActive(false);
            GameApp app = GameApp.Instance;
            if (app != null && !app.Settings.tutorialSeen)
            {
                app.Settings.tutorialSeen = true;
                app.SaveNow();
            }
            enabled = false;
        }
    }
}
