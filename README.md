# TOWERGLIDE (타워글라이드)

> 빌딩 사이, 한계 없이.

<img src="brand/icon-concept.webp" alt="TOWERGLIDE 아이콘 시안" width="128" />

고층 빌딩 숲을 소형 곡예 비행기로 누비는 3D 아케이드 비행 게임. **Android(Google Play) + iOS(App Store)**, Unity 6.3 LTS + URP + C#, 싱글·오프라인·개인정보 수집 없음.

- **FREE FLIGHT** — 도시를 자유 비행, 충돌해도 바로 복귀, 거리·속도·고도 기록
- **SKYLINE RUN** — 협곡·크레인·드론·스카이브리지를 지나 링 12개를 순서대로, 콤보·근접 회피 점수
- 조작: 왼손 플로팅 조이스틱(기울이면 선회) + 오른손 BOOST/BRAKE

## 🧭 지금 상태 한눈에 (2026-11-08 — 다음 작업은 여기부터)

**한 줄 요약**: 프로젝트 구조·아키텍처·핵심 코드(비행 물리, 입력, 카메라, 코스, 점수, 저장, HUD)와 에디터 자동화(설정·씬 생성·빌드)를 작성했다. **개발 PC에 Unity가 없어 아직 한 번도 열거나 실행하지 않았다** — 다음 단계는 Unity 설치 → 컴파일 오류 수정 → 조작감 튜닝.

### 검증 상태
| 범위 | 상태 |
|---|---|
| Unity로 프로젝트 열기·컴파일 | ⬜ 미실행 (Unity 미설치) |
| EditMode 테스트(Core, 약 50케이스) | ⬜ Unity에서 미실행 · GitHub Actions `core-tests`(.NET)로 푸시마다 실행 — 아래 "변경 내역" 결과 참고 |
| PlayMode 테스트(비행 방향·안정화 6케이스) | ⬜ 미실행 |
| 씬 생성 메뉴 | ⬜ 미실행 (코드만 작성) |
| 에디터 Play·조작감 | ⬜ 미실행 |
| Android 실기기 / AAB | ⬜ 미실행 |
| iOS / Xcode | ⬜ 미실행 (macOS 필요) |
| 스토어 등록 | ⬜ 계정 없음 |

### 외부 서비스·계정 (비밀 값은 여기에 적지 않음)
| 서비스 | 상태 |
|---|---|
| GitHub `Logic-Phantom/TOWERGLIDE` | ✅ `main` 푸시 |
| Unity(Personal) | ⬜ 설치·로그인 필요 |
| Google Play Console | ⬜ 미가입(US$25 1회) |
| Apple Developer Program | ⬜ 미가입(연회비) |
| GitHub Pages(처리방침) | ⬜ 미설정 — `docs/legal/privacy.html` 준비됨 |

## 다음 할 일 (순서대로)
1. **Unity 설치**: Unity Hub → Unity **6.3 LTS** + Android Build Support(OpenJDK, SDK & NDK) + iOS Build Support.
2. **프로젝트 열기**: Hub ▸ Add ▸ `C:\eclipse_AI\TOWERGLIDE` → 버전 경고가 나오면 설치한 6.3 LTS 선택 → 컴파일 오류가 있으면 Claude Code에 Console 로그를 주고 수정.
3. **메뉴 실행**: `TOWERGLIDE ▸ 1. 프로젝트 설정 적용`(입력 방식 변경 시 재시작) → `TOWERGLIDE ▸ 2. 씬 생성` → Test Runner에서 EditMode/PlayMode 실행.
4. **Phase 1 조작감 튜닝**: `CityFlight` Play → 키보드와 Device Simulator로 비행 → `docs/FLIGHT_PHYSICS.md` 표대로 `AircraftFlightTuning` 조정 → 승인 기준 7개 체크.
5. **Android 실기기**: `TOWERGLIDE ▸ Build ▸ Android APK` → `adb install -r` → 터치 조작·프레임 확인.
6. 그 다음: Phase 2 프리팹화·코스 다듬기 → Phase 3 설정 화면·사운드·튜토리얼·한글 폰트(`docs/ROADMAP.md`).
7. 출시 준비(소유자): 패키지 ID 확정(D-004), 상표 검색, Play/Apple 계정, 업로드 키, 처리방침 게시 — `docs/RELEASE_CHECKLIST.md`.

## 설치·실행
요구 사항: Unity 6.3 LTS(6000.3.x), Windows 또는 macOS. iOS 빌드는 macOS + Xcode.

```text
Unity Hub ▸ Add ▸ (이 폴더) ▸ 열기
TOWERGLIDE ▸ 1. 프로젝트 설정 적용
TOWERGLIDE ▸ 2. 씬 생성 (MainMenu + CityFlight)
Assets/_Project/Scenes/CityFlight.unity ▸ Play  (또는 MainMenu에서 시작)
```

에디터 조작: W/S 피치(W = 상승) · A/D 롤 · Q/E 요 · ↑/↓ 스로틀 · Shift 부스트 · Space 브레이크 · C 카메라 · R 복귀 · Esc 일시정지. 터치는 Window ▸ General ▸ Device Simulator. 자세히: `docs/TOUCH_CONTROLS.md`.

