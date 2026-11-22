namespace LobotomyMVP.Data
{
    /// Таблица базовой вероятности успеха по уровню профильного стата (1..5).
    public enum FavorBand { VeryHigh, High, Common, Low, VeryLow }

    public static class FavorTables
    {
        // Индекс = уровень стата - 1.
        public static readonly float[][] Table =
        {
            new float[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f }, // VeryHigh
            new float[] { 0.60f, 0.68f, 0.75f, 0.82f, 0.90f }, // High
            new float[] { 0.45f, 0.55f, 0.63f, 0.72f, 0.80f }, // Common
            new float[] { 0.30f, 0.40f, 0.50f, 0.60f, 0.70f }, // Low
            new float[] { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f }, // VeryLow
        };

        public static float Base(FavorBand band, int statLevel)
        {
            int i = statLevel - 1;
            if (i < 0) i = 0;
            if (i > 4) i = 4;
            return Table[(int)band][i];
        }
    }
}
