# 아키텍처

## 한눈에

```
┌──────────────────────── TowerGlide.Editor (Editor 전용) ─────────────────────────┐
│ TowerglideProjectSetup · TowerglideSceneBuilder · TowerglideBuild               │
└──────────────┬──────────────────────────────────────────────────────────────────┘
               │ 참조
┌──────────────▼──────────── TowerGlide.Runtime (MonoBehaviour) ──────────────────┐
│ App      GameApp(컴포지션 루트) · SceneFlow · QualityPresetApplier               │
│ Input    VirtualJoystick · TouchHoldButton · TouchFlightInputSource ·           │
│          KeyboardGamepadInputSource ─▶ FlightInputRouter ─▶ (SetInput)          │
│ Flight   AircraftFlightController(Rigidbody) · AircraftFlightTuning(SO) ·        │
│          AircraftCollisionHandler                                               │
│ Cameras  ChaseCameraController                                                  │
│ Gameplay FlightSession(규칙 조율) · CheckpointCourse/Gate · NearMissSensor ·     │
│          ObstacleMarker                                                         │
│ World    FlightBoundary · KinematicMover                                        │
│ Save     LocalSaveService(JSON 파일)   Platform HapticService   UI FlightHud…    │
└──────────────┬──────────────────────────────────────────────────────────────────┘
               │ 참조
┌──────────────▼──────── TowerGlide.Core (순수 C#, noEngineReferences) ────────────┐
│ Input  FlightInput · InputShaping     Flight  AeroModel · BoostMeter             │
│ Gameplay CheckpointSequence · ScoreTracker · NearMissTracker ·                  │
│          FlightRecordTracker · RunResult      App GameStateMachine              │
│ Save   SaveData · SaveDataMigrator    Util FlightMath                           │
└─────────────────────────────────────────────────────────────────────────────────┘
        ▲ EditMode 테스트(NUnit)는 Core만 참조 → Unity 없이 .NET에서도 실행(tools/core-tests)
```

규칙
- **Core는 Unity를 모른다.** 숫자·상태·규칙만. 그래서 단위 테스트가 빠르고 CI에서 라이선스 없이 돈다.
- **입력은 한 방향.** UI 버튼·키보드는 `FlightInputRouter`에만 말하고, 비행 컨트롤러는 `FlightInput` 구조체만 받는다. UI가 Rigidbody를 직접 만지지 않는다.
- **게임 규칙은 `FlightSession` 한 곳.** 부품(충돌·링·근접 회피·입력)은 이벤트만 내고, 점수·복귀·일시정지·결과는 세션이 결정한다.
- **설정값은 데이터.** 비행 수치는 `AircraftFlightTuning` 에셋, 점수 규칙은 `ScoreRules`, 코스는 씬 계층(링 순서 = 자식 순서).

## 폴더

```
Assets/
  _Project/
    Art/{Aircraft,Buildings,Obstacles,Materials,Textures,VFX,Audio}
    Prefabs/{Aircraft,City,Obstacles,UI,VFX}
    Scenes/            MainMenu.unity, CityFlight.unity  (TOWERGLIDE ▸ 2. 씬 생성)
    Settings/          AircraftFlightTuning.asset, Rendering/URP_*.asset
    Scripts/Core/      TowerGlide.Core.asmdef
    Scripts/Runtime/   TowerGlide.Runtime.asmdef
    Editor/            TowerGlide.Editor.asmdef
    Tests/EditMode/    TowerGlide.Tests.EditMode.asmdef
    Tests/PlayMode/    TowerGlide.Tests.PlayMode.asmdef
  ThirdParty/          외부 에셋(라이선스 기록 필수 — docs/THIRD_PARTY_ASSETS.md)
Packages/manifest.json
ProjectSettings/ProjectVersion.txt   (나머지 설정 파일은 Unity가 처음 열 때 생성)
build/version.json                   버전 단일 소스
tools/core-tests/                    Unity 없이 Core 테스트(.NET)
.github/workflows/                   core-tests(자동), unity-build(수동)
docs/                                설계·결정·출시 문서
```

