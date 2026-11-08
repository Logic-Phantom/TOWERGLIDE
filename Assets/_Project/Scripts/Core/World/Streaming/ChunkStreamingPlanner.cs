using System;
using System.Collections.Generic;

namespace TowerGlide.Core.World.Streaming
{
    [Serializable]
    public struct StreamingSettings
    {
        /// <summary>현재 위치에서 이 거리(m) 안의 청크는 반드시 로드.</summary>
        public float LoadRadius;
        /// <summary>진행 방향 앞을 미리 보는 시간(초). 속도 × 시간 = 프리로드 거리.</summary>
        public float LookaheadSeconds;
        /// <summary>부스트 중 미리 보는 시간(초).</summary>
        public float BoostLookaheadSeconds;
        /// <summary>현재 위치·예측 위치 모두에서 이 거리(m)보다 멀면 언로드(LoadRadius보다 커야 함 = 히스테리시스).</summary>
        public float UnloadRadius;
        /// <summary>동시에 진행할 최대 로드 수(메인 스레드 스파이크 방지).</summary>
        public int MaxConcurrentLoads;
        /// <summary>로드됨+로딩 중 청크 상한(메모리 예산).</summary>
        public int MaxResidentChunks;
        /// <summary>로드 실패 후 재시도 대기(초), 실패 횟수만큼 배수로 늘어남(최대 8배).</summary>
        public float RetryDelaySeconds;

        public static StreamingSettings Default => new StreamingSettings
        {
            LoadRadius = 300f,
            LookaheadSeconds = 4f,
            BoostLookaheadSeconds = 6f,
            UnloadRadius = 550f,
            MaxConcurrentLoads = 2,
            MaxResidentChunks = 16,
            RetryDelaySeconds = 2f,
        };
    }

    public sealed class StreamingPlan
    {
        /// <summary>지금 로드를 시작할 청크(우선순위 순서).</summary>
        public readonly List<WorldChunkId> ToLoad = new List<WorldChunkId>();
        /// <summary>지금 언로드를 시작할 청크.</summary>
        public readonly List<WorldChunkId> ToUnload = new List<WorldChunkId>();
        /// <summary>있어야 하는 청크 전체(우선순위 순서) — 디버그·준비 상태 확인용.</summary>
        public readonly List<WorldChunkId> Wanted = new List<WorldChunkId>();
    }

    /// <summary>
    /// 스트리밍 판단(애드온 명세 5.2) — 순수 로직. Runtime의 WorldStreamingManager가 주기적으로 호출하고 결과대로 로더를 실행한다.
    /// - 현재 위치 반경 + 진행 방향 예측 위치(속도·부스트 반영) 반경 안의 청크 = Wanted.
    /// - 우선순위 = 현재 위치에서의 거리(현재 청크 0) 와 예측 위치 거리 중 작은 값 → 앞쪽 청크가 뒤쪽보다 먼저.
    /// - Loading/Loaded/Unloading 청크는 다시 요청하지 않음(중복 방지). Failed는 지수 대기 후 재시도.
    /// - 언로드: Wanted가 아니고 현재·예측 위치 모두에서 UnloadRadius 밖인 Loaded 청크. 예산 초과 시 가장 먼 비필수 청크부터.
    /// </summary>
    public static class ChunkStreamingPlanner
    {
        public static StreamingPlan Plan(WorldChunkGrid grid, ChunkStateTracker tracker, StreamingSettings s,
            float posX, float posZ, float velX, float velZ, bool boosting, float time)
        {
            var plan = new StreamingPlan();
            float look = boosting ? s.BoostLookaheadSeconds : s.LookaheadSeconds;
            float aheadX = posX + velX * look;
            float aheadZ = posZ + velZ * look;

            var wanted = new List<(WorldChunkId id, float priority)>();
            foreach (WorldChunkId id in grid.All())
            {
                float dNow = grid.DistanceTo(id, posX, posZ);
                float dAhead = grid.DistanceTo(id, aheadX, aheadZ);
                float dPath = DistanceToSegment(grid, id, posX, posZ, aheadX, aheadZ);
                if (dNow <= s.LoadRadius || dAhead <= s.LoadRadius || dPath <= s.LoadRadius * 0.5f)
                    wanted.Add((id, Math.Min(dNow, dAhead + 1f)));
            }
            wanted.Sort((a, b) => a.priority.CompareTo(b.priority));
            foreach ((WorldChunkId id, float _) in wanted) plan.Wanted.Add(id);

            // ---- 로드 ----
            int loading = tracker.CountIn(WorldChunkState.Loading);
            int resident = loading + tracker.CountIn(WorldChunkState.Loaded);
            foreach ((WorldChunkId id, float _) in wanted)
            {
                if (loading >= s.MaxConcurrentLoads) break;
                WorldChunkState st = tracker.Get(id);
                if (st == WorldChunkState.Failed)
                {
                    float wait = s.RetryDelaySeconds * Math.Min(8, 1 << Math.Min(3, tracker.FailCount(id) - 1));
                    if (time - tracker.FailedAt(id) < wait) continue;
                }
                else if (st != WorldChunkState.Unloaded)
                {
                    continue;
                }
                plan.ToLoad.Add(id);
                loading++;
                resident++;
            }

            // ---- 언로드(히스테리시스) ----
            var wantedSet = new HashSet<WorldChunkId>(plan.Wanted);
            var candidates = new List<(WorldChunkId id, float dist)>();
            foreach (WorldChunkId id in tracker.InState(WorldChunkState.Loaded))
            {
                if (wantedSet.Contains(id)) continue;
                float d = Math.Min(grid.DistanceTo(id, posX, posZ), grid.DistanceTo(id, aheadX, aheadZ));
                candidates.Add((id, d));
            }
            candidates.Sort((a, b) => b.dist.CompareTo(a.dist)); // 먼 것부터
            foreach ((WorldChunkId id, float d) in candidates)
            {
                bool farEnough = d > s.UnloadRadius;
                bool overBudget = resident > s.MaxResidentChunks;
                if (!farEnough && !overBudget) continue;
                plan.ToUnload.Add(id);
                resident--;
            }
            return plan;
        }

        /// <summary>Wanted 청크가 모두 Loaded인지(시작·복귀 전 대기용).</summary>
        public static bool IsReady(StreamingPlan plan, ChunkStateTracker tracker, int nearestCount)
        {
            int n = Math.Min(nearestCount, plan.Wanted.Count);
            for (int i = 0; i < n; i++)
                if (tracker.Get(plan.Wanted[i]) != WorldChunkState.Loaded) return false;
            return true;
        }

        private static float DistanceToSegment(WorldChunkGrid grid, WorldChunkId id, float ax, float az, float bx, float bz)
        {
            (float cx, float cz) = grid.Center(id);
            float abx = bx - ax, abz = bz - az;
            float len2 = abx * abx + abz * abz;
            float t = len2 < 1e-4f ? 0f : Math.Max(0f, Math.Min(1f, ((cx - ax) * abx + (cz - az) * abz) / len2));
            float px = ax + abx * t, pz = az + abz * t;
            return grid.DistanceTo(id, px, pz);
        }
    }
}
