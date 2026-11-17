using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Renningenieur liest den Verkehr (Rob 16.11.2026: „neue Phrasen gegründet auf Verkehr, Stand, Ereignisse“). Läuft Rob im Rennen
    /// auf einen Überrundeten auf (gleiche Strecke, mindestens eine Runde zurück, räumlich bestätigt), sagt Ben das 3 bis 10 Sekunden
    /// vorher – und ordnet es ein: Ist ein Klassengegner dicht hinter dir, nicht hängen bleiben; ist einer dicht vor dir, kann es die
    /// Chance sein. Je Überrundetem höchstens einmal pro Minute, insgesamt höchstens alle 25 Sekunden.
    /// </summary>
    internal sealed class BackmarkerWatch
    {
        private int session = int.MinValue, variant;
        private readonly Dictionary<string, DateTime> reported = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private DateTime lastCall = DateTime.MinValue;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; reported.Clear(); lastCall = DateTime.MinValue; call = null; }

        /// <summary>Text je nach Lage: Gegner dicht hinten (Verteidigen), dicht vorne (Chance) oder frei.</summary>
        internal static string Compose(double seconds, string backmarker, float gapAhead, string frontName, float gapBehind, string rearName, int variant)
        {
            string when = RadioLanguage.Num((float)Math.Round(seconds), "0");
            string who = string.IsNullOrWhiteSpace(backmarker) ? "" : RadioLanguage.T(", das ist " + backmarker, ", that's " + backmarker, ", es " + backmarker);
            string lead = RadioLanguage.T("Überrundeter vorne, in etwa " + when + " Sekunden bist du dran" + who + ".",
                "Lapped car ahead, you'll be on him in about " + when + " seconds" + who + ".",
                "Doblado delante, lo alcanzas en unos " + when + " segundos" + who + ".");
            int v = ((variant % 2) + 2) % 2;
            if (gapBehind > 0 && gapBehind < 2f)
            {
                string r = string.IsNullOrWhiteSpace(rearName) ? RadioLanguage.T("der Hintermann", "the car behind", "el de detrás") : rearName;
                return lead + " " + (v == 0
                    ? RadioLanguage.T(Cap(r) + " ist " + GermanRadioText.Interval(gapBehind) + " hinter dir, also sauber vorbei und nicht hängen bleiben.",
                        Cap(r) + " is " + GermanRadioText.Interval(gapBehind) + " behind, so get by cleanly and don't get stuck.",
                        Cap(r) + " está a " + GermanRadioText.Interval(gapBehind) + " detrás, pasa limpio y no te quedes atascado.")
                    : RadioLanguage.T("Früh zeigen, wo du vorbei willst, " + r + " lauert " + GermanRadioText.Interval(gapBehind) + " hinter dir.",
                        "Show early where you want to pass, " + r + " is lurking " + GermanRadioText.Interval(gapBehind) + " behind.",
                        "Indica pronto por dónde pasas, " + r + " acecha a " + GermanRadioText.Interval(gapBehind) + "."));
            }
            if (gapAhead > 0 && gapAhead < 1.5f)
            {
                string f = string.IsNullOrWhiteSpace(frontName) ? RadioLanguage.T("dem Vordermann", "the car ahead", "el de delante") : frontName;
                return lead + " " + (v == 0
                    ? RadioLanguage.T("Das kann deine Chance auf " + f + " sein, bleib dicht dran.", "That could be your chance on " + f + ", stay close.",
                        "Puede ser tu oportunidad con " + f + ", mantente cerca.")
                    : RadioLanguage.T("Wer besser durch den Verkehr kommt, gewinnt hier Zeit. Schau, wie " + f + " es macht.",
                        "Whoever handles traffic better gains time here. Watch how " + f + " does it.",
                        "Quien gestione mejor el tráfico gana tiempo. Mira cómo lo hace " + f + "."));
            }
            return lead + " " + (v == 0
                ? RadioLanguage.T("Früh zeigen, wo du vorbei willst.", "Show early where you want to pass.", "Indica pronto por dónde quieres pasar.")
                : RadioLanguage.T("Ruhig vorbei, kein Risiko.", "Pass calmly, no risk.", "Pasa con calma, sin riesgo."));
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal void Observe(GameStateData gs, TrackPositionSnapshot track, RaceSituation mind, bool names, string frontName, string rearName, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (s.SessionPhase != SessionPhase.Green || s.CompletedLaps < 1 || gs.PitData.InPitlane || track == null || now < track.Utc ||
                now - track.Utc > TimeSpan.FromSeconds(1) || now - lastCall < TimeSpan.FromSeconds(25) || call != null && call.IsValid(gs, now)) return;
            var candidate = track.Cars.Where(c => !c.InPits && !c.IsSafetyCar && c.SpatiallyConsistent && c.LapOffset.HasValue && c.LapOffset.Value <= -1 &&
                    c.RouteGapM.HasValue && c.RouteGapM.Value >= 60 && c.RouteGapM.Value <= 450 && c.ClosingMps.HasValue && c.ClosingMps.Value > 3)
                .Select(c => new { Car = c, Seconds = c.RouteGapM.Value / c.ClosingMps.Value })
                .Where(x => x.Seconds >= 3 && x.Seconds <= 10)
                .OrderBy(x => x.Seconds).FirstOrDefault();
            if (candidate == null) return;
            string key = candidate.Car.NativeIdentity ?? candidate.Car.Key;
            if (reported.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(60)) return;
            reported[key] = now; lastCall = now;
            float ahead = mind != null && mind.Fresh(now) ? mind.GapAhead : -1, behind = mind != null && mind.Fresh(now) ? mind.GapBehind : -1;
            string text = Compose(candidate.Seconds, names ? candidate.Car.Name : null, ahead, frontName, behind, rearName, variant++);
            call = new ObservedRaceCall
            {
                Topic = "traffic-backmarker", Text = text, Evidence = "lapped_car_ahead_route_gap_and_closing_speed", Member = TeamMember.Ben, Priority = 5,
                Position = -1, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(4),
                AdditionalValidity = live => !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
