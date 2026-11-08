# 실기기 테스트 가이드 (Android 우선, iOS는 TestFlight)

이 문서대로 하면 Unity 설치 직후부터 실기기 1차 테스트까지 한 번에 진행할 수 있다. 결과는 맨 아래 양식과 `docs/PERFORMANCE.md`·`docs/SEOUL_WORLD_PERFORMANCE.md`·`docs/TEST_PLAN.md`에 기록한다. **해 보지 않은 항목을 통과로 적지 않는다.**

## 0. 준비 (한 번만)
| 단계 | 방법 |
|---|---|
| Unity | Unity Hub → **6.3 LTS** + Android Build Support(OpenJDK, Android SDK & NDK Tools). iOS도 할 거면 iOS Build Support |
| 프로젝트 | Hub ▸ Add ▸ `C:\eclipse_AI\TOWERGLIDE` → 버전 경고 시 설치한 6.3 LTS 선택 → 컴파일 오류가 있으면 Console 전체를 Claude Code에 붙여 넣기 |
| 메뉴 | `TOWERGLIDE ▸ 1. 프로젝트 설정 적용`(재시작 요청 시 재시작) → `TOWERGLIDE ▸ 2. 씬 생성` → `TOWERGLIDE ▸ 3. 코스 검증`(문제 0건이어야 함) |
| 에디터 테스트 | Window ▸ General ▸ Test Runner ▸ EditMode·PlayMode **Run All** → 실패가 있으면 실기기 전에 먼저 해결 |
| 폰 | 설정 ▸ 휴대전화 정보 ▸ 소프트웨어 정보 ▸ 빌드번호 7번 탭 → 개발자 옵션 ▸ **USB 디버깅** 켜기 → USB 연결 후 폰에서 "허용" |
| adb | 이 PC: `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`(이미 설치됨). 연결 확인: |

```bash
adb devices
```

## 1. 빌드 3종 — 목적에 맞게
| 메뉴 | 산출물 | 언제 |
|---|---|---|
| `TOWERGLIDE ▸ Build ▸ Android APK (개발·실기기 테스트)` | `Builds/Android/TOWERGLIDE-<버전>-<빌드>-dev.apk` | **1차 기능 테스트**. Development 빌드 — 측정 오버레이 + 로그 파일 + Profiler 연결 가능. 디버그 키 서명 |
| `TOWERGLIDE ▸ Build ▸ Android APK (성능 측정용)` | `...-perf.apk` | **FPS·발열 측정**. 릴리스와 같은 최적화 + 측정 오버레이. 문서에는 이 빌드 수치를 기록 |
| `TOWERGLIDE ▸ Build ▸ Android AAB (Google Play 릴리스)` | `...aab` | 스토어 업로드. 업로드 키 환경 변수 필요(`docs/RELEASE_CHECKLIST.md`) |

빌드할 때마다 `Builds/Android/permissions-report.txt`가 생긴다 — **VIBRATE 외 권한이 있으면 원인부터 확인**.

## 2. 설치·실행·로그
```bash
adb install -r Builds/Android/TOWERGLIDE-0.1.0-1-dev.apk
```
```bash
adb logcat -c
```
```bash
adb logcat -s Unity:V AndroidRuntime:E
```
개발 빌드는 기기 안에도 로그 파일을 남긴다(경고·오류·예외만, 최근 5개):
```bash
adb shell run-as com.logicphantom.towerglide ls files/logs
```
```bash
adb shell run-as com.logicphantom.towerglide cat files/logs/<파일명>.log
```
권한 최종 확인(설치된 앱 기준):
```bash
adb shell dumpsys package com.logicphantom.towerglide | findstr permission
```

## 3. 측정 오버레이
- 개발·성능 측정 빌드에서 왼쪽 위에 표시: 평균 FPS · 1% Low · 최대 프레임 · 30fps 미만 비율 · 메모리 · 기기·GPU · 해상도 · 그래픽 단계 · 현재 청크.
- **세 손가락 탭**(또는 키보드 F2)으로 켜기/끄기, **복사** = 현재 값을 클립보드로(메신저 등으로 PC에 옮겨 문서 표에 붙여 넣기), **초기화** = 측정 구간 다시 시작.
- 첫 실행 시 기기 사양으로 그래픽 단계가 자동으로 정해진다(RAM 4GB 미만·코어 6개 미만 = Performance·30fps). 측정 전 오버레이의 단계를 기록.

