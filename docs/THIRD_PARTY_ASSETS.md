# 외부 에셋·라이선스 기록

**현재 외부 에셋 0개.** 창문 패턴(`Art/Textures/T_Window_*.png`)·가장자리 플래시(`T_EdgeGlow.png`)도 에디터 스크립트가 직접 그려 만든다. 파티클 텍스처는 Unity 내장 `Default-Particle`. 효과음·엔진음·바람 소리는 Core `ProceduralAudio`가 실행 시 합성한다(음원 파일 없음). 앱 내 고지 문구는 `Runtime/UI/LicenseNotices.cs`(설정 ▸ 라이선스). 모든 형상은 Unity 기본 도형을 에디터 스크립트로 조합해 생성하고, 머티리얼은 URP Lit으로 직접 만든다. 폰트는 Unity 내장 LegacyRuntime(엔진 일부).

사용 우선순위(명세 7.2): ① 직접 제작 ② CC0/퍼블릭 도메인 ③ 개별 라이선스를 확인한 Unity Asset Store 무료 에셋.
금지: 라이선스 불명, CC BY-NC 등 상업 제한, 다른 게임 추출물, 실제 항공사·제조사 로고/도장, 유료 에셋 우회 사용. Kenney 등도 **에셋 페이지별로** CC0 확인(유료 번들을 무료로 가정하지 않음).

## 패키지 (Unity 공식 레지스트리)
| 패키지 | 버전 | 라이선스 | 비고 |
|---|---|---|---|
| com.unity.render-pipelines.universal | 17.3.0(에디터 고정) | Unity Companion License | |
| com.unity.inputsystem | 1.19.0 | Unity Companion License | |
| com.unity.ugui | 2.0.0 | Unity Companion License | |
| com.unity.test-framework | 1.6.0 | Unity Companion License | 개발 전용 |

## 에셋 기록 양식
| 에셋 | 파일 경로 | 제작자 | 공식 출처 URL | 가격 | 라이선스(링크) | 상업 사용 | 수정 | 저작자 표시 | 재배포 제한 | 확인일 |
|---|---|---|---|---|---|---|---|---|---|---|
| (없음) | | | | | | | | | | |

## 앱 내 고지
설정 ▸ 라이선스에서 표시(구현됨, `LicenseNotices`). 외부 에셋·폰트·지도 데이터를 추가하면 이 문서와 `LicenseNotices`를 함께 고친다.
