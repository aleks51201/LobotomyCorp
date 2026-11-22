using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;
using LobotomyMVP.Domain.Rules;

namespace LobotomyMVP.Domain.Systems
{
    /// Подавление побегов: подход пешки, автоатаки, ответные атаки и перемещение аномалий по правилам.
    public sealed class CombatSystem
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;

        public CombatSystem(GameState s, EventBus bus, EffectSink sink)
        {
            _s = s; _bus = bus; _sink = sink;
        }

        public bool Suppress(int employeeId, int anomalyIndex)
        {
            Employee e = null;
            for (int i = 0; i < _s.Employees.Count; i++)
                if (_s.Employees[i].Id == employeeId) { e = _s.Employees[i]; break; }
            if (e == null || anomalyIndex < 0 || anomalyIndex >= _s.Anomalies.Count) return false;
            var a = _s.Anomalies[anomalyIndex];
            if (!a.Breached) return false;
            if (e.State == EmployeeState.Dead || e.State == EmployeeState.Panicked) return false;
            // Отмена текущей работы/приказа
            e.PendingUnit = -1;
            e.State = EmployeeState.Moving;
            e.MoveFromRoomId = e.RoomId;
            e.TargetId = -(anomalyIndex + 1); // маркер "цель — аномалия"
            e.EdgeProgress = 0f;
            _bus.StateChanged();
            return true;
        }

        public void Tick(float dt)
        {
            TickEmployees(dt);
            TickAnomalies(dt);
        }

        private static int AnomalyTarget(Employee e) => -(e.TargetId + 1);

        private void TickEmployees(float dt)
        {
            foreach (var e in _s.Employees)
            {
                if (e.TargetId >= 0) continue; // это работа, обрабатывает WorkSystem
                if (e.State == EmployeeState.Dead || e.State == EmployeeState.Panicked) continue;
                int ai = AnomalyTarget(e);
                if (ai < 0 || ai >= _s.Anomalies.Count) { e.TargetId = -1; if (e.State == EmployeeState.Fighting || e.State == EmployeeState.Moving) e.State = EmployeeState.Idle; continue; }
                var a = _s.Anomalies[ai];
                if (!a.Breached) { e.TargetId = -1; e.State = EmployeeState.Idle; continue; }

                if (e.State == EmployeeState.Moving)
                {
                    float speed = Balance.MoveSpeedBase + Balance.MoveSpeedPerJustice * (e.Get(StatType.Justice).Level - 1);
                    e.EdgeProgress += speed * dt;
                    if (e.EdgeProgress >= 1f)
                    {
                        e.EdgeProgress = 0f;
                        e.RoomId = a.RoomId;
                        e.State = EmployeeState.Fighting;
                        e.AttackTimer = 0f;
                    }
                }
                else if (e.State == EmployeeState.Fighting)
                {
                    if (e.RoomId != a.RoomId)
                    {
                        e.State = EmployeeState.Moving;
                        e.MoveFromRoomId = e.RoomId;
                        e.EdgeProgress = 0f;
                        continue;
                    }
                    var def = AnomalyCatalog.Find(a.DefId);
                    float interval = Balance.AttackIntervalBase / (1f + Balance.AttackIntervalPerJustice * (e.Get(StatType.Justice).Level - 1));
                    e.AttackTimer += dt;
                    while (e.AttackTimer >= interval)
                    {
                        e.AttackTimer -= interval;
                        // Урон пешки (Red) умножается на резист аномалии
                        float dmg = Balance.PawnBaseDamage * def.Resist[(int)DamageType.Red];
                        a.BreachHp -= dmg;
                        if (a.BreachHp <= 0f)
                        {
                            ReturnToContainment(a, e);
                            break;
                        }
                    }
                }
            }
        }

