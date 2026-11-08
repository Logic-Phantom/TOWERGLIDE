# 서울 오픈월드 아키텍처 (Phase OW0 설계 · OW1 구현)

원본: `docs/SUPERPROMPT_ADDON_SEOUL_AIRCRAFT.md`. 범위: **OW0(조사·설계) + OW1(청크 스트리밍 POC)**만. 실제 서울 데이터 임포트(OW2)·권역 연결(OW3)은 POC가 Unity·실기기에서 검증된 뒤.

> 현재 상태(2026-11-08): 스트리밍 구조는 코드로 구현됐고 판단 로직(Core)은 CI 테스트를 통과했다. **Unity Editor·Android 실기기에서는 아직 한 번도 실행하지 않았다**(개발 PC에 Unity 없음). 도시는 실제 서울이 아니라 기존 절차적 테스트 도시다.

## 1. 기존 구조 분석 (OW0)
| 항목 | 기존(Phase 2까지) | 애드온 명세와의 관계 |
|---|---|---|
| 월드 | 단일 씬 `CityFlight`, 11×11 블록(약 1km×1km), 시드 고정 절차적 도시 | 명세 2.1 "World POC"와 같은 크기 → **청크 스트리밍 POC의 시험장으로 재사용** |
| 경계 | `FlightBoundary` 1100m 상자, 경고 → 복귀 | 명세 5.5와 같은 방향. **월드 가장자리에서만** 작동, 청크 경계에서는 아무것도 하지 않음 |
| 코스 | SKYLINE RUN 링 12개, 비행 씬에 상주 | 명세 0-5 "선택형 활동으로 유지" — 그대로 |
| 비행 물리 | 공통 컨트롤러 + `AircraftFlightTuning` | 기체 선택(14장)과 그대로 맞물림 — 변경 최소 |

### 충돌 지점과 결정
| # | 충돌 | 결정 |
|---|---|---|
| 1 | 명세 "개인·비상업 우선" ↔ 저장소 목표 "스토어 공개 배포" | 공개 배포 기준으로 라이선스 판단, 비상업·변경금지 자료 미사용(D-033) |
| 2 | 명세 "Android 우선" ↔ 기존 D-002 iOS 동시 | iOS 유지. 스트리밍·기체 코드는 플랫폼 무관 |
| 3 | 명세 OW 우선 ↔ 원 명세 "조작감 검증 전 확장 금지" | 사용자 요청으로 진행하되, **비행 물리·카메라·충돌 코드는 바꾸지 않고**(추력 정상상태 오차 보정만 D-036) POC 범위로 한정 |
| 4 | 명세 클래스 목록(`Scripts/World/*`) ↔ 기존 asmdef 계층 | 판단 로직은 Core(`Core/World/Geo`, `Core/World/Streaming`), 실행은 Runtime(`World/`). 목록 중 지금 필요한 것만 생성(명세 9 "빈 파일 금지") |
| 5 | 청크 경계 강제 복귀 금지 | 스트리밍은 플레이어를 막지 않음. 로드가 늦어도 원거리 실루엣이 보이고, 지면은 비행 씬에 상주 |

롤백: 청크 분할은 씬 생성기에서만 일어난다. `WorldStreamingManager`를 비활성화(또는 `FlightSession.streaming` 비우기)하고 이전 커밋의 `TowerglideSceneBuilder`로 씬을 다시 만들면 단일 씬 구조로 돌아간다.

## 2. 좌표계
| 항목 | 값 |
|---|---|
| 투영 | 횡축 메르카토르, **EPSG:5186 Korea 2000 / Central Belt 2010** (GRS80, 38°N·127°E, k=1, FE 200000, FN 600000) — `Core/World/Geo/TransverseMercator` |
| Unity 축 | x = 동, z = 북, y = 위, 1 unit = 1 m |
| 원점 | `SeoulWorldDefinition.Origin` = (37.5665, 126.9780) — 서울시청 부근 **계획용 대략값**. 실제 데이터 임포트 시 고정·문서화 |
| 정밀도 | 서울 전역(약 ±20km)을 원점 기준으로 두면 float 정밀도 약 2mm → 1차로 충분. 필요하면 `WorldCoordinateSystem.RebaseOffset`(floating origin) |
| 기록 규칙 | 저장·로그·데이터 파일에는 `GeoPoint`(위경도) 또는 `LocalPoint`(원점 기준 m, double)만. Unity 좌표(rebase 적용)는 저장하지 않음 |
| 검증 | CI 테스트: 원점 사상, 서울 범위 왕복 오차 < 1mm(1e-8°), 위도 1분 ≈ 1850m·경도 0.1° 거리 0.5% 이내, 축 방향 |

