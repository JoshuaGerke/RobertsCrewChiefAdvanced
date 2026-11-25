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
        // RaceRoom Salzburg 24.11.: Alvaro Garcia (zwei Runden zurück) fuhr neun Minuten lang konstant 5 bis 7 s vor Rob und wurde
        // sechsmal „in etwa 10 Sekunden bist du dran“ angesagt: Die Momentan-Annäherung in einer Bremszone reichte. Jetzt zählt die
        // gemessene Annäherung über mehrere Sekunden.
        private readonly Dictionary<string, List<KeyValuePair<DateTime, double>>> gaps = new Dictionary<string, List<KeyValuePair<DateTime, double>>>(StringComparer.Ordinal);
        internal static readonly TimeSpan ClosingWindow = TimeSpan.FromSeconds(6);
        internal static readonly TimeSpan RepeatPerCar = TimeSpan.FromSeconds(150);
        internal const float MinimumClosingMps = 3f;

        internal void Reset() { session = int.MinValue; reported.Clear(); gaps.Clear(); lastCall = DateTime.MinValue; call = null; }

        /// <summary>Gemessene mittlere Annäherung (m/s) an einen Wagen über das Fenster; null, wenn noch zu wenig Verlauf.</summary>
        internal double? SustainedClosing(string key, double gapNow, DateTime now)
        {
            if (!gaps.TryGetValue(key, out var list)) gaps[key] = list = new List<KeyValuePair<DateTime, double>>();
            list.Add(new KeyValuePair<DateTime, double>(now, gapNow));
            list.RemoveAll(p => now - p.Key > ClosingWindow + TimeSpan.FromSeconds(2) || p.Key > now);
            var first = list[0];
            double span = (now - first.Key).TotalSeconds;
            if (span < ClosingWindow.TotalSeconds * .75) return null;
            return (first.Value - gapNow) / span;
        }

        /// <summary>Text je nach Lage: Gegner dicht hinten (Verteidigen), dicht vorne (Chance) oder frei.</summary>
        internal static string Compose(double seconds, string backmarker, float gapAhead, string frontName, float gapBehind, string rearName, int variant)
        {
            string when = RadioLanguage.Num((float)Math.Round(seconds), "0");
            string who = string.IsNullOrWhiteSpace(backmarker) ? "" : RadioLanguage.T(", das ist " + backmarker, ", that's " + backmarker, ", es " + backmarker);
            string lead = RadioVariants.At(variant / 4,
                RadioLanguage.T("Überrundeter vorne, in etwa " + when + " Sekunden bist du dran" + who + ".",
                    "Lapped car ahead, you'll be on him in about " + when + " seconds" + who + ".",
                    "Doblado delante, lo alcanzas en unos " + when + " segundos" + who + "."),
                RadioLanguage.T("Langsamerer Wagen eine Runde zurück, in " + when + " Sekunden bist du dran" + who + ".",
                    "Backmarker a lap down, you're on him in " + when + " seconds" + who + ".",
                    "Coche doblado una vuelta atrás, lo tienes en " + when + " segundos" + who + "."),
                RadioLanguage.T("Verkehr voraus: Überrundeter in rund " + when + " Sekunden" + who + ".",
                    "Traffic ahead: backmarker in around " + when + " seconds" + who + ".",
                    "Tráfico delante: doblado en unos " + when + " segundos" + who + "."),
                RadioLanguage.T("In " + when + " Sekunden kommt ein Überrundeter" + who + ".",
                    "Backmarker coming up in " + when + " seconds" + who + ".",
                    "En " + when + " segundos llega un doblado" + who + "."));
            if (gapBehind > 0 && gapBehind < 2f)
            {
                string r = string.IsNullOrWhiteSpace(rearName) ? RadioLanguage.T("der Hintermann", "the car behind", "el de detrás") : rearName;
                string g = GermanRadioText.Interval(gapBehind);
                return lead + " " + RadioVariants.At(variant,
                    RadioLanguage.T(Cap(r) + " ist " + g + " hinter dir, also sauber vorbei und nicht hängen bleiben.",
                        Cap(r) + " is " + g + " behind, so get by cleanly and don't get stuck.",
                        Cap(r) + " está a " + g + " detrás, pasa limpio y no te quedes atascado."),
                    RadioLanguage.T("Früh zeigen, wo du vorbei willst, " + r + " lauert " + g + " hinter dir.",
                        "Show early where you want to pass, " + r + " is lurking " + g + " behind.",
                        "Indica pronto por dónde pasas, " + r + " acecha a " + g + "."),
                    RadioLanguage.T("Nicht hinter ihm festhängen, " + r + " ist nur " + g + " weg.",
                        "Don't get stuck behind him, " + r + " is only " + g + " back.",
                        "No te quedes detrás de él, " + r + " está a solo " + g + "."),
                    RadioLanguage.T("Entschlossen vorbei – " + r + " wartet " + g + " hinter dir auf einen Fehler.",
                        "Get by decisively – " + r + " is " + g + " behind waiting for a mistake.",
                        "Pasa con decisión: " + r + " está a " + g + " esperando un fallo."));
            }
            if (gapAhead > 0 && gapAhead < 1.5f)
            {
                string f = string.IsNullOrWhiteSpace(frontName) ? RadioLanguage.T("dem Vordermann", "the car ahead", "el de delante") : frontName;
                return lead + " " + RadioVariants.At(variant,
                    RadioLanguage.T("Das kann deine Chance auf " + f + " sein, bleib dicht dran.", "That could be your chance on " + f + ", stay close.",
                        "Puede ser tu oportunidad con " + f + ", mantente cerca."),
                    RadioLanguage.T("Wer besser durch den Verkehr kommt, gewinnt hier Zeit. Schau, wie " + f + " es macht.",
                        "Whoever handles traffic better gains time here. Watch how " + f + " does it.",
                        "Quien gestione mejor el tráfico gana tiempo. Mira cómo lo hace " + f + "."),
                    RadioLanguage.T("Verkehr ist Chance: Wenn " + f + " hängen bleibt, bist du da.", "Traffic is opportunity: if " + f + " gets held up, you're there.",
                        "El tráfico es una oportunidad: si " + f + " se queda atascado, ahí estás."),
                    RadioLanguage.T("Dranbleiben – im Überrundungsverkehr kann " + f + " Zeit verlieren.", "Stay with him – " + f + " can lose time in the traffic.",
                        "No lo sueltes: " + f + " puede perder tiempo con los doblados."));
            }
            return lead + " " + RadioVariants.At(variant,
                RadioLanguage.T("Früh zeigen, wo du vorbei willst.", "Show early where you want to pass.", "Indica pronto por dónde quieres pasar."),
                RadioLanguage.T("Ruhig vorbei, kein Risiko.", "Pass calmly, no risk.", "Pasa con calma, sin riesgo."),
                RadioLanguage.T("Sauber auf der Geraden vorbei, nicht in der Kurve.", "Clean pass on the straight, not in the corner.", "Pásalo limpio en la recta, no en la curva."),
                RadioLanguage.T("Geduld, er macht dir gleich Platz.", "Patience, he'll make room for you.", "Paciencia, enseguida te deja sitio."));
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal void Observe(GameStateData gs, TrackPositionSnapshot track, RaceSituation mind, bool names, string frontName, string rearName, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            // LMU 18.11.: „Überrundeter vorne …“ kurz vor der Boxeneinfahrt nach angemeldetem Stopp – „die Ansage war unnötig“.
            if (s.SessionPhase != SessionPhase.Green || s.CompletedLaps < 1 || gs.PitData.InPitlane || PitDamageWatch.HeadingToPits(gs) || track == null || now < track.Utc ||
                now - track.Utc > TimeSpan.FromSeconds(1)) return;
            var closing = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var c in track.Cars)
            {
                if (c == null || c.InPits || c.IsSafetyCar || !c.SpatiallyConsistent || !c.LapOffset.HasValue || c.LapOffset.Value > -1 ||
                    !c.RouteGapM.HasValue || c.RouteGapM.Value < 0 || c.RouteGapM.Value > 1500) continue;
                string id = c.NativeIdentity ?? c.Key;
                var mps = SustainedClosing(id, c.RouteGapM.Value, now);
                if (mps.HasValue) closing[id] = mps.Value;
            }
            foreach (var stale in gaps.Where(p => p.Value.Count == 0 || now - p.Value[p.Value.Count - 1].Key > TimeSpan.FromSeconds(10)).Select(p => p.Key).ToList()) gaps.Remove(stale);
            if (now - lastCall < TimeSpan.FromSeconds(25) || call != null && call.IsValid(gs, now)) return;
            var candidate = track.Cars.Where(c => c != null && !c.InPits && !c.IsSafetyCar && c.SpatiallyConsistent && c.LapOffset.HasValue && c.LapOffset.Value <= -1 &&
                    c.RouteGapM.HasValue && c.RouteGapM.Value >= 60 && c.RouteGapM.Value <= 450 && c.ClosingMps.HasValue && c.ClosingMps.Value > MinimumClosingMps &&
                    closing.TryGetValue(c.NativeIdentity ?? c.Key, out var sustained) && sustained > MinimumClosingMps)
                .Select(c => new { Car = c, Seconds = c.RouteGapM.Value / closing[c.NativeIdentity ?? c.Key] })
                .Where(x => x.Seconds >= 3 && x.Seconds <= 12)
                .OrderBy(x => x.Seconds).FirstOrDefault();
            if (candidate == null) return;
            string key = candidate.Car.NativeIdentity ?? candidate.Car.Key;
            if (reported.TryGetValue(key, out var last) && now - last < RepeatPerCar) return;
            reported[key] = now; lastCall = now;
            float ahead = mind != null && mind.Fresh(now) ? mind.GapAhead : -1, behind = mind != null && mind.Fresh(now) ? mind.GapBehind : -1;
            string text = Compose(candidate.Seconds, names ? candidate.Car.Name : null, ahead, frontName, behind, rearName, variant++);
            call = new ObservedRaceCall
            {
                Topic = "traffic-backmarker", Text = text, Evidence = "lapped_car_ahead_route_gap_and_closing_speed", Member = TeamMember.Ben, Priority = 5,
                Position = -1, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(4),
                AdditionalValidity = live => !live.PitData.InPitlane && !PitDamageWatch.HeadingToPits(live)
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