        private void ReturnToContainment(AbnormalityInstance a, Employee by)
        {
            a.Breached = false;
            a.BreachHp = 0f;
            var u = Find.Unit(_s, a.UnitId);
            if (u != null && u.OccupantId < 0) u.OccupantId = _s.Anomalies.IndexOf(a);
            a.Counter = a.CounterMax; // после подавления счётчик = max
            _sink.Log($"«{AnomalyCatalog.Find(a.DefId).Name}» подавлена{(by != null ? " (" + by.Name + ")" : "")}. Счётчик восстановлен.");
            // Освободить всех, кто шёл/дрался с любой больше не сбежавшей аномалией
            foreach (var e in _s.Employees)
            {
                int ai = AnomalyTarget(e);
                if (ai < 0 || ai >= _s.Anomalies.Count) continue;
                if (!_s.Anomalies[ai].Breached && (e.State == EmployeeState.Fighting || e.State == EmployeeState.Moving))
                {
                    e.State = EmployeeState.Idle;
                    e.TargetId = -1;
                    e.EdgeProgress = 0f;
                }
            }
            _bus.StateChanged();
        }

        private void TickAnomalies(float dt)
        {
            foreach (var a in _s.Anomalies)
            {
                if (!a.Breached) continue;
                var def = AnomalyCatalog.Find(a.DefId);

                // Аура побега (белый урон в секунду всем в комнате)
                if (def.BreachAuraDamagePerSec > 0f)
                {
                    foreach (var e in _s.Employees)
                    {
                        if (e.State == EmployeeState.Dead) continue;
                        if (e.RoomId == a.RoomId)
                            _sink.Damage(e, DamageType.White, def.BreachAuraDamagePerSec * dt);
                    }
                }

                // Перемещение по правилу
                if (def.BreachMoves)
                {
                    a.BreachMoveTimer += dt;
                    if (a.BreachMoveTimer >= def.BreachMoveIntervalSec)
                    {
                        a.BreachMoveTimer = 0f;
                        MoveAnomaly(a, def);
                    }
                }

                // Атаки по интервалу
                if (def.BreachAttackAmount > 0f && def.BreachAttackIntervalSec > 0f)
                {
                    a.BreachAttackTimer += dt;
                    if (a.BreachAttackTimer >= def.BreachAttackIntervalSec)
                    {
                        a.BreachAttackTimer = 0f;
                        AnomalyAttack(a, def);
                    }
                }

                Rules.RuleEngine.Apply(_s, def, a, RuleTrigger.OnBreachTick, null, null, default, default, _sink);
            }
        }

        private void MoveAnomaly(AbnormalityInstance a, AnomalyDef def)
        {
            if (def.BreachMoveIntervalSec > 0f && IsTeleporter(def))
            {
                // TeleportRandom: случайная комната
                int idx = _s.Rng.NextInt(0, _s.Rooms.Count - 1);
                a.RoomId = _s.Rooms[idx].Id;
                _sink.Log($"«{def.Name}» телепортировалась в комнату #{a.RoomId}.");
            }
            else
            {
                // MoveToNearest: комната ближайшей живой пешки
                Employee best = null;
                foreach (var e in _s.Employees)
                {
                    if (e.State == EmployeeState.Dead || e.State == EmployeeState.Panicked) continue;
                    if (best == null) best = e;
                }
                if (best != null && best.RoomId >= 0)
                {
                    a.RoomId = best.RoomId;
                    _sink.Log($"«{def.Name}» идёт к пешке ({best.Name}), комната #{a.RoomId}.");
                }
            }
            _bus.StateChanged();
        }

        private static bool IsTeleporter(AnomalyDef def) => def.BreachAttacksAll; // Хор: телепорт; Пёс: преследование

        private void AnomalyAttack(AbnormalityInstance a, AnomalyDef def)
        {
            foreach (var e in _s.Employees)
            {
                if (e.State == EmployeeState.Dead) continue;
                if (e.RoomId != a.RoomId) continue;
                _sink.Damage(e, def.BreachAttackType, def.BreachAttackAmount);
            }
        }
    }
}
