using System.Collections.Generic;
using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;

namespace LobotomyMVP.Domain.Systems
{
    /// Единая точка мутаций модели: урон, счётчики, события дня. Используется всеми системами и RuleEngine.
    public sealed class EffectSink
    {
        private readonly GameState _s;
        private readonly EventBus _bus;

        public EffectSink(GameState s, EventBus bus) { _s = s; _bus = bus; }

        public void Log(string text)
        {
            _s.Log.Add(new LogEntry { TimeSec = _s.SimTimeInDay, Text = text });
            if (_s.Log.Count > 200) _s.Log.RemoveAt(0);
            _bus.Log(text);
        }

        /// Формула урона: Red hp, White sp, Black hp/2+sp/2, Pale hp -= maxHp*x/100.
        public void Damage(Employee e, DamageType type, float amount)
        {
            if (e == null || e.State == EmployeeState.Dead) return;
            switch (type)
            {
                case DamageType.Red: e.Hp -= amount; break;
                case DamageType.White: e.Sp -= amount; break;
                case DamageType.Black: e.Hp -= amount / 2f; e.Sp -= amount / 2f; break;
                case DamageType.Pale: e.Hp -= e.MaxHp * amount / 100f; break;
            }
            CheckVitals(e);
            _bus.StateChanged();
        }

        public void Panic(Employee e)
        {
            if (e == null || e.State == EmployeeState.Dead || e.State == EmployeeState.Panicked) return;
            e.State = EmployeeState.Panicked;
            e.TargetId = -1;
            _s.Today.HadPanic = true;
            Log($"{e.Name} в панике!");
            _bus.StateChanged();
        }

        public void CheckVitals(Employee e)
        {
            if (e.State == EmployeeState.Dead) return;
            if (e.Hp <= 0f)
            {
                e.Hp = 0f;
                e.State = EmployeeState.Dead;
                e.TargetId = -1;
                _s.Today.HadDeath = true;
                Log($"{e.Name} погиб(ла).");
            }
            else if (e.Sp <= 0f)
            {
                e.Sp = 0f;
                Panic(e);
            }
        }

        /// Эффект правил CounterDelta с clamp [0..counterMax]; при 0 — побег (квота QliphothSystem.TryBreach).
        public void CounterDelta(AbnormalityInstance an, int delta)
        {
            if (an == null) return;
            int v = an.Counter + delta;
            if (v < 0) v = 0;
            if (v > an.CounterMax) v = an.CounterMax;
            an.Counter = v;
            var def = AnomalyCatalog.Find(an.DefId);
            string name = def != null ? def.Name : an.DefId;
            Log($"Счётчик «{name}»: {v}/{an.CounterMax} ({(delta >= 0 ? "+" : "")}{delta})");
            if (v == 0 && !an.Breached)
                QliphothSystem.TryBreach(_s, an, this);
            _bus.StateChanged();
        }

        public void FacilitySpDelta(int delta)
        {
            foreach (var e in _s.Employees)
            {
                if (e.State == EmployeeState.Dead || e.State == EmployeeState.Panicked) continue;
                e.Sp += delta;
                CheckVitals(e);
            }
            Log($"Весь объект: SP {(delta >= 0 ? "+" : "")}{delta}.");
            _bus.StateChanged();
        }
    }
}
