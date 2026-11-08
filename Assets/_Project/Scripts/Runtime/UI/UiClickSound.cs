using TowerGlide.App;
using UnityEngine;

namespace TowerGlide.UI
{
    /// <summary>메뉴 버튼 클릭음. 씬에 두고 버튼 onClick에 연결한다(AudioManager는 실행 시 생성되므로 직접 연결 불가).</summary>
    public sealed class UiClickSound : MonoBehaviour
    {
        public void Play() => GameApp.Instance?.Audio?.PlayClick();
    }
}
