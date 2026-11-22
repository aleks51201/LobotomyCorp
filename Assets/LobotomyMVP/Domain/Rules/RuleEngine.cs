namespace LobotomyMVP.Domain.Rules
{
    /// Контекст срабатывания правила. Заполняется системами, читается RuleEngine.
    public struct RuleContext
    {
        public Data.AnomalyDef Def;
        public Model.AbnormalityInstance Anomaly;
        public Model.GameState State;
        public Model.Employee Employee;      // может быть null
        public Data.WorkType Work;
        public Data.WorkResult Result;
        public Model.ContainmentUnit Unit;   // может быть null
    }

    /// Исполнитель правил аномалий: триггер -> эффекты. Правила — только данные.
    public static class RuleEngine
    {
        public static void Apply(Model.GameState s, Data.AnomalyDef def, Model.AbnormalityInstance an,
                                 Data.RuleTrigger trigger, Model.Employee emp, Model.ContainmentUnit unit,
                                 Data.WorkType work, Data.WorkResult result, Systems.EffectSink sink)
        {
            if (def == null || def.Rules == null) return;
            foreach (var r in def.Rules)
            {
                if (r.Trigger != trigger) continue;
                if (trigger == Data.RuleTrigger.OnWorkResult)
                {
                    if (r.ResultFilter.HasValue && r.ResultFilter.Value != result) continue;
                    if (r.WorkFilter.HasValue && r.WorkFilter.Value != work) continue;
                }
                else if (trigger == Data.RuleTrigger.OnWorkType)
                {
                    if (r.WorkFilter.HasValue && r.WorkFilter.Value != work) continue;
                }
                ExecuteEffect(s, an, unit, emp, r, sink);
            }
        }

        private static void ExecuteEffect(Model.GameState s, Model.AbnormalityInstance an,
                                          Model.ContainmentUnit unit, Model.Employee emp,
                                          Data.AnomalyRule r, Systems.EffectSink sink)
        {
            switch (r.Effect)
            {
                case Data.RuleEffect.CounterDelta:
                    sink.CounterDelta(an, r.CounterDelta);
                    break;
                case Data.RuleEffect.DamageEmployee:
                    if (emp != null) sink.Damage(emp, r.Damage, r.Amount);
                    break;
                case Data.RuleEffect.PanicEmployee:
                    if (emp != null) sink.Panic(emp);
                    break;
                case Data.RuleEffect.FacilitySpDelta:
                    sink.FacilitySpDelta((int)r.Amount);
                    break;
                case Data.RuleEffect.Aura:
                    // Декларативное описание ауры побега; применяется CombatSystem-ом каждый тик.
                    break;
                case Data.RuleEffect.MoveToNearest:
                case Data.RuleEffect.TeleportRandom:
                case Data.RuleEffect.Attack:
                    // Поведенческие эффекты описаны в AnomalyDef (BreachMoves/Interval/Attack),
                    // исполняются CombatSystem на основе данных того же правила.
                    break;
            }
        }
    }
}
