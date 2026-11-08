using System.Text;
using TowerGlide.App;
using TowerGlide.Core.App;
using TowerGlide.Core.Save;
using TowerGlide.Core.World.Streaming;
using TowerGlide.Flight;
using TowerGlide.Gameplay;
using UnityEngine;

namespace TowerGlide.World
{
    /// <summary>
    /// 청크 스트리밍 조율(애드온 명세 5.2). 0.2초마다 기체 위치·속도·부스트로 Core ChunkStreamingPlanner를 돌려
    /// 로드/언로드를 요청한다. 판단은 Core(테스트됨), 실행은 WorldChunkLoader.
    /// 청크 경계는 게임 경계가 아니다 — 플레이어를 막거나 되돌리지 않는다(경계 처리는 FlightBoundary가 월드 가장자리에서만).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class WorldStreamingManager : MonoBehaviour
    {
        [SerializeField] private WorldChunkLoader loader;
        [SerializeField] private FlightSession session;
        [SerializeField] private float planInterval = 0.2f;
        [SerializeField] private float loadRadius = 300f;
        [SerializeField] private float lookaheadSeconds = 4f;
        [SerializeField] private float boostLookaheadSeconds = 6f;
        [SerializeField] private float unloadRadius = 550f;
        [SerializeField] private int maxConcurrentLoads = 2;
        [SerializeField] private int maxResidentChunks = 16;
        [SerializeField] private float retryDelaySeconds = 2f;
        [Tooltip("시작 시 준비돼야 하는 가장 가까운 청크 수.")]
        [SerializeField] private int readyChunkCount = 4;

        private WorldChunkGrid _grid;
        private StreamingSettings _settings;
        private float _timer;
        private bool _started;
        private StreamingPlan _lastPlan;
        private readonly StringBuilder _sb = new StringBuilder(256);

        public WorldChunkGrid Grid => _grid;
        public WorldChunkLoader Loader => loader;
        public StreamingPlan LastPlan => _lastPlan;

        private void Awake()
        {
            if (loader == null || loader.Catalog == null)
            {
                Debug.LogError($"[{nameof(WorldStreamingManager)}] loader/catalog 미연결 — 스트리밍 비활성", this);
                enabled = false;
                return;
            }
            _grid = loader.Catalog.CreateGrid();
            _settings = new StreamingSettings
            {
                LoadRadius = loadRadius,
                LookaheadSeconds = lookaheadSeconds,
                BoostLookaheadSeconds = boostLookaheadSeconds,
                UnloadRadius = Mathf.Max(unloadRadius, loadRadius + 100f),
                MaxConcurrentLoads = Mathf.Max(1, maxConcurrentLoads),
                // 그래픽 단계별 메모리 예산(저사양일수록 적게 상주) — 인스펙터 값이 상한
                MaxResidentChunks = Mathf.Max(4, Mathf.Min(maxResidentChunks,
                    QualityAutoSelector.MaxResidentChunks(GameApp.Instance != null ? (QualityTier)GameApp.Instance.Settings.qualityTier : QualityTier.Quality))),
                RetryDelaySeconds = retryDelaySeconds,
            };
        }

        /// <summary>시작 위치 기준으로 즉시 로드를 시작한다(FlightSession이 호출).</summary>
        public void Begin(Vector3 position)
        {
            if (!enabled) return;
            _started = true;
            Step(position, Vector3.zero, false);
        }

        /// <summary>해당 위치 주변 청크가 로드됐는지(시작·디버그용).</summary>
        public bool IsReadyAround(Vector3 position)
        {
            if (!enabled) return true;
            StreamingPlan p = ChunkStreamingPlanner.Plan(_grid, loader.Tracker, _settings, position.x, position.z, 0f, 0f, false, Time.time);
            if (p.ToLoad.Count > 0) Execute(p);
            return ChunkStreamingPlanner.IsReady(p, loader.Tracker, readyChunkCount);
        }

        private void Update()
        {
            if (!_started) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer < planInterval) return;
            _timer = 0f;

            AircraftFlightController a = session != null ? session.Aircraft : null;
            if (a == null) return;
            Vector3 v = a.Body != null && !a.Body.isKinematic ? a.Body.linearVelocity : Vector3.zero;
            Step(a.transform.position, v, a.IsBoosting);
        }

        private void Step(Vector3 position, Vector3 velocity, bool boosting)
        {
            _lastPlan = ChunkStreamingPlanner.Plan(_grid, loader.Tracker, _settings, position.x, position.z, velocity.x, velocity.z, boosting, Time.time);
            Execute(_lastPlan);
        }

        private void Execute(StreamingPlan plan)
        {
            foreach (WorldChunkId id in plan.ToUnload) loader.RequestUnload(id);
            foreach (WorldChunkId id in plan.ToLoad) loader.RequestLoad(id);
        }

        /// <summary>개발용 스트리밍 디버그 문자열(애드온 명세 8 Streaming Debug).</summary>
        public string DebugSummary(Vector3 position)
        {
            if (!enabled || _grid == null) return "streaming off";
            ChunkStateTracker t = loader.Tracker;
            WorldChunkId cur = _grid.IdAt(position.x, position.z);
            _sb.Clear()
                .Append("CHUNK ").Append(cur.Key).Append(' ').Append(t.Get(cur))
                .Append("  loaded ").Append(t.CountIn(WorldChunkState.Loaded))
                .Append(" loading ").Append(t.CountIn(WorldChunkState.Loading))
                .Append(" failed ").Append(t.CountIn(WorldChunkState.Failed))
                .Append("  load ").Append(loader.LastLoadMilliseconds.ToString("0")).Append("ms (max ")
                .Append(loader.MaxLoadMilliseconds.ToString("0")).Append(")  mem ")
                .Append((System.GC.GetTotalMemory(false) / (1024 * 1024)).ToString()).Append("MB managed");
            return _sb.ToString();
        }
    }
}
