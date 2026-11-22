using System.Collections.Generic;
using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;
using LobotomyMVP.Domain.Systems;
using LobotomyMVP.Flow;
using LobotomyMVP.Sim;

namespace LobotomyMVP.App
{
    /// Фасад приложения: единственная точка входа для Presentation (команды + чтение GameState).
    public sealed class GameSession
    {
        public readonly GameState State;
        public readonly EventBus Bus;
        public readonly SimulationClock Clock;
        public readonly DayCycleStateMachine Cycle;
        public float Speed = 1f;              // индекс в Balance.Speeds хранит UI, здесь множитель
        public int SpeedIndex = 1;

        private readonly EffectSink _sink;
        private readonly WorkSystem _work;
        private readonly CombatSystem _combat;
        private readonly InfoSystem _info;
        private readonly Queue<GameCommand> _commands = new Queue<GameCommand>();

        public GameSession()
        {
            State = new GameState();
            Bus = new EventBus();
            _sink = new EffectSink(State, Bus);
            _work = new WorkSystem(State, Bus, _sink);
            _combat = new CombatSystem(State, Bus, _sink);
            _info = new InfoSystem(State, Bus, _sink);
            Clock = new SimulationClock(State, Bus);
            Cycle = new DayCycleStateMachine(State, Bus);
            NewGame(System.Environment.TickCount);
        }

        public void NewGame(int seed)
        {
            var s = State;
            s.RngSeed = seed;
            s.Rng = new Rng(seed);
            s.Day = 1;
            s.Phase = GamePhase.Preparation;
            s.Energy = 0;
            s.Pe = Balance.StartPe;
            s.Lob = Balance.StartLob;
            s.MeltdownGauge = 0;
            s.MeltdownLevel = 0;
            s.SimTimeInDay = 0f;
            s.Quota = Balance.QuotaBase;
            s.Employees.Clear();
            s.Anomalies.Clear();
            s.Units.Clear();
            s.Rooms.Clear();
            s.Log.Clear();
            s.Today = new DayStats();
            s.NextEmployeeId = 1;
            s.AvailablePool.Clear();
            s.OfferedIds.Clear();
            Cycle.DayStartSnapshot = null;

            // Комнаты: 2 отдела × 3 слота + комната отдыха (id 99)
            int roomId = 1;
            for (int d = 0; d < Balance.Departments; d++)
                for (int slot = 0; slot < Balance.SlotsPerDepartment; slot++)
                {
                    s.Rooms.Add(new RoomInfo { Id = roomId, DepartmentId = d, SlotIndex = slot });
                    s.Units.Add(new ContainmentUnit { Id = s.Units.Count + 1, DepartmentId = d, RoomId = roomId });
                    roomId++;
                }
            s.Rooms.Add(new RoomInfo { Id = 99, DepartmentId = -1, SlotIndex = -1 });

            // Стартовые пешки
            for (int i = 0; i < Balance.StartEmployees; i++)
                s.Employees.Add(MakeEmployee(i % Balance.Departments));

            // Стартовая аномалия A1 в первой камере
            AddAnomalyToFreeUnit(AnomalyCatalog.A1.Id);

            // Пул выбора: всё кроме стартовой
            foreach (var def in AnomalyCatalog.All)
                if (def.Id != AnomalyCatalog.A1.Id) s.AvailablePool.Add(def.Id);

            _sink.Log("Объект инициализирован. Подготовка к дню 1.");
            Bus.PhaseChanged(s);
        }

        private Employee MakeEmployee(int departmentId)
        {
            var s = State;
            var e = new Employee
            {
                Id = s.NextEmployeeId++,
                Name = EmployeeNames.Names[(s.NextEmployeeId - 2) % EmployeeNames.Names.Length],
                DepartmentId = departmentId,
                RoomId = 99,
            };
            e.MaxHp = Balance.HpBase + Balance.HpPerFortitude * e.Get(StatType.Fortitude).Level;
            e.MaxSp = Balance.SpBase + Balance.SpPerPrudence * e.Get(StatType.Prudence).Level;
            e.Hp = e.MaxHp; e.Sp = e.MaxSp;
            return e;
        }

        private void AddAnomalyToFreeUnit(string defId)
        {
            var s = State;
            var def = AnomalyCatalog.Find(defId);
            ContainmentUnit free = null;
            foreach (var u in s.Units) if (u.OccupantId < 0) { free = u; break; }
            if (free == null) return;
            var a = new AbnormalityInstance
            {
                DefId = defId, UnitId = free.Id, Counter = def.CounterMax, CounterMax = def.CounterMax,
                InfoLevel = 0, RoomId = free.RoomId,
            };
            s.Anomalies.Add(a);
            free.OccupantId = s.Anomalies.Count - 1;
        }

