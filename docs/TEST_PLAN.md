# 테스트 계획

결과는 **자동 테스트 / 에디터 Play / Android 실기기 / iOS 실기기**를 구분해 기록한다. 실행하지 않은 것을 통과로 쓰지 않는다.

## 1. EditMode 단위 테스트 (`Assets/_Project/Tests/EditMode`, Core만 참조)
| 명세 11.1 항목 | 테스트 |
|---|---|
| 입력 정규화·데드존 | `InputShapingTests` (데드존 재스케일, expo, 반전, 감도 상한, NaN, 합치기) |
| 속도·고도 표시 변환 | `UnitConversionTests`, `FlightRecordTrackerTests` |
| 점수·충돌 패널티 | `ScoreTrackerTests` (콤보 창·상한, 근접 회피 스케일, 감점, 음수 방지, 종료 1회) |
| 링 중복 판정 방지 | `CheckpointSequenceTests` (중복·순서 건너뛰기·완주 후 무시) |
| 체크포인트 순서·진행률 | `CheckpointSequenceTests` |
| 부스트 게이지·쿨다운 경계 | `BoostMeterTests` (소진 잠금, 재시작 최소 비율, 충전 지연, 상한) |
| 저장 직렬화 검증 | `SaveDataTests` (범위 보정, 누락 섹션, 버전 0 이관, 새 버전 보호, 기록 갱신 규칙) |
| 손상 데이터 기본값 복구 | `SaveDataTests.NullLoadReturnsDefaultsAsCorrupted` (+ 파일 단위는 `LocalSaveService` — PlayMode 후속) |
| 공력 함수 | `AeroModelTests` (실속 후 완만 감소, 저속 양력 감소, 에어브레이크, 추력 목표, 조종 권한) |
| 상태 전이 | `GameStateMachineTests` |
| 코스 규칙(간격·고도·선회각·상승각) | `CourseValidatorTests` |
| 건물 아키타입(10종 이상, 모든 높이 커버) | `BuildingCatalogTests` |
| 도시 배치(결정성, 코스 비우기, 협곡 벽, 예약·지지 건물, 형태 다양성) | `CityLayoutGeneratorTests` |
| 좌표계(EPSG:5186 원점, 왕복 오차, 거리 정확도, 축, rebase) | `CoordinateSystemTests` |
| 월드 정의(격자가 코스 포함, 계획 좌표 미검증 표시) | `WorldDefinitionTests` |
| 청크 격자·상태 기계(중복 요청 거부, 실패 재시도) | `ChunkGridTests`, `ChunkStateTrackerTests` |
| 스트리밍 판단(현재 청크 우선, 진행 방향·부스트 프리로드, 동시 로드 제한, 히스테리시스, 예산 퇴출, 실패 대기, 준비 판정) | `ChunkStreamingPlannerTests` |
| 기체 해금·모드 호환·선택 복구, 성능 막대, 저장 v1→v2, 누적 기록, 항력 보상 추력 | `AircraftRulesTests`, `AircraftStatsTests`, `AircraftSaveTests`, `ThrustCompensationTests` |
| 사운드 합성(NaN 없음·정규화·짧은 효과음·루프 이음매·기종별 차이·결정성) | `ProceduralAudioTests` |
| 엔진음 모델(속도↑ 음높이↑, 터보팬 범위 좁음·부스트 무시, 범위) | `EngineSoundModelTests` |
| 튜토리얼(조작 유지해야 진행, 누적, 부스트 없는 기체, 건너뛰기) | `TutorialSequenceTests` |
| 설정 선택지(순환, 감도 0.1 단위, 초기화 시 튜토리얼 유지, 표시 문구) | `SettingsOptionsTests` |

실행: Unity Test Runner(Window ▸ General ▸ Test Runner ▸ EditMode) 또는 Unity 없이 `dotnet test tools/core-tests`(GitHub Actions `core-tests`).

