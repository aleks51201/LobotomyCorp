using System.Collections.Generic;
using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;

namespace LobotomyMVP.Flow
{
    /// Полный снимок GameState для отката дня и сейва (plain fields, JSON-сериализуемо).
    [System.Serializable]
    public sealed class DaySnapshot
    {
        public int Day;
        public int Energy;
        public int Pe;
        public int Lob;
        public int RngSeed;
        public ulong RngState;
        public int MeltdownLevel;
        public List<string> PoolIds = new List<string>();
        public List<EmpSnap> Employees = new List<EmpSnap>();
        public List<AnSnap> Anomalies = new List<AnSnap>();
        public List<UnitSnap> Units = new List<UnitSnap>();

        [System.Serializable]
        public sealed class EmpSnap
        {
            public int Id; public string Name;
            public int[] Levels = new int[4]; public int[] Xps = new int[4];
            public float Hp, MaxHp, Sp, MaxSp; public int DepartmentId; public int RoomId;
        }

        [System.Serializable]
        public sealed class AnSnap
        {
            public string DefId; public int UnitId; public int Counter; public int InfoLevel;
            public int[] History = new int[12];
        }

        [System.Serializable]
        public sealed class UnitSnap
        {
            public int Id, DepartmentId, RoomId, OccupantId;
        }

        public static DaySnapshot Capture(GameState s)
        {
            var d = new DaySnapshot
            {
                Day = s.Day, Energy = s.Energy, Pe = s.Pe, Lob = s.Lob,
                RngSeed = s.RngSeed, RngState = s.Rng.RawState,
                MeltdownLevel = s.MeltdownLevel,
            };
            d.PoolIds.AddRange(s.AvailablePool);
            foreach (var e in s.Employees)
            {
                if (e.State == EmployeeState.Dead) continue; // мёртвые удаляются из ростера навсегда
                var es = new EmpSnap { Id = e.Id, Name = e.Name, Hp = e.Hp, MaxHp = e.MaxHp, Sp = e.Sp, MaxSp = e.MaxSp, DepartmentId = e.DepartmentId, RoomId = e.RoomId };
                for (int i = 0; i < 4; i++) { es.Levels[i] = e.Stats[i].Level; es.Xps[i] = e.Stats[i].Xp; }
                d.Employees.Add(es);
            }
            foreach (var a in s.Anomalies)
            {
                d.Anomalies.Add(new AnSnap { DefId = a.DefId, UnitId = a.UnitId, Counter = a.Counter, InfoLevel = a.InfoLevel });
                a.WorkHistoryCounts.CopyTo(d.Anomalies[d.Anomalies.Count - 1].History, 0);
            }
            foreach (var u in s.Units)
                d.Units.Add(new UnitSnap { Id = u.Id, DepartmentId = u.DepartmentId, RoomId = u.RoomId, OccupantId = u.OccupantId });
            return d;
        }

        /// Восстанавливает состояние дня из снапшота (используется и для "повторить день", и для загрузки).
        public void RestoreInto(GameState s)
        {
            s.Day = Day; s.Energy = Energy; s.Pe = Pe; s.Lob = Lob;
            s.MeltdownLevel = MeltdownLevel;
            s.RngSeed = RngSeed;
            s.Rng = new Rng(RngSeed);
            s.Rng.SetStateForRestore(RngSeed, RngState);
            s.AvailablePool.Clear(); s.AvailablePool.AddRange(PoolIds);

            s.Employees.Clear();
            foreach (var es in Employees)
            {
                var e = new Employee { Id = es.Id, Name = es.Name, Hp = es.Hp, MaxHp = es.MaxHp, Sp = es.Sp, MaxSp = es.MaxSp, DepartmentId = es.DepartmentId, RoomId = es.RoomId };
                for (int i = 0; i < 4; i++) { e.Stats[i].Level = es.Levels[i]; e.Stats[i].Xp = es.Xps[i]; }
                s.Employees.Add(e);
            }
            if (s.NextEmployeeId <= LastEmpId()) s.NextEmployeeId = LastEmpId() + 1;

            s.Anomalies.Clear();
            foreach (var a in Anomalies)
            {
                var def = AnomalyCatalog.Find(a.DefId);
                s.Anomalies.Add(new AbnormalityInstance
                {
                    DefId = a.DefId, UnitId = a.UnitId, Counter = a.Counter,
                    CounterMax = def.CounterMax, InfoLevel = a.InfoLevel,
                });
                a.History.CopyTo(s.Anomalies[s.Anomalies.Count - 1].WorkHistoryCounts, 0);
            }

            s.Units.Clear();
            foreach (var u in Units)
                s.Units.Add(new ContainmentUnit { Id = u.Id, DepartmentId = u.DepartmentId, RoomId = u.RoomId, OccupantId = u.OccupantId });

            s.Phase = GamePhase.Preparation;
            s.SimTimeInDay = 0f;
            s.MeltdownGauge = 0;
            s.Quota = Balance.QuotaBase + Balance.QuotaPerDay * (s.Day - 1);
            s.Log.Clear();
            s.Today = new DayStats();
            s.DayFailed = false;
        }

        private int LastEmpId()
        {
            int m = 0;
            foreach (var e in Employees) if (e.Id > m) m = e.Id;
            return m;
        }
    }
}