## 테스트
- Unity: Window ▸ General ▸ Test Runner ▸ EditMode / PlayMode ▸ Run All
- Unity 없이 Core만(.NET 8 SDK 필요):
```bash
dotnet test tools/core-tests
```
- GitHub Actions: `core-tests`(push마다 자동), `unity-build`(수동, Unity 라이선스 시크릿 필요)

## 빌드
| 산출물 | 메뉴 | 비고 |
|---|---|---|
| 개발 APK | TOWERGLIDE ▸ Build ▸ Android APK | 디버그 키, 실기기 테스트용 |
| Google Play AAB | TOWERGLIDE ▸ Build ▸ Android AAB | 업로드 키 환경 변수 `TOWERGLIDE_KEYSTORE_PATH/PASS`, `TOWERGLIDE_KEY_ALIAS/_PASS` 필요 |
| iOS | TOWERGLIDE ▸ Build ▸ iOS Xcode 프로젝트 | macOS Xcode에서 Archive → TestFlight |

버전은 `build/version.json` 한 곳에서 관리(업로드마다 `build` +1). 출력은 `Builds/`(git 제외).

## 구조
```
Assets/_Project/
  Scripts/Core/      순수 C# 규칙(입력 정규화·부스트·공력·체크포인트·점수·근접 회피·상태·저장 검증)
  Scripts/Runtime/   App · Flight · Input · Cameras · Gameplay · World · Save · Platform · UI
  Editor/            프로젝트 설정 · 씬 생성 · 빌드
  Tests/             EditMode(Core) · PlayMode(비행)
  Art/ Prefabs/ Scenes/ Settings/
build/version.json · tools/core-tests/ · .github/workflows/ · docs/ · brand/
```
의존 방향 `Editor → Runtime → Core`, 입력 흐름 `UI·키보드 → FlightInputRouter → AircraftFlightController`. 자세히: `docs/ARCHITECTURE.md`.

## 문서
| 문서 | 내용 |
|---|---|
| [ARCHITECTURE](docs/ARCHITECTURE.md) | 계층·런타임 흐름·씬 생성·확장 지점 |
| [DECISIONS](docs/DECISIONS.md) | 결정 기록 D-001~ |
| [PROMPT_REVIEW](docs/PROMPT_REVIEW.md) | 원본 명세 검토·보완 15항목 |
| [GAME_DESIGN](docs/GAME_DESIGN.md) · [FLIGHT_PHYSICS](docs/FLIGHT_PHYSICS.md) · [TOUCH_CONTROLS](docs/TOUCH_CONTROLS.md) | 모드·코스·장애물·점수 / 물리 모델·튜닝 / 조작 |
| [PERFORMANCE](docs/PERFORMANCE.md) · [TEST_PLAN](docs/TEST_PLAN.md) | 최적화·측정 / 테스트 매핑·결과 |
| [RELEASE_CHECKLIST](docs/RELEASE_CHECKLIST.md) · [APP_STORE_AND_COSTS](docs/APP_STORE_AND_COSTS.md) · [STORE_LISTING](docs/STORE_LISTING.md) | 두 스토어 출시 절차 / 비용 / 등록 문안 |
| [PRIVACY](docs/PRIVACY.md) · [legal/privacy.html](docs/legal/privacy.html) · [BRAND_AND_TRADEMARKS](docs/BRAND_AND_TRADEMARKS.md) · [THIRD_PARTY_ASSETS](docs/THIRD_PARTY_ASSETS.md) | 개인정보 / 처리방침 / 상표 / 에셋 라이선스 |
| [ROADMAP](docs/ROADMAP.md) · [SUPERPROMPT](docs/SUPERPROMPT.md) | 단계별 진행 / 원본 명세 |
| [WORKLOG](WORKLOG.md) · [CLAUDE.md](CLAUDE.md) | 작업 일지 / 에이전트 작업 규칙 |

## 변경 내역 (Changelog)
### 0.1.0 (build 1) — 2026-11-08 · 프로젝트 부트스트랩
- Unity 6.3 LTS 프로젝트 골격, asmdef 4계층(Core/Runtime/Editor/Tests), 패키지 매니페스트
- 비행 물리(질량 무관 가속도·목표 각속도·협조 선회·실속·부스트 게이지), 터치 조이스틱·버튼, 키보드/게임패드, 추적 카메라(충돌 회피·FOV·흔들림)
- FREE FLIGHT / SKYLINE RUN 세션, 12링 코스, 장애물 7종, 근접 회피, 경계, 안전 지점 복귀, 일시정지·결과·재시작
- 로컬 저장(원자적 쓰기·버전·손상 복구), 햅틱, 품질 3단계, SafeArea HUD, 메인 메뉴
- 에디터 자동화: 프로젝트 설정·씬 생성·빌드(AAB/APK/iOS Xcode)
- 테스트 EditMode ~50 / PlayMode 6, CI(core-tests, unity-build)
- 문서 17종, 개인정보처리방침 초안, 출시 체크리스트(Android+iOS)
- **검증**: Unity·실기기 미실행. `core-tests`는 GitHub Actions에서 실행(결과는 Actions 탭)

## 작업 내역 (Work Log)
| 날짜 | 작업 | 결과 |
|---|---|---|
| 2026-11-08 | 환경 조사, 명세 검토(15개 보완), 구조·아키텍처·핵심 코드·에디터 자동화·테스트·CI·문서 작성, 첫 푸시 | 코드 작성 완료 / Unity 미검증 — 상세 [WORKLOG.md](WORKLOG.md) |