## 3. 청크·스트리밍 (OW1 구현)
```
[비행 씬 CityFlight — 항상 로드]
  지면 · SKYLINE 코스(링) · 경계 · 기체 · 카메라 · UI
  FlightSystems/WorldStreaming
     WorldChunkLoader        SceneManager.LoadSceneAsync(Additive) / UnloadSceneAsync, 상태 = Core ChunkStateTracker
     WorldStreamingManager   0.2초마다 Core ChunkStreamingPlanner(위치·속도·부스트) → 로드/언로드 요청
     SkylineImpostorController 청크별 원거리 실루엣(충돌 없음), 상세 청크가 로드되면 숨김
  Skyline/Skyline_<키>      건물 외곽 상자를 합친 저폴리 메시
[청크 씬 Scenes/Chunks/Chunk_<키>.unity — 필요할 때만]
  건물(아키타입 모듈·LOD·콜라이더) · 장애물(크레인·드론·광고판·케이블·안테나·기구·스카이브리지)
```

| 항목 | 초기값 | 근거·조정 계획 |
|---|---|---|
| 청크 크기 | **250m**, 4×4 = 16개(-500..500m) | 명세 후보값. 테스트 도시 한 블록(70m)의 3.5배, 최고 속도 170m/s(제트 부스트)에서 1.5초에 한 청크 |
| 로드 반경 | 300m(현재 위치·예측 위치 모두) | 근거리 + 인접 청크 |
| 예측 | 속도 × 4초(부스트 6초) | 앞쪽 청크 우선. 제트 부스트 시 약 1km 앞까지 |
| 언로드 반경 | 550m(히스테리시스) | 경계에서 로드/언로드 깜빡임 방지 |
| 동시 로드 | 2 | 메인 스레드 스파이크 방지 |
| 상주 상한 | 16 | 메모리 예산(실측 후 조정). 초과 시 필요 없는 먼 청크부터 언로드 |
| 실패 처리 | Failed → 2·4·8·16초 지수 대기 후 재시도, 그동안 실루엣 유지 | 추락·정지 없음(fallback) |
| 시작 | 시작 지점 근처 4개 청크가 로드될 때까지 기체 고정(최대 15초) | 빈 도시로 출발하거나 건물을 통과하지 않게 |

## 4. LOD (명세 5.4 대비)
| 단계 | 명세 예시 | 현재 구현 |
|---|---|---|
| LOD0 | 0~250m 상세 | 건물 LODGroup LOD0(본체+옥상 설비·경고등), 충돌 포함 |
| LOD1 | 250~700m | LODGroup LOD1(본체만), 화면 점유율 기준 |
| LOD2 | 700~1500m | (미구현 — 청크 언로드 + 실루엣이 대신) |
| Skyline | 1500m+ | 청크별 실루엣 메시(청크 상세가 없을 때 항상 보임) |

## 5. 다음 단계
- **OW1 검증(지금 필요)**: Unity에서 씬 생성 → PlayMode `OpenWorldFlowPlayModeTests` → 최고 속도+부스트로 청크 연속 통과 스트레스 비행 → `docs/SEOUL_WORLD_PERFORMANCE.md` 기록.
- OW2: 데이터 라이선스 확정(`docs/SEOUL_WORLD_DATA_SOURCES.md`) → 한 권역(우선 후보: 여의도·한강 — 강·교량·고층의 대비가 비행에 좋음)만 임포트 → `SeoulWorldDataImporter`, `SeoulChunkBuilder`, `SeoulWorldValidator`, `SeoulWorldLicenseReportBuilder`.
- 그 뒤: 권역 진입 라벨·지도(2D 정적), 랜드마크 레지스트리, 지형 높이 샘플러.
