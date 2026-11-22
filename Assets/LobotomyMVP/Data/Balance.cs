namespace LobotomyMVP.Data
{
    /// Все числа баланса игры. Больше нигде констант быть не должно.
    public static class Balance
    {
        // Симуляция
        public const float TickDt = 0.1f;              // fixed tick, сек сим-времени
        public static readonly float[] Speeds = { 0f, 1f, 2f, 4f };

        // Стартовые условия дня 1
        public const int StartEmployees = 4;
        public const int StartLob = 5;
        public const int StartPe = 0;
        public const int Departments = 2;
        public const int SlotsPerDepartment = 3;

        // Статы сотрудников
        public const int StatLevelMin = 1;
        public const int StatLevelMax = 5;
        public const int HpBase = 20, HpPerFortitude = 15;   // maxHp = 20 + 15*Стойкость
        public const int SpBase = 20, SpPerPrudence = 15;    // maxSp = 20 + 15*Осторожность
        public const float WorkSpeedPerControl = 0.08f;      // workSpeed = 1 + 0.08*(Cont-1)
        public const float SuccessBonusPerControl = 0.03f;   // +0.03*(Cont-1) к вероятности
        public const float AttackIntervalBase = 2.0f;        // 2.0 / (1 + 0.12*(Just-1))
        public const float AttackIntervalPerJustice = 0.12f;
        public const float MoveSpeedBase = 3f;               // комнат/сек: 3 + 0.4*(Just-1)
        public const float MoveSpeedPerJustice = 0.4f;
        public const float PawnBaseDamage = 8f;              // базовый урон пешки (Red)
        public const float SuccessMin = 0.05f, SuccessMax = 0.95f;

        // XP и уровни
        public const int XpGood = 12, XpNormal = 6, XpBad = 2;
        public static readonly int[] XpThresholds = { 0, 30, 90, 200, 400 }; // кумулятивно, индекс = уровень-1

        // Экономика
        public const int HireCostLob = 3;
        public const int EmployeeLimit = 8;
        public static readonly int[] UpgradeCostLob = { 2, 4, 7, 11 };       // повышение 1->2, 2->3, 3->4, 4->5
        public const int QuotaBase = 120, QuotaPerDay = 80;                  // 120 + 80*(day-1)
        public const int EnergyPerPe = 10;                                   // 1 PE = 10 энергии
        public const float DayTimeLimitSec = 600f;                           // жёсткий лимит дня -> провал
        public const int MeltdownGaugeThreshold = 6;                         // расплав после 6 работ

        // Награда за день: LOB = 3 + floor(energy/100) + (без потерь ? 2 : 0)
        public const int DayRewardBase = 3;
        public const int DayRewardCalmBonus = 2;
        public const int CalmRecoveryCostLob = 1;    // лечение паники на конец дня, за пешку
        public const int SkipSelectionLob = 2;       // пропуск выбора аномалии при исчерпанном пуле

        // Реген в покое (в секунду), только если в комнате нет сбежавшей аномалии
        public const float RegenSpPerSec = 0.5f;
        public const float RegenHpPerSec = 0.3f;

        // Расплав: k = 1 + floor(level/2) камер; -10% успеха; таймер 45 сек
        public const float MeltdownSuccessPenalty = 0.10f;
        public const float MeltdownTimerSec = 45f;
        public const int MeltdownFlagWorkPeReward = 1;

        // Стоимость открытия уровней информации (PE)
        public static readonly int[] InfoCostPe = { 4, 8, 14 }; // L1, L2, L3
        public static readonly float[] RiskInfoMultiplier = { 1f, 1.5f, 2f }; // ZAYIN, TETH, HE

        // Паника: аура White по соседям в комнате
        public const float PanicAuraDamagePerSec = 2f;
    }
}
