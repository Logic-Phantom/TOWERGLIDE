using System;
using System.Collections.Generic;
using TowerGlide.Core.World.Streaming;
using UnityEngine;

namespace TowerGlide.World
{
    /// <summary>
    /// 원거리 도시 실루엣(애드온 명세 5.4 Skyline). 청크마다 건물 외곽 상자를 합친 저폴리 메시(충돌 없음)를 비행 씬에 상주시키고,
    /// 그 청크의 상세 씬이 로드되면 숨기고 언로드되면 다시 보인다(parent-stays-visible) → 도시가 갑자기 사라지지 않음.
    /// 로드 실패 시에도 실루엣은 남는다(fallback).
    /// </summary>
    public sealed class SkylineImpostorController : MonoBehaviour
    {
        [Serializable]
        public struct ChunkImpostor
        {
            public int x;
            public int z;
            public GameObject root;
        }

        [SerializeField] private WorldChunkLoader loader;
        [SerializeField] private ChunkImpostor[] impostors = Array.Empty<ChunkImpostor>();

        private readonly Dictionary<WorldChunkId, GameObject> _map = new Dictionary<WorldChunkId, GameObject>();

        public int VisibleCount
        {
            get
            {
                int n = 0;
                foreach (GameObject g in _map.Values)
                    if (g != null && g.activeSelf) n++;
                return n;
            }
        }

        private void Awake()
        {
            foreach (ChunkImpostor i in impostors)
                if (i.root != null) _map[new WorldChunkId(i.x, i.z)] = i.root;
        }

        private void OnEnable()
        {
            if (loader == null) return;
            loader.ChunkLoaded += OnLoaded;
            loader.ChunkUnloaded += OnUnloaded;
        }

        private void OnDisable()
        {
            if (loader == null) return;
            loader.ChunkLoaded -= OnLoaded;
            loader.ChunkUnloaded -= OnUnloaded;
        }

        private void OnLoaded(WorldChunkId id) => Set(id, false);
        private void OnUnloaded(WorldChunkId id) => Set(id, true);

        private void Set(WorldChunkId id, bool visible)
        {
            if (_map.TryGetValue(id, out GameObject g) && g != null && g.activeSelf != visible) g.SetActive(visible);
        }

        /// <summary>에디터 생성기 전용.</summary>
        public void Configure(WorldChunkLoader chunkLoader, ChunkImpostor[] list)
        {
            loader = chunkLoader;
            impostors = list;
        }
    }
}
