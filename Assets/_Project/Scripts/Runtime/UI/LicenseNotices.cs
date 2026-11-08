using UnityEngine;

namespace TowerGlide.UI
{
    /// <summary>
    /// 앱 내 오픈소스·에셋 고지(설정 ▸ 라이선스). docs/THIRD_PARTY_ASSETS.md와 같은 내용을 유지한다.
    /// 외부 에셋·폰트·지도 데이터를 추가하면 여기와 문서를 함께 고친다(OSM 사용 시 "© OpenStreetMap contributors" 필수).
    /// </summary>
    public static class LicenseNotices
    {
        public const string Text =
            "TOWERGLIDE 라이선스 고지\n\n" +
            "■ 엔진·패키지\n" +
            "Unity 6 (Unity Personal) — Unity Technologies\n" +
            "Universal Render Pipeline, Input System, uGUI — Unity Companion License\n\n" +
            "■ 글꼴\n" +
            "Pretendard v1.3.9 — Copyright (c) 2021, Kil Hyung-jin, with Reserved Font Name Pretendard.\n" +
            "SIL Open Font License 1.1 (전문은 아래)\n\n" +
            "■ 그래픽·사운드\n" +
            "건물·장애물·기체 형상, 창문 텍스처, 효과음·엔진음은 모두 이 게임을 위해 코드로 직접 만들었습니다.\n" +
            "외부 3D 모델·텍스처·음원·지도 데이터를 사용하지 않습니다.\n\n" +
            "■ 기체·도시\n" +
            "기체 이름과 외형은 가상이며 실제 항공기 제조사·항공사와 관계없습니다.\n" +
            "도시는 절차적으로 만든 가상 도시입니다(실제 지도 데이터 아님).";

        /// <summary>
        /// 앱 내 표시용 전문: 고지 + 폰트 라이선스 원문.
        /// OFL은 폰트를 함께 배포할 때 저작권 고지와 라이선스 동봉을 요구한다 → Resources/Licenses/Pretendard-OFL.txt.
        /// </summary>
        public static string FullText()
        {
            var ofl = Resources.Load<TextAsset>("Licenses/Pretendard-OFL");
            return ofl != null ? Text + "\n\n────────────\n" + ofl.text : Text;
        }
    }
}
