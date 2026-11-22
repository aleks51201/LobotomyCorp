using System.IO;
using LobotomyMVP.Flow;

namespace LobotomyMVP.App
{
    /// Сейв = сериализация снапшота начала дня + meta (seed, LOB/PE, день). Побитово воспроизводим.
    public static class SaveService
    {
        [System.Serializable]
        private sealed class SaveData
        {
            public DaySnapshot Snapshot;
            public int Seed;
            public string Path;
        }

        public static string SaveFilePath =>
            Path.Combine(UnityEngine.Application.persistentDataPath, "lobotomy_mvp_save.json");

        public static void Save(GameSession session)
        {
            var snap = session.Cycle.DayStartSnapshot;
            if (snap == null) return; // день ещё не начинался — сейвить нечего
            var data = new SaveData { Snapshot = snap, Seed = session.State.RngSeed };
            File.WriteAllText(SaveFilePath, UnityEngine.JsonUtility.ToJson(data));
        }

        public static bool HasSave() => File.Exists(SaveFilePath);

        public static bool Load(GameSession session)
        {
            if (!File.Exists(SaveFilePath)) return false;
            var data = UnityEngine.JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFilePath));
            if (data == null || data.Snapshot == null) return false;
            data.Snapshot.RestoreInto(session.State);
            session.SpeedIndex = 1;
            session.Bus.PhaseChanged(session.State);
            return true;
        }
    }
}
