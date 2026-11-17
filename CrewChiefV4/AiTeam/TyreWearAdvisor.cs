using System;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Reifenverschleiß wie ein Renningenieur (Rob 16.11.2026): abwechselnd technisch („hinten rechts 8,8 Prozent“) und menschlich
    /// („Rob, ich hab mir deine Reifen angeschaut: vorne etwa 5, hinten etwa 7 Prozent“), dazu eine fundierte Hochrechnung,
    /// wann ein Reifen bei gleichem Tempo am Ende ist.
    /// Fundiert heißt: Die Hochrechnung gibt es nur für Simulationen, in denen Crew Chief den Verschleiß auf die Grenze
    /// „abgefahren“ der jeweiligen Simulation normiert (100 % = abgefahren: AMS2, Project CARS 2/3, RaceRoom, Assetto Corsa),
    /// und nur mit einer gemessenen Verschleißrate aus mindestens zwei sauberen Runden. Andere Simulationen bekommen nur die Werte.
    /// </summary>
    internal static class TyreWearAdvisor
    {
        /// <summary>Ab hier gilt ein Reifen als „am Ende“: 90 % der normierten Grenze, damit die Warnung vor dem Abfall kommt.</summary>
        internal const float EndOfLife = 90f;

        internal static bool Calibrated(GameType type) =>
            type == GameType.AMS2 || type == GameType.PCARS2 || type == GameType.PCARS3 || type == GameType.RACE_ROOM || type == GameType.ASSETTO;

        private static readonly string[] De = { "vorne links", "vorne rechts", "hinten links", "hinten rechts" };
        private static readonly string[] En = { "front left", "front right", "rear left", "rear right" };
        private static readonly string[] Es = { "delantero izquierdo", "delantero derecho", "trasero izquierdo", "trasero derecho" };
        private static string Corner(int i) => RadioLanguage.Pick(De, En, Es)[i];
        private static string Pct(float v) => RadioLanguage.Num(v, v < 10 ? "0.0" : "0");

        /// <summary>Runden, bis die schlimmste Ecke bei gleicher Rate „am Ende“ ist; -1 ohne belastbare Rate oder Kalibrierung.</summary>
        internal static int LapsToEnd(float[] wear, float[] perLap, bool calibrated, out int corner)
        {
            corner = -1;
            if (!calibrated || wear == null || perLap == null || wear.Length != 4 || perLap.Length != 4) return -1;
            int best = -1; double laps = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (!(perLap[i] > .05f) || wear[i] < 0 || wear[i] >= EndOfLife) continue;
                double l = (EndOfLife - wear[i]) / perLap[i];
                if (l < laps) { laps = l; best = i; }
            }
            if (best < 0) return -1;
            corner = best;
            return (int)Math.Floor(laps);
        }

        /// <summary>Routinebericht zum Verschleiß, drei Arten im Wechsel; mit Hochrechnung, wenn sie fundiert ist und bis 15 Runden reicht.</summary>
        internal static string Report(float[] wear, float[] perLap, bool calibrated, string name, int variant) =>
            Report(wear, perLap, null, calibrated, name, variant);

        /// <summary>Wie oben; mit vorsichtiger Rate (oberes Quartil) nennt die Hochrechnung auch die sichere Untergrenze.</summary>
        internal static string Report(float[] wear, float[] perLap, float[] cautious, bool calibrated, string name, int variant)
        {
            if (wear == null || wear.Length != 4 || !wear.All(w => w >= 0 && w <= 100)) return null;
            int worst = Array.IndexOf(wear, wear.Max());
            if (wear[worst] < 1) return null;
            string n = string.IsNullOrWhiteSpace(name) ? "" : name;
            float front = (wear[0] + wear[1]) / 2, rear = (wear[2] + wear[3]) / 2;
            string text;
            switch (((variant % 3) + 3) % 3)
            {
                case 0:
                    text = RadioLanguage.T("Am stärksten abgenutzt ist der Reifen " + Corner(worst) + ": " + Pct(wear[worst]) + " Prozent Verschleiß.",
                        "Most worn tyre is " + Corner(worst) + ": " + Pct(wear[worst]) + " percent wear.",
                        "El neumático más gastado es el " + Corner(worst) + ": " + Pct(wear[worst]) + " por ciento de desgaste.");
                    break;
                case 1:
                    text = RadioLanguage.T((n.Length > 0 ? n + ", ich" : "Ich") + " hab mir deine Reifen angeschaut: vorne etwa " + Pct(front) + ", hinten etwa " + Pct(rear) + " Prozent Verschleiß.",
                        (n.Length > 0 ? n + ", I" : "I") + " had a look at your tyres: about " + Pct(front) + " percent wear at the front, about " + Pct(rear) + " at the rear.",
                        (n.Length > 0 ? n + ", he" : "He") + " mirado tus neumáticos: delante un " + Pct(front) + ", detrás un " + Pct(rear) + " por ciento de desgaste.");
                    break;
                default:
                    text = Math.Abs(front - rear) < 1.5f
                        ? RadioLanguage.T("Die Reifen bauen gleichmäßig ab, rundum etwa " + Pct((front + rear) / 2) + " Prozent.",
                            "The tyres are wearing evenly, about " + Pct((front + rear) / 2) + " percent all round.",
                            "Los neumáticos se desgastan por igual, alrededor de un " + Pct((front + rear) / 2) + " por ciento.")
                        : RadioLanguage.T((rear > front ? "Die Hinterachse" : "Die Vorderachse") + " arbeitet mehr: " + Corner(worst) + " liegt bei " + Pct(wear[worst]) + " Prozent.",
                            (rear > front ? "The rear axle" : "The front axle") + " is working harder: " + Corner(worst) + " is at " + Pct(wear[worst]) + " percent.",
                            (rear > front ? "El eje trasero" : "El eje delantero") + " trabaja más: el " + Corner(worst) + " está al " + Pct(wear[worst]) + " por ciento.");
                    break;
            }
            int laps = LapsToEnd(wear, perLap, calibrated, out int c);
            if (laps >= 1 && laps <= 15)
            {
                text += " " + Projection(laps, c, n, variant);
                int safe = LapsToEnd(wear, cautious, calibrated, out _);
                if (safe >= 1 && safe < laps) text += " " + SafeFloor(safe);
            }
            return text;
        }

        /// <summary>Untergrenze aus der vorsichtigen Rate – Ben nennt eine Spanne statt einer falschen Punktzahl.</summary>
        internal static string SafeFloor(int safe)
        {
            return RadioLanguage.T("Sicher sind auf jeden Fall noch " + safe + (safe == 1 ? " Runde." : " Runden."),
                "You've definitely got at least " + safe + (safe == 1 ? " lap." : " laps."),
                "Seguro que aguanta al menos " + safe + (safe == 1 ? " vuelta." : " vueltas."));
        }

        internal static string Projection(int laps, int corner, string name, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? "" : name;
            return ((variant % 2) + 2) % 2 == 0
                ? RadioLanguage.T("Pass auf" + (n.Length > 0 ? ", " + n : "") + ": Wenn du so weiter pushst, ist " + Corner(corner) + " in etwa " + laps + (laps == 1 ? " Runde" : " Runden") + " am Ende.",
                    "Careful" + (n.Length > 0 ? ", " + n : "") + ": if you keep pushing like this, the " + Corner(corner) + " is finished in about " + laps + (laps == 1 ? " lap." : " laps."),
                    "Ojo" + (n.Length > 0 ? ", " + n : "") + ": si sigues apretando así, el " + Corner(corner) + " estará acabado en unas " + laps + (laps == 1 ? " vuelta." : " vueltas."))
                : RadioLanguage.T("Bei diesem Tempo hält " + Corner(corner) + " noch rund " + laps + (laps == 1 ? " Runde." : " Runden."),
                    "At this pace the " + Corner(corner) + " lasts about " + laps + (laps == 1 ? " more lap." : " more laps."),
                    "A este ritmo el " + Corner(corner) + " aguanta unas " + laps + (laps == 1 ? " vuelta más." : " vueltas más."));
        }
    }
}

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Proaktive Reifenwarnung mit Stopp-Empfehlung (Rob 16.11.2026: „bei deutlichem Verschleiß eine klare Empfehlung, ob ein Stopp
    /// nötig ist“). Nach jeder sauberen Runde: Ist eine Ecke bei gleichem Tempo in höchstens fünf Runden am Ende, warnt Mik; im
    /// Rennen mit bekannter Restdistanz sagt er, ob es bis ins Ziel reicht oder ein Wechsel nötig wird. Höchstens alle drei Runden.
    /// </summary>
    internal sealed class TyreWearWatch
    {
        private int session = int.MinValue, lastWarnLap = -100, variant;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; lastWarnLap = -100; call = null; }

        internal static string Advice(int lapsToEnd, int lapsLeft, bool race)
        {
            if (!race || lapsLeft <= 0) return "";
            return lapsLeft <= lapsToEnd
                ? RadioLanguage.T(" Bis ins Ziel sind es noch " + lapsLeft + (lapsLeft == 1 ? " Runde" : " Runden") + ", das reicht ohne Stopp, wenn du sie etwas schonst.",
                    " There " + (lapsLeft == 1 ? "is 1 lap" : "are " + lapsLeft + " laps") + " to go, that works without a stop if you look after them a bit.",
                    " Quedan " + lapsLeft + (lapsLeft == 1 ? " vuelta" : " vueltas") + ", llegas sin parar si los cuidas un poco.")
                : RadioLanguage.T(" Bis ins Ziel sind es noch " + lapsLeft + " Runden. So wird ein Reifenwechsel nötig, oder du nimmst deutlich Tempo raus.",
                    " There are " + lapsLeft + " laps to go. Like this you'll need a tyre change, or you'll have to back off noticeably.",
                    " Quedan " + lapsLeft + " vueltas. Así hará falta cambiar neumáticos, o bajar bastante el ritmo.");
        }

        internal void Observe(GameStateData gs, LapRecord lap, AiRaceMemory memory, string name, DateTime now, float[] storedRate = null)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (lap == null || memory == null || !lap.Valid || lap.InPitLap || s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane) return;
            if (s.CompletedLaps - lastWarnLap < 3) return;
            var t = gs.TyreData;
            if (!t.TyreWearActive || !DataTruth.Current.MaySpeakAsCurrent(SimChannel.TyreWear)) return;
            float[] wear = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
            // Eigene Messung dieser Fahrt zuerst; sonst die gespeicherte Rate dieses Autos auf dieser Strecke mit derselben Mischung.
            // Robuster Median statt Mittelwert: eine Verkehrs- oder Fehlerrunde löst keine falsche Warnung aus.
            float[] rate = memory.WearRates(5, out var median, out _, out int samples) && samples >= 2 ? median : memory.AverageWearPerLap(3);
            int laps = TyreWearAdvisor.LapsToEnd(wear, rate ?? storedRate, TyreWearAdvisor.Calibrated(Game.gameType), out int corner);
            if (laps < 1 || laps > 5) return;
            bool race = s.SessionType == SessionType.Race;
            int left = race ? LapWatch.LapsLeft(s) : -1;
            if (race && left > 0 && left <= 1) return; // letzte Runde: nichts mehr zu ändern
            lastWarnLap = s.CompletedLaps;
            string text = TyreWearAdvisor.Projection(laps, corner, name, variant++) + Advice(laps, left, race);
            call = new ObservedRaceCall
            {
                Topic = "tyre-wear-warning", Text = text, Evidence = "measured_wear_rate_vs_sim_worn_out_level", Member = TeamMember.Mik, Priority = 5,
                Position = -1, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(30),
                AdditionalValidity = live => !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool mikEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !mikEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
