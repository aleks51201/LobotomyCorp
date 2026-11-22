using System.Collections.Generic;
using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;
using LobotomyMVP.Domain.Systems;

namespace LobotomyMVP.Flow
{
    /// Preparing -> Management -> (QuotaMet | AllLost | Timeout) -> Report -> Selection -> Preparation(day+1)
    public sealed class DayCycleStateMachine
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;
        private readonly QliphothSystem _qliphoth;

        public DaySnapshot DayStartSnapshot;

        public DayCycleStateMachine(GameState s, EventBus bus)
        {
            _s = s; _bus = bus;
            _sink = new EffectSink(s, bus);
            _qliphoth = new QliphothSystem(s, bus, _sink);
        }

        public void StartDay()
        {
            if (_s.Phase != GamePhase.Preparation) return;
            // Снимок начала дня для отката/сейва
            DayStartSnapshot = DaySnapshot.Capture(_s);
            VitalSystem.RestoreForNewDay(_s);
            _s.Energy = 0;
            _s.Today = new DayStats();
            _s.SimTimeInDay = 0f;
            _s.MeltdownGauge = 0;
            _s.Quota = Balance.QuotaBase + Balance.QuotaPerDay * (_s.Day - 1);
            foreach (var u in _s.Units) { u.MeltdownFlag = false; u.MeltdownTimer = 0f; }
            _s.Phase = GamePhase.Management;
            _sink.Log($"День {_s.Day}: начало смены. Квота {_s.Quota} энергии.");
            _bus.PhaseChanged(_s);
        }

        /// Вызывается из UI ("Завершить день") при QuotaMet или автоматически по таймауту/AllLost.
        public void FinishDay()
        {
            if (_s.Phase == GamePhase.Timeout || _s.Phase == GamePhase.AllLost)
            {
                _s.DayFailed = true;
                EnterReport();
                return;
            }
            if (_s.Phase != GamePhase.QuotaMet) return;
            _s.DayFailed = false;
            EnterReport();
        }

        private void EnterReport()
        {
            _qliphoth.RecallAllBreached(); // конец дня: все сбежавшие возвращаются
            if (_s.DayFailed)
            {
                // Провал: без наград, откат к снапшоту начала дня, тот же день снова в подготовке.
                _sink.Log("День провален — возврат к началу дня.");
                RetryDay();
                return;
            }
            _s.Phase = GamePhase.Report;
            _bus.PhaseChanged(_s);
            _bus.DayReportReady();
        }

        /// Награда LOB за успешный день + лечение паники. Возвращает начисленный LOB.
        public int ApplyReportRewards()
        {
            int reward = Balance.DayRewardBase + _s.Energy / 100;
            bool calm = !_s.Today.HadDeath && !_s.Today.HadPanic;
            if (calm) reward += Balance.DayRewardCalmBonus;
            // Лечение паники: 1 LOB за пешку, иначе пешка теряется
            int panicked = 0;
            foreach (var e in _s.Employees) if (e.State == EmployeeState.Panicked) panicked++;
            int budget = _s.Lob + reward;
            int healed = panicked <= budget ? panicked : budget;
            if (healed < 0) healed = 0;
            int processed = 0;
            foreach (var e in _s.Employees)
            {
                if (e.State != EmployeeState.Panicked) continue;
                processed++;
                if (processed <= healed) e.State = EmployeeState.Idle;
                else { e.State = EmployeeState.Dead; _sink.Log($"{e.Name} не вылечена после паники — потеряна."); }
            }
            _s.Lob += reward - healed;
            _sink.Log($"Награда дня: +{reward} LOB{(healed > 0 ? $", лечение паники -{healed} LOB" : "")}.");
            PrepareOffers();
            return reward;
        }

        private void PrepareOffers()
        {
            _s.OfferedIds.Clear();
            var pool = _s.AvailablePool;
            // перемешивание детерминированное (Fisher-Yates через seeded rng)
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = _s.Rng.NextInt(0, i);
                var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
            }
            for (int i = 0; i < 3 && i < pool.Count; i++) _s.OfferedIds.Add(pool[i]);
        }

        public void EnterSelection()
        {
            if (_s.Phase != GamePhase.Report) return;
            _s.Phase = GamePhase.Selection;
            _bus.PhaseChanged(_s);
        }

        public void PickAnomaly(string defId)
        {
            if (_s.Phase != GamePhase.Selection) return;
            if (!_s.OfferedIds.Contains(defId)) return;
            _s.AvailablePool.Remove(defId);
            SpawnAnomaly(defId);
            NextDay();
        }

        public void SkipSelection()
        {
            if (_s.Phase != GamePhase.Selection) return;
            if (_s.OfferedIds.Count == 0)
            {
                _s.Lob += Balance.SkipSelectionLob;
                _sink.Log($"Пул аномалий исчерпан: пропуск (+{Balance.SkipSelectionLob} LOB).");
            }
            NextDay();
        }

        private void SpawnAnomaly(string defId)
        {
            var def = AnomalyCatalog.Find(defId);
            // первая свободная камера
            ContainmentUnit free = null;
            foreach (var u in _s.Units) if (u.OccupantId < 0) { free = u; break; }
            if (free == null) return; // нет места — аномалия не добавляется (малый scope)
            var a = new AbnormalityInstance
            {
                DefId = defId,
                UnitId = free.Id,
                Counter = def.CounterMax,
                CounterMax = def.CounterMax,
                InfoLevel = 0,
                RoomId = free.RoomId,
            };
            _s.Anomalies.Add(a);
            a.UnitId = free.Id;
            free.OccupantId = _s.Anomalies.Count - 1;
            _sink.Log($"Новая аномалия в камере #{free.Id}: «{def.Name}».");
        }

        private void NextDay()
        {
            _s.Day++;
            _s.Phase = GamePhase.Preparation;
            _sink.Log($"Подготовка к дню {_s.Day}.");
            _bus.PhaseChanged(_s);
        }

        /// Провал дня: возврат к снапшоту начала дня.
        public void RetryDay()
        {
            if (DayStartSnapshot != null)
            {
                DayStartSnapshot.RestoreInto(_s);
                _sink.Log("День повторён: состояние восстановлено из снапшота.");
                _bus.PhaseChanged(_s);
            }
        }

        /// После Report при провале — без наград, сразу откат и подготовка того же дня.
        public void FailDayRollback()
        {
            RetryDay();
        }

        public bool CheckGameOver()
        {
            bool anyAlive = false;
            foreach (var e in _s.Employees) if (e.State != EmployeeState.Dead) { anyAlive = true; break; }
            if (!anyAlive && _s.Lob < Balance.HireCostLob)
            {
                _s.Phase = GamePhase.GameOver;
                _bus.PhaseChanged(_s);
                _bus.GameOver();
                return true;
            }
            return false;
        }
    }
}
