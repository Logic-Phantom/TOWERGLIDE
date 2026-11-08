# 결정 기록 (Decisions)

작은 결정은 사용자에게 묻지 않고 안전한 기본값으로 정한 뒤 여기에 기록한다. **[소유자 확인]** 표시는 출시 전에 소유자가 확정해야 하는 항목이다.

| ID | 날짜 | 결정 | 이유 / 위험 |
|---|---|---|---|
| D-001 | 2026-11-08 | **Unity 6.3 LTS(6000.3.x) + URP 17.3 + Input System 1.19 + Test Framework + uGUI**. `ProjectSettings/ProjectVersion.txt`는 `6000.3.0f1`로 두고, 처음 열 때 Unity Hub에서 설치된 6.3 LTS 최신 패치를 선택한다 | 프롬프트 지정. 6.3용 Input System 1.19.0은 Unity 공식 6000.3 문서에서 확인. URP·uGUI·Test Framework는 에디터에 고정된 코어 패키지라 에디터가 맞는 버전으로 맞춘다. 위험: 개발 PC에 Unity가 없어 **패키지 해석·컴파일은 아직 미검증** |
| D-002 | 2026-11-08 | **Android + iOS 동시 출시 구조**(프롬프트는 Android 전용). 코드·설정은 공통, 빌드만 분기. iOS는 Windows에서 Xcode 프로젝트까지 만들고 Archive·업로드는 macOS(CI 또는 Mac)에서 | 사용자 요구("앱스토어 및 구글 스토어"). Unity iOS 빌드는 Xcode 필수 → Windows 단독으로 IPA 생성 불가 |
| D-003 | 2026-11-08 | 표기: 영문 **TOWERGLIDE**(로고·`productName`), 한글 **타워글라이드**(스토어 표시명). 스토어 제목 초안 `타워글라이드 - 빌딩 사이 비행` | 로고가 영문. 기기 홈 화면 한글 라벨은 Phase 5에서 Android `values-ko`/iOS `InfoPlist.strings`로 현지화 **[소유자 확인]** |
| D-004 | 2026-11-08 | **패키지/번들 ID = `com.logicphantom.towerglide`**(Android·iOS 동일) | 프롬프트 권장값. 이전 앱들(`app.cutpick.mobile` 등)과 형식이 다르지만 게임은 퍼블리셔 계열 ID가 일반적. **첫 업로드 후 변경 불가 → 첫 업로드 전 확정 [소유자 확인]** |
| D-005 | 2026-11-08 | Android **minSdk 26(8.0) / targetSdk 36**, ARM64 전용, IL2CPP, AAB. iOS **15.0+**, IL2CPP, 가로 전용 `requiresFullScreen` | Play 신규 앱 targetSdk 36 요건(제출 직전 재확인). 26 미만 기기는 Vulkan/GLES3 성능 하한이 낮고 3D 비행 게임 대상이 아님. 위험: 아주 오래된 기기 제외 |
| D-006 | 2026-11-08 | 입력 = **Input System 패키지 직접 폴링**(액션 에셋 없음) + **uGUI EventSystem 터치 조이스틱**(`InputSystemUIInputModule`). 자이로는 후속 옵션 | 액션 에셋은 Unity 에디터가 있어야 안전하게 만들 수 있음. 터치 UI는 멀티터치 pointerId 추적으로 오입력 방지 |
| D-007 | 2026-11-08 | **어셈블리 4계층**: `TowerGlide.Core`(순수 C#, `noEngineReferences`) → `TowerGlide.Runtime`(MonoBehaviour) → `TowerGlide.Editor` / 테스트. 입력 흐름은 `UI·키보드 → FlightInputRouter → AircraftFlightController` 단방향 | 규칙(점수·부스트·체크포인트·저장 검증)을 Unity 없이 테스트. asmdef가 의존 방향을 컴파일 단계에서 강제(명세 15-7, 15-8) |
| D-008 | 2026-11-08 | 비행 물리 = **질량 무관 가속도(ForceMode.Acceleration)** + **목표 각속도 추종 토크** + **속도 벡터 정렬(velocityAlignment)** + 협조 선회 yaw 보조 | 튜닝 값이 질량과 무관해 직관적. 손을 떼면 목표 각속도 0으로 수렴 = 자연 감쇠. 정렬 계수로 "레일 위 ↔ 미끄러짐" 감각 조절. 위험: **실제 조작감은 미검증 — Phase 1 실플레이 튜닝 필수** |
| D-009 | 2026-11-08 | 저장 = `JsonUtility` + `persistentDataPath/save.json`, **원자적 쓰기**(tmp → Replace), 버전 필드, 손상 파일 백업 후 기본값, 새 버전 파일 덮어쓰기 금지. PlayerPrefs 미사용 | 명세 4.3. PlayerPrefs는 구조·버전 관리가 어렵고 Android에서 XML로 노출 |
| D-010 | 2026-11-08 | **`GameApp` 소형 컴포지션 루트**(`RuntimeInitializeOnLoadMethod`로 자동 생성) — 저장·진동·상태만 명시적 프로퍼티. DI 프레임워크·범용 서비스 로케이터 없음 | 명세 8.1 "과도한 추상화 금지". 어떤 씬에서 Play해도 동작 → Boot 씬 불필요 |
| D-011 | 2026-11-08 | 씬은 **Editor 스크립트로 생성**(시드 고정 → 같은 결과, 멱등). `Boot` 씬은 만들지 않고 `MainMenu` + `CityFlight` 2개 | 명세 9.1이 Boot/MainMenu 통합 허용. 씬 YAML을 손으로 쓰지 않음(GUID 깨짐 위험). 수동 편집을 시작한 뒤에는 재생성하지 않는다 |
| D-012 | 2026-11-08 | **Core + EditMode 테스트를 일반 .NET(net8, C# 9)으로도 컴파일·실행**(`tools/core-tests`, GitHub Actions `core-tests`) | Unity 라이선스 없이 매 푸시 검증. Core에 Unity API가 섞이거나 C# 9 초과 문법을 쓰면 여기서 실패 |
| D-013 | 2026-11-08 | MVP UI 텍스트는 **legacy uGUI `Text` + LegacyRuntime 폰트** | TextMeshPro는 에디터에서 리소스 임포트가 필요. Phase 3에서 TMP + 한글 OFL 폰트(예: Pretendard, Noto Sans KR)로 교체. 위험: 한글·▲ 글리프는 OS 폰트 폴백에 의존 |
| D-014 | 2026-11-08 | 그래픽 프리셋 3단계 = Quality Level `Performance/Balanced/Quality` + URP 에셋 3종(렌더 스케일 0.75/0.9/1.0, MSAA 1/2/4, 그림자 120/220/350m) | 명세 5.5·10.2. 기본 Balanced. 실기기 측정 후 조정 |
| D-015 | 2026-11-08 | **MVP에 광고·인앱결제·분석·로그인·서버·리더보드 없음** → 개인정보 수집 0, INTERNET 권한 없음 | 명세 0.1-5. 스토어 데이터 보안/개인정보 라벨을 "수집 안 함"으로 유지. Game Center/Play 게임즈 리더보드는 후속 검토 |
| D-016 | 2026-11-08 | Unity 스플래시 로고 끔(`showUnityLogo = false`) | Unity 6부터 Personal도 스플래시 비활성 가능. **출시 시점 Unity 약관 재확인 [소유자 확인]** |
| D-017 | 2026-11-08 | 버전 단일 소스 = `build/version.json`(`version` = 표시 버전, `build` = Android versionCode·iOS build number). 빌드 메뉴가 매번 반영 | 두 스토어 버전 불일치 방지. 업로드마다 `build`를 1씩 올린다 |
| D-018 | 2026-11-08 | Android 서명 = **Play App Signing + 업로드 키**. 업로드 키는 저장소 밖에 두고 환경 변수(`TOWERGLIDE_KEYSTORE_*`)로만 전달, 빌드 후 비밀번호를 설정에서 지움 | 키 유출 시 피해 최소화(명세 13). `.gitignore`에 `*.keystore` 등 |
| D-019 | 2026-11-08 | 출시 문서는 `docs/GOOGLE_PLAY_RELEASE.md` 대신 **`docs/RELEASE_CHECKLIST.md` 하나에 Android+iOS**, `[코드]`/`[소유자]` 구분 | 이전 앱(컷픽·웨어로그·킵온)과 같은 관례. 두 스토어 공통 항목 중복 방지 |
| D-020 | 2026-11-08 | 개발 PC에 .NET SDK 로컬 설치 시도 → **권한 거부로 미설치**. 로컬 테스트 실행 대신 GitHub Actions에서 실행 | 시스템 정책 존중. 결과는 README 검증 상태에 그대로 기록 |
| D-021 | 2026-11-08 | 근접 회피 = 기체 자식 **반경 7m 트리거 구** + `Collider.ClosestPoint` 최근접 거리, 속도 20m/s 이상, 장애물별 쿨다운 3초, 충돌한 장애물 2초 제외. 링 프레임·지면은 제외 | 명세 4.2 중복 방지. 기체 트리거가 링 트리거를 먼저 건드려 조기 통과되는 문제를 `other.isTrigger` 검사로 차단 |
| D-022 | 2026-11-08 | 코스 보장: 코스 구간 주변 건물 높이를 **구간 고도 − 22m 이하로 자동 제한**(15m 미만이면 공터) | 랜덤 도시가 필수 링 경로를 막지 않음(명세 3.2 "랜덤이 플레이 공간을 막지 않게") |
| D-023 | 2026-11-08 | 케이블 충돌 판정 = 경고 구슬 크기(지름 2.2m) | 보이는 것보다 얇은 판정은 불공정, 두꺼운 판정은 억울함 — 보이는 경고 표식 기준 |
