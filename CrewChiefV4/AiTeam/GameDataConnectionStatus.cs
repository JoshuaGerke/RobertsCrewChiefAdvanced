using System;
using System.Threading;

namespace CrewChiefV4.AiTeam
{
    /// <summary>A successful reader call proves an accessible source, not valid or progressing driving telemetry.</summary>
    public static class GameDataConnectionStatus
    {
        private sealed class Read { public string Game; public DateTime Utc; }
        private static Read latest;

        public static void Record(string game, DateTime utc)
        { Volatile.Write(ref latest, new Read { Game=game, Utc=utc }); }

        public static string SourceAt(DateTime utc)
        {
            var read = Volatile.Read(ref latest);
            return read != null && utc >= read.Utc && utc-read.Utc <= TimeSpan.FromSeconds(3) ? read.Game : null;
        }
    }
}
