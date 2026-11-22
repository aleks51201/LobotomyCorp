using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;
using LobotomyMVP.Domain.Rules;

namespace LobotomyMVP.Domain.Systems
{
    /// Работа пешки с камерой: путь -> работа (workTime/workSpeed) -> бросок -> PE/XP/урон -> правила.
    public sealed class WorkSystem
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;

        public WorkSystem(GameState s, EventBus bus, EffectSink sink)
        {
            _s = s; _bus = bus; _sink = sink;
        }

        public bool Assign(int employeeId, int unitId, WorkType work)
        {
            var e = Find.Emp(_s, employeeId);
            var u = Find.Unit(_s, unitId);
            if (e == null || u == null) return false;
            if (e.State == EmployeeState.Dead || e.State == EmployeeState.Panicked) return false;
            if (u.OccupantId < 0) return false;
            var an = _s.Anomalies[u.OccupantId];
            if (an.Breached) return false; // камера недоступна
            e.State = EmployeeState.Moving;
            e.MoveFromRoomId = e.RoomId;
            e.TargetId = unitId;
            e.PendingUnit = unitId;
            e.PendingWork = work;
            e.WorkTimer = 0f;
            e.EdgeProgress = 0f;
            _bus.StateChanged();
            return true;
        }

        public void Tick(float dt)
        {
            foreach (var e in _s.Employees)
            {
                if (e.State == EmployeeState.Moving && e.PendingUnit >= 0)
                    MoveTick(e, dt);
                else if (e.State == EmployeeState.Working)
                    WorkTick(e, dt);
            }
        }

        private void MoveTick(Employee e, float dt)
        {
            var u = Find.Unit(_s, e.PendingUnit);
            if (u == null || u.OccupantId < 0 || _s.Anomalies[u.OccupantId].Breached)
            {
                e.State = EmployeeState.Idle; e.PendingUnit = -1; e.TargetId = -1;
                return;
            }
            // Скорость в комнатах/сек; ребро = 1 комната.
            float speed = Balance.MoveSpeedBase + Balance.MoveSpeedPerJustice * (e.Get(StatType.Justice).Level - 1);
            e.EdgeProgress += speed * dt;
            if (e.EdgeProgress >= 1f)
            {
                e.EdgeProgress = 0f;
                e.RoomId = u.RoomId;
                e.State = EmployeeState.Working;
                e.WorkTimer = 0f;
                _bus.StateChanged();
            }
        }

        private void WorkTick(Employee e, float dt)
        {
            var u = Find.Unit(_s, e.PendingUnit);
            if (u == null || u.OccupantId < 0) { e.State = EmployeeState.Idle; e.PendingUnit = -1; return; }
            var an = _s.Anomalies[u.OccupantId];
            if (an.Breached) { e.State = EmployeeState.Idle; e.PendingUnit = -1; e.TargetId = -1; return; }
            var def = AnomalyCatalog.Find(an.DefId);
            float workSpeed = 1f + Balance.WorkSpeedPerControl * (e.Get(StatType.Control).Level - 1);
            e.WorkTimer += dt;
            if (e.WorkTimer >= def.WorkTimeSec / workSpeed)
                ResolveWork(e, an, u, def);
        }

