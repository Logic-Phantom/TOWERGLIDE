using System;
using System.Collections.Generic;

namespace TowerGlide.Core.App
{
    public enum GameState
    {
        Boot,
        MainMenu,
        Loading,
        Flying,
        Paused,
        Results,
    }

    /// <summary>
    /// 앱 상위 상태. 허용된 전이만 통과시키고 잘못된 전이는 false를 돌려준다(예외 대신 — 터치 연타 등 경쟁 상황 방어).
    /// </summary>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> Allowed = new Dictionary<GameState, GameState[]>
        {
            { GameState.Boot, new[] { GameState.MainMenu, GameState.Loading } },
            { GameState.MainMenu, new[] { GameState.Loading } },
            { GameState.Loading, new[] { GameState.Flying, GameState.MainMenu } },
            { GameState.Flying, new[] { GameState.Paused, GameState.Results, GameState.Loading } },
            { GameState.Paused, new[] { GameState.Flying, GameState.Loading, GameState.MainMenu } },
            { GameState.Results, new[] { GameState.Loading, GameState.MainMenu } },
        };

        public GameStateMachine(GameState initial = GameState.Boot) { Current = initial; }

        public GameState Current { get; private set; }

        /// <summary>(이전, 다음)</summary>
        public event Action<GameState, GameState> Changed;

        public bool CanTransition(GameState next) =>
            Allowed.TryGetValue(Current, out GameState[] targets) && Array.IndexOf(targets, next) >= 0;

        public bool TryTransition(GameState next)
        {
            if (!CanTransition(next)) return false;
            GameState previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
            return true;
        }
    }
}
