using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;

namespace LobotomyMVP.Domain.Systems
{
    /// Гейдж расплава: +1 за каждую работу; на 6 — событие на k занятых камерах.
    public sealed class MeltdownSystem
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;

        public MeltdownSystem(GameState s, EventBus bus, EffectSink sink)
        {
            _s = s; _bus = bus; _sink = sink;
        }

        public static void RegisterWork(GameState s, EffectSink sink)
        {
            s.MeltdownGauge++;
            if (s.MeltdownGauge >= Balance.MeltdownGaugeThreshold)
                TriggerEvent(s, sink);
        }

        private static void TriggerEvent(GameState s, EffectSink sink)
        {
            int k = 1 + s.MeltdownLevel / 2;
            // случайные k камер с обитателем
            var occupied = new System.Collections.Generic.List<ContainmentUnit>();
            foreach (var u in s.Units)
                if (u.OccupantId >= 0 && !s.Anomalies[u.OccupantId].Breached)
                    occupied.Add(u);
            for (int i = 0; i < k && occupied.Count > 0; i++)
            {
                int idx = s.Rng.NextInt(0, occupied.Count - 1);
                var u = occupied[idx];
                occupied.RemoveAt(idx);
                var a = s.Anomalies[u.OccupantId];
                u.MeltdownFlag = true;
                u.MeltdownTimer = Balance.MeltdownTimerSec;
                sink.CounterDelta(a, -1); // -1 счётчик сразу; при достижении 0 вызовет побег
                sink.Log($"РАСПЛАВ: камера #{u.Id} помечена (-1 счётчик, -10% успеха, {Balance.MeltdownTimerSec:0} сек).");
            }
            s.MeltdownLevel++;
            s.MeltdownGauge = 0;
            sink.Log($"Уровень расплава: {s.MeltdownLevel}.");
        }

        public void Tick(float dt)
        {
            foreach (var u in _s.Units)
            {
                if (!u.MeltdownFlag) continue;
                u.MeltdownTimer -= dt;
                if (u.MeltdownTimer <= 0f)
                {
                    u.MeltdownFlag = false;
                    u.MeltdownTimer = 0f;
                    if (u.OccupantId >= 0)
                    {
                        var a = _s.Anomalies[u.OccupantId];
                        sinkTimeout(-1, a);
                    }
                }
            }
        }

        private void sinkTimeout(int delta, AbnormalityInstance a)
        {
            _sink.Log("Таймер расплава истёк: ещё -1 счётчик.");
            var def = AnomalyCatalog.Find(a.DefId);
            Rules.RuleEngine.Apply(_s, def, a, RuleTrigger.OnMeltdownTimeout, null, null, default, default, _sink);
            _sink.CounterDelta(a, delta);
        }
    }
}
