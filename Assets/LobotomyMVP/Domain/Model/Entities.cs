using System;

namespace LobotomyMVP.Domain.Model
{
    public enum EmployeeState { Idle, Moving, Working, Fighting, Panicked, Dead }

    /// Один экран = одна фаза. Промежуточные события сворачиваются в Management с баннером.
    public enum GamePhase { Preparation, Management, Report, Selection, GameOver }

    public enum StatType { Fortitude, Prudence, Control, Justice }

    public sealed class Stat
    {
        public int Level = 1;   // 1..5
        public int Xp;          // кумулятивный XP; уровень по Balance.XpThresholds

        public void AddXp(int amount) { Xp += amount; }
    }

    public sealed class Employee
    {
        public int Id;
        public string Name;
        public Stat[] Stats = new Stat[4]; // индекс = (int)StatType
        public float Hp, MaxHp;
        public float Sp, MaxSp;
        public EmployeeState State = EmployeeState.Idle;
        public int RoomId = -1;
        public float EdgeProgress;         // 0..1 прогресс перемещения между комнатами
        public int TargetId = -1;          // unitId работы или anomalyId подавления
        public int LastWorkedUnitId = -1;  // для правила "та же пешка дважды подряд"
        public int DepartmentId;
        // Внутренние таймеры систем (сек сим-времени)
        public float MoveFromRoomId = -1;
        public float WorkTimer;
        public WorkType PendingWork;
        public int PendingUnit = -1;
        public float AttackTimer;

        public Employee()
        {
            for (int i = 0; i < 4; i++) Stats[i] = new Stat();
        }

        public Stat Get(StatType t) => Stats[(int)t];

        public static StatType ProfilerStatFor(WorkType w)
        {
            // Инстинкт->Стойкость, Озарение->Осторожность, Привязанность->Воздержанность, Подавление->Справедливость
            switch (w)
            {
                case WorkType.Instinct: return StatType.Fortitude;
                case WorkType.Insight: return StatType.Prudence;
                case WorkType.Attachment: return StatType.Control;
                default: return StatType.Justice;
            }
        }

        public static string StatName(StatType t)
        {
            switch (t)
            {
                case StatType.Fortitude: return "Стойкость";
                case StatType.Prudence: return "Осторожность";
                case StatType.Control: return "Воздержанность";
                default: return "Справедливость";
            }
        }

        public static string WorkName(WorkType w)
        {
            switch (w)
            {
                case WorkType.Instinct: return "Инстинкт";
                case WorkType.Insight: return "Озарение";
                case WorkType.Attachment: return "Привязанность";
                default: return "Подавление";
            }
        }

        public static int LevelFromXp(int xp, int[] thresholds)
        {
            int lvl = 1;
            for (int i = 0; i < thresholds.Length; i++)
                if (xp >= thresholds[i]) lvl = i + 1;
            if (lvl > 5) lvl = 5;
            return lvl;
        }
    }

    public sealed class AbnormalityInstance
    {
        public string DefId;
        public int UnitId = -1;
        public int Counter;
        public int CounterMax;
        public int InfoLevel;                 // 0..3
        public int[] WorkHistoryCounts = new int[12]; // [тип*3 + result], ведётся всегда
        public bool Breached;
        public float BreachHp;
        public int RoomId = -1;
        public float BreachMoveTimer;
        public float BreachAttackTimer;

        public void RecordWork(Data.WorkType type, Data.WorkResult res)
        {
            WorkHistoryCounts[(int)type * 3 + (int)res]++;
        }
    }

    public sealed class ContainmentUnit
    {
        public int Id;
        public int DepartmentId;
        public int RoomId;
        public int OccupantId = -1;      // индекс в GameState.Anomalies
        public bool MeltdownFlag;
        public float MeltdownTimer;
    }

    public sealed class RoomInfo
    {
        public int Id;
        public int DepartmentId;
        public int SlotIndex;            // -1 = комната отдыха
    }

    public sealed class LogEntry
    {
        public float TimeSec;
        public string Text;
    }

    public sealed class DayStats
    {
        public int GoodCount, NormalCount, BadCount;
        public bool HadDeath;
        public bool HadPanic;
        public int EnergyAtDayStart;
    }

    public sealed class GameState
    {
        public int Day = 1;
        public GamePhase Phase = GamePhase.Preparation;
        public int Energy;               // накоплено за день
        public int Quota;                // цель дня
        public int Pe;
        public int Lob;
        public float SimTimeInDay;       // сек сим-времени текущего дня
        public System.Collections.Generic.List<Employee> Employees = new System.Collections.Generic.List<Employee>();
        public System.Collections.Generic.List<AbnormalityInstance> Anomalies = new System.Collections.Generic.List<AbnormalityInstance>();
        public System.Collections.Generic.List<ContainmentUnit> Units = new System.Collections.Generic.List<ContainmentUnit>();
        public System.Collections.Generic.List<RoomInfo> Rooms = new System.Collections.Generic.List<RoomInfo>();
        public int MeltdownGauge;
        public int MeltdownLevel;
        public int RngSeed;
        public Rng Rng;
        public System.Collections.Generic.List<LogEntry> Log = new System.Collections.Generic.List<LogEntry>();
        public DayStats Today = new DayStats();
        public System.Collections.Generic.List<string> AvailablePool = new System.Collections.Generic.List<string>(); // defId ещё не выбранных
        public System.Collections.Generic.List<string> OfferedIds = new System.Collections.Generic.List<string>();   // 3 варианта выбора
        public int NextEmployeeId = 1;
        public bool DayFailed;           // провал квоты/таймаута -> откат к снапшоту
    }
}
