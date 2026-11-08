using System.Collections;
using TowerGlide.App;
using TowerGlide.Cameras;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.App;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Input;
using TowerGlide.Core.Save;
using TowerGlide.Flight;
using TowerGlide.Input;
using TowerGlide.Platform;
using TowerGlide.UI;
using TowerGlide.World;
using UnityEngine;

namespace TowerGlide.Gameplay
{
    /// <summary>
    /// 비행 씬의 게임 규칙 조율자(FREE FLIGHT / SKYLINE RUN). 각 부품의 이벤트를 받아
    /// 점수·기록·복귀·일시정지·결과를 결정한다. 물리·입력·카메라·UI 내부에는 관여하지 않는다.
    /// </summary>
    public sealed class FlightSession : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private AircraftFlightController aircraft;
        [SerializeField] private AircraftCollisionHandler collisionHandler;
        [SerializeField] private NearMissSensor nearMissSensor;
        [SerializeField] private FlightInputRouter inputRouter;
        [SerializeField] private ChaseCameraController chaseCamera;
        [SerializeField] private CheckpointCourse course;
        [SerializeField] private FlightBoundary boundary;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private FlightHud hud;
        [Tooltip("충돌·링·근접 회피 파티클(선택).")]
        [SerializeField] private FeedbackFx feedbackFx;
        [Tooltip("청크 스트리밍(OW1). 있으면 시작 지점 주변 청크가 준비될 때까지 기체를 고정하고 기다린다.")]
        [SerializeField] private WorldStreamingManager streaming;
        [SerializeField] private float streamingReadyTimeout = 15f;

        [Header("Rules")]
        [Tooltip("에디터에서 이 씬을 바로 Play할 때 사용할 모드(메뉴에서 들어오면 메뉴 선택이 우선).")]
        [SerializeField] private FlightMode editorDefaultMode = FlightMode.FreeFlight;
        [SerializeField] private float skylineTimeLimitSeconds = 180f;
        [SerializeField] private float respawnDelaySeconds = 1.2f;
        [SerializeField] private float skylineRespawnTimePenalty = 3f;
        [SerializeField] private float spawnSpeed = 30f;
        [Tooltip("FREE FLIGHT 안전 복귀 지점 갱신 주기(초).")]
        [SerializeField] private float safePoseInterval = 2f;
        [SerializeField] private float safePoseClearance = 18f;

        private readonly GameStateMachine _state = new GameStateMachine(GameState.Loading);
        private ScoreTracker _score;
        private readonly NearMissTracker _nearMiss = new NearMissTracker();
        private readonly FlightRecordTracker _records = new FlightRecordTracker();
        private FlightMode _mode;
        private float _runTime;
        private float _timeLeft;
        private Pose _safePose;
        private float _safePoseTimer;
        private Vector3 _lastPosition;
        private bool _respawning;
        private RunResult _lastResult;
        private AircraftDefinition _definition;
        private bool _subscribedAircraft;

        public FlightMode Mode => _mode;
        public GameState State => _state.Current;
        public ScoreTracker Score => _score;
        public FlightRecordTracker Records => _records;
        public CheckpointCourse Course => course;
        public float TimeLeft => _timeLeft;
        public RunResult LastResult => _lastResult;
        public AircraftFlightController Aircraft => aircraft;
        public AircraftDefinition AircraftDefinition => _definition;
        public bool IsWaitingForWorld { get; private set; }

        /// <summary>
        /// 선택 기체 연결(AircraftSpawnService가 Awake에서 호출). 기체 교체는 비행 시작 전에만(애드온 명세 14.6).
        /// </summary>
        public void BindAircraft(AircraftFlightController value, AircraftDefinition definition)
        {
            UnsubscribeAircraft();
            aircraft = value;
            collisionHandler = value != null ? value.GetComponent<AircraftCollisionHandler>() : null;
            nearMissSensor = value != null ? value.GetComponentInChildren<NearMissSensor>() : null;
            _definition = definition;
            if (definition != null) spawnSpeed = definition.SpawnSpeed;
            if (isActiveAndEnabled) SubscribeAircraft();
        }

        private void SubscribeAircraft()
        {
            if (_subscribedAircraft) return;
            if (collisionHandler != null) collisionHandler.Crashed += OnCrashed;
            if (nearMissSensor != null) nearMissSensor.NearMiss += OnNearMiss;
            _subscribedAircraft = true;
        }

        private void UnsubscribeAircraft()
        {
            if (!_subscribedAircraft) return;
            if (collisionHandler != null) collisionHandler.Crashed -= OnCrashed;
            if (nearMissSensor != null) nearMissSensor.NearMiss -= OnNearMiss;
            _subscribedAircraft = false;
        }

