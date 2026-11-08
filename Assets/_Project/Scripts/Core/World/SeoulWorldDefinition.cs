using System.Collections.Generic;
using TowerGlide.Core.World.Geo;
using TowerGlide.Core.World.Streaming;

namespace TowerGlide.Core.World
{
    public enum DistrictDataStatus
    {
        /// <summary>계획만 있음. 실제 데이터·모델 없음.</summary>
        Planned,
        /// <summary>절차적 테스트 데이터로만 구성(실제 서울 데이터 아님).</summary>
        ProceduralTest,
        /// <summary>라이선스가 확인된 실제 데이터로 임포트됨.</summary>
        Imported,
    }

    /// <summary>
    /// 권역 정의(애드온 명세 2.2). 중심 좌표는 **계획용 대략값**이며 실제 데이터 임포트 전에는 위치 근거로 쓰지 않는다(CoordinatesVerified = false).
    /// </summary>
    public sealed class SeoulDistrictDefinition
    {
        public SeoulDistrictDefinition(string id, string nameKo, string nameEn, string theme, GeoPoint approxCenter, double radiusMeters,
            DistrictDataStatus status, bool coordinatesVerified)
        {
            Id = id;
            NameKo = nameKo;
            NameEn = nameEn;
            Theme = theme;
            ApproxCenter = approxCenter;
            RadiusMeters = radiusMeters;
            Status = status;
            CoordinatesVerified = coordinatesVerified;
        }

        public string Id { get; }
        public string NameKo { get; }
        public string NameEn { get; }
        public string Theme { get; }
        public GeoPoint ApproxCenter { get; }
        public double RadiusMeters { get; }
        public DistrictDataStatus Status { get; }
        public bool CoordinatesVerified { get; }
    }

    /// <summary>
    /// 서울 월드 설정(애드온 명세 4.1, 9): 원점·CRS·청크 크기·플레이 범위·권역.
    /// 현재 단계(OW1)의 플레이 범위는 기존 테스트 도시(약 1km × 1km)이며 실제 서울 데이터는 없다.
    /// </summary>
    public sealed class SeoulWorldDefinition
    {
        public SeoulWorldDefinition(string id, GeoPoint origin, float chunkSize, int minChunkX, int minChunkZ, int maxChunkX, int maxChunkZ,
            IReadOnlyList<SeoulDistrictDefinition> districts)
        {
            Id = id;
            Origin = origin;
            ChunkSize = chunkSize;
            Grid = new WorldChunkGrid(chunkSize, minChunkX, minChunkZ, maxChunkX, maxChunkZ);
            Coordinates = new WorldCoordinateSystem(TransverseMercator.Korea2000CentralBelt2010(), origin);
            Districts = districts;
        }

        public string Id { get; }
        public GeoPoint Origin { get; }
        public float ChunkSize { get; }
        public WorldChunkGrid Grid { get; }
        public WorldCoordinateSystem Coordinates { get; }
        public IReadOnlyList<SeoulDistrictDefinition> Districts { get; }

        /// <summary>
        /// OW1 스트리밍 POC: 기존 테스트 도시(-500..500m)를 250m 청크 4×4로 나눈 월드.
        /// 원점은 계획용으로 서울시청 부근의 대략 좌표를 둔다 — 테스트 도시는 실제 지리와 무관한 절차적 데이터다.
        /// </summary>
        public static SeoulWorldDefinition StreamingPoc() => new SeoulWorldDefinition(
            "seoul-poc",
            new GeoPoint(37.5665, 126.9780),
            250f, -2, -2, 1, 1,
            PlannedDistricts());

        /// <summary>
        /// 우선 제작 권역(애드온 명세 2.2). 좌표는 공개적으로 널리 알려진 지점의 대략값(소수 3자리 수준)으로 계획 전용이다.
        /// 실제 임포트 시 라이선스가 확인된 데이터에서 다시 정한다(docs/SEOUL_WORLD_DATA_SOURCES.md).
        /// </summary>
        public static IReadOnlyList<SeoulDistrictDefinition> PlannedDistricts() => new[]
        {
            new SeoulDistrictDefinition("yeouido-hangang", "여의도·한강", "Yeouido & Hangang", "강·교량·고층 업무지구",
                new GeoPoint(37.525, 126.925), 1500, DistrictDataStatus.Planned, false),
            new SeoulDistrictDefinition("gangnam-teheran", "강남·테헤란로", "Gangnam & Teheran-ro", "고층 밀집·넓은 간선도로",
                new GeoPoint(37.503, 127.040), 1500, DistrictDataStatus.Planned, false),
            new SeoulDistrictDefinition("jongno-namsan", "종로·광화문·남산", "Jongno, Gwanghwamun & Namsan", "전통·현대 혼합, 산지",
                new GeoPoint(37.565, 126.985), 1800, DistrictDataStatus.Planned, false),
            new SeoulDistrictDefinition("jamsil", "잠실", "Jamsil", "대형 랜드마크·강·공원",
                new GeoPoint(37.512, 127.100), 1500, DistrictDataStatus.Planned, false),
            new SeoulDistrictDefinition("hongdae-mapo", "홍대·마포", "Hongdae & Mapo", "저층 밀집·골목",
                new GeoPoint(37.555, 126.923), 1200, DistrictDataStatus.Planned, false),
            new SeoulDistrictDefinition("poc-test-city", "테스트 도시", "Test City", "절차적 테스트 데이터(실제 서울 아님)",
                new GeoPoint(37.5665, 126.9780), 500, DistrictDataStatus.ProceduralTest, false),
        };
    }
}
