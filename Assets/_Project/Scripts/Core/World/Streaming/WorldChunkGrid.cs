using System;
using System.Collections.Generic;

namespace TowerGlide.Core.World.Streaming
{
    /// <summary>청크 고유 인덱스(격자 X/Z). 저장·로그·씬 이름에 쓰는 안정적인 ID.</summary>
    [Serializable]
    public readonly struct WorldChunkId : IEquatable<WorldChunkId>
    {
        public readonly int X;
        public readonly int Z;

        public WorldChunkId(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>씬·에셋 이름용 키. 예: "x-2_z+1".</summary>
        public string Key => $"x{X:+0;-0;+0}_z{Z:+0;-0;+0}";

        public bool Equals(WorldChunkId other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldChunkId o && Equals(o);
        public override int GetHashCode() => X * 73856093 ^ Z * 19349663;
        public static bool operator ==(WorldChunkId a, WorldChunkId b) => a.Equals(b);
        public static bool operator !=(WorldChunkId a, WorldChunkId b) => !a.Equals(b);
        public override string ToString() => Key;
    }

    /// <summary>
    /// 월드를 같은 크기의 정사각 청크로 나눈다(애드온 명세 5.1). 좌표는 월드 로컬 m(x = 동, z = 북).
    /// 청크 (0,0)의 남서 모서리 = (0,0). 격자 범위 밖 청크는 존재하지 않는다(월드 경계).
    /// </summary>
    public sealed class WorldChunkGrid
    {
        public WorldChunkGrid(float chunkSize, int minX, int minZ, int maxX, int maxZ)
        {
            if (chunkSize <= 0f) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (maxX < minX || maxZ < minZ) throw new ArgumentException("Empty grid");
            ChunkSize = chunkSize;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public float ChunkSize { get; }
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }
        public int Count => (MaxX - MinX + 1) * (MaxZ - MinZ + 1);

        /// <summary>월드 로컬 위치가 속한 청크(격자 밖이어도 계산은 한다 — Contains로 확인).</summary>
        public WorldChunkId IdAt(float x, float z) =>
            new WorldChunkId((int)Math.Floor(x / ChunkSize), (int)Math.Floor(z / ChunkSize));

        public bool Contains(WorldChunkId id) => id.X >= MinX && id.X <= MaxX && id.Z >= MinZ && id.Z <= MaxZ;

        public (float minX, float minZ, float maxX, float maxZ) Bounds(WorldChunkId id) =>
            (id.X * ChunkSize, id.Z * ChunkSize, (id.X + 1) * ChunkSize, (id.Z + 1) * ChunkSize);

        public (float x, float z) Center(WorldChunkId id) => ((id.X + 0.5f) * ChunkSize, (id.Z + 0.5f) * ChunkSize);

        /// <summary>점에서 청크 사각형까지 최단 거리(안에 있으면 0).</summary>
        public float DistanceTo(WorldChunkId id, float x, float z)
        {
            (float minX, float minZ, float maxX, float maxZ) = Bounds(id);
            float dx = Math.Max(Math.Max(minX - x, 0f), x - maxX);
            float dz = Math.Max(Math.Max(minZ - z, 0f), z - maxZ);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public IEnumerable<WorldChunkId> All()
        {
            for (int x = MinX; x <= MaxX; x++)
            for (int z = MinZ; z <= MaxZ; z++)
                yield return new WorldChunkId(x, z);
        }
    }
}
