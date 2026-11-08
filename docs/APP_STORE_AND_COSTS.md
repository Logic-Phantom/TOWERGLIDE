# 비용·스토어 배포

> 금액·조건은 2026-11-08 작성 시점의 알려진 값이며 공식 페이지에서 다시 확인하지 않았다. **출시 직전 공식 링크에서 재확인**하고 날짜와 함께 갱신한다. 이 프로젝트를 "완전히 비용 0원으로 게시할 수 있다"고 보지 않는다.

| 구분 | 항목 | 비용 성격 | 공식 출처 |
|---|---|---|---|
| 엔진 | Unity Personal | 무료(최근 12개월 매출·자금 US$200K 미만 등 자격 조건) | https://unity.com/products |
| 개발 | Android 빌드(Windows 가능) | 무료(Unity가 SDK/NDK/OpenJDK 모듈 제공) | Unity Hub |
| 개발 | iOS 빌드 | **macOS + Xcode 필요**. 이 PC(Windows)에서는 Xcode 프로젝트 생성까지만 | developer.apple.com |
| 배포 | Google Play Console | US$25 1회 | https://support.google.com/googleplay/android-developer/answer/6112435 |
| 배포 | Apple Developer Program | 연회비(US$99) | https://developer.apple.com/programs/ |
| CI(선택) | GitHub Actions Linux(Core 테스트) | 공개 저장소 무료, 비공개는 무료 분량 후 과금 | https://docs.github.com/billing |
| CI(선택) | macOS 러너 / 클라우드 빌드 | 사용량 과금 가능 | 각 서비스 가격표 |
| 운영 | 서버·DB·분석 | **MVP는 없음 → 0** | — |
| 수익 | 스토어 수수료 | 유료화 시 발생(현재 무료·광고 없음) | 각 스토어 |
| 향후 | 광고·IAP·리더보드 | 도입 시 개인정보 라벨·데이터 보안·연령 정책 재검토 | — |

## 빌드 경로 요약
| 산출물 | 어디서 | 명령 |
|---|---|---|
| 개발 APK(실기기) | Windows Unity | `TOWERGLIDE ▸ Build ▸ Android APK` → `adb install -r Builds/Android/...-dev.apk` |
| Play 업로드 AAB | Windows Unity 또는 CI | `TOWERGLIDE ▸ Build ▸ Android AAB` |
| iOS | Windows Unity → macOS Xcode | `TOWERGLIDE ▸ Build ▸ iOS Xcode 프로젝트` → Xcode Archive → TestFlight |