        public void Enqueue(GameCommand c) => _commands.Enqueue(c);

        public void Update(float realDelta)
        {
            DrainCommands();
            Clock.Advance(realDelta, Balance.Speeds[SpeedIndex]);
        }

        private void DrainCommands()
        {
            while (_commands.Count > 0)
                Execute(_commands.Dequeue());
        }

        public void Execute(GameCommand c)
        {
            var s = State;
            switch (c.Kind)
            {
                case CommandKind.AssignWork:
                    if (s.Phase == GamePhase.Management) _work.Assign(c.EmployeeId, c.UnitId, c.Work);
                    break;
                case CommandKind.Suppress:
                    if (s.Phase == GamePhase.Management) _combat.Suppress(c.EmployeeId, c.AnomalyIndex);
                    break;
                case CommandKind.RecallToRest:
                {
                    var e = FindEmp(c.EmployeeId);
                    if (e != null && (e.State == EmployeeState.Idle || e.State == EmployeeState.Fighting))
                    {
                        e.State = EmployeeState.Idle; e.TargetId = -1; e.PendingUnit = -1; e.RoomId = 99;
                        Bus.StateChanged();
                    }
                    break;
                }
                case CommandKind.Hire:
                    if (s.Phase == GamePhase.Preparation && s.Lob >= Balance.HireCostLob && AliveCount() < Balance.EmployeeLimit)
                    {
                        s.Lob -= Balance.HireCostLob;
                        s.Employees.Add(MakeEmployee(c.DepartmentId));
                        _sink.Log($"Нанят новый сотрудник (-{Balance.HireCostLob} LOB).");
                        Bus.StateChanged();
                    }
                    break;
                case CommandKind.UpgradeStat:
                {
                    var e = FindEmp(c.EmployeeId);
                    if (e == null || s.Phase != GamePhase.Preparation) break;
                    var st = e.Get(c.Stat);
                    if (st.Level >= Balance.StatLevelMax) break;
                    int cost = Balance.UpgradeCostLob[st.Level - 1]; // 1->2 = index0 = 2 LOB
                    if (s.Lob < cost) break;
                    s.Lob -= cost;
                    st.Level++;
                    RecalcVitals(e);
                    _sink.Log($"{e.Name}: {Employee.StatName(c.Stat)} -> ур. {st.Level} (-{cost} LOB).");
                    Bus.StateChanged();
                    break;
                }
                case CommandKind.StartDay:
                    Cycle.StartDay();
                    break;
                case CommandKind.EndDay:
                    Cycle.FinishDay();
                    break;
                case CommandKind.OpenInfo:
                    if (s.Phase == GamePhase.Preparation || s.Phase == GamePhase.Management)
                        _info.Unlock(c.AnomalyIndex, c.InfoLevel);
                    break;
                case CommandKind.PickAnomaly:
                    Cycle.PickAnomaly(c.AnomalyDefId);
                    Cycle.CheckGameOver();
                    break;
                case CommandKind.SkipSelection:
                    Cycle.SkipSelection();
                    Cycle.CheckGameOver();
                    break;
                case CommandKind.RetryDay:
                    Cycle.RetryDay();
                    break;
            }
        }

        private Employee FindEmp(int id)
        {
            foreach (var e in State.Employees) if (e.Id == id) return e;
            return null;
        }

        private int AliveCount()
        {
            int n = 0;
            foreach (var e in State.Employees) if (e.State != EmployeeState.Dead) n++;
            return n;
        }

        private void RecalcVitals(Employee e)
        {
            e.MaxHp = Balance.HpBase + Balance.HpPerFortitude * e.Get(StatType.Fortitude).Level;
            e.MaxSp = Balance.SpBase + Balance.SpPerPrudence * e.Get(StatType.Prudence).Level;
            if (e.Hp > e.MaxHp) e.Hp = e.MaxHp;
            if (e.Sp > e.MaxSp) e.Sp = e.MaxSp;
        }

        // Публичные помощники для UI
        public Employee EmpById(int id) => FindEmp(id);
        public ContainmentUnit UnitById(int id) { foreach (var u in State.Units) if (u.Id == id) return u; return null; }
        public static string StateName(EmployeeState st)
        {
            switch (st)
            {
                case EmployeeState.Idle: return "Ожидание";
                case EmployeeState.Moving: return "В пути";
                case EmployeeState.Working: return "Работа";
                case EmployeeState.Fighting: return "Бой";
                case EmployeeState.Panicked: return "Паника";
                default: return "Мёртв";
            }
        }
    }

}
