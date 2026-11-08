# 성능

목표(측정 전까지는 목표일 뿐): 중급 Android 30 FPS 이상, 상위 기기 60 FPS, 프레임 스파이크·GC 최소화.

## 적용한 것 (코드/설정)
- 빌딩·장애물 머티리얼 공유 + **GPU Instancing** 켬, 정적 건물 `BatchingStatic | OccluderStatic`.
- 단순 콜라이더만(Box/Capsule/Sphere), MeshCollider는 지면 하나.
- 선형 안개 280–1300m → 원거리 디테일 생략 근거.
- 품질 3단계(URP 렌더 스케일·MSAA·그림자 거리·캐스케이드·HDR) — D-014.
- `Application.targetFrameRate` 30/60 설정, `vSyncCount 0`, Android `optimizedFramePacing`.
- HUD는 값이 바뀔 때만 문자열 갱신(StringBuilder), `NearMissSensor`는 리스트 풀.
- 이동 장애물은 Kinematic Rigidbody(MovePosition) — 물리 비용 예측 가능.

## 해야 할 것 (Phase 4)
- [ ] LODGroup(원거리 건물 단순화), 창문 셰이더/텍스처 아틀라스
- [ ] Occlusion Culling 베이크 검토
- [ ] 파티클(충돌·부스트·링) + 오브젝트 풀
- [ ] 텍스처 최대 크기·ASTC 압축 규칙
- [ ] Profiler(CPU/GPU/Memory/GC Alloc), Android GPU Inspector(선택)
- [ ] 5분 이상 비행 발열·메모리

## 측정 기록
| 날짜 | 기기 | 해상도 | 프리셋 | 빌드 | 평균 FPS | 1% Low | 메모리 | 비고 |
|---|---|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — | — | 아직 측정 없음 |
