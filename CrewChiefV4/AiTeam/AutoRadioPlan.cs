using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Wie viel die Crew von sich aus funkt. Cockpit-Regler und Funkbefehle („Ruhe bitte“, „mehr Infos“, „viel mehr Infos“).</summary>
    public enum RadioAmount { Quiet, Normal, More, MuchMore }

    /// <summary>Wie lang das Rennen ist; danach richtet sich, wie oft ein Bericht kommt.</summary>
    internal enum RaceSpan { Practice, Sprint, Short, Medium, Long }

    /// <summary>
    /// Rob 23.11.: Keine Sekunden-Takte mehr im Cockpit. Die Crew bestimmt selbst, wie oft sie berichtet:
    /// <list type="number">
    /// <item><b>Renndauer</b>: Bis 15 Minuten kommt jedes Technikthema nur ein- bis zweimal, in langen Rennen öfter.</item>
    /// <item><b>Umfeld zuerst</b>: Vorder- und Hintermann kommen in jeder Renndauer am häufigsten (Rob: „das Spannendste und
    /// Hilfreichste“). Im Zweikampf halbiert der Funkplan diesen Abstand zusätzlich.</item>
    /// <item><b>Spannung</b>: Aus der Anspannung des Rennkopfs (0–1). Ruhig: Technik, Reifen, Schaden und Wetter kommen etwas
    /// öfter. Spannend: Technik wartet, damit Platz für Zweikampf und Taktik bleibt.</item>
    /// <item><b>Funkmenge</b>: Ruhig hält die Routine an, Mehr und Viel mehr verkürzen alle Abstände.</item>
    /// </list>
    /// Sicherheit, Sara, Crew-Chief-Meldungen und Antworten laufen nicht über diesen Plan und bleiben unberührt.
    /// </summary>
    internal static class AutoRadioPlan
    {
        internal static readonly string[] Topics = { "status", "weather", "fuel", "tyres", "pace", "ahead", "behind", "stint", "vehicle", "progress" };

        // Grundabstand in Sekunden je Renndauer, Reihenfolge wie Topics.
        private static readonly int[] PracticeSeconds = { 300, 600, 300, 300, 180, 240, 240, 420, 600, 360 };
        private static readonly int[] SprintSeconds   = { 300, 900, 480, 480, 420,  90,  90, 900, 600, 360 };
        private static readonly int[] ShortSeconds    = { 360, 720, 480, 480, 420, 100, 100, 720, 600, 420 };
        private static readonly int[] MediumSeconds   = { 360, 720, 420, 420, 420, 110, 110, 600, 600, 600 };
        private static readonly int[] LongSeconds     = { 300, 600, 360, 360, 420, 120, 120, 480, 540, 900 };

        /// <summary>Kürzester Abstand, den ein Bericht je bekommen kann.</summary>
        internal const int MinimumSeconds = 30;
        /// <summary>Unter dieser Anspannung gilt das Rennen als ruhig, ab der oberen als spannend.</summary>
        internal const double CalmBelow = .3, TenseFrom = .6;

        /// <summary>Aktuelle Funkmenge für alle, die keinen Funkplan in der Hand haben (Funklast).</summary>
        internal static RadioAmount CurrentAmount { get => (RadioAmount)System.Threading.Volatile.Read(ref current); set => System.Threading.Volatile.Write(ref current, (int)value); }
        private static int current = (int)RadioAmount.Normal;

        internal static RaceSpan Span(double raceMinutes) =>
            raceMinutes <= 0 ? RaceSpan.Short : raceMinutes <= 15 ? RaceSpan.Sprint : raceMinutes <= 30 ? RaceSpan.Short :
            raceMinutes <= 60 ? RaceSpan.Medium : RaceSpan.Long;

        /// <summary>Renndauer in Minuten: feste Zeit direkt, sonst Runden mal Rundenzeit; unbekannt ist -1.</summary>
        internal static double RaceMinutes(GameStateData gs)
        {
            if (gs == null) return -1;
            var s = gs.SessionData;
            if (s.SessionHasFixedTime && s.SessionTotalRunTime > 0) return s.SessionTotalRunTime / 60.0;
            float lap = s.PlayerLapTimeSessionBest > 20 ? s.PlayerLapTimeSessionBest : s.LapTimePrevious;
            if (s.SessionNumberOfLaps > 0 && lap > 20 && lap < 3600) return s.SessionNumberOfLaps * lap / 60.0;
            // Ohne Rundenzeit: grob 2 Minuten je Runde.
            if (s.SessionNumberOfLaps > 0) return s.SessionNumberOfLaps * 2.0;
            return -1;
        }

        internal static RaceSpan Span(GameStateData gs) =>
            gs == null || gs.SessionData.SessionType != SessionType.Race ? RaceSpan.Practice : Span(RaceMinutes(gs));

        internal static int BaseSeconds(RaceSpan span, string topic)
        {
            int i = Array.IndexOf(Topics, topic);
            if (i < 0) return 0;
            switch (span)
            {
                case RaceSpan.Sprint: return SprintSeconds[i];
                case RaceSpan.Short: return ShortSeconds[i];
                case RaceSpan.Medium: return MediumSeconds[i];
                case RaceSpan.Long: return LongSeconds[i];
                default: return PracticeSeconds[i];
            }
        }

        /// <summary>
        /// Faktor auf den Abstand: kleiner heißt öfter. Bei Ruhig spricht die Routine ohnehin nicht (der Dienst hält sie an); der
        /// Plan läuft mit langen Abständen weiter, damit nach „normal“ nicht alles auf einmal fällig ist.
        /// </summary>
        internal static double AmountFactor(RadioAmount amount)
        {
            switch (amount)
            {
                case RadioAmount.Quiet: return 1.6;
                case RadioAmount.More: return .75;
                case RadioAmount.MuchMore: return .55;
                default: return 1;
            }
        }

        /// <summary>
        /// Spannung: In ruhigen Phasen kommen Technik, Reifen, Schaden und Wetter etwas öfter, in spannenden seltener. Vorder- und
        /// Hintermann bleiben hier unverändert, sie werden im Zweikampf vom Funkplan ohnehin halbiert.
        /// </summary>
        internal static double TensionFactor(string topic, double tension)
        {
            if (!AiRadioSchedule.IsTechnical(topic) && topic != "pace") return 1;
            if (tension < CalmBelow) return topic == "pace" ? .9 : .8;
            if (tension >= TenseFrom) return 1.5;
            return 1;
        }

        internal static string Level(double tension) => tension < CalmBelow ? "calm" : tension >= TenseFrom ? "tense" : "medium";

        /// <summary>Abstand bis zum nächsten Bericht dieses Themas, mit ±8 % Streuung, damit nichts mechanisch klingt.</summary>
        internal static int Seconds(RaceSpan span, string topic, RadioAmount amount, double tension, Random random)
        {
            double factor = AmountFactor(amount);
            int baseSeconds = BaseSeconds(span, topic);
            if (factor <= 0 || baseSeconds <= 0) return 0;
            double jitter = random == null ? 1 : 1 + (random.NextDouble() * .16 - .08);
            return (int)Math.Max(MinimumSeconds, Math.Min(3600, Math.Round(baseSeconds * factor * TensionFactor(topic, tension) * jitter)));
        }

        /// <summary>Abstand ohne Streuung; für Dringlichkeit, Technikpause und Bilanz.</summary>
        internal static int NominalSeconds(RaceSpan span, string topic, RadioAmount amount) => Seconds(span, topic, amount, .45, null);

        internal static RadioAmount FromMode(AiRadioMode mode)
        {
            switch (mode)
            {
                case AiRadioMode.MINIMAL: return RadioAmount.Quiet;
                case AiRadioMode.ACTIVE: return RadioAmount.More;
                case AiRadioMode.VERBOSE: return RadioAmount.MuchMore;
                default: return RadioAmount.Normal;
            }
        }

        internal static AiRadioMode ToMode(RadioAmount amount)
        {
            switch (amount)
            {
                case RadioAmount.Quiet: return AiRadioMode.MINIMAL;
                case RadioAmount.More: return AiRadioMode.ACTIVE;
                case RadioAmount.MuchMore: return AiRadioMode.VERBOSE;
                default: return AiRadioMode.NORMAL;
            }
        }
    }
}
