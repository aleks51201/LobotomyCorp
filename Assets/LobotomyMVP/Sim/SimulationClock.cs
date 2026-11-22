using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;

namespace LobotomyMVP.Sim
{
    /// Дискретный симулятор: fixed tick 0.1 сек, порядок систем фиксирован -> детерминизм.
    public sealed class SimulationClock
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;
        private readonly WorkSystem _work;
        private readonly CombatSystem _combat;
        private readonly VitalSystem _vital;
        private readonly MeltdownSystem _meltdown;
        private float _accumulator;

        public SimulationClock(GameState s, EventBus bus)
        {
            _s = s; _bus = bus;
            _sink = new EffectSink(s, bus);
            _work = new WorkSystem(s, bus, _sink);
            _combat = new CombatSystem(s, bus, _sink);
            _vital = new VitalSystem(s, bus, _sink);
            _meltdown = new MeltdownSystem(s, bus, _sink);
        }

        /// realDelta — время реальных секунд; внутри умножается на выбранную скорость.
        public void Advance(float realDelta, float speedMultiplier)
        {
            if (_s.Phase != GamePhase.Management) return;
            _accumulator += realDelta * speedMultiplier;
            while (_accumulator >= Balance.TickDt)
            {
                _accumulator -= Balance.TickDt;
                TickOnce(Balance.TickDt);
            }
        }

        private void TickOnce(float dt)
        {
            _s.SimTimeInDay += dt;
            _work.Tick(dt);       // перемещения и работы
            _combat.Tick(dt);     // бои и поведение побегов
            _meltdown.Tick(dt);   // таймеры расплава
            _vital.Tick(dt);      // реген, ауры, проверка потерь

            if (_s.Phase == GamePhase.Management && _s.SimTimeInDay >= Balance.DayTimeLimitSec)
            {
                _s.Phase = GamePhase.Timeout;
                _sink.Log("Время дня вышло — провал.");
                _bus.PhaseChanged(_s);
            }
        }
    }
}
