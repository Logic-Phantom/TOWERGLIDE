# WORKLOG

각 작업 단위의 변경 파일·테스트 결과·알려진 문제·수동 작업을 기록한다. 최신이 위.

---

## 2026-11-08 — 한글 폰트(Pretendard) 도입

요청: "폰트 도입"(D-048 후속).

### 한 일
- 다운로드: `orioncactus/pretendard` 태그 v1.3.9 `packages/pretendard/dist/public/static/Pretendard-{Regular,Bold}.otf` + `LICENSE`(raw.githubusercontent.com). jsDelivr 경로는 404·패키지 크기 제한으로 실패 → GitHub API 트리에서 경로 확인 후 받음.
- 검증: 파일 크기가 GitHub 트리 값과 일치(1,574,352 / 1,576,660 B), OTF(`OTTO`) 헤더·테이블 정상, SHA-256 기록. cmap 분석 결과 한글 11,172자 전부, UI에 쓰는 기호(▲ ★ ° · — … © → ↔) 지원. 미지원: 🔒(격납고 문구 → "잠김"으로 교체), ▸(에디터 로그 전용이라 영향 없음).
- 배치: `Art/Fonts/`(폰트 2 + OFL 원문), `Resources/Licenses/Pretendard-OFL.txt`(앱 표시용).
- 적용: 프로젝트 설정 메뉴가 동적 폰트·폰트 데이터 포함·대체 폰트 이름 설정, 씬 생성기의 모든 라벨·토글이 Pretendard(굵게 = Bold 파일), 라이선스 화면을 스크롤 + OFL 전문.

### 테스트 결과
| 범위 | 결과 |
|---|---|
| 폰트 파일 형식·문자 지원 | ✅ 스크립트로 확인(위) |
| Unity 임포트·화면 표시 | **미실행** — Unity 미설치. `TOWERGLIDE ▸ 2. 씬 생성`을 다시 실행해야 기존 씬에 반영됨 |

### 알려진 위험
- CFF 기반 OTF — Unity 동적 폰트(FreeType)는 지원하지만, 문제가 있으면 같은 저장소의 TTF(`static/alternative/`, 약 2.7MB씩)로 교체.

---

## 2026-11-08 — Phase 3 UI·오디오·진행

### 한 일
- Core: `Audio/ProceduralAudio`(엔진 3종·바람 루프, 효과음 5종, 교차 페이드 루프), `EngineSoundModel`, `Tutorial/TutorialSequence`, `Save/SettingsOptions`.
- Runtime: `AudioManager`(GameApp 소유, 클립 캐시, 8 보이스), `FlightAudioController`(엔진·바람 루프), `SettingsPanelController`, `TutorialController`, `LicenseNotices`, `UiClickSound`. `GameApp.CommitSettings`/`SettingsChanged`, `FlightSession`의 효과음·설정 즉시 반영, 기체 정의에 엔진음 종류.
- Editor: 설정 화면(DefaultControls 슬라이더·토글)을 메인 메뉴·일시정지에 생성, 튜토리얼 UI, 비행 사운드, 모든 메뉴 버튼 클릭음, 기체별 엔진음 지정.
- 이미 있던 Phase 3 항목(메뉴·HUD·일시정지·결과·로컬 기록·햅틱)은 유지.

### 테스트 결과
| 범위 | 결과 |
|---|---|
| core-tests | ✅ 111케이스 통과 ([run](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42467226975)) — 신규 17: 사운드 6·엔진음 3·튜토리얼 4·설정 4 |
| PlayMode 신규 6(저장 파일 3·사운드 2·튜토리얼 1) | **미실행** — Unity 미설치 |
| 실제 소리·화면 배치 | **미확인** |

### 알려진 위험·미포함
- 합성 사운드는 기능 검증용 품질 — 실제 기기 스피커에서 거슬리지 않는지 들어 봐야 한다.
- 설정 화면 배치는 1920×1080 기준 좌표 — 21:9 폰·태블릿에서 겹침 확인 필요.
- 한글 폰트(TMP) 미도입(D-048), 음악 없음(D-044).

---

## 2026-11-08 — 애드온: 서울 오픈월드 OW0·OW1 + 기체 선택 AC0·AC1

요청: `TOWERGLIDE_Seoul_OpenWorld_Aircraft_Addon_Prompt.md` 추가 요건 반영(사본 `docs/SUPERPROMPT_ADDON_SEOUL_AIRCRAFT.md`). 명세 지시대로 **OW0·OW1·AC0·AC1만** 수행.

### 기존 구조 분석·충돌
- 800m 테스트 도시(단일 씬) = 명세의 World POC와 같은 크기 → 청크 스트리밍 시험장으로 재사용. 비행 컨트롤러·튜닝 분리가 이미 돼 있어 기체 선택은 데이터 추가로 해결.
- 충돌 5건과 결정: `docs/SEOUL_WORLD_ARCHITECTURE.md` §1, `docs/PROMPT_REVIEW.md` A1~A8, D-033~D-041. 가장 큰 것: 명세의 "비상업 우선" ↔ 스토어 공개 배포 → 공개 배포 기준으로 데이터 판정.

