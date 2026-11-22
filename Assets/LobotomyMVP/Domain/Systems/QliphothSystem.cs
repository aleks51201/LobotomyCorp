using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;
using LobotomyMVP.Domain.Rules;

namespace LobotomyMVP.Domain.Systems
{
    /// Счётчики аномалий и побеги: clamp [0..counterMax], при 0 -> Breach.
    public sealed class QliphothSystem
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;

        public QliphothSystem(GameState s, EventBus bus, EffectSink sink)
        {
            _s = s; _bus = bus; _sink = sink;
        }

        public static void TryBreach(GameState s, AbnormalityInstance an, EffectSink sink)
        {
            if (an.Breached) return;
            var def = AnomalyCatalog.Find(an.DefId);
            var unit = Find.Unit(s, an.UnitId);
            if (unit != null) unit.OccupantId = -1; // камера пуста и недоступна
            an.Breached = true;
            an.BreachHp = def.BreachHp;
            an.BreachMoveTimer = 0f;
            an.BreachAttackTimer = 0f;
            int roomId = unit != null ? unit.RoomId : 0;
            an.RoomId = roomId; // спавн аномалии в её комнате
            sink.Log($"ПОБЕГ: «{def.Name}»!");
            // OnBreachStart: эффекты правил
            Rules.RuleEngine.Apply(s, def, an, RuleTrigger.OnBreachStart, null, unit, default, default, sink);
        }

        /// Конец дня: все сбежавшие возвращаются в камеры, счётчик не восстанавливается сам.
        public void RecallAllBreached()
        {
            foreach (var a in _s.Anomalies)
            {
                if (!a.Breached) continue;
                a.Breached = false;
                a.BreachHp = 0f;
                var u = Find.Unit(_s, a.UnitId);
                if (u != null && u.OccupantId < 0) u.OccupantId = _s.Anomalies.IndexOf(a);
            }
        }
    }
}
