# 기체 라인업·선택 시스템 (Phase AC0 · AC1)

원본: `docs/SUPERPROMPT_ADDON_SEOUL_AIRCRAFT.md` 14장. 모든 기체명·외형은 **가상**이며 실제 제조사·항공사·군 표식을 쓰지 않는다. 성능 값은 **게임 내 밸런스 수치**다(실제 항공기 제원 아님).

## AC0 — 기존 구조 분석
- `AircraftFlightController`(공통 Rigidbody 물리) + `AircraftFlightTuning`(ScriptableObject)이 이미 분리돼 있었다 → **컨트롤러는 그대로 재사용**, 기종별 차이는 튜닝 데이터만.
- 추가로 바꾼 것: 기종별 질량·관성 배율·부스트 사용 여부 필드, 순항 속도를 정확히 유지하는 항력 보상 추력(D-036). 입력·카메라·충돌 구조는 그대로.
- HANGAR 메뉴는 "준비 중"이었다 → 실제 선택 화면으로 구현.

## 구조
```
Core/Aircraft   AircraftIds(저장용 고정 ID) · AircraftCategory · AircraftTags · UnlockRule
                AircraftRules(해금·모드 호환성·선택 복구) · AircraftStats(상대 성능 막대)
Core/Save       SaveData v2: aircraft.selectedAircraftId, 누적 거리·시간·SKYLINE 완주 수
Runtime/Flight  AircraftDefinition(SO) · AircraftCatalog(SO, Resources) · AircraftCameraProfile
                AircraftSelectionService(GameApp 소유) · AircraftSpawnService(비행 씬 시작 시 스폰·연결)
Runtime/UI      HangarController · AircraftPreviewController
Editor          TowerglideAircraftFactory(튜닝 에셋·프리팹·정의·카탈로그)
```
새 기체 추가 = `AircraftIds`에 ID → 튜닝 에셋 + 프리팹 + `AircraftDefinition` → 카탈로그 등록. 코드 분기 추가 없음.

## 라인업 (2026-11-08)
| ID | 이름 | 분류 | 상태 | 크기(길이·날개폭) | 태그 | 비고 |
|---|---|---|---|---|---|---|
| `scout_p1` | Scout P-1 | 소형 프로펠러기 | ✅ 선택 가능 | 7.5 · 8.4m | TightRoute | 기본 기체, SKYLINE RUN 가능 |
| `viper_f9` | Viper F-9 | 전투기형 제트 | ✅ 선택 가능 | 15 · 10m | HighSpeed, WideAirspace | 무장 없음. 스폰 +60m |
| `cityliner_180` | Cityliner 180 | 중형 여객기 | ✅ 선택 가능 | 38 · 34m | WideAirspace | 부스트 없음. 스폰 +260m(고층 위) |
| `aero_s2` | Aero S-2 | 곡예기 | 개발 예정 | 6.5 · 7.5m | TightRoute, Aerobatics | |
| `metro_70` | Metro 70 | 지역 여객기 | 개발 예정 | 27 · 27m | WideAirspace | |
| `longhaul_300` | Longhaul 300 | 대형 여객기 | 개발 예정 | 64 · 60m | WideAirspace | |
| `cargolift_220` | CargoLift 220 | 화물기 | 개발 예정 | 45 · 42m | WideAirspace | |

## 튜닝 초깃값 (에셋이 없을 때만 생성 — 이후엔 에셋 값이 기준)
| 값 | Scout P-1 (`AircraftFlightTuning.asset`) | Viper F-9 | Cityliner 180 |
|---|---|---|---|
| 질량 kg | 900 | 9,000 | 70,000 |
| 최소/순항/최고/부스트 m/s | 22 / 30 / 65 / 80 | 45 / 70 / 140 / 170 | 50 / 65 / 95 / — |
| 최대 추력 m/s² | 18 | 35 (+25 부스트) | 9 |
| 항력 계수 | 0.0035 | 0.0012 | 0.0009 |
| 피치/롤/요 deg/s | 75 / 160 / 30 | 60 / 220 / 25 | 18 / 35 / 8 |
| 반응(angularResponse) | 6 | 7 | 1.6 |
| 최대 기울기 | 75° | 85° | 35° |
| 고속 조종 권한 | 0.65 | 0.45 | 0.8 |

"묵직함"은 질량이 아니라 **반응·회전 속도·추력·항력**으로 표현한다 — 힘이 질량 무관 가속도라서(D-008, D-035). 질량은 충돌 반응에만 영향.

## 규칙
- **모드 호환성**: SKYLINE RUN 링(지름 16m)은 `TightRoute` 기체 전용. 여객기·제트로 시작하면 메뉴가 막고, 혹시 진입해도 세션이 FREE FLIGHT로 바꾼다(명세 14.7).
- **선택 저장·복구**: 마지막 선택 ID를 저장. 없는 ID·잠긴 기체·미구현 기체·손상 값이면 기본 기체(Scout P-1).
- **해금**: 로컬 기록(누적 거리·시간·SKYLINE 완주)만 사용. 광고·결제·서버 없음. 지금은 구현된 3종 모두 기본 제공.
- **공중 교체 없음**: 선택은 비행 시작 전에만.
- **카메라**: 기체 크기로 추적 거리·높이·충돌 반경·FOV 기준 속도 자동 계산(`AircraftCameraProfile.ForSize`).

## 검증 상태
| 항목 | 상태 |
|---|---|
| 해금·호환성·선택 복구·성능 막대·저장 v1→v2 이관 | ✅ Core 테스트(CI) |
| 세 기종 속도·롤·선회 차이, 여객기 부스트 없음 | ⬜ PlayMode `AircraftComparisonPlayModeTests` 작성, 미실행 |
| 선택 기체 스폰·튜닝·카메라·저장, 여객기 SKYLINE 차단 | ⬜ PlayMode `OpenWorldFlowPlayModeTests` 작성, 미실행 |
| 실제 조작감 차이(사람이 느끼는 것) | ⬜ 실플레이 필요 |
| Android 성능(모델·카메라 추가 영향) | ⬜ 미측정 |

**명세 14.13 금지 사항 확인**: 세 기체는 외형만 다른 것이 아니라 튜닝 에셋이 각각 다르고(질량·속도·추력·항력·회전·반응·부스트), 그 차이를 확인하는 테스트를 작성했다. 다만 **테스트를 아직 실행하지 않았으므로 "기체 선택 시스템 완성"으로 보고하지 않는다.**
