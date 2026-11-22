namespace LobotomyMVP.Data
{
    public enum WorkType { Instinct, Insight, Attachment, Repression }
    public enum DamageType { Red, White, Black, Pale }
    public enum RiskLevel { ZAYIN, TETH, HE }
    public enum RuleTrigger { OnWorkResult, OnWorkType, OnRepeatEmployee, OnBreachStart, OnBreachTick, OnMeltdownTimeout }
    public enum RuleEffect { CounterDelta, DamageEmployee, PanicEmployee, FacilitySpDelta, Aura, MoveToNearest, TeleportRandom, Attack }

    /// Правило аномалии — чистые данные, исполняются RuleEngine.
    public struct AnomalyRule
    {
        public RuleTrigger Trigger;
        public WorkType? WorkFilter;      // для OnWorkResult/OnWorkType: тип работы
        public WorkResult? ResultFilter;  // для OnWorkResult: результат работы
        public RuleEffect Effect;
        public int CounterDelta;          // Effect.CounterDelta
        public DamageType Damage;         // Effect.DamageEmployee / Attack / Aura
        public float Amount;              // урон (Pale — % от maxHp) или dmg/sec для ауры
        public float IntervalSec;         // период для Attack / MoveToNearest / TeleportRandom / OnBreachTick
        public string Description;        // текст для Info L3
    }

    public enum WorkResult { Good, Normal, Bad }

    /// Полное описание аномалии. Новая аномалия = новая запись здесь, ноль нового кода.
    public sealed class AnomalyDef
    {
        public string Id;
        public string Name;
        public RiskLevel Risk;
        public int CounterMax;
        public float WorkTimeSec;
        public int YieldGoodPeMin, YieldGoodPeMax;   // Good: PE в [min..max]
        public int YieldNormalPeMin, YieldNormalPeMax; // Normal: PE в [min..max]; Bad: 0
        public FavorBand[] Favor;                    // индекс = (int)WorkType
        public DamageType BadDamageType;
        public float BadDamageAmount;                // Pale — проценты от maxHp
        public AnomalyRule[] Rules;
        // Побег
        public float BreachHp;
        public bool BreachMoves;
        public float BreachMoveIntervalSec;
        public DamageType BreachAttackType;
        public float BreachAttackAmount;
        public float BreachAttackIntervalSec;
        public bool BreachAttacksAll;                // true — по всем в комнате (chorus)
        public float BreachAuraDamagePerSec;         // >0 — постоянная аура в комнате (White)
        public float[] Resist;                       // R,W,B,P
    }

    public static class AnomalyCatalog
    {
        public static readonly AnomalyDef A1 = new AnomalyDef
        {
            Id = "A1", Name = "Лампа, Которая Смотрит", Risk = RiskLevel.ZAYIN,
            CounterMax = 4, WorkTimeSec = 8f,
            YieldGoodPeMin = 3, YieldGoodPeMax = 3, YieldNormalPeMin = 1, YieldNormalPeMax = 2,
            Favor = new[] { FavorBand.Low, FavorBand.High, FavorBand.Common, FavorBand.VeryLow },
            BadDamageType = DamageType.White, BadDamageAmount = 6f,
            Rules = new[]
            {
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, ResultFilter = WorkResult.Bad, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "После плохой работы счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, WorkFilter = WorkType.Insight, ResultFilter = WorkResult.Good, Effect = RuleEffect.CounterDelta, CounterDelta = +1, Description = "Хорошая работа Озарением: счётчик +1." },
            },
            BreachHp = 60f, BreachMoves = false, BreachAuraDamagePerSec = 3f,
            BreachAttackType = DamageType.White, BreachAttackAmount = 0f, BreachAttackIntervalSec = 0f,
            Resist = new[] { 1.0f, 1.5f, 1.0f, 1.0f },
        };

        public static readonly AnomalyDef A2 = new AnomalyDef
        {
            Id = "A2", Name = "Пёс в Клетке", Risk = RiskLevel.TETH,
            CounterMax = 2, WorkTimeSec = 10f,
            YieldGoodPeMin = 5, YieldGoodPeMax = 5, YieldNormalPeMin = 2, YieldNormalPeMax = 3,
            Favor = new[] { FavorBand.VeryHigh, FavorBand.Low, FavorBand.VeryLow, FavorBand.Common },
            BadDamageType = DamageType.Red, BadDamageAmount = 14f,
            Rules = new[]
            {
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, ResultFilter = WorkResult.Bad, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "После плохой работы счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkType, WorkFilter = WorkType.Attachment, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "Любая работа Привязанностью: счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, WorkFilter = WorkType.Instinct, ResultFilter = WorkResult.Good, Effect = RuleEffect.CounterDelta, CounterDelta = +1, Description = "Хорошая работа Инстинктом: счётчик +1." },
            },
            BreachHp = 140f, BreachMoves = true, BreachMoveIntervalSec = 4f,
            BreachAttackType = DamageType.Red, BreachAttackAmount = 12f, BreachAttackIntervalSec = 2f,
            Resist = new[] { 0.7f, 1.2f, 1.0f, 1.0f },
        };

        public static readonly AnomalyDef A3 = new AnomalyDef
        {
            Id = "A3", Name = "Хор Незавершённых", Risk = RiskLevel.HE,
            CounterMax = 2, WorkTimeSec = 12f,
            YieldGoodPeMin = 8, YieldGoodPeMax = 8, YieldNormalPeMin = 3, YieldNormalPeMax = 5,
            Favor = new[] { FavorBand.VeryLow, FavorBand.Common, FavorBand.Low, FavorBand.High },
            BadDamageType = DamageType.White, BadDamageAmount = -1f, // -1 => урон = maxSp цели (гарантированная паника)
            Rules = new[]
            {
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, ResultFilter = WorkResult.Bad, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "После плохой работы счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnRepeatEmployee, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "Та же пешка работает дважды подряд: счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, WorkFilter = WorkType.Repression, ResultFilter = WorkResult.Good, Effect = RuleEffect.CounterDelta, CounterDelta = +1, Description = "Хорошая работа Подавлением: счётчик +1." },
                new AnomalyRule { Trigger = RuleTrigger.OnBreachStart, Effect = RuleEffect.FacilitySpDelta, Amount = -10f, Description = "При побеге все пешки теряют 10 SP." },
            },
            BreachHp = 200f, BreachMoves = true, BreachMoveIntervalSec = 8f,
            BreachAttackType = DamageType.Black, BreachAttackAmount = 10f, BreachAttackIntervalSec = 2.5f,
            BreachAttacksAll = true,
            Resist = new[] { 1.0f, 0.5f, 1.5f, 1.0f },
        };

        public static readonly AnomalyDef A1b = new AnomalyDef
        {
            Id = "A1b", Name = "Тусклая Лампа", Risk = RiskLevel.ZAYIN,
            CounterMax = 3, WorkTimeSec = 8f,
            YieldGoodPeMin = 3, YieldGoodPeMax = 3, YieldNormalPeMin = 1, YieldNormalPeMax = 2,
            Favor = new[] { FavorBand.Low, FavorBand.Common, FavorBand.High, FavorBand.VeryLow },
            BadDamageType = DamageType.White, BadDamageAmount = 8f,
            Rules = new[]
            {
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, ResultFilter = WorkResult.Bad, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "После плохой работы счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, WorkFilter = WorkType.Insight, ResultFilter = WorkResult.Good, Effect = RuleEffect.CounterDelta, CounterDelta = +1, Description = "Хорошая работа Озарением: счётчик +1." },
            },
            BreachHp = 60f, BreachMoves = false, BreachAuraDamagePerSec = 3f,
            BreachAttackType = DamageType.White, BreachAttackAmount = 0f, BreachAttackIntervalSec = 0f,
            Resist = new[] { 1.0f, 1.5f, 1.0f, 1.0f },
        };

        public static readonly AnomalyDef A2b = new AnomalyDef
        {
            Id = "A2b", Name = "Пёс на Цепи", Risk = RiskLevel.TETH,
            CounterMax = 1, WorkTimeSec = 10f,
            YieldGoodPeMin = 5, YieldGoodPeMax = 5, YieldNormalPeMin = 2, YieldNormalPeMax = 3,
            Favor = new[] { FavorBand.VeryHigh, FavorBand.Low, FavorBand.VeryLow, FavorBand.Common },
            BadDamageType = DamageType.Red, BadDamageAmount = 14f,
            Rules = new[]
            {
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, ResultFilter = WorkResult.Bad, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "После плохой работы счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkType, WorkFilter = WorkType.Insight, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "Любая работа Озарением: счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, WorkFilter = WorkType.Instinct, ResultFilter = WorkResult.Good, Effect = RuleEffect.CounterDelta, CounterDelta = +1, Description = "Хорошая работа Инстинктом: счётчик +1." },
            },
            BreachHp = 140f, BreachMoves = true, BreachMoveIntervalSec = 4f,
            BreachAttackType = DamageType.Red, BreachAttackAmount = 12f, BreachAttackIntervalSec = 2f,
            Resist = new[] { 0.7f, 1.2f, 1.0f, 1.0f },
        };

        public static readonly AnomalyDef A3b = new AnomalyDef
        {
            Id = "A3b", Name = "Эхо Хора", Risk = RiskLevel.HE,
            CounterMax = 3, WorkTimeSec = 12f,
            YieldGoodPeMin = 8, YieldGoodPeMax = 8, YieldNormalPeMin = 3, YieldNormalPeMax = 5,
            Favor = new[] { FavorBand.VeryLow, FavorBand.Common, FavorBand.Low, FavorBand.VeryLow },
            BadDamageType = DamageType.White, BadDamageAmount = -1f,
            Rules = new[]
            {
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, ResultFilter = WorkResult.Bad, Effect = RuleEffect.CounterDelta, CounterDelta = -1, Description = "После плохой работы счётчик -1." },
                new AnomalyRule { Trigger = RuleTrigger.OnWorkResult, WorkFilter = WorkType.Repression, ResultFilter = WorkResult.Good, Effect = RuleEffect.CounterDelta, CounterDelta = +1, Description = "Хорошая работа Подавлением: счётчик +1." },
                new AnomalyRule { Trigger = RuleTrigger.OnBreachStart, Effect = RuleEffect.FacilitySpDelta, Amount = -10f, Description = "При побеге все пешки теряют 10 SP." },
            },
            BreachHp = 200f, BreachMoves = true, BreachMoveIntervalSec = 8f,
            BreachAttackType = DamageType.Black, BreachAttackAmount = 10f, BreachAttackIntervalSec = 2.5f,
            BreachAttacksAll = true,
            Resist = new[] { 1.0f, 0.5f, 1.5f, 1.0f },
        };

        public static readonly AnomalyDef[] All = { A1, A2, A3, A1b, A2b, A3b };

        public static AnomalyDef Find(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }
    }

    public static class EmployeeNames
    {
        public static readonly string[] Names =
        {
            "Йехошуа", "Анна", "Мартин", "Софья", "Дэвид", "Клава", "Роман", "Ирма",
            "Ноа", "Вера", "Тимур", "Луиза", "Марк", "Нина", "Оскар", "Поля",
        };
    }
}
