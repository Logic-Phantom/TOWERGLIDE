# 출시 체크리스트 (Google Play + App Store)

**[코드]** = 저장소에 반영됨, **[소유자]** = 계정·결제·법적 판단이 필요해 소유자가 직접 해야 함. 끝나면 `- [x]`와 날짜를 적는다. 정책 수치(타깃 API, 비용, 테스터 수)는 **제출 직전 공식 문서로 재확인**한다.

## A. 코드에 반영된 것 (2026-11-08, 모두 Unity 미실행 상태 — 에디터에서 확인 필요)
- [x] [코드] 패키지/번들 ID `com.logicphantom.towerglide`, 버전 단일 소스 `build/version.json`(D-004, D-017)
- [x] [코드] Android: IL2CPP · ARM64 · minSdk 26 · targetSdk 36 · AAB · INTERNET 강제 권한 끔 · 가로 고정(D-005)
- [x] [코드] iOS: IL2CPP · iOS 15.0+ · 가로 전용 `requiresFullScreen` · 가장자리 제스처 지연
- [x] [코드] 빌드 메뉴 + 배치 진입점(`TowerglideBuild`), 릴리스 빌드 전 설정 검증(씬·IL2CPP·ARM64·targetSdk·Development 여부)
- [x] [코드] 업로드 키는 환경 변수로만, 빌드 후 비밀번호 제거, `.gitignore`에 키·인증서 확장자(D-018)
- [x] [코드] 광고·분석·로그인·서버 SDK 없음 → 데이터 수집 없음(D-015)
- [x] [코드] 백그라운드 전환 시 자동 일시정지 + 저장
- [x] [코드] (2026-11-08) 한국어 홈 화면 이름 "타워글라이드"(Android values-ko, iOS ko.lproj), iOS `ITSAppUsesNonExemptEncryption = NO`, Android 권한 점검 보고서, 임시 아이콘·스플래시 배경, 성능 측정용 APK 메뉴 — 실기기 절차는 `docs/DEVICE_TEST_GUIDE.md`

## B. 개발 환경 (소유자)
- [ ] [소유자] Unity Hub 설치 → **Unity 6.3 LTS** + 모듈: Android Build Support(OpenJDK, Android SDK & NDK Tools), iOS Build Support
- [ ] [소유자] Unity 계정 로그인 → Personal 라이선스 활성화(최근 12개월 매출·자금 US$200K 미만 조건 — 공식 약관 확인)
- [ ] 프로젝트 열기 → `TOWERGLIDE ▸ 1. 프로젝트 설정 적용` → (재시작) → `TOWERGLIDE ▸ 2. 씬 생성` → Test Runner에서 EditMode/PlayMode 실행 → 결과를 README에 기록

## C. Android / Google Play (등록비 US$25 1회)
1. 업로드 키 만들기(한 번, 저장소 밖 안전한 곳 + 별도 백업):
   ```bash
   keytool -genkeypair -v -keystore towerglide-upload.keystore -alias towerglide -keyalg RSA -keysize 2048 -validity 10000
   ```
   - [ ] [소유자] 키 파일·비밀번호를 비밀번호 관리자 등에 백업(분실 시 Play 지원팀 재설정 절차 필요)
2. - [ ] 환경 변수 설정 후 `TOWERGLIDE ▸ Build ▸ Android AAB` → `Builds/Android/TOWERGLIDE-<버전>-<빌드>.aab`
3. - [ ] [소유자] Play Console 개발자 계정(개인: 신원 확인). **2023-11-13 이후 만든 개인 계정은 프로덕션 전 테스터 12명 이상 × 14일 연속 비공개 테스트**(현재 기준 재확인)
4. - [ ] [소유자] 앱 만들기(게임, 무료, 기본 언어 한국어) → Play App Signing 사용
5. - [ ] 스토어 등록정보: `docs/STORE_LISTING.md`, 512px 아이콘, 1024×500 그래픽 이미지, **실제 플레이 스크린샷**(구현 안 된 기능 금지)
6. - [ ] 앱 콘텐츠: 개인정보처리방침 URL(`docs/legal/privacy.html` → GitHub Pages), 데이터 보안 "수집·공유 없음", 광고 없음, 콘텐츠 등급(IARC), 타깃 연령 **[소유자 결정 — 13세 이상 권장: 아동 대상 선택 시 가족 정책 추가 요건]**
7. - [ ] 내부 테스트 → 비공개 테스트(12명·14일) → Pre-launch report 확인(크래시·ANR·화면 잘림) → 프로덕션

## D. iOS / App Store (Apple Developer Program 연회비 US$99)
- [ ] [소유자] Apple Developer Program 가입(개인 — 판매자 이름이 실명으로 표시됨)
- [ ] [소유자] App Store Connect에 앱 생성(번들 ID 동일), SKU, 기본 언어 한국어
- [ ] **macOS 빌드 경로 결정 [소유자]**: ① Mac(직접/대여) ② GitHub Actions macOS 러너(공개 저장소 무료, 비공개는 분당 과금·배수 적용) ③ Unity Build Automation 등 클라우드 — 비용은 결정 시점 공식 가격 확인
- [ ] `TOWERGLIDE ▸ Build ▸ iOS Xcode 프로젝트` → macOS Xcode에서 Signing(자동 서명) → Archive → TestFlight
- [ ] 개인정보 보호 라벨 "데이터 수집 안 함", 개인정보처리방침 URL, 연령 등급 설문, 수출 규정 "암호화 미사용"(Info.plist `ITSAppUsesNonExemptEncryption = NO`는 빌드 후처리로 반영됨, D-053)
- [ ] Unity 6가 포함하는 **Privacy Manifest(PrivacyInfo.xcprivacy)**가 Xcode 프로젝트에 들어갔는지 확인
- [ ] 실기기(TestFlight)에서 노치·홈 인디케이터·가로 회전·백그라운드 복귀 확인

## E. 공통 자산·법률
- [ ] [소유자] 아이콘 확정(현재 시안 이미지 — 상표·제3자 요소 없는지 확인 후 Android 적응형 아이콘·iOS 1024px 생성)
- [ ] [소유자] 상표 검색: KIPRIS "TOWERGLIDE/타워글라이드" 9류·41류, 스토어 동명 앱 검색(`docs/BRAND_AND_TRADEMARKS.md`)
- [ ] 개인정보처리방침 게시: GitHub → Settings → Pages → `main` / `/docs` → `https://logic-phantom.github.io/TOWERGLIDE/legal/privacy.html` 200 확인
- [ ] 지원 이메일 확정(스토어 필수 연락처) → `docs/legal/privacy.html`·스토어에 반영
- [ ] 외부 에셋·폰트를 추가했다면 `docs/THIRD_PARTY_ASSETS.md`와 앱 내 고지

## F. CI 빌드 (선택)
- [ ] [소유자] GitHub Secrets: `UNITY_LICENSE`(또는 `UNITY_EMAIL`/`UNITY_PASSWORD`), `TOWERGLIDE_KEYSTORE_BASE64`, `TOWERGLIDE_KEYSTORE_PASS`, `TOWERGLIDE_KEY_ALIAS`, `TOWERGLIDE_KEY_ALIAS_PASS`
- [ ] Actions ▸ unity-build ▸ Run workflow(`tests` → `Android` → `iOS`)

## 보고 원칙
"출시 준비 완료"와 "실제 스토어 게시 완료"를 구분해 기록한다. 실제로 만든 빌드 파일만 경로·크기와 함께 적는다.
