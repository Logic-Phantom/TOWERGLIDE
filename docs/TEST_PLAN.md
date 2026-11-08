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

실행: Unity Test Runner(Window ▸ General ▸ Test Runner ▸ EditMode) 또는 Unity 없이 `dotnet test tools/core-tests`(GitHub Actions `core-tests`).

## 2. PlayMode (`Assets/_Project/Tests/PlayMode`)
| 명세 11.2 항목 | 상태 |
|---|---|
| 비행기 스폰·Rigidbody 활성 | `SpawnsWithRigidbodyAndFliesForward` |
| 입력 → 예상 방향(피치 상승, 롤 → 오른쪽 기울기·선회) | `PitchUpClimbs`, `RollRightBanksRightThenTurnsRight` |
| 입력 해제 후 각속도 안정 | `AngularVelocitySettlesAfterRelease` |
| 수평 순항 유지 | `HoldsAltitudeRoughlyWithNoInput` |
| 부스트 제한 | `BoostIsLimited` |
| 충돌 처리·복귀, 링 진행률, 일시정지/재개, 결과 → 재시작 | **후속**(씬 기반 테스트 — CityFlight 로드 후 검증) |

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
| 2026-11-08 | EditMode/PlayMode | — | **미실행**: Unity 미설치, .NET 로컬 설치 권한 거부. 푸시 후 GitHub Actions `core-tests` 결과를 README에 기록 |
