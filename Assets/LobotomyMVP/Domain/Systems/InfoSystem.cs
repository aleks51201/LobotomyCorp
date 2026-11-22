using LobotomyMVP.Data;
using LobotomyMVP.Domain.Model;

namespace LobotomyMVP.Domain.Systems
{
    /// Уровни информации об аномалии: L1 риск/тип урона/время, L2 таблица favor, L3 правила+побег.
    public sealed class InfoSystem
    {
        private readonly GameState _s;
        private readonly EventBus _bus;
        private readonly EffectSink _sink;

        public InfoSystem(GameState s, EventBus bus, EffectSink sink)
        {
            _s = s; _bus = bus; _sink = sink;
        }

        public static int CostForLevel(int targetLevel) => Balance.InfoCostPe[targetLevel - 1];

        public bool Unlock(int anomalyIndex, int targetLevel)
        {
            if (anomalyIndex < 0 || anomalyIndex >= _s.Anomalies.Count) return false;
            var a = _s.Anomalies[anomalyIndex];
            if (targetLevel != a.InfoLevel + 1 || targetLevel > 3) return false;
            int cost = CostForLevel(targetLevel);
            if (_s.Pe < cost) return false;
            _s.Pe -= cost;
            a.InfoLevel = targetLevel;
            var def = AnomalyCatalog.Find(a.DefId);
            _sink.Log($"Информация «{def.Name}»: открыт уровень {targetLevel} (-{cost} PE).");
            _bus.StateChanged();
            return true;
        }

        public static string RiskText(AnomalyDef def)
        {
            // множитель риска ZAYIN x1, TETH x1.5, HE x2 — отображается как "класс"
            switch (def.Risk)
            {
                case RiskLevel.ZAYIN: return "ZAYIN (риск ×1)";
                case RiskLevel.TETH: return "TETH (риск ×1.5)";
                default: return "HE (риск ×2)";
            }
        }

        public static string DamageName(DamageType t)
        {
            switch (t)
            {
                case DamageType.Red: return "Красный";
                case DamageType.White: return "Белый";
                case DamageType.Black: return "Чёрный";
                default: return "Бледный";
            }
        }

        public static string BandName(FavorBand b)
        {
            switch (b)
            {
                case FavorBand.VeryHigh: return "Очень высокая";
                case FavorBand.High: return "Высокая";
                case FavorBand.Common: return "Обычная";
                case FavorBand.Low: return "Низкая";
                default: return "Очень низкая";
            }
        }
    }
}
