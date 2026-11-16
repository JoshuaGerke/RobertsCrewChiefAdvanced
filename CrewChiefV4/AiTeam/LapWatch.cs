using System;
using System.Collections.Generic;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennfortschritt ohne KI und deshalb sofort: noch zwei Runden, letzte Runde, Zielflagge.
    /// Ersetzt im Modus "Nur noch das KI-Team spricht" die Rundenzähler-Ansagen von Crew Chief.
    /// </summary>
    public class LapWatch
    {
        private int lastCalledLap = -1;
        private int pendingLap = -1;
        private float pendingSince;
        private bool finishCalled, timedClockCalled;
        private ObservedRaceCall disqualification;
        private const string DisqualificationGroup = "race-disqualified";
        private readonly RadioVariantCursor disqualificationVariants = new RadioVariantCursor();

        internal static string DriverAddress(string driverName)
        {
            string name = AiPersonas.CleanForRadio(driverName ?? "", 30, false);
            return string.IsNullOrWhiteSpace(name) ? "" : ", " + name;
        }

        internal static IEnumerable<string> DisqualificationPhrases(string driverName)
        {
            var catalog = RadioPhraseCatalog.Current;
            string address = DriverAddress(driverName);
            for (int variant = 0; variant < catalog.Count(DisqualificationGroup); variant++)
                yield return catalog.Format(DisqualificationGroup, variant, address);
        }
        private int lastLapVariant;
        private static string[] LastLapMotivation => RadioLanguage.Pick(
            new[] { "Sauber ins Ziel.", "Konzentration bis zur Linie.", "Bring ihn sicher heim." },
            new[] { "Clean to the flag.", "Focus all the way to the line.", "Bring it home safely." },
            new[] { "Limpio hasta meta.", "Concentración hasta la línea.", "Llévalo a casa con seguridad." });
        private readonly RaceRadioLanguage language = new RaceRadioLanguage();

        public void Reset(bool sameDisqualification = false)
        {
            lastCalledLap = pendingLap = -1;
            finishCalled = timedClockCalled = false; ForgetTimedFinish();
            if (!sameDisqualification) disqualification = null;
        }

        internal static bool ConfirmedDisqualification(GameStateData state) =>
            state != null && state.SessionData.SessionType == SessionType.Race &&
            state.SessionData.SessionPhase == SessionPhase.Finished && state.SessionData.IsDisqualified;

        internal static bool SameDisqualification(GameStateData previous, GameStateData current) =>
            ConfirmedDisqualification(previous) && ConfirmedDisqualification(current) &&
            previous.SessionData.SessionId == current.SessionData.SessionId &&
            previous.SessionData.CompletedLaps == current.SessionData.CompletedLaps &&
            previous.SessionData.TrackDefinition?.name == current.SessionData.TrackDefinition?.name;

        internal ObservedRaceCall TakeDisqualification(GameStateData current, DateTime now, bool busy, string driverName = null)
        {
            if (!ConfirmedDisqualification(current)) return null;
            if (disqualification == null)
            {
                var catalog = RadioPhraseCatalog.Current;
                int variant = disqualificationVariants.Select(DisqualificationGroup, catalog.Count(DisqualificationGroup));
                disqualification = new ObservedRaceCall {
                    Topic = DisqualificationGroup, Text = catalog.Format(DisqualificationGroup, variant, DriverAddress(driverName)),
                    Member = TeamMember.Mik,
                    Evidence = "confirmed_player_disqualification", Priority = 7, AllowPitlane = true, Position = -1,
                    SessionId = current.SessionData.SessionId, SessionType = SessionType.Race,
                    DetectedUtc = now, ExpiresUtc = now.AddSeconds(30), AdditionalValidity = ConfirmedDisqualification
                };
                disqualification.BindVariant(disqualificationVariants, DisqualificationGroup, variant);
                LiveDiagnostics.Decision(disqualification.Topic, "detected", disqualification.Evidence,
                    disqualification.Text, disqualification.EventId);
            }
            return !busy && disqualification.IsValid(current, now) && disqualification.TryReserve(now) ? disqualification : null;
        }

        /// <summary>
        /// Restrunden inklusive der aktuellen, -1 = unbekannt. Rundenrennen: vom Spiel. Zeitrennen: nur eine
        /// positive Schätzung zur aktuellen Runde bei gültiger Restzeit oder ein explizites letztes-Runden-Signal.
        /// Das Ablaufen der Uhr allein beweist bei möglichen Zusatzrunden keine letzte Runde.
        /// </summary>
        public static int LapsLeft(SessionData s)
        {
            if (s == null) return -1;
            if (s.IsLastLap || (s.SessionType == SessionType.Race && s.SessionPhase == SessionPhase.Checkered)) return 1;
            int planned = Volatile.Read(ref timedFinalLap);
            if (s.SessionHasFixedTime && s.SessionType == SessionType.Race && planned >= 0 && s.SessionId == Volatile.Read(ref timedFinalSession))
            {
                int left = planned - s.CompletedLaps + 1;
                if (left >= 1 && left <= 4) return left;
            }
            if (!s.SessionHasFixedTime)
                return s.SessionLapsRemaining > 0 && s.SessionLapsRemaining < 10000 ? s.SessionLapsRemaining : -1;
            return LapsAreEstimated(s) ? CrewChiefV4.Events.Fuel.EstimatedLapsRemaining : -1;
        }

        public static bool LapsAreEstimated(SessionData s)
        {
            int estimate = CrewChiefV4.Events.Fuel.EstimatedLapsRemaining;
            return s != null && s.SessionHasFixedTime && !s.IsLastLap && s.SessionPhase != SessionPhase.Checkered &&
                Finite(s.SessionTimeRemaining) && s.SessionTimeRemaining > 0 &&
                CrewChiefV4.Events.Fuel.EstimatedLapsRemainingAtLap == s.CompletedLaps && estimate > 0 && estimate < 10000;
        }

        public static bool OnLastLap(SessionData s)
        {
            return s != null && s.SessionType == SessionType.Race && (s.SessionPhase == SessionPhase.Checkered || (!LapsAreEstimated(s) && LapsLeft(s) == 1));
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private static int timedFinalSession = int.MinValue, timedFinalLap = -1;

        /// <summary>
        /// Zeitrennen bei Ablauf der Uhr: Wer zuerst über die Linie kommt – der Führende oder wir –, entscheidet, ob unsere laufende
        /// Runde die letzte ist. Gleiches Tempo angenommen, zählt der Anteil der Runde. Zusatzrunden des Formats kommen dazu.
        /// Gibt die Rundenzahl (CompletedLaps während der letzten Runde) zurück oder -1, wenn es nicht belegt ist.
        /// </summary>
        internal static int PlanTimedFinish(GameStateData current)
        {
            var s = current.SessionData;
            float length = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            int extra = Math.Max(0, Math.Min(5, s.ExtraLapsAfterTimedSessionComplete));
            bool currentIsLast;
            if (s.OverallPosition == 1) currentIsLast = true;
            else
            {
                OpponentData leader = null;
                foreach (var o in current.OpponentData.Values)
                    if (o != null && o.IsActive && o.OverallPosition == 1) { if (leader != null) return -1; leader = o; }
                float own = current.PositionAndMotionData.DistanceRoundTrack;
                if (leader == null || !(length > 100) || !Finite(leader.DistanceRoundTrack) || !Finite(own) ||
                    leader.DistanceRoundTrack < 0 || own < 0 || own > length * 1.01f) return -1;
                double leaderShare = (leader.DistanceRoundTrack % length) / length, ownShare = own / length;
                currentIsLast = leaderShare >= ownShare; // Der Führende ist näher an der Linie und nimmt die Flagge vor uns
            }
            int lap = s.CompletedLaps + (currentIsLast ? 0 : 1) + extra;
            Volatile.Write(ref timedFinalLap, lap);
            Volatile.Write(ref timedFinalSession, s.SessionId);
            return lap;
        }

        internal static void ForgetTimedFinish() { Volatile.Write(ref timedFinalLap, -1); Volatile.Write(ref timedFinalSession, int.MinValue); }

        private static bool TimedClockElapsed(SessionData s)
        {
            return s.SessionHasFixedTime && s.SessionTimeRemaining == 0 && Finite(s.SessionTotalRunTime) &&
                s.SessionTotalRunTime > 0 && Finite(s.SessionRunningTime) && s.SessionRunningTime >= s.SessionTotalRunTime;
        }

        /// <summary>Ein Telemetrie-Tick. gapAhead/gapBehind in Sekunden, &lt;= 0 = unbekannt. Liefert den Funkspruch oder null.</summary>
        internal string ReferenceTemplate { get; private set; }
        internal bool IncludesFrontGap { get; private set; }
        internal string CoreCall { get; private set; }

        public string Evaluate(GameStateData previous, GameStateData current, float gapAhead, float gapBehind, bool minimalMode,
            bool preciseFront = true, string rivalName = null, string finalTactic = null, string driverName = null)
        {
            ReferenceTemplate = null;
            IncludesFrontGap = false;
            CoreCall = null;
            if (previous == null || current == null || current.SessionData.SessionType != SessionType.Race ||
                previous.SessionData.SessionType != SessionType.Race || previous.SessionData.SessionId != current.SessionData.SessionId)
            {
                return null;
            }
            var s = current.SessionData;
            var ps = previous.SessionData;
            bool separate = RaceRadioLanguage.SeparateClass(s);
            string rank = s.ClassPosition > 0 ? RaceRadioLanguage.Rank(s, s.ClassPosition) + "." : "";
            string duel = separate ? "" : Duel(gapAhead, gapBehind);
            if (s.SessionPhase == SessionPhase.Finished && ps.SessionPhase != SessionPhase.Finished && !finishCalled)
            {
                finishCalled = true;
                return language.Finish(s);
            }
            if (s.SessionPhase == SessionPhase.Checkered && ps.SessionPhase == SessionPhase.Green && lastCalledLap != s.CompletedLaps)
            {
                lastCalledLap = s.CompletedLaps;
                return RadioLanguage.T("Zielflagge ist draußen, bring die Runde sauber zu Ende.", "Chequered flag is out, bring the lap home clean.", "Bandera a cuadros, termina la vuelta limpio.") +
                    (s.ClassPosition == 1 ? (separate ? RadioLanguage.T(" Du führst deine Klasse.", " You're leading your class.", " Lideras tu clase.") :
                        RadioLanguage.T(" Du führst, jetzt konzentriert nach Hause bringen.", " You're leading, bring it home focused.", " Vas líder, llévalo a casa concentrado.")) :
                    rank.Length > 0 ? " " + rank : "") + (finalTactic == null ? duel : " " + finalTactic);
            }
            if (!timedClockCalled && s.SessionPhase == SessionPhase.Green && !current.PitData.InPitlane && TimedClockElapsed(s))
            {
                timedClockCalled = true;
                // Rob 16.11.: Nach Ablauf der Uhr muss klar sein, ob dies die letzte Runde ist.
                int finalLap = PlanTimedFinish(current);
                if (finalLap == s.CompletedLaps)
                {
                    lastCalledLap = s.CompletedLaps;
                    string name = AiPersonas.CleanForRadio(driverName ?? "", 30, false);
                    CoreCall = RadioLanguage.T("Die Uhr ist abgelaufen" + (name.Length > 0 ? ", " + name : "") + ". Das ist deine letzte Runde!",
                        "The clock has run out" + (name.Length > 0 ? ", " + name : "") + ". This is your last lap!",
                        "Se acabó el tiempo" + (name.Length > 0 ? ", " + name : "") + ". ¡Esta es tu última vuelta!") + (rank.Length > 0 ? " " + rank : "");
                    if (s.ClassPosition != 1 && finalTactic == null) CoreCall += " " + LastLapMotivation[(lastLapVariant++) % LastLapMotivation.Length];
                    string front = FrontChase(current, preciseFront, gapAhead, rivalName);
                    IncludesFrontGap = front != null;
                    return CoreCall + (finalTactic == null ? front ?? duel : " " + finalTactic);
                }
                if (finalLap == s.CompletedLaps + 1)
                    return RadioLanguage.T("Die Uhr ist abgelaufen. Noch diese Runde, dann kommt die letzte.", "The clock has run out. Finish this lap, then one more.",
                        "Se acabó el tiempo. Termina esta vuelta y queda una más.");
                return RadioLanguage.T("Die Rennzeit ist abgelaufen. Achte auf die Zielflagge.", "Race time is up. Watch for the chequered flag.", "Se acabó el tiempo de carrera. Atento a la bandera a cuadros.");
            }
            if (s.IsNewLap && s.SessionPhase == SessionPhase.Green)
            {
                pendingLap = s.CompletedLaps;
                pendingSince = s.SessionRunningTime;
            }
            if (s.IsLastLap && s.SessionPhase == SessionPhase.Green && !current.PitData.InPitlane &&
                !ps.IsLastLap && lastCalledLap != s.CompletedLaps)
            {
                pendingLap = s.CompletedLaps;
                pendingSince = s.SessionRunningTime;
            }
            if (pendingLap != s.CompletedLaps || lastCalledLap == s.CompletedLaps || s.SessionPhase != SessionPhase.Green)
            {
                return null;
            }
            // die Schätzung für Zeitrennen rechnet Crew Chief im selben Moment, deshalb bis zu 3 s darauf warten
            int left = LapsLeft(s);
            if (left < 0 && s.SessionRunningTime - pendingSince < 3)
            {
                return null;
            }
            pendingLap = -1;
            if (left == 1 && !LapsAreEstimated(s))
            {
                lastCalledLap = s.CompletedLaps;
                string front = FrontChase(current, preciseFront, gapAhead, rivalName);
                IncludesFrontGap = front != null;
                CoreCall = (LapsAreEstimated(s) ? RadioLanguage.T("Voraussichtlich letzte Runde! ", "Probably the last lap! ", "¡Probablemente última vuelta! ") :
                        RadioLanguage.T("Letzte Runde! ", "Last lap! ", "¡Última vuelta! ")) +
                    (s.ClassPosition == 1 ? (separate ? RadioLanguage.T("Du führst deine Klasse. Bring es sauber nach Hause.", "You're leading your class. Bring it home clean.", "Lideras tu clase. Llévalo a casa limpio.") :
                        RadioLanguage.T("Du führst. Bleib ruhig, bring es sauber nach Hause.", "You're leading. Stay calm, bring it home clean.", "Vas líder. Tranquilo, llévalo a casa limpio.")) :
                        rank.Length > 0 ? rank : "");
                string name = AiPersonas.CleanForRadio(driverName ?? "", 30, false);
                if (name.Length != 0) CoreCall = name + ", " + char.ToLower(CoreCall[0], RadioLanguage.Culture) + CoreCall.Substring(1);
                if (s.ClassPosition != 1 && finalTactic == null)
                    CoreCall += name.Length == 0 ? " " + RadioLanguage.T("Bleib konzentriert.", "Stay focused.", "Mantén la concentración.") :
                        " " + LastLapMotivation[(lastLapVariant++) % LastLapMotivation.Length];
                return CoreCall + (finalTactic == null ? front ?? duel : " " + finalTactic);
            }
            if (!minimalMode && left == 2)
            {
                lastCalledLap = s.CompletedLaps;
                string front = FrontChase(current, preciseFront, gapAhead, rivalName);
                IncludesFrontGap = front != null;
                CoreCall = (LapsAreEstimated(s) ? RadioLanguage.T("Voraussichtlich noch zwei Runden", "Probably two laps to go", "Probablemente quedan dos vueltas") :
                        RadioLanguage.T("Noch zwei Runden", "Two laps to go", "Quedan dos vueltas")) +
                    (rank.Length > 0 ? ", " + rank : ".");
                return CoreCall + front;
            }
            return null;
        }

        private static string FrontChase(GameStateData current, bool precise, float gap, string rivalName)
        {
            if (!precise || current.SessionData.ClassPosition <= 1 || !(gap > .05f && gap <= 3f) || current.FlagData.isLocalYellow)
                return null;
            return string.IsNullOrWhiteSpace(rivalName)
                ? RadioLanguage.T(" Nach vorn ", " Gap ahead ", " Por delante ") + GermanRadioText.Interval(gap) + RadioLanguage.T(". Bleib dran.", ". Stay with it.", ". Sigue ahí.")
                : " " + RadioLanguage.T(rivalName + " liegt " + GermanRadioText.Interval(gap) + " vor dir. Bleib dran.",
                    rivalName + " is " + GermanRadioText.Interval(gap) + " ahead. Stay with it.",
                    rivalName + " está a " + GermanRadioText.Interval(gap) + " por delante. Sigue ahí.");
        }

        private static string Duel(float gapAhead, float gapBehind)
        {
            if (gapBehind > 0 && gapBehind < 1.5f)
            {
                return RadioLanguage.T(" Hinter dir ", " Behind you ", " Detrás tienes ") + SaraLocal.Seconds(gapBehind) + RadioLanguage.T(", bleib aufmerksam.", ", stay alert.", ", atento.");
            }
            if (gapAhead > 0 && gapAhead < 1.5f)
            {
                return RadioLanguage.T(" Nach vorn ", " Gap ahead ", " Por delante ") + SaraLocal.Seconds(gapAhead) + ".";
            }
            return "";
        }

        private static string Gained(SessionData s)
        {
            int start = s.SessionStartClassPosition;
            if (start <= 0 || start == s.ClassPosition)
            {
                return "";
            }
            int diff = start - s.ClassPosition;
            return diff > 0 ? RadioLanguage.T(" " + diff + (diff == 1 ? " Platz" : " Plätze") + " gutgemacht.",
                " " + diff + (diff == 1 ? " place" : " places") + " gained.",
                " " + diff + (diff == 1 ? " posición ganada." : " posiciones ganadas.")) : "";
        }
    }
}
