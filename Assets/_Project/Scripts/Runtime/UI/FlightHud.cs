using System.Text;
using TowerGlide.Core.Gameplay;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using TowerGlide.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TowerGlide.UI
{
    /// <summary>
    /// 조종 HUD(명세 5.3) + 일시정지/결과 패널 표시. 얇고 명확하게 — 화면 중앙을 가리지 않는다.
    /// 표시만 담당하며 게임 규칙을 바꾸지 않는다(버튼은 FlightSession의 public 메서드로 연결).
    /// 텍스트 갱신은 값이 바뀔 때만(문자열 GC 최소화).
    /// </summary>
    public sealed class FlightHud : MonoBehaviour
    {
        [Header("Flight data")]
        [SerializeField] private Text speedText;
        [SerializeField] private Text altitudeText;
        [SerializeField] private Image boostFill;
        [SerializeField] private Color boostReadyColor = new Color(0.3f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color boostLockedColor = new Color(1f, 0.35f, 0.25f, 0.9f);

        [Header("Run")]
        [SerializeField] private Text modeText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text scoreText;
        [SerializeField] private RectTransform gateArrow;
        [SerializeField] private Text gateDistanceText;

        [Header("Feedback")]
        [SerializeField] private Text toastText;
        [SerializeField] private float toastSeconds = 1.4f;
        [SerializeField] private GameObject boundaryWarning;

        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private GameObject endFreeFlightButton;

        [Header("Controls")]
        [SerializeField] private VirtualJoystick joystick;
        [SerializeField] private Camera worldCamera;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private int _lastSpeed = int.MinValue;
        private int _lastAltitude = int.MinValue;
        private int _lastScore = int.MinValue;
        private int _lastSeconds = int.MinValue;
        private int _lastPassed = int.MinValue;
        private float _toastUntil;

        private void Awake()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (boundaryWarning != null) boundaryWarning.SetActive(false);
            if (toastText != null) toastText.text = string.Empty;
            if (worldCamera == null) worldCamera = Camera.main;
        }

        public void SetFloatingJoystick(bool floating)
        {
            if (joystick != null) joystick.Floating = floating;
        }

        public void ShowRunStarted(FlightMode mode)
        {
            bool skyline = mode == FlightMode.SkylineRun;
            if (modeText != null) modeText.text = skyline ? "SKYLINE RUN" : "FREE FLIGHT";
            if (progressText != null) progressText.gameObject.SetActive(skyline);
            if (timerText != null) timerText.gameObject.SetActive(skyline);
            if (scoreText != null) scoreText.gameObject.SetActive(skyline);
            if (gateArrow != null) gateArrow.gameObject.SetActive(skyline);
            if (endFreeFlightButton != null) endFreeFlightButton.SetActive(!skyline);
            ShowPause(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            _lastPassed = _lastScore = _lastSeconds = int.MinValue;
            ShowToast(skyline ? "통과할 링을 순서대로 지나가세요" : "자유롭게 비행하세요");
        }

        public void Refresh(FlightSession session, AircraftFlightController aircraft)
        {
            int speed = Mathf.RoundToInt(aircraft.SpeedKmh);
            if (speed != _lastSpeed && speedText != null)
            {
                _lastSpeed = speed;
                speedText.text = SetInt(speed, " km/h");
            }
            int alt = Mathf.RoundToInt(aircraft.Altitude);
            if (alt != _lastAltitude && altitudeText != null)
            {
                _lastAltitude = alt;
                altitudeText.text = SetInt(alt, " m");
            }
            if (boostFill != null)
            {
                boostFill.fillAmount = aircraft.BoostNormalized;
                boostFill.color = aircraft.IsBoostLocked ? boostLockedColor : boostReadyColor;
            }

            if (session.Mode == FlightMode.SkylineRun)
            {
                int total = session.Score.Total;
                if (total != _lastScore && scoreText != null)
                {
                    _lastScore = total;
                    scoreText.text = SetInt(total, " pts");
                }
                int secs = Mathf.CeilToInt(session.TimeLeft);
                if (secs != _lastSeconds && timerText != null)
                {
                    _lastSeconds = secs;
                    _sb.Clear().Append(secs / 60).Append(':').Append((secs % 60).ToString("00"));
                    timerText.text = _sb.ToString();
                }
                CheckpointCourse course = session.Course;
                if (course != null && course.Sequence != null)
                {
                    int passed = course.Sequence.PassedCount;
                    if (passed != _lastPassed && progressText != null)
                    {
                        _lastPassed = passed;
                        _sb.Clear().Append("RING ").Append(passed).Append(" / ").Append(course.Count);
                        progressText.text = _sb.ToString();
                    }
                    UpdateGateArrow(aircraft, course.NextGate);
                }
            }

            if (toastText != null && toastText.text.Length > 0 && Time.unscaledTime > _toastUntil) toastText.text = string.Empty;
        }

        public void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            _toastUntil = Time.unscaledTime + toastSeconds;
        }

        public void SetBoundaryWarning(bool show)
        {
            if (boundaryWarning != null && boundaryWarning.activeSelf != show) boundaryWarning.SetActive(show);
        }

        public void ShowPause(bool show)
        {
            if (pausePanel != null) pausePanel.SetActive(show);
        }

        public void ShowResults(RunResult r, bool newBest)
        {
            ShowPause(false);
            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultText == null) return;

            _sb.Clear();
            if (r.Mode == FlightMode.SkylineRun)
            {
                _sb.Append(r.Completed ? "COURSE COMPLETE" : "TIME UP").Append('\n');
                _sb.Append("SCORE  ").Append(r.Score.Total).Append(newBest ? "  ★ NEW BEST" : string.Empty).Append('\n');
                _sb.Append("RINGS  ").Append(r.CheckpointsPassed).Append(" / ").Append(r.CheckpointsTotal)
                   .Append("   BEST COMBO x").Append(r.Score.BestCombo + 1).Append('\n');
                _sb.Append("NEAR MISS  ").Append(r.Score.NearMisses).Append("   CRASH  ").Append(r.Score.Collisions).Append('\n');
            }
            else
            {
                _sb.Append("FREE FLIGHT").Append('\n');
            }
            _sb.Append("TIME  ").Append(FormatTime(r.FlightSeconds)).Append('\n');
            _sb.Append("DISTANCE  ").Append(Mathf.RoundToInt(r.DistanceMeters)).Append(" m").Append('\n');
            _sb.Append("TOP SPEED  ").Append(Mathf.RoundToInt(r.MaxSpeedKmh)).Append(" km/h   MAX ALT  ")
               .Append(Mathf.RoundToInt(r.MaxAltitudeMeters)).Append(" m");
            resultText.text = _sb.ToString();
        }

        private void UpdateGateArrow(AircraftFlightController aircraft, CheckpointGate next)
        {
            if (gateArrow == null || worldCamera == null) return;
            bool show = next != null;
            if (gateArrow.gameObject.activeSelf != show) gateArrow.gameObject.SetActive(show);
            if (!show) return;

            Vector3 toGate = next.transform.position - worldCamera.transform.position;
            Vector3 local = worldCamera.transform.InverseTransformDirection(toGate);
            float angle = Mathf.Atan2(local.x, local.z < 0f ? -Mathf.Abs(local.y) - 1f : local.y) * Mathf.Rad2Deg;
            gateArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (gateDistanceText != null)
                gateDistanceText.text = SetInt(Mathf.RoundToInt(Vector3.Distance(aircraft.transform.position, next.transform.position)), " m");
        }

        private string SetInt(int value, string suffix)
        {
            _sb.Clear().Append(value).Append(suffix);
            return _sb.ToString();
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
