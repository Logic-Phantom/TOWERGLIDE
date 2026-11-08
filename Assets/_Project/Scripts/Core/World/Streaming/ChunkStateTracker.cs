using System.Collections.Generic;

namespace TowerGlide.Core.World.Streaming
{
    public enum WorldChunkState
    {
        Unloaded,
        Loading,
        Loaded,
        Unloading,
        Failed,
    }

    /// <summary>
    /// 청크별 상태 기계(애드온 명세 9: WorldChunkState). 허용된 전이만 통과시켜 중복 로드·언로드를 막는다.
    /// Unloaded → Loading → Loaded → Unloading → Unloaded, Loading → Failed → (재시도 대기 후) Loading.
    /// </summary>
    public sealed class ChunkStateTracker
    {
        private sealed class Entry
        {
            public WorldChunkState State;
            public float FailedAt;
            public int FailCount;
        }

        private readonly Dictionary<WorldChunkId, Entry> _entries = new Dictionary<WorldChunkId, Entry>();

        public WorldChunkState Get(WorldChunkId id) => _entries.TryGetValue(id, out Entry e) ? e.State : WorldChunkState.Unloaded;

        public int FailCount(WorldChunkId id) => _entries.TryGetValue(id, out Entry e) ? e.FailCount : 0;

        public float FailedAt(WorldChunkId id) => _entries.TryGetValue(id, out Entry e) ? e.FailedAt : float.NegativeInfinity;

        public int CountIn(WorldChunkState state)
        {
            int n = 0;
            foreach (Entry e in _entries.Values)
                if (e.State == state) n++;
            return n;
        }

        public IEnumerable<WorldChunkId> InState(WorldChunkState state)
        {
            foreach (KeyValuePair<WorldChunkId, Entry> kv in _entries)
                if (kv.Value.State == state) yield return kv.Key;
        }

        public bool BeginLoad(WorldChunkId id)
        {
            WorldChunkState s = Get(id);
            if (s != WorldChunkState.Unloaded && s != WorldChunkState.Failed) return false;
            Set(id, WorldChunkState.Loading);
            return true;
        }

        public bool CompleteLoad(WorldChunkId id)
        {
            if (Get(id) != WorldChunkState.Loading) return false;
            Entry e = Set(id, WorldChunkState.Loaded);
            e.FailCount = 0;
            return true;
        }

        public bool FailLoad(WorldChunkId id, float time)
        {
            if (Get(id) != WorldChunkState.Loading) return false;
            Entry e = Set(id, WorldChunkState.Failed);
            e.FailedAt = time;
            e.FailCount++;
            return true;
        }

        public bool BeginUnload(WorldChunkId id)
        {
            if (Get(id) != WorldChunkState.Loaded) return false;
            Set(id, WorldChunkState.Unloading);
            return true;
        }

        public bool CompleteUnload(WorldChunkId id)
        {
            if (Get(id) != WorldChunkState.Unloading) return false;
            Set(id, WorldChunkState.Unloaded);
            return true;
        }

        private Entry Set(WorldChunkId id, WorldChunkState state)
        {
            if (!_entries.TryGetValue(id, out Entry e))
            {
                e = new Entry();
                _entries[id] = e;
            }
            e.State = state;
            return e;
        }
    }
}