        private void ResolveWork(Employee e, AbnormalityInstance an, ContainmentUnit u, AnomalyDef def)
        {
            WorkType work = e.PendingWork;
            StatType prof = Employee.ProfilerStatFor(work);
            int level = e.Get(prof).Level;
            float p = SuccessProbability(def, an, u, e, work, level);
            double r = _s.Rng.NextDouble();
            WorkResult res;
            if (r < p * 0.55) res = WorkResult.Good;
            else if (r < p) res = WorkResult.Normal;
            else res = WorkResult.Bad;

            int pe = 0;
            if (res == WorkResult.Good) pe = _s.Rng.NextInt(def.YieldGoodPeMin, def.YieldGoodPeMax);
            else if (res == WorkResult.Normal) pe = _s.Rng.NextInt(def.YieldNormalPeMin, def.YieldNormalPeMax);
            _s.Pe += pe;
            _s.Energy += pe * Balance.EnergyPerPe;

            // XP в профильный и «рабочий» статы: Good 12 / Normal 6 / Bad 2
            int xp = res == WorkResult.Good ? Balance.XpGood : res == WorkResult.Normal ? Balance.XpNormal : Balance.XpBad;
            ApplyXp(e, prof, xp);

            an.RecordWork(work, res);
            if (res == WorkResult.Bad && def.BadDamageAmount != 0f)
            {
                float amt = def.BadDamageAmount;
                if (def.BadDamageType == DamageType.White && amt < 0f) amt = e.MaxSp; // гарантированная паника
                _sink.Damage(e, def.BadDamageType, amt);
            }

            string wname = Employee.WorkName(work);
            _sink.Log($"{e.Name}: {wname} -> {(res == WorkResult.Good ? "Хорошо" : res == WorkResult.Normal ? "Нормально" : "Плохо")}, PE +{pe}");

            _s.Today.GoodCount += res == WorkResult.Good ? 1 : 0;
            _s.Today.NormalCount += res == WorkResult.Normal ? 1 : 0;
            _s.Today.BadCount += res == WorkResult.Bad ? 1 : 0;

            // Правило повтора: та же пешка на той же камере два раза подряд
            bool repeat = e.LastWorkedUnitId == u.Id;
            e.LastWorkedUnitId = u.Id;
            e.State = EmployeeState.Idle;
            e.PendingUnit = -1;
            e.TargetId = -1;

            // Правила аномалии (данные -> RuleEngine)
            RuleEngine.Apply(_s, def, an, RuleTrigger.OnWorkType, e, u, work, res, _sink);
            RuleEngine.Apply(_s, def, an, RuleTrigger.OnWorkResult, e, u, work, res, _sink);
            if (repeat) RuleEngine.Apply(_s, def, an, RuleTrigger.OnRepeatEmployee, e, u, work, res, _sink);

            // Расплав: +1 к гейджу за каждую работу
            MeltdownSystem.RegisterWork(_s, _sink);

            // Работа по меченой камере снимает флаг и даёт +1 PE
            if (u.MeltdownFlag && e.State == EmployeeState.Idle)
            {
                u.MeltdownFlag = false;
                u.MeltdownTimer = 0f;
                _s.Pe += Balance.MeltdownFlagWorkPeReward;
                _sink.Log($"Расплав камеры #{u.Id} предотвращён (+{Balance.MeltdownFlagWorkPeReward} PE).");
            }

            if (_s.Energy >= _s.Quota && _s.Phase == GamePhase.Management)
            {
                _s.Phase = GamePhase.QuotaMet;
                _sink.Log("Квота дня выполнена!");
                _bus.PhaseChanged(_s);
            }
            _bus.StateChanged();
        }

        /// p = clamp(base + 0.03*(Cont-1) - (meltdown? 0.10 : 0), 0.05, 0.95); ruleMods сейчас всегда 0.
        public static float SuccessProbability(AnomalyDef def, AbnormalityInstance an, ContainmentUnit u,
                                               Employee e, WorkType work, int statLevel)
        {
            float baseP = FavorTables.Base(def.Favor[(int)work], statLevel);
            float p = baseP
                      + Balance.SuccessBonusPerControl * (e.Get(StatType.Control).Level - 1)
                      - (u != null && u.MeltdownFlag ? Balance.MeltdownSuccessPenalty : 0f);
            if (p < Balance.SuccessMin) p = Balance.SuccessMin;
            if (p > Balance.SuccessMax) p = Balance.SuccessMax;
            return p;
        }

        private void ApplyXp(Employee e, StatType t, int xp)
        {
            var st = e.Get(t);
            st.AddXp(xp);
            int newLvl = Employee.LevelFromXp(st.Xp, Balance.XpThresholds);
            if (newLvl > st.Level)
            {
                st.Level = newLvl;
                RecalcVitals(e);
                _sink.Log($"{e.Name}: {Employee.StatName(t)} повышен до ур. {newLvl}.");
            }
        }

        private void RecalcVitals(Employee e)
        {
            float hpRatio = e.MaxHp > 0 ? e.Hp / e.MaxHp : 1f;
            float spRatio = e.MaxSp > 0 ? e.Sp / e.MaxSp : 1f;
            e.MaxHp = Balance.HpBase + Balance.HpPerFortitude * e.Get(StatType.Fortitude).Level;
            e.MaxSp = Balance.SpBase + Balance.SpPerPrudence * e.Get(StatType.Prudence).Level;
            e.Hp = e.MaxHp * hpRatio;
            e.Sp = e.MaxSp * spRatio;
        }
    }

    internal static class Find
    {
        public static Employee Emp(GameState s, int id)
        {
            foreach (var e in s.Employees) if (e.Id == id) return e;
            return null;
        }
        public static ContainmentUnit Unit(GameState s, int id)
        {
            foreach (var u in s.Units) if (u.Id == id) return u;
            return null;
        }
    }
}
