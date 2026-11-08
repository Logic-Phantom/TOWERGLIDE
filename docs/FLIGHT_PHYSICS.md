# 비행 물리

목표: **현실적인 인상 + 배우기 쉬운 아케이드**(명세 1.1, 2.2). 시뮬레이터가 아니다.

## 모델 (`AircraftFlightController.FixedUpdate`)

모든 힘은 질량과 무관한 가속도(m/s²)로 `ForceMode.Acceleration` 적용(D-008). 1 unit = 1 m, 내부 속도 m/s, 표시 km/h.

| 항목 | 식 / 동작 | 튜닝 값 |
|---|---|---|
| 추력 | 목표 속도까지 `min(maxThrust, (목표−전진속도)×responsiveness)`. 목표 = 스로틀 0이면 순항, +1이면 최고, −1이면 최소. 부스트 중 = `boostMaxSpeed`, 추가 가속 | `cruiseSpeed 30`, `maxSpeed 65`, `boostMaxSpeed 80`, `maxThrustAcceleration 18` |
| 기본 양력 | 속도에 수직·날개 평면 위쪽(`cross(v, right)`), 크기 `g × gravityCompensation × min(1, v/vmin)²` → 순항 시 수평 유지, **기울이면 양력도 기울어 선회** | `gravityCompensation 1`, `minFlightSpeed 22` |
| 받음각 양력 | `Cl(aoa) × liftFactor × v² × 저속감쇠`. 실속각까지 선형, 이후 40%까지 완만히 감소(급격한 실속 없음) | `liftSlope 4`, `stallAngleDeg 16`, `liftFactor 0.004` |
| 항력 | `dragFactor × v²`, 에어브레이크 ×4, 최고 속도 초과분 소프트 캡 | `dragFactor 0.0035`, `airBrakeDragMultiplier 4` |
| 속도 정렬 | 속도 벡터를 기수 방향으로 `velocityAlignment` rad/s만큼 회전 → "미끄러짐" 감소 | `velocityAlignment 1.6` |
| 회전 | **목표 각속도 추종**: `(목표 − 현재) × angularResponse`. 손을 떼면 목표 0 → 자연 감쇠, 무한 회전 없음 | `maxPitchRate 75`, `maxRollRate 160`, `maxYawRate 30`, `angularResponse 6` (deg/s) |
| 협조 선회 | 기울기에 비례한 yaw + 선회 중 약간의 기수 당김 | `coordinatedTurnYawRate 55`, `turnPitchAssistRate 18` |
| 자동 수평 | 롤 입력이 없으면 기울기를 천천히 0으로 | `autoLevelRate 40` (0 = 끔) |
| 최대 기울기 | 초보자 제한(넘어가면 더 못 기울임) | `maxBankAngleDeg 75` (180 = 제한 없음) |
| 조종 권한 | 저속에서 둔해지고(0.35), 고속에서 과민하지 않게(0.65) | `lowSpeedAuthority`, `highSpeedAuthority` |
| 실속 | 최소 속도 미만이면 양력 감소 + 기수 처짐 | `stallNoseDropRate 25` |
| 부스트 | 게이지 3초, 6초에 완충, 1초 지연, 소진 시 25%까지 잠금(무제한 금지) | `boost*` |

입력 부호(Unity 왼손 좌표): +Pitch = 기수 상승(local −X 회전), +Roll = 오른쪽 기울기(local −Z), +Yaw = 오른쪽(local +Y). PlayMode 테스트가 방향을 검증한다.

## 튜닝 절차 (Phase 1 — 코드보다 먼저)

1. `CityFlight` Play → 키보드(W/S/A/D/Q/E, Shift 부스트, Space 브레이크)로 확인 → Device Simulator로 터치 확인.
2. `Assets/_Project/Settings/AircraftFlightTuning.asset`을 선택하고 **Play 중에 값 조정**(ScriptableObject라 Play 종료 후에도 남음 — 의도한 값만 남기기).
3. 승인 기준(명세 2.2)을 하나씩 체크하고 아래 표에 기록.

| 증상 | 먼저 바꿀 값 |
|---|---|
| 드론처럼 즉시 꺾임 | `angularResponse`↓, `maxRollRate`↓ |
| 둔하고 늦음 | `angularResponse`↑ |
| 기울여도 잘 안 돎 | `coordinatedTurnYawRate`↑, `velocityAlignment`↑ |
| 선회 시 고도가 너무 빠짐 | `turnPitchAssistRate`↑ |
| 미끄러지듯 옆으로 흐름 | `velocityAlignment`↑ |
| 손 떼면 출렁임 | `angularResponse`↑(과감쇠 방향), `autoLevelRate`↓ |
| 빠른데 속도감 없음 | 카메라 `maxExtraFov`, `positionSharpness`↓(뒤처짐) |

## 튜닝 기록

| 날짜 | 기기/환경 | 변경 | 결과 |
|---|---|---|---|
| — | — | 아직 실플레이 없음(Unity 미설치) | — |
