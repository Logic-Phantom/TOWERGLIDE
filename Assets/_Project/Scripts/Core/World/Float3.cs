using System;

namespace TowerGlide.Core.World
{
    /// <summary>Core 전용 최소 3D 벡터(UnityEngine.Vector3 대체). 1 unit = 1 m, Y = 위.</summary>
    [Serializable]
    public readonly struct Float3 : IEquatable<Float3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator *(Float3 a, float s) => new Float3(a.X * s, a.Y * s, a.Z * s);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public float HorizontalLength => (float)Math.Sqrt(X * X + Z * Z);

        public static float Distance(Float3 a, Float3 b) => (a - b).Length;

        public bool Equals(Float3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Float3 o && Equals(o);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString() => $"({X:0.#}, {Y:0.#}, {Z:0.#})";
    }
}
