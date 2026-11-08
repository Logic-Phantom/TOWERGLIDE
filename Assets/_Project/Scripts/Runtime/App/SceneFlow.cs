using TowerGlide.Core.App;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerGlide.App
{
    /// <summary>씬 이름과 전환을 한 곳에서 관리. 전환 시 일시정지 상태(timeScale·오디오)를 항상 복구한다.</summary>
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string CityFlight = "CityFlight";

        public static void LoadMainMenu() => Load(MainMenu, GameState.MainMenu);

        public static void LoadFlight() => Load(CityFlight, GameState.Loading);

        private static void Load(string scene, GameState state)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameApp app = GameApp.Instance;
            if (app != null)
            {
                app.SaveNow();
                if (state == GameState.MainMenu && !app.State.TryTransition(GameState.MainMenu))
                {
                    // Flying 등에서 바로 메뉴로 갈 수 없는 상태면 Loading을 거친다
                    app.State.TryTransition(GameState.Loading);
                    app.State.TryTransition(GameState.MainMenu);
                }
                else if (state == GameState.Loading)
                {
                    app.State.TryTransition(GameState.Loading);
                }
            }
            SceneManager.LoadScene(scene);
        }
    }
}
