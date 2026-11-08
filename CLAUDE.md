# TOWERGLIDE — 작업 규칙 (Claude Code / 에이전트용)

Unity 6.3 LTS + URP + C# 모바일 3D 비행 게임. Android(Google Play) + iOS(App Store). 원본 명세: `docs/SUPERPROMPT.md`, 검토·수정 사항: `docs/PROMPT_REVIEW.md`.

## 시작할 때
1. `README.md`(지금 상태·다음 할 일), `WORKLOG.md`, `docs/DECISIONS.md`를 먼저 읽는다.
2. Unity Editor 실행 가능 여부를 확인한다. 없으면 숨기지 말고, 코드·문서·테스트를 쓰되 에디터 검증이 필요한 단계를 분리해 기록한다.

## 아키텍처 규칙
- `Scripts/Core`(TowerGlide.Core)는 **순수 C#**: `UnityEngine` 금지, C# 9 이하 문법. 규칙·계산·상태·저장 검증만. → `tools/core-tests`가 .NET으로 컴파일해 경계를 감시한다.
- `Scripts/Runtime`의 비행 물리와 입력은 섞지 않는다. UI·키보드는 `FlightInputRouter`에만, 비행 컨트롤러는 `FlightInput`만 받는다.
- 게임 규칙(점수·복귀·일시정지·결과)은 `FlightSession`에서만 결정. 부품은 이벤트만 낸다.
- 물리 수치는 `AircraftFlightTuning`(ScriptableObject)로. 코드에 하드코딩 금지. 힘은 `FixedUpdate`, 입력 수신은 `Update`, 카메라는 `LateUpdate`.
- `Rigidbody.linearVelocity`(Unity 6 API) 사용. Transform 직접 이동으로 물리를 우회하지 않는다(복귀 텔레포트 제외).
- 네임스페이스 `TowerGlide.Camera` 금지(UnityEngine.Camera를 가림) → `TowerGlide.Cameras`.
- 씬은 `TOWERGLIDE ▸ 2. 씬 생성`으로 만든다. 씬/프리팹 YAML을 손으로 쓰지 않는다.
- 파일명 = public 클래스명. Inspector 참조 누락은 `Debug.LogError`로 즉시 알린다.

## 금지
- 유료 에셋·플러그인·API·서버, 광고·분석·로그인 SDK(소유자 승인 없이). 라이선스를 확인하지 못한 에셋.
- 다른 게임·사이트의 모델·코드·사운드·브랜드 복제, 실제 항공사·제조사 표식.
- 서명키·비밀번호 커밋(`*.keystore`, `*.p12`, `.env*`).
- 실행·빌드하지 않은 것을 성공으로 보고하기. 존재하지 않는 빌드 파일 경로 만들기.

## 끝낼 때
- 결정은 `docs/DECISIONS.md`(D-번호), 테스트 결과는 `docs/TEST_PLAN.md`, 작업 내역은 `WORKLOG.md`.
- `README.md`의 "지금 상태 한눈에"·"변경 내역"·"다음 할 일"을 마지막에 갱신한다.
- 커밋 메시지는 한국어 요약 + 변경 범위.
