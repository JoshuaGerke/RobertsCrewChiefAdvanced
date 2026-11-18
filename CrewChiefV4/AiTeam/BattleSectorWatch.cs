using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Angriffs- und Verteidigungspunkt im Zweikampf (Rob 18.11.2026: „das Renngeschehen kommentieren, mich pushen“).
    /// Wie ein Renningenieur vergleicht Ben die Sektorzeiten der letzten drei sauberen Runden mit dem direkten Gegner:
    /// Ist Rob gegen den Vordermann (bis 2,5 s) in einem Sektor klar schneller, sagt er, wo der Angriff liegt. Ist der
    /// Hintermann (bis 1,5 s) in einem Sektor klar schneller, sagt er, wo verteidigt werden muss. Nur gemessene Sektorzeiten
    /// beider Fahrer, mindestens 0,15 s Unterschied im Schnitt und in mindestens zwei von drei Runden in dieselbe Richtung.
    /// Je Gegner höchstens alle vier Runden, nicht in der letzten Runde.
    /// </summary>
    internal sealed class BattleSectorWatch
    {
        internal const float MinDelta = .15f, FrontWindow = 2.5f, RearWindow = 1.5f;
        private int session = int.MinValue, lastLap = -1, lastCallLap = -100, variant;
        private readonly Dictionary<string, int> saidLap = new Dictionary<string, int>();
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; lastLap = -1; lastCallLap = -100; saidLap.Clear(); call = null; }

        /// <summary>Sektor (0–2) mit dem größten Vorteil des Fahrers (ahead) bzw. des Gegners (!ahead) und dessen Betrag; -1 ohne klares Bild.</summary>
        internal static int DecisiveSector(IList<float[]> own, IList<float[]> rival, bool attack, out float delta)
        {
            delta = 0;
            if (own == null || rival == null || own.Count < 3 || rival.Count < 3) return -1;
            int best = -1;
            for (int k = 0; k < 3; k++)
            {
                double rivalAvg = rival.Average(l => l[k]);
                double d = own.Average(l => l[k]) - rivalAvg;        // negativ: Rob schneller
                double edge = attack ? -d : d;                         // Vorteil der Seite, die zählt
                int agree = own.Count(l => attack ? l[k] < rivalAvg : l[k] > rivalAvg);
                if (edge >= MinDelta && agree >= 2 && edge > delta) { delta = (float)edge; best = k; }
            }
            return best;
        }

        internal void Observe(GameStateData gs, RaceMind mind, AiRaceMemory memory, bool names, DateTime now)
        {
            if (gs == null || mind == null || memory == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (s.SessionPhase != SessionPhase.Green || mind.SampledLap == lastLap || mind.SampledLap < 3) return;
            lastLap = mind.SampledLap;
            if (call != null || s.CompletedLaps - lastCallLap < 2 || LapWatch.LapsLeft(s) == 1 || gs.PitData.InPitlane) return;
            var own = memory.GetLaps().Where(l => l.Valid && !l.InPitLap && l.SectorTimes != null && l.SectorTimes.Length == 3 &&
                l.SectorTimes.All(v => v > 1 && v < 3600)).Reverse().Take(3).Select(l => l.SectorTimes).ToList();
            if (own.Count < 3) return;
            call = Decide(gs, mind.Front, true, own, names, now) ?? Decide(gs, mind.Rear, false, own, names, now);
            if (call != null) { lastCallLap = s.CompletedLaps; LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, call.Text, call.EventId); }
        }

        private ObservedRaceCall Decide(GameStateData gs, GapHistory history, bool ahead, List<float[]> own, bool names, DateTime now)
        {
            var s = gs.SessionData;
            if (history?.Id == null || history.Gaps.Count == 0) return null;
            if (ahead && s.ClassPosition <= 1) return null;
            var op = RaceDataTools.Neighbour(gs, ahead);
            if (op == null || op.InPits || OpponentStopLog.Key(op) != history.Id) return null;
            float gap = history.Gaps[history.Gaps.Count - 1];
            if (!(gap > 0) || gap > (ahead ? FrontWindow : RearWindow)) return null;
            if (saidLap.TryGetValue(history.Id, out int said) && s.CompletedLaps - said < 4) return null;
            var rival = (op.OpponentLapData ?? new List<LapData>()).Where(l => l.IsValid && !l.InLap && !l.OutLap && !l.hasMissingSectors &&
                l.SectorTimes != null && l.SectorTimes.Length == 3 && l.SectorTimes.All(v => v > 1 && v < 3600)).Reverse().Take(3).Select(l => l.SectorTimes).ToList();
            int sector = DecisiveSector(own, rival, ahead, out float delta);
            if (sector < 0) return null;
            saidLap[history.Id] = s.CompletedLaps;
            string name = names && op.CanUseName ? RadioPhraseCatalog.SafeRivalName(op.DriverRawName) : "";
            string text = ahead ? AttackText(name, sector + 1, delta, variant++) : DefendText(name, sector + 1, delta, variant++);
            string id = history.Id;
            return new ObservedRaceCall
            {
                Topic = "battle-sector", Text = text, Member = TeamMember.Ben, Priority = 3, Position = s.ClassPosition,
                SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(15),
                FieldNamesAllowed = name.Length > 0,
                Evidence = (ahead ? "attack" : "defend") + "_sector_" + (sector + 1) + "_avg_three_clean_laps_both_drivers_delta_" + delta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                AdditionalValidity = live =>
                {
                    var next = RaceDataTools.Neighbour(live, ahead);
                    return next != null && !next.InPits && OpponentStopLog.Key(next) == id && !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
                }
            };
        }

        internal static string AttackText(string name, int sector, float delta, int variant)
        {
            string r = GapTrendWatch.Rate(delta), n = sector.ToString(RadioLanguage.Culture);
            string who = name.Length > 0 ? name : RadioLanguage.T("dem Vordermann", "the car ahead", "el de delante");
            string Who = name.Length > 0 ? name : RadioLanguage.T("Der Vordermann", "The car ahead", "El de delante");
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T("In Sektor " + n + " bist du " + r + " schneller als " + (name.Length > 0 ? name : "der Vordermann") + ". Da kannst du angreifen.",
                    "In sector " + n + " you're " + r + " quicker than " + who + ". That's where you can attack.", "En el sector " + n + " eres " + r + " más rápido que " + who + ". Ahí puedes atacar.");
                case 1: return RadioLanguage.T(Who + " verliert " + r + " auf dich in Sektor " + n + ". Dort Druck machen.",
                    Who + " loses " + r + " to you in sector " + n + ". Put the pressure on there.", Who + " pierde " + r + " contigo en el sector " + n + ". Aprieta ahí.");
                case 2: return RadioLanguage.T("Dein Angriffspunkt ist Sektor " + n + ", dort hast du " + r + " Vorteil.",
                    "Your attack point is sector " + n + ", you've got " + r + " there.", "Tu punto de ataque es el sector " + n + ", ahí tienes " + r + " de ventaja.");
                case 3: return RadioLanguage.T("Sektor " + n + " gehört dir, " + r + " schneller. Bau dort den Angriff auf.",
                    "Sector " + n + " is yours, " + r + " quicker. Set up the move there.", "El sector " + n + " es tuyo, " + r + " más rápido. Prepara el ataque ahí.");
                default: return RadioLanguage.T("Info: " + r + " Vorteil in Sektor " + n + " gegenüber " + who + ". Bleib dran und schlag dort zu.",
                    "Info: " + r + " advantage in sector " + n + " over " + who + ". Stay close and strike there.", "Info: " + r + " de ventaja en el sector " + n + " sobre " + who + ". Mantente cerca y ataca ahí.");
            }
        }

        internal static string DefendText(string name, int sector, float delta, int variant)
        {
            string r = GapTrendWatch.Rate(delta), n = sector.ToString(RadioLanguage.Culture);
            string Who = name.Length > 0 ? name : RadioLanguage.T("Der Hintermann", "The car behind", "El de detrás");
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T(Who + " ist " + r + " schneller in Sektor " + n + ". Dort die Linie sauber halten.",
                    Who + " is " + r + " quicker in sector " + n + ". Keep your line tidy there.", Who + " es " + r + " más rápido en el sector " + n + ". Mantén la trazada limpia ahí.");
                case 1: return RadioLanguage.T("Achtung Sektor " + n + ": da holt " + Lower(Who) + " " + r + ". Gute Ausfahrten, keine Lücke lassen.",
                    "Watch sector " + n + ": " + Lower(Who) + " gains " + r + " there. Good exits, don't leave a gap.", "Ojo al sector " + n + ": ahí " + Lower(Who) + " gana " + r + ". Buenas salidas, no dejes hueco.");
                case 2: return RadioLanguage.T("Verteidigen musst du in Sektor " + n + ", dort ist er " + r + " stärker.",
                    "Sector " + n + " is where you defend, he's " + r + " stronger there.", "Tienes que defender en el sector " + n + ", ahí es " + r + " más fuerte.");
                case 3: return RadioLanguage.T(Who + " kommt in Sektor " + n + " näher, " + r + " pro Runde. Innen zumachen, sauber raus.",
                    Who + " closes in sector " + n + ", " + r + " a lap. Cover the inside, clean exit.", Who + " se acerca en el sector " + n + ", " + r + " por vuelta. Cierra el interior, salida limpia.");
                default: return RadioLanguage.T("Info nach hinten: Sektor " + n + " ist seine Stärke, " + r + ". Dort keine Fehler.",
                    "Info behind: sector " + n + " is his strength, " + r + ". No mistakes there.", "Info de atrás: el sector " + n + " es su fuerte, " + r + ". Sin errores ahí.");
            }
        }

        private static string Lower(string who) => who.Length > 0 && (who.StartsWith("Der ") || who.StartsWith("The ") || who.StartsWith("El ")) ? char.ToLower(who[0]) + who.Substring(1) : who;

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
