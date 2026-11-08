using System;
using TowerGlide.Core.World.Streaming;
using UnityEngine;

namespace TowerGlide.World
{
    /// <summary>
    /// 청크 ID → 청크 씬 경로(애드온 명세 9 WorldChunkCatalog). 에디터의 씬 생성기가 만든다.
    /// 씬이 없는 청크(공터·격자 가장자리)는 목록에 없고, 로더는 즉시 "로드됨"으로 처리한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TOWERGLIDE/World Chunk Catalog", fileName = "WorldChunkCatalog")]
    public sealed class WorldChunkCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int x;
            public int z;
            [Tooltip("Build Settings에 등록된 씬 경로(Assets/...unity).")]
            public string scenePath;
            public int buildingCount;
        }

        [SerializeField] private float chunkSize = 250f;
        [SerializeField] private int minX = -2;
        [SerializeField] private int minZ = -2;
        [SerializeField] private int maxX = 1;
        [SerializeField] private int maxZ = 1;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public float ChunkSize => chunkSize;
        public Entry[] Entries => entries;

        public WorldChunkGrid CreateGrid() => new WorldChunkGrid(chunkSize, minX, minZ, maxX, maxZ);

        public bool TryGetScene(WorldChunkId id, out string scenePath)
        {
            foreach (Entry e in entries)
            {
                if (e.x != id.X || e.z != id.Z) continue;
                scenePath = e.scenePath;
                return !string.IsNullOrEmpty(scenePath);
            }
            scenePath = null;
            return false;
        }

        /// <summary>에디터 생성기 전용.</summary>
        public void Configure(float size, int gridMinX, int gridMinZ, int gridMaxX, int gridMaxZ, Entry[] chunkEntries)
        {
            chunkSize = size;
            minX = gridMinX;
            minZ = gridMinZ;
            maxX = gridMaxX;
            maxZ = gridMaxZ;
            entries = chunkEntries;
        }
    }
}