## 4. 1차 테스트 순서 (약 30~40분)
| # | 항목 | 확인 방법 | 기대 결과 |
|---|---|---|---|
| 1 | 설치·첫 실행 | 앱 아이콘·이름 확인 후 실행 | 아이콘 표시, 한국어 폰은 홈 이름 **타워글라이드**, 가로 고정, 메인 메뉴 한글이 Pretendard로 표시 |
| 2 | 메뉴 | 각 버튼, 뒤로가기 | 클릭음, HANGAR/SETTINGS 열고 닫기, 뒤로가기 두 번 = 종료 |
| 3 | 첫 비행 튜토리얼 | FREE FLIGHT 시작 | 롤 → 선회 → 피치 → 부스트 안내, SKIP 가능, 두 번째 비행부터 안 나옴 |
| 4 | **조작감(가장 중요)** | 3분 자유 비행 | 명세 2.2 승인 기준 7개 — `docs/FLIGHT_PHYSICS.md` 표에 기종·증상·조정값 기록 |
| 5 | 터치 | 왼손 조이스틱 + 오른손 BOOST 동시, 손 떼기 | 오입력 없음, 손 떼면 즉시 중립, 버튼이 엄지에 닿음 |
| 6 | 화면 | 노치·펀치홀 방향 바꿔 가며 | HUD·버튼이 가려지지 않음(SafeArea), 설정 화면 요소 겹침 없음 |
| 7 | SKYLINE RUN | 완주 1회 + 일부러 충돌 | 링 순서·빛기둥·콤보 소리, 충돌 후 마지막 링에서 재개, 결과 화면·최고 기록 저장 |
| 8 | 기체 3종 | 격납고에서 각각 FREE FLIGHT | 속도·선회·무게감이 확실히 다름, 여객기 SKYLINE 막힘, 카메라 거리 적절 |
| 9 | 청크 스트리밍 | 제트 부스트로 도시 대각선 왕복 5회 | 빈 도시·건물 갑툭튀 없음, 오버레이 로드 시간·실패 0, 프레임 급락 여부 |
| 10 | 소리 | 엔진·바람·링·근접 회피·충돌 | 거슬리지 않음, 설정 볼륨 반영, 음소거 스위치 동작 |
| 11 | 진동 | 링·충돌 | 약/강 구분, 설정에서 끄면 없음 |
| 12 | 백그라운드 | 비행 중 홈 → 30초 → 복귀 | 자동 일시정지, 소리 멈춤, 복귀 후 정상 |
| 13 | 저장 | 설정 몇 개 바꾸고 앱 완전 종료 → 재실행 | 설정·기록·선택 기체 유지 |
| 14 | 장시간 | 성능 측정 빌드로 10분 | FPS 추이, 발열(손 감각·기기 온도), 메모리 증가 여부 |

## 5. 문제가 생기면
| 증상 | 먼저 볼 것 |
|---|---|
| 실행 직후 종료 | `adb logcat -s Unity:V AndroidRuntime:E`의 첫 Exception |
| 검은 화면 | logcat의 그래픽 API 오류 → Player ▸ Graphics APIs에서 Vulkan 제거 후 OpenGLES3만으로 재시도 |
| 글자가 네모 | 씬 생성을 다시 했는지(폰트 적용은 씬 생성 시), `Art/Fonts` 임포트 설정 |
| 건물 통과 | 오버레이 청크 상태(Failed?), 시작 시 "도시 불러오는 중" 대기 시간 |
| 조작 방향 반대 | 설정의 피치 반전 확인 후 `docs/FLIGHT_PHYSICS.md` 축 규칙과 비교 |

## 6. iOS (Mac 필요)
`TOWERGLIDE ▸ Build ▸ iOS Xcode 프로젝트` → Mac Xcode에서 팀 서명 → 실기기 실행 또는 Archive → TestFlight. Info.plist의 암호화 미사용 값·한국어 이름은 빌드 후처리가 넣는다. 위 4절 표를 같은 순서로.

## 7. 결과 기록 양식
| 날짜 | 기기·OS·RAM | 빌드(dev/perf) | 그래픽 단계 | 항목 # | 결과 | 메모(증상·수치·영상 링크) |
|---|---|---|---|---|---|---|
| | | | | | | |
