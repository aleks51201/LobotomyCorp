namespace LobotomyMVP.Domain.Systems
{
    /// Команды из Presentation в симуляцию. Одна команда = одно намерение игрока.
    public struct GameCommand
    {
        public CommandKind Kind;
        public int EmployeeId;
        public int UnitId;
        public int AnomalyIndex;   // индекс в GameState.Anomalies
        public Data.WorkType Work;
        public string AnomalyDefId;
        public Data.StatType Stat;
        public int InfoLevel;      // целевой уровень информации (1..3)
        public int DepartmentId;   // отдел при найме
    }

    public enum CommandKind
    {
        AssignWork,
        Suppress,
        RecallToRest,
        Hire,
        UpgradeStat,
        StartDay,
        EndDay,
        OpenInfo,
        PickAnomaly,
        SkipSelection,
        RetryDay,
    }
}
