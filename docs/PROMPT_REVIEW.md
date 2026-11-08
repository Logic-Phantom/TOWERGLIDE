# SuperPrompt 검토 결과 (2026-11-08)

원본: `TOWERGLIDE_Unity_ClaudeCode_SuperPrompt.md`(소유자 제공). 방향(조작감 최우선, 무료·오프라인, Phase 순서, 검증 과장 금지)은 그대로 따르고, 아래 항목을 **보완·수정**해 반영했다.

| # | 원본 | 문제 / 빈틈 | 반영 |
|---|---|---|---|
| 1 | Android·Google Play 전용 | 소유자 요구는 **App Store + Google Play** | iOS 설정(IL2CPP, 15.0+, 가로 전용 전체 화면, 제스처 지연), iOS Xcode 빌드 메뉴, App Store 체크리스트·비용·개인정보 라벨 추가(D-002) |
| 2 | Windows 개발 환경 언급 없음 | Unity iOS는 Xcode(macOS) 필수 | Windows에서 Xcode 프로젝트까지, Archive는 Mac/CI — 경로 선택을 소유자 결정 항목으로 |
| 3 | `Scripts/` 하위 폴더만 제시 | 폴더만으로는 의존 방향이 강제되지 않음 | **asmdef 4계층**(Core 순수 C# / Runtime / Editor / Tests) — UI→Router→Flight 규칙을 컴파일러가 강제(D-007) |
| 4 | 테스트는 Unity Test Runner만 | Unity 라이선스 없으면 CI 불가 | Core + EditMode 테스트를 **일반 .NET으로도 실행**(`tools/core-tests`, GitHub Actions)(D-012) |
| 5 | 입력: "Input System 또는 EventSystem" 미정 | 결정 필요 | Input System 1.19 직접 폴링 + uGUI 조이스틱(D-006) |
| 6 | 물리 단위·힘 적용 방식 미정 | 질량을 바꾸면 모든 수치가 틀어짐 | **질량 무관 가속도** + 목표 각속도 추종(손 떼면 자연 감쇠) + 속도 정렬 계수(D-008) |
| 7 | `GOOGLE_PLAY_RELEASE.md` | 두 스토어면 중복 | `RELEASE_CHECKLIST.md` 하나에 `[코드]/[소유자]` 구분(기존 앱 관례, D-019) |
| 8 | 버전 관리 "일관되게" | 두 스토어 버전이 따로 놀기 쉬움 | `build/version.json` 단일 소스 → 빌드 시 Android versionCode·iOS build number 동시 반영(D-017) |
| 9 | 서명키 "커밋하지 않는다" | 구체 방법 없음 | 환경 변수 전달·빌드 후 비밀번호 제거·CI 시크릿(base64) 경로(D-018) |
| 10 | 랜덤 도시 "플레이 공간을 막지 않게" | 방법 미정 | 코스 구간 주변 건물 높이 자동 제한(D-022) |
| 11 | 근접 회피 중복 방지 | 트리거 간 간섭(기체 센서가 링 트리거를 먼저 건드림) | 트리거끼리 무시 + 장애물별 쿨다운·충돌 제외(D-021) |
| 12 | 개인정보처리방침 언급만 | 데이터 수집이 없어도 스토어에 URL 필요 | `docs/legal/privacy.html`(GitHub Pages 게시용) |
| 13 | 타깃 연령 미언급 | 아동 대상 선택 시 Play 가족 정책 추가 요건 | 13세 이상 권장, 소유자 결정 항목 |
| 14 | Unity 스플래시 미언급 | Unity 6 Personal은 끌 수 있음 | 끔 + 약관 재확인(D-016) |
| 15 | Boot 씬 필수처럼 기술 | 어느 씬에서 Play해도 초기화되면 불필요 | `GameApp` 자동 생성, Boot 생략(D-010, D-011) |

## 그대로 유지한 원칙
- 조작감 검증 전 대형 도시·메뉴·모드 금지 / 무료·오프라인 / 유료 에셋·SDK 금지 / 라이선스 기록 / 다른 작품 복제 금지 / 실행 안 한 것을 성공으로 보고하지 않기.