        private void OnEnable()
        {
            SubscribeAircraft();
            if (course != null) course.GateProcessed += OnGateProcessed;
            if (inputRouter != null)
            {
                inputRouter.CameraToggleRequested += OnCameraToggle;
                inputRouter.RecoverRequested += OnRecoverRequested;
                inputRouter.PauseRequested += TogglePause;
            }
        }

        private void OnDisable()
        {
            UnsubscribeAircraft();
            if (course != null) course.GateProcessed -= OnGateProcessed;
            if (inputRouter != null)
            {
                inputRouter.CameraToggleRequested -= OnCameraToggle;
                inputRouter.RecoverRequested -= OnRecoverRequested;
                inputRouter.PauseRequested -= TogglePause;
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private IEnumerator Start()
        {
            if (aircraft == null || inputRouter == null || chaseCamera == null || spawnPoint == null)
            {
                Debug.LogError($"[{nameof(FlightSession)}] 필수 참조가 비어 있습니다. TOWERGLIDE ▸ 2. 씬 생성으로 씬을 다시 만드세요.", this);
                yield break;
            }

            GameApp app = GameApp.Instance;
            bool fromMenu = app != null && app.State.Current == GameState.Loading;
            _mode = fromMenu ? app.PendingMode : editorDefaultMode;

            // 기체-모드 호환성(14.7): 소형기 전용 링 코스를 큰 기체에 강제하지 않는다
            if (_definition != null && !AircraftRules.CanPlay(_mode, _definition.tags, out string reason))
            {
                Debug.Log($"[{nameof(FlightSession)}] {_definition.displayName}: {reason}");
                _mode = FlightMode.FreeFlight;
                if (app != null) app.PendingMode = FlightMode.FreeFlight;
            }
            if (app != null)
            {
                if (!fromMenu) app.State.TryTransition(GameState.Loading); // 에디터에서 이 씬을 바로 Play한 경우
                app.State.TryTransition(GameState.Flying);
            }

            SettingsData settings = app != null ? app.Settings : new SettingsData();
            ApplySettings(settings);

            // 청크 스트리밍(OW1): 시작 지점 주변이 로드될 때까지 기체를 고정 — 빈 도시로 출발하거나 건물을 통과하지 않게
            if (streaming != null)
            {
                IsWaitingForWorld = true;
                inputRouter.SetFlightInputEnabled(false);
                aircraft.Body.isKinematic = true;
                aircraft.ResetTo(SpawnPose.position, SpawnPose.rotation, 0f);
                hud?.ShowToast("도시 불러오는 중…");
                streaming.Begin(SpawnPose.position);
                float waited = 0f;
                while (!streaming.IsReadyAround(SpawnPose.position) && waited < streamingReadyTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (waited >= streamingReadyTimeout)
                    Debug.LogWarning($"[{nameof(FlightSession)}] 시작 지점 청크가 {streamingReadyTimeout}초 안에 준비되지 않았습니다 — 원거리 실루엣 상태로 시작합니다.");
                aircraft.Body.isKinematic = false;
                IsWaitingForWorld = false;
            }

            BeginRun();
        }

        private Pose SpawnPose => new Pose(
            spawnPoint.position + Vector3.up * (_definition != null ? _definition.spawnHeightOffset : 0f),
            spawnPoint.rotation);

        public void ApplySettings(SettingsData settings)
        {
            InputShapingSettings shaping = InputShapingSettings.Default;
            shaping.Sensitivity = settings.controlSensitivity;
            shaping.InvertPitch = settings.invertPitch;
            inputRouter.SetShaping(shaping);
            chaseCamera.SetShakeIntensity(settings.cameraShake);
            if (hud != null) hud.SetFloatingJoystick(settings.floatingJoystick);
        }

        private void BeginRun()
        {
            _score = new ScoreTracker(ScoreRules.Default);
            _nearMiss.Clear();
            _records.Reset();
            _runTime = 0f;
            _timeLeft = skylineTimeLimitSeconds;
            _respawning = false;

            bool skyline = _mode == FlightMode.SkylineRun;
            if (course != null)
            {
                course.SetCourseVisible(skyline);
                if (skyline) course.ResetCourse();
            }

            Pose start = SpawnPose;
            _safePose = start;
            aircraft.ResetTo(start.position, start.rotation, spawnSpeed);
            _lastPosition = start.position;
            nearMissSensor?.ClearAll();
            chaseCamera.SetMode(CameraMode.Chase);
            chaseCamera.SnapToTarget();

            _state.TryTransition(GameState.Flying);
            inputRouter.SetFlightInputEnabled(true);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            hud?.ShowRunStarted(_mode);
        }

        private void Update()
        {
            if (_state.Current != GameState.Flying) return;

            float dt = Time.deltaTime;
            _runTime += dt;
            Vector3 pos = aircraft.transform.position;
            _records.Tick(dt, Vector3.Distance(pos, _lastPosition), aircraft.Speed, aircraft.Altitude);
            _lastPosition = pos;

            if (_mode == FlightMode.SkylineRun)
            {
                _timeLeft -= dt;
                if (_timeLeft <= 0f)
                {
                    _timeLeft = 0f;
                    FinishRun(completed: false);
                    return;
                }
            }

            UpdateSafePose(dt);
            UpdateBoundary(pos);
            hud?.Refresh(this, aircraft);
        }

        // ---------------- 이벤트 처리 ----------------

        private void OnGateProcessed(CheckpointGate gate, CheckpointPassResult result)
        {
            if (_state.Current != GameState.Flying || _mode != FlightMode.SkylineRun) return;
            if (result != CheckpointPassResult.Passed && result != CheckpointPassResult.Completed) return;

            int points = _score.OnRingPassed(_runTime);
            GameApp.Instance?.Haptics.Play(HapticStrength.Light);
            if (feedbackFx != null) feedbackFx.PlayRingPass(gate.transform.position, gate.transform.rotation);
            hud?.ShowToast($"RING +{points}" + (_score.CurrentCombo > 0 ? $"  x{_score.CurrentCombo + 1}" : string.Empty));
            if (result == CheckpointPassResult.Completed) FinishRun(completed: true);
        }

        private void OnNearMiss(ObstacleMarker obstacle, float proximity)
        {
            if (_state.Current != GameState.Flying || _respawning) return;
            if (!_nearMiss.TryAward(obstacle.Id, _runTime)) return;
            int points = _score.OnNearMiss(proximity, aircraft.SpeedKmh);
            hud?.ShowToast($"NEAR MISS +{points}");
            hud?.FlashNearMiss(proximity);
            GameApp.Instance?.Haptics.Play(HapticStrength.Light);
            if (feedbackFx != null)
            {
                Transform t = aircraft.transform;
                feedbackFx.PlayNearMiss(t.position, t.rotation);
            }
        }

        private void OnCrashed(Vector3 point, Collider other, float impactSpeed)
        {
            if (_state.Current != GameState.Flying || _respawning) return;
            ObstacleMarker marker = other != null ? other.GetComponentInParent<ObstacleMarker>() : null;
            if (marker != null)
            {
                _nearMiss.RegisterCollision(marker.Id, _runTime);
                nearMissSensor?.DiscardObstacle(marker);
            }
            _score.OnCollision();
            chaseCamera.AddShake(Mathf.Clamp01(impactSpeed / 40f));
            if (feedbackFx != null) feedbackFx.PlayCrash(point, (aircraft.transform.position - point).normalized);
            GameApp.Instance?.Haptics.Play(HapticStrength.Heavy);
            hud?.ShowToast("CRASH");
            StartCoroutine(RespawnRoutine(penalty: true));
        }

        private void OnRecoverRequested()
        {
            if (_state.Current != GameState.Flying || _respawning) return;
            StartCoroutine(RespawnRoutine(penalty: false));
        }

        private void OnCameraToggle()
        {
            if (_state.Current == GameState.Flying) chaseCamera.ToggleMode();
        }

        // ---------------- 복귀 ----------------

        private IEnumerator RespawnRoutine(bool penalty)
        {
            _respawning = true;
            inputRouter.SetFlightInputEnabled(false);
            if (penalty && _mode == FlightMode.SkylineRun) _timeLeft = Mathf.Max(0f, _timeLeft - skylineRespawnTimePenalty);

            float wait = penalty ? respawnDelaySeconds : 0.2f;
            float t = 0f;
            while (t < wait)
            {
                if (_state.Current == GameState.Flying) t += Time.deltaTime; // 일시정지 중에는 대기
                yield return null;
            }
            if (_state.Current == GameState.Results) yield break;

            Pose pose = ResolveRespawnPose();
            aircraft.ResetTo(pose.position, pose.rotation, spawnSpeed);
            _lastPosition = pose.position;
            nearMissSensor?.ClearAll();
            chaseCamera.SnapToTarget();
            inputRouter.SetFlightInputEnabled(true);
            _respawning = false;
        }

        private Pose ResolveRespawnPose()
        {
            if (_mode == FlightMode.SkylineRun && course != null)
            {
                CheckpointGate last = course.LastPassedGate;
                return last != null ? last.RespawnPoseAfterPass : SpawnPose;
            }
            return _safePose;
        }

        /// <summary>주변이 비어 있고 충분히 높은 곳을 주기적으로 기억 → FREE FLIGHT 복귀 지점.</summary>
        private void UpdateSafePose(float dt)
        {
            _safePoseTimer += dt;
            if (_safePoseTimer < safePoseInterval || _respawning) return;
            _safePoseTimer = 0f;
            Transform t = aircraft.transform;
            if (aircraft.Altitude < safePoseClearance || aircraft.Speed < 15f) return;
            if (Mathf.Abs(aircraft.BankDeg) > 30f || Vector3.Dot(t.up, Vector3.up) < 0.7f) return;
            if (Physics.CheckSphere(t.position, safePoseClearance, ~0, QueryTriggerInteraction.Ignore))
            {
                // 기체 자신의 콜라이더도 포함되므로, 기체를 제외한 겹침이 있는지 확인
                Collider[] hits = Physics.OverlapSphere(t.position, safePoseClearance, ~0, QueryTriggerInteraction.Ignore);
                foreach (Collider c in hits)
                    if (c.attachedRigidbody != aircraft.Body) return;
            }
            Vector3 flatForward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.01f) return;
            _safePose = new Pose(t.position, Quaternion.LookRotation(flatForward.normalized, Vector3.up));
        }

        private void UpdateBoundary(Vector3 pos)
        {
            if (boundary == null) return;
            BoundaryStatus status = boundary.Evaluate(pos);
            hud?.SetBoundaryWarning(status == BoundaryStatus.Warning, boundary.DirectionToCenter(pos));
            if (status == BoundaryStatus.Outside && !_respawning)
            {
                hud?.ShowToast("RETURNING TO AREA");
                StartCoroutine(RespawnRoutine(penalty: false));
            }
        }

        // ---------------- 일시정지 / 결과 / 재시작 (UI 버튼이 호출) ----------------

        public void TogglePause()
        {
            if (_state.Current == GameState.Flying) Pause();
            else if (_state.Current == GameState.Paused) Resume();
        }

        public void Pause()
        {
            if (!_state.TryTransition(GameState.Paused)) return;
            GameApp.Instance?.State.TryTransition(GameState.Paused);
            inputRouter.SetFlightInputEnabled(false);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            hud?.ShowPause(true);
        }

        public void Resume()
        {
            if (!_state.TryTransition(GameState.Flying)) return;
            GameApp.Instance?.State.TryTransition(GameState.Flying);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (!_respawning) inputRouter.SetFlightInputEnabled(true);
            hud?.ShowPause(false);
        }

        /// <summary>FREE FLIGHT 종료 버튼 / SKYLINE RUN 완주·시간 초과.</summary>
        public void FinishRun(bool completed)
        {
            if (_state.Current != GameState.Flying && _state.Current != GameState.Paused) return;
            if (_state.Current == GameState.Paused) _state.TryTransition(GameState.Flying);
            _state.TryTransition(GameState.Results);
            GameApp.Instance?.State.TryTransition(GameState.Results);
            StopAllCoroutines();
            _respawning = false;
            inputRouter.SetFlightInputEnabled(false);

            ScoreBreakdown breakdown = _score.Finish(completed, _timeLeft);
            _lastResult = new RunResult
            {
                Mode = _mode,
                Completed = completed,
                FlightSeconds = _runTime,
                DistanceMeters = _records.DistanceMeters,
                MaxSpeedKmh = _records.MaxSpeedKmh,
                MaxAltitudeMeters = _records.MaxAltitudeMeters,
                CheckpointsPassed = course != null && course.Sequence != null ? course.Sequence.PassedCount : 0,
                CheckpointsTotal = course != null ? course.Count : 0,
                Score = breakdown,
            };

            bool newBest = false;
            GameApp app = GameApp.Instance;
            if (app != null)
            {
                newBest = app.Records.Apply(_lastResult);
                app.SaveNow();
            }

            Time.timeScale = 0f; // 결과 화면 뒤 물리 정지
            hud?.ShowResults(_lastResult, newBest);
        }

        public void EndFreeFlight() => FinishRun(completed: false);

        public void Restart()
        {
            Time.timeScale = 1f;
            GameApp app = GameApp.Instance;
            if (app != null) app.PendingMode = _mode;
            SceneFlow.LoadFlight();
        }

        public void GoToMainMenu() => SceneFlow.LoadMainMenu();

        private void OnApplicationPause(bool paused)
        {
            if (paused && _state.Current == GameState.Flying) Pause(); // 백그라운드 전환 시 자동 일시정지
        }

#if !UNITY_EDITOR
        private void OnApplicationFocus(bool focus)
        {
            // 알림 센터·전화 수신 등 포커스 상실 시 일시정지(에디터에서는 창 전환마다 멈추지 않도록 제외)
            if (!focus && _state.Current == GameState.Flying) Pause();
        }
#endif
    }
}
