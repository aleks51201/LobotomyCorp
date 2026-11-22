using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;

namespace LobotomyMVP.Domain.Systems
{
    /// Жизни сотрудников: реген в покое, аура паники, смерть/паника, отдых.
    public sealed class VitalSystem
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;

        public VitalSystem(GameState s, EventBus bus, EffectSink sink)
        {
            _s = s; _bus = bus; _sink = sink;
        }

        public void Tick(float dt)
        {
            // Аура паники: White 2/сек по соседям в комнате паникующего.
            foreach (var p in _s.Employees)
            {
                if (p.State != EmployeeState.Panicked) continue;
                foreach (var e in _s.Employees)
                {
                    if (e == p || e.State == EmployeeState.Dead) continue;
                    if (e.RoomId == p.RoomId && e.RoomId >= 0)
                        _sink.Damage(e, DamageType.White, Balance.PanicAuraDamagePerSec * dt);
                }
            }

            foreach (var e in _s.Employees)
            {
                if (e.State != EmployeeState.Idle) continue;
                if (RoomHasBreach(e.RoomId)) continue; // реген только если в комнате нет сбежавшей аномалии
                e.Sp += Balance.RegenSpPerSec * dt;
                e.Hp += Balance.RegenHpPerSec * dt;
                if (e.Sp > e.MaxSp) e.Sp = e.MaxSp;
                if (e.Hp > e.MaxHp) e.Hp = e.MaxHp;
            }

            // Проверка "все потеряны"
            bool anyAlive = false;
            foreach (var e in _s.Employees)
                if (e.State != EmployeeState.Dead) { anyAlive = true; break; }
            if (!anyAlive && _s.Phase == GamePhase.Management)
            {
                _s.Phase = GamePhase.AllLost;
                _sink.Log("Все сотрудники потеряны.");
                _bus.PhaseChanged(_s);
            }
        }

        private bool RoomHasBreach(int roomId)
        {
            foreach (var a in _s.Anomalies)
                if (a.Breached && a.RoomId == roomId) return true;
            return false;
        }

        /// Начало дня: HP/SP полные; паникующие за ночь недоступны до конца предыдущего дня — сброс состояния.
        public static void RestoreForNewDay(GameState s)
        {
            foreach (var e in s.Employees)
            {
                if (e.State == EmployeeState.Dead) continue;
                e.State = EmployeeState.Idle;
                e.Hp = e.MaxHp;
                e.Sp = e.MaxSp;
                e.TargetId = -1;
                e.PendingUnit = -1;
                e.EdgeProgress = 0f;
            }
        }
    }
}
