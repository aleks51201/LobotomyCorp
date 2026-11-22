namespace LobotomyMVP.Domain.Model
{
    /// Шина событий: домен/системы пишут, Presentation подписывается.
    public sealed class EventBus
    {
        public System.Action<string> OnLog;
        public System.Action OnStateChanged;          // любое изменение модели (для ре-рендера)
        public System.Action<GameState> OnPhaseChanged;
        public System.Action OnDayReportReady;
        public System.Action OnGameOver;

        public void Log(string text) => OnLog?.Invoke(text);
        public void StateChanged() => OnStateChanged?.Invoke();
        public void PhaseChanged(GameState s) => OnPhaseChanged?.Invoke(s);
        public void DayReportReady() => OnDayReportReady?.Invoke();
        public void GameOver() => OnGameOver?.Invoke();
    }
}