### 한 일
- Core: `World/Geo`(TM 투영 EPSG:5186, `WorldCoordinateSystem`), `World/Streaming`(청크 격자·상태 기계·스트리밍 판단), `SeoulWorldDefinition`(원점·청크·계획 권역 6개, 좌표 미검증 표시), `Aircraft`(ID·분류·태그·해금·호환성·선택 복구·성능 막대), 저장 v2, 항력 보상 추력.
- Runtime: 청크 카탈로그·로더(Additive 비동기, 로드 시간 측정)·스트리밍 매니저·원거리 실루엣, 기체 정의·카탈로그·선택 서비스·스폰 서비스·카메라 프로파일, 격납고·3D 미리보기, 메인 메뉴 연동, HUD 나침반·스트리밍 디버그, 세션의 기체 연결·모드 호환성·청크 준비 대기.
- Editor: 기체 팩토리(튜닝 3·프리팹 3·정의 7·카탈로그), 씬 생성기의 청크 분할(16개 씬)·실루엣·스트리밍·스폰·격납고 UI, 코스 검증의 청크 씬 Additive 열기.
- 문서: SEOUL_WORLD_DATA_SOURCES / ARCHITECTURE / PERFORMANCE, AIRCRAFT, 결정·로드맵·작업 규칙·명세 검토.

### 서울 데이터 소스·라이선스 확인 결과
- OSM(ODbL): 공개 배포 가능하나 파생 DB 공유 방식 결정 필요 → `PENDING_REVIEW`. S-Map: 참고 전용, 데이터셋 조건 미확인. 공공 데이터·건물 높이·DEM: `PENDING_REVIEW`. 지도 서비스 캡처: 금지.
- **다운로드·임포트한 실제 데이터 0건.** 권역 좌표는 계획용 대략값이며 테스트로 "미검증"이 강제된다.

### 테스트 결과
| 범위 | 결과 |
|---|---|
| core-tests | ✅ 94케이스 통과 ([run](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42466537780)) — 신규 29: 좌표계 5·월드 정의 2·청크 격자 3·상태 기계 2·스트리밍 판단 7·기체 규칙 3·성능 막대 2·저장 3·추력 2 |
| PlayMode 신규 7(기체 비교 4·오픈월드 흐름 3) + 기존 수정 | **미실행** — Unity 미설치 |
| 청크 전환 실제 동작·로드 시간·메모리·부스트 스트레스 | **미실행** |
| Android 실기기 | **미실행** |

### 알려진 위험
- 씬 생성기가 크게 늘었다(청크 씬 생성·이동·저장, CombineMeshes, 프리팹 인스턴스의 씬 간 이동). Unity에서 처음 실행할 때 가장 먼저 확인할 곳.
- 청크 씬 16개가 Build Settings에 들어간다 — 씬 경로가 바뀌면 카탈로그와 함께 다시 생성해야 한다.
- 항력 보상 추력으로 기존 Scout의 실제 순항 속도가 약간 올라갔다(이전엔 목표보다 약 2~3 m/s 낮게 유지됐음).
- 여객기는 스폰 +260m에서 시작한다 — 도시 최고층(약 320m) 근처라 시작 직후 고도 판단 필요.

---

## 2026-11-08 — Phase 2 도시·장애물 코스

### 한 일
- Core `World/`: `Float3`, `CourseDefinition`·`SkylineCourses`, `CourseValidator`, `BuildingCatalog`(12종·높이 범위·가중치), `CityLayoutGenerator`(블록당 난수 소비 고정, 예약·최소 높이 블록, 코스 높이 제한).
- Runtime: `ProceduralMeshes`(미터 UV 박스, 외벽/지붕 서브메시), `FeedbackFx`+`PooledParticle`(ObjectPool), 링 빛기둥(`CheckpointGate.beacon`), HUD 근접 회피 플래시·경계 화살표, `FlightSession`에서 효과·진동 연결.
- Editor: `TowerglideMaterials`(공유 머티리얼, 창문 텍스처 3종 생성), `TowerglideBuildingFactory`(아키타입 모듈·콜라이더·LOD), `TowerglidePrefabFactory`(프리팹 13종), `TowerglideCourseValidator`(3번 메뉴), `TowerglideSceneBuilder` 개편(레이아웃·프리팹 기반, 메시 에셋 저장, 생성 후 자동 검증).
- 테스트: `WorldLayoutTests` 15케이스(EditMode), `CityFlightSceneTests` 5케이스(PlayMode).
- CI: `GitHubActionsTestLogger`로 실패 테스트를 주석 출력.

### 테스트 결과
| 범위 | 결과 |
|---|---|
| core-tests 1차 | ❌ `ReservedAndMinHeightLotsAreRespected` — 스카이브리지 지지 건물(210,140) 높이 28m. **원인: 코스 08→09 대각선 구간이 건물 옆을 지나 높이 제한이 걸림 = 실제로는 링 경로가 120m 건물에 막히는 설계 결함** |
| 조치 | 09~12번 링 재배치(08→09는 거리 따라 상승) — D-026 |
| core-tests 최종 | ✅ 65케이스 통과 ([run](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42457662405)) |
| Unity 컴파일·PlayMode·씬 생성·코스 검증 메뉴 | **미실행** — Unity 미설치 |

