using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennkopf, Teil 1 (Rob 16.11.2026): Ein Renningenieur fasst zwischendurch zusammen, wie das Rennen läuft – aber nur in einer
    /// ruhigen Minute. 1–3 Zwischenresümees je Rennen, über die Renndistanz verteilt (mit Zufall), nie in den letzten zwei Runden,
    /// nie mit einem Auto innerhalb von 5 s vor oder hinter uns, nie unter Gelb oder in der Box. Inhalt nur aus gemessenen Werten:
    /// Platz und Bilanz seit dem Start, Abstände mit Trend, Sprit, Vorfallpunkte wenn knapp. Der Schlusssatz passt zur Lage:
    /// angreifen, verteidigen, sicher fahren oder gleichmäßig weiter – jeweils in wechselnden Formulierungen.
    /// </summary>
    internal sealed class RaceSummaryWatch
    {
        internal const float CalmSeconds = 5f;
        private readonly Random random;
        private int session = int.MinValue, startPosition, delivered;
        private readonly List<double> windows = new List<double>();
        private ObservedRaceCall call;
        private readonly int[] closingVariant = new int[5];
        private RaceMind mind;
        private static readonly List<float> NoGaps = new List<float>();

        internal RaceSummaryWatch(int seed = 0) { random = seed == 0 ? new Random() : new Random(seed); }

        internal void Reset()
        {
            session = int.MinValue; startPosition = 0; delivered = 0; windows.Clear(); call = null;
        }

        /// <summary>Anteil des Rennens (0..1) aus Runden oder Zeit; -1 wenn unbekannt.</summary>
        internal static double Progress(SessionData s)
        {
            if (s.SessionHasFixedTime)
                return s.SessionTotalRunTime > 0 && s.SessionRunningTime >= 0 ? Math.Min(1, s.SessionRunningTime / s.SessionTotalRunTime) : -1;
            return s.SessionNumberOfLaps > 0 ? Math.Min(1, (double)s.CompletedLaps / s.SessionNumberOfLaps) : -1;
        }

        /// <summary>Wie viele Zusammenfassungen passen zu dieser Renndauer (1–3).</summary>
        internal static int PlannedCount(SessionData s)
        {
            double minutes = s.SessionHasFixedTime ? s.SessionTotalRunTime / 60.0 : -1;
            int laps = s.SessionHasFixedTime ? -1 : s.SessionNumberOfLaps;
            if (minutes > 0) return minutes < 15 ? 1 : minutes < 35 ? 2 : 3;
            if (laps > 0) return laps < 10 ? 1 : laps < 25 ? 2 : 3;
            return 1;
        }

        private void Plan(SessionData s)
        {
            windows.Clear();
            int n = PlannedCount(s);
            for (int i = 1; i <= n; i++)
            {
                double centre = (double)i / (n + 1);
                double jitter = (random.NextDouble() - .5) * (.5 / (n + 1));
                windows.Add(Math.Max(.15, Math.Min(.85, centre + jitter)));
            }
        }

        /// <summary>Ruhige Minute: kein Auto innerhalb von 5 s (gemessen oder über Streckenmeter geschätzt), keine Gelbphase, nicht in der Box.</summary>
        internal static bool Calm(GameStateData gs, float gapAhead, float gapBehind)
        {
            var s = gs.SessionData;
            if (s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane || gs.FlagData.isLocalYellow) return false;
            if (gapAhead >= 0 && gapAhead < CalmSeconds || gapBehind >= 0 && gapBehind < CalmSeconds) return false;
            float speed = gs.PositionAndMotionData.CarSpeed, length = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            if (!(speed > 10) || !(length > 100)) return false;
            float own = gs.PositionAndMotionData.DistanceRoundTrack, reach = speed * CalmSeconds;
            foreach (var o in gs.OpponentData.Values)
            {
                if (o == null || !o.IsActive || o.InPits) continue;
                float d = Math.Abs(o.DistanceRoundTrack - own);
                d = Math.Min(d, length - d);
                if (d < reach) return false; // auch Überrundete oder andere Klassen zählen als Verkehr
            }
            return true;
        }

        internal void Observe(GameStateData gs, RaceMind raceMind, Func<RaceDataContext> context, DateTime now)
        {
            if (gs == null) return;
            mind = raceMind;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (startPosition <= 0) startPosition = RaceStartLatch.Position(s);
            if (windows.Count == 0 && (s.SessionNumberOfLaps > 0 || s.SessionTotalRunTime > 0)) Plan(s);
            // Robs Vorgabe: frühestens nach etwa drei Runden.
            if (s.SessionPhase != SessionPhase.Green || s.CompletedLaps < 3) return;

            if (call != null || delivered >= windows.Count) return;
            double progress = Progress(s);
            if (progress < 0 || progress < windows[delivered]) return;
            int left = LapWatch.LapsLeft(s);
            if (left > 0 && left <= 2) { delivered = windows.Count; return; } // Schlussphase gehört dem Rennen
            if (progress > windows[delivered] + .12) { delivered++; return; } // Kein ruhiger Moment gefunden: diese Gelegenheit verfällt
            var c = context();
            float ahead = Gap(c, true), behind = Gap(c, false);
            if (!Calm(gs, ahead, behind)) return;
            string text = Compose(c, ahead, behind);
            if (text == null) return;
            call = new ObservedRaceCall
            {
                Topic = "race-summary", Text = text, Evidence = "calm_window_measured_position_gaps_fuel", Member = TeamMember.Ben, Priority = 2,
                Position = s.ClassPosition, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(15),
                AdditionalValidity = live => Calm(live, -1, -1),
                // C3: Platz und Abstände erst beim Sprechen festlegen – wartet die Ansage, sagt Ben trotzdem den aktuellen Stand.
                RefreshText = Refresh
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        /// <summary>Zwischenstand mit den Werten von jetzt; null, wenn es gerade nicht ruhig ist.</summary>
        internal string Refresh(RaceDataContext ctx)
        {
            if (ctx?.GameState == null) return null;
            float ahead = Gap(ctx, true), behind = Gap(ctx, false);
            return Calm(ctx.GameState, ahead, behind) ? Compose(ctx, ahead, behind) : null;
        }

        /// <summary>Die rundenweise Abstandsmessung des Rennkopfs, nur wenn sie zum aktuellen Nachbarn gehört (letzte bis zu vier Runden).</summary>
        private List<float> Gaps(GameStateData gs, bool ahead)
        {
            var h = mind == null ? null : ahead ? mind.Front : mind.Rear;
            var op = RaceDataTools.Neighbour(gs, ahead);
            if (h == null || op == null || h.Id != OpponentStopLog.Key(op)) return NoGaps;
            return h.Gaps.Count <= 4 ? h.Gaps : h.Gaps.GetRange(h.Gaps.Count - 4, 4);
        }

        private static float Gap(RaceDataContext ctx, bool ahead)
        {
            var op = RaceDataTools.Neighbour(ctx.GameState, ahead);
            if (op == null) return -1;
            var m = RaceDataTools.ReadGap(ctx, op, ahead);
            return m.IsRaceGap ? m.Seconds : -1;
        }

        /// <summary>Veränderung über die letzten bis zu drei Runden (negativ = Abstand schrumpft).</summary>
        private static float Trend(List<float> gaps) => gaps.Count >= 2 ? gaps[gaps.Count - 1] - gaps[0] : 0;

        internal enum Mood { Attack, Defend, Safe, Steady, Leading }

        internal string Compose(RaceDataContext ctx, float ahead, float behind)
        {
            var gs = ctx.GameState; var s = gs.SessionData;
            if (s.ClassPosition <= 0) return null;
            var parts = new List<string>();
            string name = AiPersonas.CleanForRadio(ctx.DriverName ?? "", 30, false);
            string rank = RaceRadioLanguage.Rank(s, s.ClassPosition);
            int start = startPosition > 0 ? startPosition : RaceStartLatch.Position(s);
            int gained = start > 0 ? start - s.ClassPosition : 0;
            string intro = name.Length > 0 ? name + ", " : "";
            string head = RadioVariants.Pick("summary-head",
                RadioLanguage.T("kurzer Zwischenstand", "quick update", "resumen rápido"),
                RadioLanguage.T("kurz zur Lage", "quick status", "situación rápida"),
                RadioLanguage.T("Stand der Dinge", "where we are", "cómo vamos"),
                RadioLanguage.T("kleines Update", "short update", "pequeña actualización"));
            if (intro.Length == 0) head = Capitalize(head);
            if (gained > 0)
                parts.Add(RadioLanguage.T(intro + head + ": " + rank + ", seit dem Start " + gained + (gained == 1 ? " Platz" : " Plätze") + " gutgemacht.",
                    intro + head + ": " + rank + ", " + gained + (gained == 1 ? " place" : " places") + " gained since the start.",
                    intro + head + ": " + rank + ", " + gained + (gained == 1 ? " puesto" : " puestos") + " ganados desde la salida."));
            else if (gained < 0)
                parts.Add(RadioLanguage.T(intro + head + ": " + rank + ", " + (-gained) + (gained == -1 ? " Platz" : " Plätze") + " hinter der Startposition.",
                    intro + head + ": " + rank + ", " + (-gained) + (gained == -1 ? " place" : " places") + " down from the start.",
                    intro + head + ": " + rank + ", " + (-gained) + (gained == -1 ? " puesto" : " puestos") + " por detrás de la salida."));
            else
                parts.Add(intro + head + ": " + rank + ".");

            var front = RaceDataTools.Neighbour(gs, true);
            var rear = RaceDataTools.Neighbour(gs, false);
            var frontGaps = Gaps(gs, true); var rearGaps = Gaps(gs, false);
            float frontTrend = Trend(frontGaps), rearTrend = Trend(rearGaps);
            string frontName = front != null && ctx.SendDriverNames && front.CanUseName ? RadioPhraseCatalog.SafeRivalName(front.DriverRawName) : "";
            string rearName = rear != null && ctx.SendDriverNames && rear.CanUseName ? RadioPhraseCatalog.SafeRivalName(rear.DriverRawName) : "";
            if (ahead > 0 && s.ClassPosition > 1)
            {
                string who = frontName.Length > 0 ? frontName : RadioLanguage.T("dem Vordermann", "the car ahead", "el de delante");
                string trend = frontGaps.Count >= 2 && frontTrend < -.3f ? RadioLanguage.T(", du holst auf", ", and you're closing", ", y te acercas")
                    : frontGaps.Count >= 2 && frontTrend > .3f ? RadioLanguage.T(", er zieht leicht weg", ", he's pulling away a bit", ", se escapa un poco") : "";
                string gap = GermanRadioText.Interval(ahead);
                string whoN = frontName.Length > 0 ? frontName : RadioLanguage.T("der Vordermann", "the car ahead", "el de delante");
                parts.Add(RadioVariants.Pick("summary-front",
                    RadioLanguage.T("Auf " + who + " fehlen " + gap + trend + ".", gap + " to " + who + trend + ".", gap + " hasta " + who + trend + "."),
                    RadioLanguage.T(Capitalize(whoN) + " ist " + gap + " vor dir" + trend + ".", Capitalize(whoN) + " is " + gap + " up the road" + trend + ".", Capitalize(whoN) + " va " + gap + " por delante" + trend + "."),
                    RadioLanguage.T("Abstand nach vorn " + gap + trend + ".", "Gap ahead " + gap + trend + ".", "Diferencia hacia delante " + gap + trend + "."),
                    RadioLanguage.T("Nach vorn fehlen " + gap + trend + ".", gap + " to find up front" + trend + ".", "Faltan " + gap + " hacia delante" + trend + ".")));
            }
            if (behind > 0)
            {
                string who = rearName.Length > 0 ? rearName : RadioLanguage.T("der Hintermann", "the car behind", "el de detrás");
                string trend = rearGaps.Count >= 2 && rearTrend < -.3f ? RadioLanguage.T(" und kommt näher", " and closing", " y se acerca")
                    : rearGaps.Count >= 2 && rearTrend > .3f ? RadioLanguage.T(", du baust den Vorsprung aus", ", you're extending the gap", ", amplías la ventaja") : "";
                string gap = GermanRadioText.Interval(behind);
                parts.Add(RadioVariants.Pick("summary-rear",
                    RadioLanguage.T(Capitalize(who) + " liegt " + gap + " hinter dir" + trend + ".", Capitalize(who) + " is " + gap + " back" + trend + ".", Capitalize(who) + " está a " + gap + trend + "."),
                    RadioLanguage.T(Capitalize(who) + " folgt mit " + gap + trend + ".", Capitalize(who) + " follows at " + gap + trend + ".", Capitalize(who) + " te sigue a " + gap + trend + "."),
                    RadioLanguage.T(Capitalize(who) + " hängt " + gap + " hinter dir" + trend + ".", Capitalize(who) + " sits " + gap + " behind" + trend + ".", Capitalize(who) + " va " + gap + " detrás" + trend + "."),
                    RadioLanguage.T("Im Rückspiegel " + who + ", " + gap + " zurück" + trend + ".", "In the mirrors, " + who + ", " + gap + " back" + trend + ".", "En el retrovisor, " + who + ", a " + gap + trend + ".")));
            }
            var fuel = FuelEstimate.From(ctx, 1);
            string verdict = fuel?.Verdict();
            if (verdict != null) parts.Add(verdict);
            string tyres = OwnTyreAnswer.Brief(gs);
            if (tyres != null) parts.Add(tyres);
            bool incidentsTight = IncidentPointsWatch.Known(s) && IncidentPointsWatch.Level(s.CurrentIncidentCount, s.MaxIncidentCount) >= 3;
            if (incidentsTight)
            {
                string c = s.CurrentIncidentCount.ToString(), m = s.MaxIncidentCount.ToString();
                parts.Add(RadioVariants.Pick("summary-incidents",
                    RadioLanguage.T("Vorfallpunkte " + c + " von " + m + ".", "Incident points " + c + " of " + m + ".", "Puntos de incidente " + c + " de " + m + "."),
                    RadioLanguage.T("Achtung Vorfallpunkte: " + c + " von " + m + ".", "Watch the incident points: " + c + " of " + m + ".", "Ojo a los puntos de incidente: " + c + " de " + m + "."),
                    RadioLanguage.T("Du stehst bei " + c + " von " + m + " Vorfallpunkten.", "You're on " + c + " of " + m + " incident points.", "Llevas " + c + " de " + m + " puntos de incidente."),
                    RadioLanguage.T(c + " von " + m + " Vorfallpunkten, sauber bleiben.", c + " of " + m + " incident points, keep it clean.", c + " de " + m + " puntos de incidente, mantenlo limpio.")));
            }

            // Der Rennkopf kennt Trends, Gefahren und Chancen; seine Einschätzung bestimmt den Schlusssatz. Ohne frische Lage die eigene Regel.
            var x = RaceMind.Latest;
            Mood mood = x != null && x.SessionId == s.SessionId && x.Fresh(ctx.NowUtc) && x.Focus != RaceFocus.None
                ? MoodOf(x.Focus, s.ClassPosition)
                : ChooseMood(s, ahead, behind, frontTrend, rearTrend, frontGaps.Count, rearGaps.Count, incidentsTight);
            parts.Add(Closing(mood, closingVariant[(int)mood]++, name));
            return string.Join(" ", parts);
        }

        internal static Mood MoodOf(RaceFocus focus, int classPosition)
        {
            switch (focus)
            {
                case RaceFocus.Safe: return Mood.Safe;
                case RaceFocus.Defend: return Mood.Defend;
                case RaceFocus.Attack: return Mood.Attack;
                default: return classPosition == 1 ? Mood.Leading : Mood.Steady;
            }
        }

        internal static Mood ChooseMood(SessionData s, float ahead, float behind, float frontTrend, float rearTrend, int frontSamples, int rearSamples, bool incidentsTight)
        {
            if (incidentsTight) return Mood.Safe;
            if (s.ClassPosition == 1) return behind > 0 && rearSamples >= 2 && rearTrend < -.3f ? Mood.Defend : Mood.Leading;
            if (behind > 0 && behind < 10 && rearSamples >= 2 && rearTrend < -.3f && !(ahead > 0 && frontSamples >= 2 && frontTrend < -.3f)) return Mood.Defend;
            if (ahead > 0 && (ahead < 15 || frontSamples >= 2 && frontTrend < -.3f)) return Mood.Attack;
            return Mood.Steady;
        }

        internal static string Closing(Mood mood, int variant, string name)
        {
            string n = string.IsNullOrEmpty(name) ? "" : name;
            string[] de, en, es;
            switch (mood)
            {
                case Mood.Attack:
                    de = new[] { "Push, " + n + ", den schnappst du dir!", "Jetzt Druck machen, da ist was drin.", "Weiter so, der Abstand schmilzt. Hol ihn dir!", "Gib Gas nach vorne, wir sind dran." };
                    en = new[] { "Push, " + n + ", go and get him!", "Put the pressure on, there's something in it.", "Keep going, the gap is melting. Go get him!", "Push forward, we're on it." };
                    es = new[] { "¡Aprieta, " + n + ", a por él!", "Mete presión, hay algo ahí.", "Sigue así, la distancia se reduce. ¡A por él!", "Acelera, vamos a por ello." };
                    break;
                case Mood.Defend:
                    de = new[] { "Konzentriert und sauber, " + n + ", halt ihn hinter dir.", "Saubere Linien, keine Fehler – dann kommt er nicht vorbei.", "Ruhig bleiben und die Bremspunkte treffen, wir halten die Position.", "Verteidige clever, nicht hektisch." };
                    en = new[] { "Focused and clean, " + n + ", keep him behind.", "Clean lines, no mistakes – then he won't get by.", "Stay calm, hit your braking points, we hold the place.", "Defend smart, not frantic." };
                    es = new[] { "Concentrado y limpio, " + n + ", mantenlo detrás.", "Trazadas limpias, sin errores, y no pasará.", "Tranquilo, clava las frenadas, mantenemos la posición.", "Defiende con cabeza, sin nervios." };
                    break;
                case Mood.Safe:
                    de = new[] { "Ab jetzt ruhig und sicher, " + n + ", kein Risiko mehr.", "Sauber ins Ziel bringen ist jetzt das Wichtigste.", "Lieber eine Zehntel liegen lassen als einen Vorfall riskieren.", "Konzentriert und sicher weiter." };
                    en = new[] { "Calm and safe from here, " + n + ", no more risks.", "Bringing it home clean is what matters now.", "Better to leave a tenth than risk an incident.", "Focused and safe, keep going." };
                    es = new[] { "Tranquilo y seguro desde aquí, " + n + ", sin riesgos.", "Lo importante ahora es llegar limpio.", "Mejor dejar una décima que arriesgar un incidente.", "Concentrado y seguro, sigue." };
                    break;
                case Mood.Leading:
                    de = new[] { "Stark, " + n + ", du führst. Schön gleichmäßig weiter.", "Das Rennen gehört dir, bleib konzentriert.", "Kopf runter, saubere Runden, so bleibt es.", "Ruhig und schnell, genau so." };
                    en = new[] { "Strong, " + n + ", you're leading. Nice and steady.", "The race is yours, stay focused.", "Head down, clean laps, that's how we keep it.", "Calm and quick, just like that." };
                    es = new[] { "Muy bien, " + n + ", vas líder. Sigue constante.", "La carrera es tuya, concentración.", "Cabeza abajo, vueltas limpias, así se mantiene.", "Tranquilo y rápido, así." };
                    break;
                default:
                    de = new[] { "Weiter so, " + n + ", schön gleichmäßig.", "Gute Arbeit bisher, konzentriert bleiben.", "Saubere Runden fahren, der Rest kommt.", "Rhythmus halten, das passt." };
                    en = new[] { "Keep it up, " + n + ", nice and steady.", "Good work so far, stay focused.", "Clean laps, the rest will come.", "Keep the rhythm, it's working." };
                    es = new[] { "Sigue así, " + n + ", constante.", "Buen trabajo hasta ahora, concentración.", "Vueltas limpias, lo demás llegará.", "Mantén el ritmo, va bien." };
                    break;
            }
            var pick = RadioLanguage.Pick(de, en, es);
            string line = pick[((variant % pick.Length) + pick.Length) % pick.Length];
            return n.Length == 0 ? line.Replace(", ,", ",").Replace(" , ", " ").Replace(",  ", ", ").Replace(", !", "!").Replace(", den", ", den") : line;
        }

        private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && call.DeliveryStartedUtc != DateTime.MinValue) { call = null; delivered++; }
            else if (call != null && !call.IsValid(gs, now)) call = null; // Lage nicht mehr ruhig: später erneut versuchen
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