## 런타임 흐름

1. 앱 시작 → `GameApp.Bootstrap`(BeforeSceneLoad): 저장 파일 로드 → 프레임 제한·품질·진동 적용 → `MainMenu`.
2. 메뉴에서 모드 선택 → `GameApp.PendingMode` 설정 → `SceneFlow.LoadFlight()`.
3. `FlightSession.Start`: 설정 반영(감도·반전·흔들림·조이스틱 방식) → 모드별 코스 표시 → 스폰 → Flying.
4. 매 프레임
   - `Update`: 입력 소스 → `FlightInputRouter`(합치기·데드존·감도) → `AircraftFlightController.SetInput`
   - `FixedUpdate`: 추력·양력·항력·정렬·토크(물리)
   - `LateUpdate`: `ChaseCameraController`(보간된 Transform 추적, 충돌 회피)
   - `FlightSession.Update`: 기록·타이머·안전 지점·경계 → `FlightHud.Refresh`
5. 이벤트: 링 통과(`CheckpointCourse.GateProcessed`) · 근접 회피(`NearMissSensor.NearMiss`) · 충돌(`AircraftCollisionHandler.Crashed`) → 세션이 점수·햅틱·토스트·복귀 처리.
6. 종료: SKYLINE 완주/시간 초과 또는 FREE FLIGHT `END` → `RunResult` → `RecordsData.Apply` → 저장 → 결과 패널(재도전/메뉴).
7. 백그라운드 전환·포커스 상실 → 자동 일시정지 + 저장.

## 상태

`GameStateMachine`(Core): `Boot → MainMenu → Loading → Flying ⇄ Paused → Results → Loading/MainMenu`. 허용되지 않은 전이는 false(연타·경쟁 상황에서 예외 없이 무시). 앱 전체(`GameApp.State`)와 비행 세션 내부 상태를 같은 클래스로 관리한다.

## 씬 생성 (Editor 자동화)

| 메뉴 | 하는 일 | 멱등성 |
|---|---|---|
| TOWERGLIDE ▸ 1. 프로젝트 설정 적용 | 폴더, Player 설정(ID·버전·가로·Android/iOS), Active Input Handling, URP 3단계 + Quality Level, 비행 튜닝 에셋 | 같은 값으로 덮어씀 |
| TOWERGLIDE ▸ 2. 씬 생성 | `CityFlight`: 시드 고정 그레이박스 도시(11×11 블록), 장애물 7종, 링 12개 코스, 기체, 추적 카메라, 터치 HUD / `MainMenu`: 배경·메뉴 / Build Settings 등록 | 씬 파일을 통째로 다시 만듦(같은 시드 → 같은 결과) |
| TOWERGLIDE ▸ Build ▸ … | 설정 검증, Android AAB(릴리스)/APK(개발), iOS Xcode 프로젝트 | — |

**수동 조정 포인트**: 씬을 손으로 다듬기 시작하면 2번 메뉴를 다시 누르지 않는다(덮어씀). 이후 변경은 프리팹화(Phase 2)해서 관리한다.

## 확장 지점

| 하고 싶은 것 | 어디를 바꾸나 |
|---|---|
| 새 입력 장치(자이로) | `IFlightInputSource` 구현 → Router `sources`에 추가 |
| 비행 감각 | `AircraftFlightTuning` 에셋 값(코드 수정 없이) |
| 점수 규칙 | `ScoreRules.Default` (후속: ScriptableObject로 이동) |
| 새 장애물 | 콜라이더 + `ObstacleMarker`(유형) + 필요 시 `KinematicMover` |
| 새 모드(Time Attack 등) | `FlightMode` 값 추가 → `FlightSession` 분기, `RecordsData` 필드(저장 버전 올리고 `SaveDataMigrator`) |
| 스토어 기능(리뷰 요청·리더보드) | `Platform/` 아래 인터페이스 + 플랫폼별 구현(개인정보 영향 검토 후) |