### 알려진 위험
- 에디터 스크립트 분량이 크게 늘었다(머티리얼·텍스처 임포터·프리팹·LOD·파티클 API). Unity에서 처음 열 때 API 이름 오류 가능성이 가장 높은 곳.
- 코스 검증은 직선 근사. 실제 선회 경로는 실플레이로 확인.
- URP 파티클 머티리얼 투명 설정을 속성 값으로만 지정 — 에디터에서 Surface Type이 Transparent로 보이는지 확인 필요.

---

## 2026-11-08 — Phase 0 부트스트랩 + Phase 1/2 코드 골격

### 환경 조사
| 항목 | 결과 |
|---|---|
| 작업 폴더 | `C:\eclipse_AI\TOWERGLIDE` — 비어 있음, git 아님 |
| 원격 | `https://github.com/Logic-Phantom/TOWERGLIDE` — 비어 있음(브랜치 없음) |
| Unity Editor / Hub | **설치 안 됨** (`C:\Program Files\Unity*` 없음) |
| Android SDK | `%LOCALAPPDATA%\Android\Sdk` — platform android-37, NDK 27.1, build-tools 있음(이전 앱 작업용). Unity는 자체 모듈 SDK/NDK 사용 권장 |
| JDK | OpenJDK 17.0.20 |
| .NET SDK | 없음. `C:\eclipse_AI\.tools`에 로컬 설치 시도 → **권한 거부**(D-020) |
| gh CLI | 없음 |
| 참고 프로젝트 | 컷픽(로컬 `C:\eclipse_AI\Cutpick`), 웨어로그·킵온(GitHub `Logic-Phantom/WearLog`, `KeepOn` 얕은 복제로 문서 구조만 확인) — README "지금 상태 한눈에", `docs/DECISIONS.md` D-번호, `RELEASE_CHECKLIST.md` [코드]/[소유자], 비용·브랜드·개인정보 문서, GitHub Pages 처리방침 관례를 따름 |

### 한 일
- 명세 검토 → 15개 보완점 반영(`docs/PROMPT_REVIEW.md`): iOS 추가, asmdef 계층, .NET Core 테스트, 버전 단일 소스, 서명키 처리 등.
- Unity 프로젝트 골격: `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `Assets/_Project/**` 폴더, asmdef 5개.
- Core(순수 C#) 11파일: 입력 정규화, 부스트 게이지, 공력 함수, 체크포인트 순서, 점수, 근접 회피 쿨다운, 비행 기록, 상태 기계, 저장 데이터·이관.
- Runtime 22파일: 비행 컨트롤러·튜닝 SO, 충돌 처리, 입력 라우터·가상 조이스틱·터치 버튼·키보드/게임패드, 추적 카메라, 링·코스·근접 회피 센서·장애물 표시, 경계·이동 장애물, 비행 세션, 저장 서비스, 햅틱, 앱 루트·씬 전환·품질 프리셋, HUD·SafeArea·메인 메뉴.
- Editor 4파일: 프로젝트 설정 적용(Player·입력·URP 3단계·Quality), 씬 생성(그레이박스 도시·장애물 7종·12링 코스·기체·HUD·메뉴), 빌드(AAB/APK/iOS Xcode, 검증), 공통 도우미.
- 테스트: EditMode 6파일(50케이스), PlayMode 1파일(6케이스).
- CI: `core-tests`(push마다 .NET 테스트), `unity-build`(수동, GameCI — 시크릿 필요).
- 문서: README, CLAUDE/AGENTS, docs 17종 + 처리방침 HTML, 아이콘 시안(`brand/icon-concept.webp`), 원본 명세 사본(`docs/SUPERPROMPT.md`).

### 테스트 결과
| 범위 | 결과 |
|---|---|
| Unity 컴파일 | **미실행** — Unity 미설치 |
| EditMode / PlayMode | **미실행** — Unity 미설치 |
| .NET core-tests(로컬) | **미실행** — .NET SDK 설치 권한 거부 |
| .NET core-tests(GitHub Actions) | ✅ 성공 — Core 컴파일 + EditMode 50케이스 통과 ([run 42456690200](https://github.com/Logic-Phantom/TOWERGLIDE/actions/runs/42456690200)). 로그 원문은 인증이 필요해 개수는 테스트 메서드 수 기준 |
| 씬 생성 / 빌드 | **미실행** |

### 알려진 위험
- 코드 전체가 **Unity 컴파일러로 한 번도 검증되지 않음**. 처음 열 때 API 이름·asmdef 참조 오류가 있을 수 있음(특히 Editor 스크립트의 URP 속성, `PlayerSettings` 일부, QualitySettings SerializedObject 경로).
- 비행 튜닝 값은 계산상 시작점 — 실플레이 전에는 조작감 판단 불가.
- legacy `Text` 한글·▲ 글리프는 OS 폰트 폴백 의존.

### 소유자 수동 작업
`README.md` ▸ "다음 할 일" 1~3번.
