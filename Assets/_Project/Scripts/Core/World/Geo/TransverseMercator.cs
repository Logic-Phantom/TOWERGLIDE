using System;

namespace TowerGlide.Core.World.Geo
{
    /// <summary>위경도(도). WGS84/GRS80 타원체 기준(두 타원체 차이는 게임 스케일에서 무시 가능, 1mm 미만).</summary>
    [Serializable]
    public readonly struct GeoPoint
    {
        public readonly double Latitude;
        public readonly double Longitude;

        public GeoPoint(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public override string ToString() => $"({Latitude:0.000000}, {Longitude:0.000000})";
    }

    /// <summary>투영 좌표(m). Easting = 동쪽, Northing = 북쪽.</summary>
    [Serializable]
    public readonly struct ProjectedPoint
    {
        public readonly double Easting;
        public readonly double Northing;

        public ProjectedPoint(double easting, double northing)
        {
            Easting = easting;
            Northing = northing;
        }
    }

    /// <summary>
    /// 횡축 메르카토르(TM) 투영 — Snyder(1987) 급수식. 위경도를 거리·각도 계산이 가능한 평면 미터 좌표로 바꾼다(애드온 명세 4.1).
    /// 기본 매개변수는 EPSG:5186(Korea 2000 / Central Belt 2010): GRS80, 원점 위도 38°, 중앙 경선 127°, 축척 1.0, FE 200000, FN 600000.
    /// 국내 공공 공간정보에서 널리 쓰이는 좌표계라 서울 데이터와 직접 맞출 수 있다(데이터별 CRS는 반드시 확인).
    /// </summary>
    public sealed class TransverseMercator
    {
        private readonly double _a;
        private readonly double _e2;
        private readonly double _ep2;
        private readonly double _k0;
        private readonly double _lat0;
        private readonly double _lon0;
        private readonly double _fe;
        private readonly double _fn;
        private readonly double _m0;

        public TransverseMercator(double semiMajorAxis, double inverseFlattening, double originLatitudeDeg, double centralMeridianDeg,
            double scaleFactor, double falseEasting, double falseNorthing, string name)
        {
            double f = 1.0 / inverseFlattening;
            _a = semiMajorAxis;
            _e2 = f * (2.0 - f);
            _ep2 = _e2 / (1.0 - _e2);
            _k0 = scaleFactor;
            _lat0 = ToRad(originLatitudeDeg);
            _lon0 = ToRad(centralMeridianDeg);
            _fe = falseEasting;
            _fn = falseNorthing;
            _m0 = MeridianArc(_lat0);
            Name = name;
        }

        public string Name { get; }

        /// <summary>EPSG:5186 Korea 2000 / Central Belt 2010.</summary>
        public static TransverseMercator Korea2000CentralBelt2010() =>
            new TransverseMercator(6378137.0, 298.257222101, 38.0, 127.0, 1.0, 200000.0, 600000.0, "EPSG:5186");

        public ProjectedPoint Forward(GeoPoint p)
        {
            double phi = ToRad(p.Latitude);
            double lambda = ToRad(p.Longitude);
            double sin = Math.Sin(phi), cos = Math.Cos(phi), tan = Math.Tan(phi);
            double n = _a / Math.Sqrt(1.0 - _e2 * sin * sin);
            double t = tan * tan;
            double c = _ep2 * cos * cos;
            double aa = (lambda - _lon0) * cos;
            double m = MeridianArc(phi);

            double a2 = aa * aa, a3 = a2 * aa, a4 = a3 * aa, a5 = a4 * aa, a6 = a5 * aa;
            double x = _fe + _k0 * n * (aa + (1 - t + c) * a3 / 6.0 + (5 - 18 * t + t * t + 72 * c - 58 * _ep2) * a5 / 120.0);
            double y = _fn + _k0 * (m - _m0 + n * tan * (a2 / 2.0 + (5 - t + 9 * c + 4 * c * c) * a4 / 24.0
                                                         + (61 - 58 * t + t * t + 600 * c - 330 * _ep2) * a6 / 720.0));
            return new ProjectedPoint(x, y);
        }

        public GeoPoint Inverse(ProjectedPoint p)
        {
            double e4 = _e2 * _e2, e6 = e4 * _e2;
            double m = _m0 + (p.Northing - _fn) / _k0;
            double mu = m / (_a * (1 - _e2 / 4.0 - 3 * e4 / 64.0 - 5 * e6 / 256.0));
            double sq = Math.Sqrt(1 - _e2);
            double e1 = (1 - sq) / (1 + sq);
            double e12 = e1 * e1, e13 = e12 * e1, e14 = e13 * e1;
            double phi1 = mu + (3 * e1 / 2 - 27 * e13 / 32) * Math.Sin(2 * mu)
                             + (21 * e12 / 16 - 55 * e14 / 32) * Math.Sin(4 * mu)
                             + (151 * e13 / 96) * Math.Sin(6 * mu)
                             + (1097 * e14 / 512) * Math.Sin(8 * mu);

            double sin1 = Math.Sin(phi1), cos1 = Math.Cos(phi1), tan1 = Math.Tan(phi1);
            double c1 = _ep2 * cos1 * cos1;
            double t1 = tan1 * tan1;
            double n1 = _a / Math.Sqrt(1 - _e2 * sin1 * sin1);
            double r1 = _a * (1 - _e2) / Math.Pow(1 - _e2 * sin1 * sin1, 1.5);
            double d = (p.Easting - _fe) / (n1 * _k0);
            double d2 = d * d, d3 = d2 * d, d4 = d3 * d, d5 = d4 * d, d6 = d5 * d;

            double phi = phi1 - (n1 * tan1 / r1) * (d2 / 2
                                                   - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * _ep2) * d4 / 24
                                                   + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * _ep2 - 3 * c1 * c1) * d6 / 720);
            double lambda = _lon0 + (d - (1 + 2 * t1 + c1) * d3 / 6
                                     + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * _ep2 + 24 * t1 * t1) * d5 / 120) / cos1;
            return new GeoPoint(ToDeg(phi), ToDeg(lambda));
        }

        private double MeridianArc(double phi)
        {
            double e4 = _e2 * _e2, e6 = e4 * _e2;
            return _a * ((1 - _e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * phi
                         - (3 * _e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * phi)
                         + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * phi)
                         - (35 * e6 / 3072) * Math.Sin(6 * phi));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
        private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
    }
}
