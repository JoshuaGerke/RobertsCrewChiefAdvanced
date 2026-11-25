using System;
using System.Collections.Generic;
using System.Linq;
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
        // LMU 20.11.: In 20 Minuten Multiclass kam kein einziger Zwischenstand – mit 5 s freier Bahn auch zu Überrundeten und
        // anderen Klassen gab es nie einen „ruhigen“ Moment. Jetzt 3 s, und nur Autos derselben Klasse in derselben Runde zählen.
        internal const float CalmSeconds = 3f;
        /// <summary>
        /// LMU 21.11.: 41 Autos, Rob immer im Pulk – in 15 Minuten kein einziges Zwischenfazit, jede Gelegenheit verfiel. Wer schon
        /// eine Weile auf die ruhige Minute wartet, braucht nur noch eine Sekunde Luft (kein Zweikampf); die Gelegenheit verfällt
        /// erst später.
        /// </summary>
        internal const float LateCalmSeconds = 1f;
        internal const double LateAfter = .06, GiveUpAfter = .2;
        /// <summary>Lebensdauer eines Zwischenstands; Platz und Abstände werden beim Sprechen ohnehin neu gelesen.</summary>
        internal const int SummarySeconds = 30;
        private float calmNeeded = CalmSeconds;
        private readonly Random random;
        private int session = int.MinValue, startPosition, delivered, tallyVariant;
        private readonly List<double> windows = new List<double>();
        private ObservedRaceCall call;
        private readonly int[] closingVariant = new int[5];
        private RaceMind mind;
        private static readonly List<float> NoGaps = new List<float>();

        internal RaceSummaryWatch(int seed = 0) { random = seed == 0 ? new Random() : new Random(seed); }

        internal void Reset()
        {
            session = int.MinValue; startPosition = 0; delivered = 0; windows.Clear(); call = null; halfWindow = -1;
        }

        /// <summary>Anteil des Rennens (0..1) aus Runden oder Zeit; -1 wenn unbekannt.</summary>
        internal static double Progress(SessionData s)
        {
            if (s.SessionHasFixedTime)
                return s.SessionTotalRunTime > 0 && s.SessionRunningTime >= 0 ? Math.Min(1, s.SessionRunningTime / s.SessionTotalRunTime) : -1;
            return s.SessionNumberOfLaps > 0 ? Math.Min(1, (double)s.CompletedLaps / s.SessionNumberOfLaps) : -1;
        }

        /// <summary>
        /// Rob 24.11. (LMU Algarve): „Die großen Zusammenfassungen von Ben sind gut, brauchen wir bei kurzen Rennen aber nur einmal und
        /// bei längeren 2–3-mal.“ Bis 30 Minuten (bzw. unter 20 Runden) eine, bis 60 Minuten (unter 40 Runden) zwei, darüber drei.
        /// </summary>
        internal static int TotalCount(SessionData s) => PlannedCount(s);

        /// <summary>Grenzen kurz/mittel/lang in Minuten bzw. Runden.</summary>
        internal const int ShortRaceMinutes = 30, LongRaceMinutes = 60, ShortRaceLaps = 20, LongRaceLaps = 40;

        /// <summary>Wie viele Zusammenfassungen passen zu dieser Renndauer (1–3).</summary>
        internal static int PlannedCount(SessionData s)
        {
            double minutes = s.SessionHasFixedTime ? s.SessionTotalRunTime / 60.0 : -1;
            int laps = s.SessionHasFixedTime ? -1 : s.SessionNumberOfLaps;
            if (minutes > 0) return minutes <= ShortRaceMinutes ? 1 : minutes <= LongRaceMinutes ? 2 : 3;
            if (laps > 0) return laps < ShortRaceLaps ? 1 : laps < LongRaceLaps ? 2 : 3;
            return 1;
        }

        /// <summary>Rennanteil des festen Zwischenfazits zur Rennhälfte.</summary>
        internal const double HalfRace = .5;
        private int halfWindow = -1;

        /// <summary>Index des Fensters zur Rennhälfte; -1, wenn das Rennen dafür zu kurz ist.</summary>
        internal int HalfWindow => halfWindow;

        private void Plan(SessionData s)
        {
            windows.Clear();
            int n = TotalCount(s);
            // LMU Bahrain 22.11.: „Kein Zwischenfazit zur Rennhälfte.“ Das Fazit zur Rennhälfte ist immer dabei; im langen Rennen
            // kommt eines nach dem ersten Viertel dazu, im mittleren und langen eines im letzten Viertel (mit etwas Zufall).
            if (n >= 3) windows.Add(.25 + (random.NextDouble() - .5) * .06);
            halfWindow = windows.Count;
            windows.Add(HalfRace);
            if (n >= 2) windows.Add(.75 + (random.NextDouble() - .5) * .06);
        }

        internal IReadOnlyList<double> Windows => windows;

        /// <summary>Ruhige Minute: kein Auto innerhalb von 5 s (gemessen oder über Streckenmeter geschätzt), keine Gelbphase, nicht in der Box.</summary>
        internal static bool Calm(GameStateData gs, float gapAhead, float gapBehind) => Calm(gs, gapAhead, gapBehind, CalmSeconds);

        internal static bool Calm(GameStateData gs, float gapAhead, float gapBehind, float calmSeconds)
        {
            var s = gs.SessionData;
            if (s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane || gs.FlagData.isLocalYellow) return false;
            if (gapAhead >= 0 && gapAhead < calmSeconds || gapBehind >= 0 && gapBehind < calmSeconds) return false;
            float speed = gs.PositionAndMotionData.CarSpeed, length = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            if (!(speed > 10) || !(length > 100)) return false;
            float own = gs.PositionAndMotionData.DistanceRoundTrack, reach = speed * calmSeconds;
            double ownTotal = s.CompletedLaps + own / length;
            foreach (var o in gs.OpponentData.Values)
            {
                if (o == null || !o.IsActive || o.InPits) continue;
                // Andere Klasse oder überrundet/überrundend: Verkehr, aber kein Zweikampf, der einen Zwischenstand verbietet.
                if (gs.carClass != null && o.CarClass != null && !CarData.IsCarClassEqual(gs.carClass, o.CarClass, false)) continue;
                if (Math.Abs(o.CompletedLaps + o.DistanceRoundTrack / length - ownTotal) > .5) continue;
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
            int latchedStart = RaceStartLatch.Position(s);
            if (latchedStart > 0) startPosition = latchedStart;
            if (windows.Count == 0 && (s.SessionNumberOfLaps > 0 || s.SessionTotalRunTime > 0)) Plan(s);
            // Früher Zwischenstand nach der ersten vollen Runde, die weiteren frühestens nach drei Runden.
            if (s.SessionPhase != SessionPhase.Green || s.CompletedLaps < (delivered == 0 ? 1 : 3)) return;

            if (call != null || delivered >= windows.Count) return;
            double progress = Progress(s);
            if (progress < 0 || progress < windows[delivered]) return;
            int left = LapWatch.LapsLeft(s);
            if (left > 0 && left <= 2) { delivered = windows.Count; return; } // Schlussphase gehört dem Rennen
            if (progress > windows[delivered] + GiveUpAfter) { delivered++; return; } // Kein ruhiger Moment gefunden: diese Gelegenheit verfällt
            // Das Fazit zur Rennhälfte ist ein fester Moment: es wartet nicht erst, bis es „spät“ ist.
            bool late = progress > windows[delivered] + LateAfter || delivered == halfWindow;
            float need = late ? LateCalmSeconds : CalmSeconds;
            var c = context();
            float ahead = Gap(c, true), behind = Gap(c, false);
            if (!Calm(gs, ahead, behind, need)) return;
            calmNeeded = need;
            string text = Compose(c, ahead, behind);
            if (text == null) return;
            call = new ObservedRaceCall
            {
                Topic = "race-summary", Text = text, Evidence = "calm_window_measured_position_gaps_fuel", Member = TeamMember.Ben, Priority = 2,
                Position = s.ClassPosition, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(SummarySeconds),
                AdditionalValidity = live => Calm(live, -1, -1, need), Guaranteed = late,
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
            return Calm(ctx.GameState, ahead, behind, calmNeeded) ? Compose(ctx, ahead, behind) : null;
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

        /// <summary>
        /// Klassenbestzeit mit Gegenprobe am Feld. LMU Bahrain 22.11.: „Deine 1:22,361 ist die schnellste Runde deiner Klasse“ –
        /// Krobber war schneller, die Sim-Klassenbestzeit zeigte Robs Zeit. Der schnellere Wert aus Sim und Bestzeiten der Gegner
        /// derselben Klasse zählt; -1, wenn keiner bekannt ist.
        /// </summary>
        internal static float FieldCheckedClassBest(GameStateData gs)
        {
            float best = gs.SessionData.PlayerClassSessionBestLapTime > 10 ? gs.SessionData.PlayerClassSessionBestLapTime : float.MaxValue;
            foreach (var o in ClassRivalBests(gs)) if (o < best) best = o;
            return best == float.MaxValue ? -1 : best;
        }

        /// <summary>„Schnellste Runde deiner Klasse“ ist nur belegt, wenn mindestens ein Gegner derselben Klasse eine Zeit hat.</summary>
        internal static bool ClassFieldHasTimes(GameStateData gs) => ClassRivalBests(gs).Any();

        private static IEnumerable<float> ClassRivalBests(GameStateData gs)
        {
            foreach (var o in gs.OpponentData.Values)
            {
                if (o == null || !(o.CurrentBestLapTime > 10) || o.CurrentBestLapTime >= 3600) continue;
                if (gs.carClass != null && o.CarClass != null && !CarData.IsCarClassEqual(gs.carClass, o.CarClass, false)) continue;
                yield return o.CurrentBestLapTime;
            }
        }

        internal enum Mood { Attack, Defend, Safe, Steady, Leading }

        internal string Compose(RaceDataContext ctx, float ahead, float behind)
        {
            var gs = ctx.GameState; var s = gs.SessionData;
            bool half = halfWindow >= 0 && delivered == halfWindow;
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
            if (half)
                head = RadioVariants.Pick("summary-head-half",
                    RadioLanguage.T("Halbzeit im Rennen", "halfway through the race", "mitad de carrera"),
                    RadioLanguage.T("die Hälfte ist geschafft", "half the race is done", "ya llevamos la mitad"),
                    RadioLanguage.T("Rennhälfte, kurz zur Lage", "halfway, quick status", "mitad de carrera, situación rápida"),
                    RadioLanguage.T("halbe Distanz, Stand der Dinge", "half distance, where we are", "media distancia, cómo vamos"));
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

            // Rob 20.11.: „wie ich mich die ersten Runden geschlagen habe“ – eigene Pace gegen die Klassenbestzeit.
            float pb = s.PlayerLapTimeSessionBest, cb = FieldCheckedClassBest(gs);
            if (pb > 10 && cb > 10 && pb < 3600 && cb < 3600)
            {
                string own = LocalAnswers.LapTime(pb);
                if (pb - cb < .05f && ClassFieldHasTimes(gs))
                    parts.Add(RadioVariants.Pick("summary-pace-best",
                        RadioLanguage.T("Deine " + own + " ist die schnellste Runde deiner Klasse.", "Your " + own + " is the fastest lap in your class.", "Tu " + own + " es la vuelta más rápida de tu clase."),
                        RadioLanguage.T("Mit " + own + " hast du die Klassenbestzeit.", "With " + own + " you hold the class best.", "Con " + own + " tienes el mejor tiempo de la clase."),
                        RadioLanguage.T("Niemand in deiner Klasse war schneller als deine " + own + ".", "Nobody in your class has gone quicker than your " + own + ".", "Nadie de tu clase ha ido más rápido que tu " + own + "."),
                        RadioLanguage.T("Klassenbestzeit gehört dir, " + own + ".", "The class best is yours, " + own + ".", "El mejor tiempo de la clase es tuyo, " + own + ".")));
                else if (pb - cb >= .05f)
                {
                    string gap = GermanRadioText.Interval(pb - cb);
                    parts.Add(RadioVariants.Pick("summary-pace",
                        RadioLanguage.T("Deine persönliche Bestzeit " + own + ", die Klassenbestzeit ist " + gap + " schneller.", "Your personal best is " + own + ", the class best is " + gap + " quicker.", "Tu mejor vuelta personal es " + own + ", la mejor de la clase es " + gap + " más rápida."),
                        RadioLanguage.T("Bei der Pace fehlen dir " + gap + " auf die Schnellsten deiner Klasse.", "On pace you're " + gap + " off the quickest in your class.", "De ritmo te faltan " + gap + " respecto a los más rápidos de tu clase."),
                        RadioLanguage.T("Persönliche Bestzeit " + own + ", " + gap + " hinter der Klassenbestzeit.", "Personal best " + own + ", " + gap + " behind the class best.", "Mejor personal " + own + ", " + gap + " detrás del mejor de la clase."),
                        RadioLanguage.T("Zur Klassenbestzeit fehlen " + gap + ", deine beste ist " + own + ".", "You're " + gap + " off the class best, your best is " + own + ".", "Te faltan " + gap + " para el mejor de la clase, tu mejor es " + own + ".")));
                }
            }
            // Rennverständnis (Rob 21.11.): Was ist auf der Strecke passiert? Überholbilanz und Hauptrivale aus der Rennchronik.
            var chronicle = RaceChronicle.Current;
            string tally = chronicle.Session == s.SessionId && chronicle.Type == SessionType.Race ?
                RaceStory.TallyText(chronicle, ctx.SendDriverNames, tallyVariant++) : "";
            if (tally.Length > 0) parts.Add(tally);
            if (gs.PenaltiesData.NumOutstandingPenalties > 0)
                parts.Add(gs.PenaltiesData.NumOutstandingPenalties == 1 ? RadioLanguage.T("Eine Strafe ist noch offen.", "One penalty is still open.", "Queda una penalización pendiente.")
                    : RadioLanguage.T(gs.PenaltiesData.NumOutstandingPenalties + " Strafen sind noch offen.", gs.PenaltiesData.NumOutstandingPenalties + " penalties still open.", "Quedan " + gs.PenaltiesData.NumOutstandingPenalties + " penalizaciones pendientes."));

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
