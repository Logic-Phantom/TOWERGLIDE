using System;

namespace TowerGlide.Core.World.Geo
{
    /// <summary>
    /// 지리 좌표 ↔ Unity 월드 좌표 변환의 단일 창구(애드온 명세 4.1).
    /// - 위경도를 Unity에 직접 넣지 않는다: TM 투영(m) → 고정 원점을 뺀 로컬 좌표(x = 동, z = 북, 1 unit = 1 m).
    /// - 서울 전체를 큰 절대 좌표로 두지 않도록 원점을 월드 중심 근처에 둔다(±20km → float 정밀도 약 2mm).
    /// - 장래 floating origin: <see cref="RebaseOffset"/>만큼 Unity 좌표를 옮겨 플레이어 주변을 원점 가까이 유지할 수 있다.
    /// 저장·로그에는 항상 <see cref="LocalPoint"/>(원점 기준, rebase 전) 또는 GeoPoint를 쓰고 Unity 좌표를 쓰지 않는다.
    /// </summary>
    public sealed class WorldCoordinateSystem
    {
        private readonly TransverseMercator _projection;
        private readonly ProjectedPoint _originProjected;

        public WorldCoordinateSystem(TransverseMercator projection, GeoPoint origin)
        {
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
            Origin = origin;
            _originProjected = projection.Forward(origin);
        }

        public GeoPoint Origin { get; }
        public string CrsName => _projection.Name;

        /// <summary>floating origin 이동량(로컬 m). 기본 0.</summary>
        public LocalPoint RebaseOffset { get; set; }

        public LocalPoint GeoToLocal(GeoPoint p)
        {
            ProjectedPoint q = _projection.Forward(p);
            return new LocalPoint(q.Easting - _originProjected.Easting, q.Northing - _originProjected.Northing);
        }

        public GeoPoint LocalToGeo(LocalPoint p) =>
            _projection.Inverse(new ProjectedPoint(p.X + _originProjected.Easting, p.Z + _originProjected.Northing));

        /// <summary>로컬(원점 기준) → Unity 위치(rebase 적용, float).</summary>
        public Float3 LocalToUnity(LocalPoint p, float height) =>
            new Float3((float)(p.X - RebaseOffset.X), height, (float)(p.Z - RebaseOffset.Z));

        public LocalPoint UnityToLocal(float x, float z) => new LocalPoint(x + RebaseOffset.X, z + RebaseOffset.Z);
    }

    /// <summary>월드 원점 기준 로컬 평면 좌표(m, double). X = 동, Z = 북.</summary>
    [Serializable]
    public readonly struct LocalPoint
    {
        public readonly double X;
        public readonly double Z;

        public LocalPoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double DistanceTo(LocalPoint o)
        {
            double dx = X - o.X, dz = Z - o.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public override string ToString() => $"(E{X:0.0}, N{Z:0.0})";
    }
}
