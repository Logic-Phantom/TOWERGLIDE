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
| D-024 | 2026-11-08 | 건물 = **매개변수 아키타입 모듈 12종**(`BuildingCatalog` + `TowerglideBuildingFactory`). 블록마다 폭·깊이·높이가 달라 고정 프리팹 대신 모듈로 조립. 절차 메시는 `Art/Buildings/CityMeshes.asset` 하나에 하위 에셋으로 저장 | 늘린 프리팹은 옥상 설비·첨탑이 찌그러짐. 메시를 에셋으로 저장해 씬 비대화·누락 방지 |
| D-025 | 2026-11-08 | 도시 배치·코스·코스 규칙을 **Core로 이동**(`CityLayoutGenerator`, `SkylineCourses`, `CourseValidator`, `Float3`). 씬 생성기는 조립만 | 배치 규칙(코스 비우기, 지지 건물 높이, 아키타입 높이 범위)을 Unity 없이 테스트 |
| D-026 | 2026-11-08 | 코스 09~12 재배치: 08→09를 z=175 거리 따라 상승, 10번 230m, 12번 (10,110,-340) | CI 테스트가 **대각선 08→09 구간이 스카이브리지 지지 건물을 28m로 깎는 충돌**을 발견(다리가 허공에 뜸) |
| D-027 | 2026-11-08 | 크기가 고정된 것은 **프리팹**: 기체, 링, 크레인 2종, 드론, 광고판, 스카이브리지, 케이블 존, 안테나, 화물 기구, VFX 3종(`Prefabs/*`). 축 규칙 +Z = 통과 방향, 크레인 붐 = +X | 명세 8 폴더 구조. 같은 경로에 재저장 → GUID·씬 연결 유지 |
| D-028 | 2026-11-08 | VFX = `UnityEngine.Pool.ObjectPool` + `stopAction = Callback`, 종류별 3개 예열·최대 8개. 충돌 불꽃 0.5초·링 고리·근접 회피 줄무늬, 모두 짧게 | 명세 6.2·10.2 풀링, 2.4 "오래 흔들리거나 과한 효과 금지" |
| D-029 | 2026-11-08 | 안내: 다음 링 **빛기둥 월드 마커**, 경계 경고 시 **구역 중심 화살표**, 근접 회피 **가장자리 플래시**(최대 알파 0.5, 0.35초) + 가벼운 진동 | 명세 1.3·3.4·4.2. 화면 중앙을 가리지 않는 피드백만 |
| D-030 | 2026-11-08 | 8번째 장애물 = **화물 기구**(오르내리는 풍선+화물), 코스 영향 없는 광장 위 최대 3개 | 명세 2.5-8. 자유 비행 구역에 움직이는 목표물 |
| D-031 | 2026-11-08 | 코스 검증 메뉴: 링 사이 **직선**을 반지름 4.5m 구로 훑는 근사 검사 + 스폰·링 겹침 | 실제 비행 경로는 곡선이라 완전하지 않음 — 실플레이 확인 병행 |
| D-032 | 2026-11-08 | CI 실패 테스트를 GitHub 주석으로 출력(`GitHubActionsTestLogger`) | 로그 원문은 인증이 필요 — 공개 API로 실패 원인 확인 가능 |
| D-033 | 2026-11-08 | 서울 오픈월드·기체 선택 애드온 적용 범위 = **OW0·OW1·AC0·AC1만**. 라이선스 판단 기준은 애드온의 "비상업 우선"이 아니라 **스토어 공개 배포**(비상업·변경금지 자료 미사용) | 애드온 명세 12-6·14.13 지시. 저장소 목표(D-002)가 공개 배포 |
| D-034 | 2026-11-08 | 좌표계 = **EPSG:5186 TM 투영 → 원점 기준 로컬 m**(x=동, z=북). 원점은 계획용 대략값(서울시청 부근), floating origin은 `RebaseOffset`으로 열어 둠 | 국내 공공 공간정보 표준 좌표계와 직접 호환. 서울 ±20km에서 float 정밀도 약 2mm |
| D-035 | 2026-11-08 | 기종별 `mass`·`inertiaTensorScale`은 Rigidbody에 적용하되 **조작감은 가속도 기반 튜닝으로 차별화**(질량은 충돌 반응용) | D-008 유지(질량 무관 가속도). 애드온 14.4 "질량만 바꾸고 보정 안 함" 함정을 구조적으로 피함 |
| D-036 | 2026-11-08 | 추력 = **항력 보상 + 비례 제어**(`AeroModel.ThrustWithDragCompensation`) | 기존 비례 제어는 항력 때문에 목표 속도보다 낮게 머무름 → 순항 속도가 크게 다른 기종(30~70 m/s)에서 오차 확대. 비행 감각에 영향 있음 — 실플레이 재튜닝 대상 |
| D-037 | 2026-11-08 | 청크 = **250m 정사각, Additive 씬**(`Scenes/Chunks`), 지면·코스·실루엣·기체·UI는 비행 씬 상주. 판단은 Core `ChunkStreamingPlanner`(테스트), 실행은 `WorldChunkLoader` | 명세 5.1·5.3. Addressables는 청크 수가 늘 때 재검토 |
| D-038 | 2026-11-08 | 원거리 표현 = 청크별 **건물 외곽 상자 합친 실루엣**(충돌 없음, 상세 청크가 로드되면 숨김). 시작 시 근처 4청크 로드까지 기체 고정(최대 15초) | 명세 5.4 parent-stays-visible, 5.2 로드 실패 fallback |
| D-039 | 2026-11-08 | 기체 MVP 3종 = Scout P-1 / Viper F-9 / Cityliner 180, 나머지 4종은 "개발 예정"으로 목록에만. 카탈로그는 `Resources/AircraftCatalog` | 명세 14.2 MVP 라인업. Resources는 GameApp(코드 생성)이 씬 참조 없이 찾기 위함 |
| D-040 | 2026-11-08 | SKYLINE RUN = `TightRoute` 기체만(현재 Scout). 제트·여객기는 FREE FLIGHT | 명세 14.7 "여객기에 소형기 링 코스 강제 금지". 제트 전용 넓은 게이트 코스는 AC4 |
| D-041 | 2026-11-08 | 저장 v2: `aircraft.selectedAircraftId` + 누적 거리·시간·SKYLINE 완주. v1 이관 시 누적 기록은 0에서 시작 | 해금 조건을 과거 최장 기록으로 추정해 부풀리지 않음 |
| D-042 | 2026-11-08 | 사운드 = **코드 합성**(Core `ProceduralAudio`: 엔진 3종·바람 루프, 충돌·링·근접 회피·부스트·클릭). 22.05kHz 모노, 루프는 교차 페이드 후 잘라 이음매 제거 | 외부 음원 0개 → 라이선스 문제 없음, 앱 용량 0. 음질은 기본 수준 — Phase 4에서 라이선스 확인된 음원으로 교체 검토 |
| D-043 | 2026-11-08 | 기체별 엔진음 = `AircraftDefinition.engineSound`(프로펠러·제트·터보팬), 음높이·음량은 Core `EngineSoundModel`(속도·스로틀·부스트) | 애드온 명세 14.8 기종별 사운드 구분 |
| D-044 | 2026-11-08 | 음악은 아직 없음(음악 볼륨 설정만 저장) | 라이선스 확인된 음원이 없음. 합성 BGM은 품질 대비 효과가 낮아 보류 |
| D-045 | 2026-11-08 | 설정은 바뀌는 즉시 적용·저장(`GameApp.CommitSettings` → `SettingsChanged` 이벤트 → 비행 중 즉시 반영). 메인 메뉴·일시정지에서 같은 화면 | 명세 5.5. "저장 버튼"을 누르지 않아도 유지 |
| D-046 | 2026-11-08 | 설정 초기화는 튜토리얼 완료 여부를 유지 | 초기화할 때마다 튜토리얼이 다시 뜨는 것 방지(다시 보기는 별도 버튼) |
| D-047 | 2026-11-08 | 튜토리얼 = 실제 조작을 일정 시간 유지해야 다음 단계(롤 → 선회 → 피치 → 부스트 → 완료), 진행은 누적, 언제든 SKIP, 부스트 없는 기체는 부스트 단계 생략 | 명세 2.2 "1~2분 안에 이해". 읽고 넘기기 방지 |
| D-048 | 2026-11-08 | (→ D-049로 해결) 한글 폰트(TMP + OFL 폰트)는 **이번에 넣지 않음** — 폰트 파일 다운로드가 필요해 소유자 확인 후 진행. 현재는 OS 폰트 폴백 | 외부 파일 반입은 라이선스 기록과 함께 해야 함(Pretendard·Noto Sans KR 모두 OFL 후보) |
| D-049 | 2026-11-08 | 한글 폰트 = **Pretendard v1.3.9 Regular·Bold(OTF, SIL OFL 1.1)**, 공식 저장소 태그에서 받아 크기·OTF 헤더·SHA-256 확인. **uGUI Text에 동적 폰트로 적용**(TMP 전환은 Unity에서 확인 후). 굵게는 실제 Bold 파일 사용, 없는 글자는 OS 한글 폰트로 대체 | 소유자 승인(D-048 후속). 한글 11,172자 전부 + UI 기호(▲ ★ ° · — … ©) 지원 확인, 🔒 이모지는 "잠김"으로 교체. TMP는 SDF 에셋 생성·전체 Text 교체가 필요해 Unity 없이 바꾸면 위험. 앱 용량 +약 3.1MB(두 굵기만) |
| D-050 | 2026-11-08 | **첫 실행 품질 자동 선택**(Core `QualityAutoSelector`): RAM 4GB 미만 또는 코어 6개 미만 → Performance·30fps, RAM 8GB+·코어 8+ → Quality, 나머지 Balanced. 한 번만 적용, 이후 사용자 설정 우선. 설정 초기화 시 기기 기본값 재계산. 단계별 청크 상주 상한 9/12/16 | 명세 7.1 "저사양 자동 낮춤". 기준은 시작점 — 실기기 측정 후 조정 |
| D-051 | 2026-11-08 | 실기기 측정 도구: **개발·성능 측정 빌드 전용** 오버레이(OnGUI, 세 손가락 탭) + 로그 파일(경고 이상, 최근 5개·2MB, 기기 밖 전송 없음). 성능 측정 빌드 = 릴리스 최적화 + `TG_PERF_HUD` 정의 | 명세 10.3 Development/Release 구분 측정. 릴리스(AAB)에는 둘 다 없음 |
| D-052 | 2026-11-08 | Android 뒤로가기: 설정 → 격납고 순으로 닫고, 메인 메뉴에서는 2초 안에 두 번 눌러야 종료. 비행 중엔 일시정지(설정 열려 있으면 설정부터 닫기) | Android 사용 관례·실수 종료 방지 |
| D-053 | 2026-11-08 | 홈 화면 이름: 기본 "TOWERGLIDE", 한국어 기기 "타워글라이드" — Android는 Gradle 후처리(launcher `values-ko/strings.xml`), iOS는 `ko.lproj/InfoPlist.strings`. iOS `ITSAppUsesNonExemptEncryption = NO` | D-003 확정 구현. 플랫폼 모듈 의존 코드는 `#if UNITY_ANDROID/UNITY_IOS` |
| D-054 | 2026-11-08 | 빌드마다 Android 권한 점검 보고서(`Builds/Android/permissions-report.txt`), 허용 목록 = VIBRATE. 릴리스 AAB에서 목록 밖 권한은 오류 로그 | 명세 13 "사용하지 않는 권한 금지", 데이터 보안 답변과 일치 |
| D-055 | 2026-11-08 | **임시 앱 아이콘** = 소유자 시안(webp)을 Windows 이미지 코덱으로 디코딩 → 검은 둥근 모서리(대각선 70px) 잘라 1024/512/192 PNG. 모든 플랫폼 기본 아이콘으로 지정, 스플래시 배경 남색 | 실기기에서 식별용. 출시 아이콘 확정·적응형 아이콘은 [소유자] |
