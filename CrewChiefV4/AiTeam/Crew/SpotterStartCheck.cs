using System;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Warum spottet Sara nicht? Seit dem 18.11. kamen in RaceRoom, AMS2 und LMU keine Spottermeldungen mehr, ohne dass das
    /// Protokoll einen Grund nannte. Crew Chief startet den Spotter nur, wenn alle Bedingungen erfüllt sind; diese Prüfung
    /// benennt die erste, die fehlt, und schreibt sie bei jeder Änderung ins Protokoll (Konsole und Diagnose).
    /// Der Spotter selbst bleibt der von Crew Chief; hier wird nur beobachtet und erklärt.
    /// </summary>
    internal static class SpotterStartCheck
    {
        private static string last;

        internal static string Reason(bool hasSpotter, bool running, bool enabled, bool enabledInSettings, bool realistic, bool oval,
            bool crewAndSaraOn, bool exempt)
        {
            if (!hasSpotter) return "no_spotter_for_this_game";
            if (running) return enabled ? "running" : "stopping";
            if (!enabledInSettings) return "spotter_switched_off_in_settings";
            if (!enabled && realistic && !oval) return "realistic_mode_on_road_course";
            if (!enabled) return "spotter_off_for_this_session";
            if (!crewAndSaraOn && !exempt) return "crew_or_sara_switched_off";
            return "ready_to_start";
        }

        /// <summary>Gibt den Grund zurück, wenn er sich geändert hat, sonst null.</summary>
        internal static string Changed(string reason)
        {
            if (reason == last) return null;
            last = reason;
            return reason;
        }

        internal static void Reset() { last = null; }

        internal static void Report(bool hasSpotter, bool running, bool enabled, bool enabledInSettings, bool realistic, bool oval,
            bool crewAndSaraOn, bool exempt)
        {
            string reason = Changed(Reason(hasSpotter, running, enabled, enabledInSettings, realistic, oval, crewAndSaraOn, exempt));
            if (reason == null) return;
            string state = "spotter=" + reason + " enabled=" + enabled + " settings=" + enabledInSettings + " realistic=" + realistic +
                " oval=" + oval + " sara=" + crewAndSaraOn;
            try { Console.WriteLine("RRCA Spotter: " + state); } catch (Exception) { }
            try { LiveDiagnostics.Decision("spotter", "state", reason, state); } catch (Exception) { }
        }
    }
}
