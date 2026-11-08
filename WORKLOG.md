# WORKLOG

각 작업 단위의 변경 파일·테스트 결과·알려진 문제·수동 작업을 기록한다. 최신이 위.

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
