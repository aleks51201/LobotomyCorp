using UnityEngine;

namespace LobotomyMVP.Presentation
{
    /// Точка входа: скрипты в Assets + Play в пустой сцене. Никаких настроек.
    public static class GameBootstrap
    {
        private static GameObject _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_runner != null) return;
            UiKit.EnsureEventSystem();
            var canvas = UiKit.CreateCanvas(10);
            _runner = new GameObject("LobotomyGameLoop");
            var loop = _runner.AddComponent<GameLoopRunner>();
            loop.Init(canvas.transform);
            Object.DontDestroyOnLoad(_runner);
        }
    }
}