## 2. PlayMode (`Assets/_Project/Tests/PlayMode`)
| 명세 11.2 항목 | 상태 |
|---|---|
| 비행기 스폰·Rigidbody 활성 | `SpawnsWithRigidbodyAndFliesForward` |
| 입력 → 예상 방향(피치 상승, 롤 → 오른쪽 기울기·선회) | `PitchUpClimbs`, `RollRightBanksRightThenTurnsRight` |
| 입력 해제 후 각속도 안정 | `AngularVelocitySettlesAfterRelease` |
| 수평 순항 유지 | `HoldsAltitudeRoughlyWithNoInput` |
| 부스트 제한 | `BoostIsLimited` |
| 씬 스폰·SKYLINE 시작 | `CityFlightSceneTests.SpawnsAircraftAndStartsSkylineRun` |
| 링 통과 → 진행률, 중복 통과 무시 | `FlyingThroughFirstGateAdvancesProgress` |
| 충돌 집계 → 복귀 | `CrashIsCountedThenAircraftRespawns` |
| 일시정지/재개 | `PauseStopsTimeAndResumeRestores` |
| 결과 → 재시작(같은 모드) | `FinishShowsResultsAndRestartReloads` |
| 기체별 튜닝·질량 분리, 최고 속도 순서, 롤·선회 차이, 여객기 부스트 없음 | `AircraftComparisonPlayModeTests` |
| 선택 기체 스폰·튜닝·카메라·저장, 여객기 SKYLINE 차단 | `OpenWorldFlowPlayModeTests` |
| 청크 앞쪽 로드·뒤쪽 언로드·중복 로드 없음 | `ChunksStreamInAheadAndUnloadBehindWithoutDuplicates` |
| 저장 파일 재실행 유지·손상 백업·새 버전 보호(실제 파일 입출력) | `LocalSaveServicePlayModeTests` |
| 합성 클립 생성·캐시, 설정 적용(볼륨·이벤트) | `AudioPlayModeTests` |
| 첫 비행 튜토리얼 표시·건너뛰기 저장 | `TutorialPlayModeTests` |

씬 테스트는 `TOWERGLIDE ▸ 2. 씬 생성`으로 CityFlight가 Build Settings에 들어간 뒤에만 실행되고, 그 전에는 Ignore된다.

## 3. 수동 (에디터 Play)
- [ ] 3분 이상 비행 중 예외 없음(Console)
- [ ] 조작감 승인 기준(명세 2.2) 7개 항목
- [ ] SKYLINE RUN 처음부터 완주, 충돌 후 마지막 링 재개, 시간 초과
- [ ] FREE FLIGHT 충돌 복귀, 경계 경고 → 복귀
- [ ] 일시정지 중 버튼·물리 정지, 재개 후 입력 정상
- [ ] 결과 → 재도전/메뉴, 기록 저장 후 재실행 유지

## 4. 실기기 (명세 11.3)
- [ ] 저/중/고급 Android 설치·실행, 가로 고정, 노치·시스템 UI 겹침
- [ ] 터치 조작 편의·오입력, 손 떼면 즉시 해제
- [ ] 5분 이상: 프레임·발열·메모리(`docs/PERFORMANCE.md`)
- [ ] 백그라운드 전환 → 자동 일시정지·저장 → 복귀
- [ ] 진동 On/Off
- [ ] iOS(TestFlight): 위 항목 + 홈 인디케이터·가장자리 제스처

## 결과 기록
| 날짜 | 범위 | 환경 | 결과 |
|---|---|---|---|
| 2026-11-08 | EditMode(Core) 50 | GitHub Actions ubuntu, .NET 8, C# 9 | ✅ 전체 통과 ([run](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42456690200)) |
| 2026-11-08 | PlayMode 6 · Unity Test Runner | — | **미실행**: Unity 미설치 |
| 2026-11-08 | EditMode(Core) 65 | GitHub Actions | 1차 실패 → 원인: 코스 08→09 대각선이 스카이브리지 지지 건물을 깎음(실제 설계 결함) → 코스 수정 후 통과. 최종 결과는 README |
| 2026-11-08 | PlayMode 11 · 코스 검증 메뉴 | — | **미실행**: Unity 미설치 |
| 2026-11-08 | EditMode(Core) 94 | GitHub Actions | ✅ 전체 통과 ([run](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42466537780)) |
| 2026-11-08 | PlayMode 18 · 코스 검증 · 스트리밍 스트레스 | — | **미실행**: Unity 미설치 |
| 2026-11-08 | EditMode(Core) 111 | GitHub Actions | ✅ 전체 통과 ([run](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42467226975)) |
| 2026-11-08 | PlayMode 24 | — | **미실행**: Unity 미설치 |
