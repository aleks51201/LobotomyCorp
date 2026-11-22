using UnityEngine;

namespace LobotomyMVP.Presentation
{
    public static class UiTheme
    {
        public static readonly Color Bg = new Color(0.08f, 0.09f, 0.11f);
        public static readonly Color Panel = new Color(0.14f, 0.15f, 0.18f);
        public static readonly Color Panel2 = new Color(0.19f, 0.20f, 0.24f);
        public static readonly Color Accent = new Color(0.85f, 0.35f, 0.15f);
        public static readonly Color Text = new Color(0.92f, 0.92f, 0.90f);
        public static readonly Color DimText = new Color(0.6f, 0.6f, 0.62f);
        public static readonly Color HpBar = new Color(0.75f, 0.2f, 0.2f);
        public static readonly Color SpBar = new Color(0.25f, 0.55f, 0.85f);
        public static readonly Color EnergyBar = new Color(0.95f, 0.8f, 0.2f);
        public static readonly Color MeltdownBar = new Color(0.9f, 0.45f, 0.1f);
        public static readonly Color Selected = new Color(1f, 0.85f, 0.2f);
        public static readonly Color Danger = new Color(0.9f, 0.25f, 0.25f);
        public static readonly Color RoomColor = new Color(0.22f, 0.24f, 0.28f);
        public static readonly Color RoomBreached = new Color(0.45f, 0.15f, 0.15f);
        public static readonly Color PawnColor = new Color(0.45f, 0.75f, 0.55f);
        public static readonly Color AnomalyColor = new Color(0.6f, 0.4f, 0.8f);

        public static Color RiskColor(Data.RiskLevel r)
        {
            switch (r)
            {
                case Data.RiskLevel.ZAYIN: return new Color(0.35f, 0.7f, 0.4f);
                case Data.RiskLevel.TETH: return new Color(0.85f, 0.75f, 0.25f);
                default: return new Color(0.85f, 0.35f, 0.3f);
            }
        }
    }
}
