using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennverlauf als Geschichte (Rob 23.11.: „die Crew soll das Rennen verstehen, nicht nur Zahlen ansagen“).
    ///
    /// Aus den Rundenabständen zum selben Vorder- und Hintermann, die der Rennkopf ohnehin misst, erkennt Ben Zusammenhänge
    /// über mehrere Runden:
    ///  - Zweikampf: seit drei oder mehr Runden unter einer Sekunde am selben Gegner (nach vorn oder nach hinten);
    ///  - wie angekündigt: die Hochrechnung „in X Runden bist du dran“ ist eingetroffen.
    /// Hochrechnungen selbst macht weiter der Abstandstrend; hier wird nur festgestellt, was eingetreten ist.
    ///
    /// Freigefahren und abgesetzt erkennt Crew Chief selbst („Abstand nach hinten bzw. vorn wächst“). Rob 23.11.: gute
    /// Erkennungen von Crew Chief behalten, die Crew bewertet nur. Deshalb hängt Ben an genau diese Meldung einen kurzen
    /// Satz an, wenn der Gegner eben noch im Zweikampf war (<see cref="CommentOnGrowingGap"/>); eine eigene Meldung dazu gibt es nicht.
    ///
    /// Wie am echten Kommandostand bekommt eine solche Meldung manchmal eine ganz kurze Reaktion eines Kollegen (Mik): erst nach
    /// dem tatsächlichen Ende der Meldung, mit kurzer Pause, nur solange die Lage noch dieselbe ist, und ohne neue Zahlen.
    /// Ohne sprechbaren Gegnernamen bleibt die Geschichte stumm, damit kein holpriger Satz entsteht.
    /// </summary>
    internal sealed class RaceStorylineWatch
    {
        internal const float DuelGap = 1.0f, FreeGap = 2.0f; // FreeGap: ab hier endet ein Zweikampf
        internal const int DuelLaps = 3, RepeatLaps = 4, ReleaseLaps = 3;
        internal static readonly TimeSpan FollowUpDelay = TimeSpan.FromSeconds(.8), FollowUpLife = TimeSpan.FromSeconds(8);
        internal const double FollowUpChance = .5;

        /// <summary>Zufall für die Reaktion des Kollegen (0..1); in Tests ersetzbar.</summary>
        internal static Func<double> FollowUpRoll = () => { lock (random) return random.NextDouble(); };
        private static readonly Random random = new Random();

        internal enum Story { DuelFront, DuelRear, Free, Escaped, KeptAhead, KeptBehind }

        /// <summary>Bens Bewertung zu einer Crew-Chief-Abstandsmeldung und was nach ihrem Ende passiert (Reaktion des Kollegen).</summary>
        internal sealed class StoryComment
        {
            public Story Kind;
            public string Text;
            public Action<DateTime> PlaybackCompleted;
        }

        private sealed class Side
        {
            public string Id;
            public int DuelSince = -1, LastCloseLap = -100, CloseLaps, ToldLap = -100;
            public string ToldId;
            internal void Follow(string id)
            {
                if (id == Id) return;
                Id = id; DuelSince = -1; LastCloseLap = -100; CloseLaps = 0;
            }
        }

        private sealed class Expectation { public int Session; public string Id; public bool Ahead; public int Lap; }

        private static readonly object expectGate = new object();
        private static readonly List<Expectation> expectations = new List<Expectation>();

        /// <summary>
        /// Der Abstandstrend hat angekündigt, dass sich zwei Autos etwa in Runde <paramref name="lap"/> treffen. Wird es wahr,
        /// meldet Ben „wie angekündigt“.
        /// </summary>
        internal static void Expect(int session, string rivalId, bool ahead, int lap)
        {
            if (rivalId == null || lap <= 0) return;
            lock (expectGate)
            {
                expectations.RemoveAll(e => e.Ahead == ahead || e.Session != session);
                expectations.Add(new Expectation { Session = session, Id = rivalId, Ahead = ahead, Lap = lap });
            }
        }

        internal static void ForgetExpectations() { lock (expectGate) expectations.Clear(); }

        private readonly Side front = new Side(), rear = new Side();
        private int session = int.MinValue, lastLap = -1;
        private ObservedRaceCall call;
        private volatile ObservedRaceCall followUp;

        internal void Reset()
        {
            session = int.MinValue; lastLap = -1; call = null; followUp = null;
            foreach (var s in new[] { front, rear }) { s.Id = null; s.DuelSince = -1; s.LastCloseLap = -100; s.CloseLaps = 0; s.ToldLap = -100; s.ToldId = null; }
        }

        /// <summary>Wertet die Rundenabstände des Rennkopfs aus, einmal je gemessener Runde.</summary>
        internal void Observe(GameStateData gs, RaceMind mind, Func<RaceDataContext> context, DateTime now)
        {
            if (gs == null || mind == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) { Reset(); ForgetExpectations(); } return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (s.SessionPhase != SessionPhase.Green || mind.SampledLap == lastLap || mind.SampledLap < 1) return;
            lastLap = mind.SampledLap;
            var ctx = context?.Invoke();
            Evaluate(gs, mind.SampledLap, mind.Front, mind.Rear, ctx == null || ctx.SendDriverNames, now);
        }

        /// <summary>Eine gemessene Runde auswerten (Rundenabstände zum selben Vorder- und Hintermann).</summary>
        internal void Evaluate(GameStateData gs, int lap, GapHistory frontHistory, GapHistory rearHistory, bool names, DateTime now)
        {
            var s = gs.SessionData;

            var frontCar = RaceDataTools.Neighbour(gs, true);
            var rearCar = RaceDataTools.Neighbour(gs, false);
            Story? story = null; float gap = -1; OpponentData rival = null; int laps = 0;
            foreach (bool ahead in new[] { true, false })
            {
                var side = ahead ? front : rear;
                var h = ahead ? frontHistory : rearHistory;
                var car = ahead ? frontCar : rearCar;
                string id = car == null ? null : OpponentStopLog.Key(car);
                if (id == null || h.Id != id || h.Gaps.Count == 0) { side.Follow(null); continue; }
                side.Follow(id);
                float last = h.Gaps[h.Gaps.Count - 1];
                bool close = last < DuelGap;
                if (close)
                {
                    if (side.DuelSince < 0) side.DuelSince = lap;
                    side.LastCloseLap = lap; side.CloseLaps = lap - side.DuelSince + 1;
                }
                else if (last > FreeGap) side.DuelSince = -1;
                if (story != null) continue;

                if (close && ExpectationMet(s.SessionId, id, ahead, lap))
                { story = ahead ? Story.KeptAhead : Story.KeptBehind; rival = car; gap = last; continue; }
                if (close && side.CloseLaps >= DuelLaps && (side.ToldId != id || lap - side.ToldLap >= RepeatLaps))
                { story = ahead ? Story.DuelFront : Story.DuelRear; rival = car; gap = last; laps = side.CloseLaps; }
            }
            ExpireExpectations(s.SessionId, lap);
            if (story == null || call != null) return;
            if (IsAhead(story.Value) && s.ClassPosition <= 1) return;
            if (LapWatch.LapsLeft(s) == 1) return;
            string name = names && rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
            if (name.Length == 0) { LiveDiagnostics.Decision("story", "suppressed", "no_speakable_rival_name", story.Value.ToString()); return; }
            var told = IsAhead(story.Value) ? front : rear;
            told.ToldId = OpponentStopLog.Key(rival); told.ToldLap = lap;
            string text = Text(story.Value, name, laps, gap);
            int position = s.ClassPosition;
            string rivalId = OpponentStopLog.Key(rival);
            bool aheadSide = IsAhead(story.Value);
            Func<GameStateData, bool> still = live => live.SessionData.ClassPosition == position && !live.PitData.InPitlane &&
                RaceDataTools.Neighbour(live, aheadSide) is OpponentData n && OpponentStopLog.Key(n) == rivalId;
            var kind = story.Value;
            call = new ObservedRaceCall
            {
                Topic = Topic(kind), Text = text, Evidence = "measured_class_gap_per_lap_same_rival", Member = TeamMember.Ben,
                Priority = kind == Story.KeptAhead || kind == Story.KeptBehind ? 4 : 3,
                Position = position, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now,
                ExpiresUtc = now.AddSeconds(15), AdditionalValidity = still
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        /// <summary>
        /// Crew Chief meldet, dass der Abstand zum direkten Gegner wächst. War genau dieser Gegner in den letzten Runden noch im
        /// Zweikampf (mindestens zwei Runden unter einer Sekunde), bewertet Ben das mit einem Satz ohne Zahlen: Den Abstand selbst
        /// sagt schon die Crew-Chief-Meldung. Sonst null. Jeder Zweikampf wird nur einmal so bewertet.
        /// </summary>
        internal StoryComment CommentOnGrowingGap(GameStateData gs, OpponentData op, bool ahead, bool names)
        {
            if (gs == null || op == null || gs.SessionData.SessionType != SessionType.Race) return null;
            var s = gs.SessionData;
            var side = ahead ? front : rear;
            string id = OpponentStopLog.Key(op);
            if (side.Id != id || side.CloseLaps < 2 || side.LastCloseLap < 0 || s.CompletedLaps - side.LastCloseLap > ReleaseLaps) return null;
            if (ahead && s.ClassPosition <= 1 || LapWatch.LapsLeft(s) == 1) return null;
            string name = names && op.CanUseName ? RadioPhraseCatalog.SafeRivalName(op.DriverRawName) : "";
            if (name.Length == 0) return null;
            side.CloseLaps = 0; side.LastCloseLap = -100; side.ToldId = id; side.ToldLap = s.CompletedLaps;
            var kind = ahead ? Story.Escaped : Story.Free;
            int position = s.ClassPosition, sessionId = s.SessionId;
            var type = s.SessionType;
            Func<GameStateData, bool> still = live => live.SessionData.ClassPosition == position && !live.PitData.InPitlane &&
                RaceDataTools.Neighbour(live, ahead) is OpponentData n && OpponentStopLog.Key(n) == id;
            string text = Text(kind, name, 0, 0);
            LiveDiagnostics.Decision(Topic(kind), "attached", "crew_chief_gap_growing_after_fight", text);
            return new StoryComment { Kind = kind, Text = text, PlaybackCompleted = ended => PlanFollowUp(kind, ended, still, position, sessionId, type) };
        }

        private static bool IsAhead(Story s) => s == Story.DuelFront || s == Story.Escaped || s == Story.KeptAhead;

        internal static string Topic(Story s)
        {
            switch (s)
            {
                case Story.DuelFront: return "story-duel-front";
                case Story.DuelRear: return "story-duel-rear";
                case Story.Free: return "story-free";
                case Story.Escaped: return "story-escaped";
                case Story.KeptAhead: return "story-kept-ahead";
                default: return "story-kept-behind";
            }
        }

        private static bool ExpectationMet(int session, string id, bool ahead, int lap)
        {
            lock (expectGate)
            {
                var hit = expectations.Find(e => e.Session == session && e.Id == id && e.Ahead == ahead && lap >= e.Lap - 1 && lap <= e.Lap + 2);
                if (hit == null) return false;
                expectations.Remove(hit);
                return true;
            }
        }

        private static void ExpireExpectations(int session, int lap)
        {
            lock (expectGate) expectations.RemoveAll(e => e.Session != session || lap > e.Lap + 2);
        }

        /// <summary>Kurze Reaktion eines Kollegen, nur auf freie Fahrt oder einen weggefahrenen Vordermann.</summary>
        private void PlanFollowUp(Story kind, DateTime ended, Func<GameStateData, bool> still, int position, int sessionId, SessionType type)
        {
            if (kind != Story.Free && kind != Story.Escaped) return;
            if (FollowUpRoll() >= FollowUpChance) { LiveDiagnostics.Decision("story-followup", "skipped", "not_every_call_gets_a_reply"); return; }
            DateTime at = ended + FollowUpDelay;
            followUp = new ObservedRaceCall
            {
                Topic = "story-followup", Text = FollowUpText(kind), Evidence = "colleague_reacts_to_" + Topic(kind), Member = TeamMember.Mik,
                Priority = 2, Position = position, SessionId = sessionId, SessionType = type, DetectedUtc = at, ExpiresUtc = at + FollowUpLife,
                AdditionalValidity = still
            };
            LiveDiagnostics.Decision("story-followup", "detected", followUp.Evidence, followUp.Text, followUp.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, Func<TeamMember, bool> enabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (followUp != null && (now > followUp.ExpiresUtc || followUp.DeliveryStartedUtc != DateTime.MinValue ||
                now >= followUp.DetectedUtc && !followUp.IsValid(gs, now))) followUp = null;
            if (busy) return null;
            if (followUp != null && now >= followUp.DetectedUtc && (enabled == null || enabled(followUp.Member)) && followUp.TryReserve(now)) return followUp;
            if (call == null || enabled != null && !enabled(call.Member)) return null;
            return call.TryReserve(now) ? call : null;
        }

        // ---------- Formulierungen (je Lage sechs, Reaktionen fünf; DE/EN/ES) ----------

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal static string Text(Story story, string name, int laps, float gap)
        {
            string who = name, Who = Cap(name), n = laps.ToString(RadioLanguage.Culture);
            switch (story)
            {
                case Story.DuelFront:
                    return RadioVariants.Pick("story-duel-front",
                        RadioLanguage.T("Seit " + n + " Runden klebst du an " + who + ". Geduld, die Lücke kommt.",
                            n + " laps glued to " + who + ". Patience, the gap will come.",
                            n + " vueltas pegado a " + who + ". Paciencia, el hueco llegará."),
                        RadioLanguage.T(n + " Runden jetzt hinter " + who + ". Ruhig bleiben, sauber rausbeschleunigen.",
                            n + " laps behind " + who + " now. Stay calm, nail the exits.",
                            "Ya son " + n + " vueltas detrás de " + who + ". Tranquilo, clava las salidas."),
                        RadioLanguage.T("Du bist seit " + n + " Runden an " + who + " dran. Druck machen, aber nichts erzwingen.",
                            "You've been on " + who + " for " + n + " laps. Keep the pressure on, don't force it.",
                            "Llevas " + n + " vueltas encima de " + who + ". Mantén la presión, sin forzar."),
                        RadioLanguage.T(Who + " hat dich seit " + n + " Runden im Spiegel. Bleib da, der Druck zählt.",
                            Who + " has had you in the mirrors for " + n + " laps. Stay there, that pressure counts.",
                            Who + " te lleva " + n + " vueltas en los retrovisores. Sigue ahí, esa presión cuenta."),
                        RadioLanguage.T("Das geht jetzt " + n + " Runden so mit " + who + ". Such dir die eine Stelle, wo du besser bist.",
                            "Same picture with " + who + " for " + n + " laps. Find the one place where you're stronger.",
                            n + " vueltas con la misma imagen frente a " + who + ". Busca el punto donde eres más fuerte."),
                        RadioLanguage.T("Zweikampf mit " + who + ", schon " + n + " Runden. Kopf frei, deine Chance kommt.",
                            "Fight with " + who + ", " + n + " laps of it now. Clear head, your chance will come.",
                            "Duelo con " + who + ", ya " + n + " vueltas. Cabeza fría, tu oportunidad llegará."));
                case Story.DuelRear:
                    return RadioVariants.Pick("story-duel-rear",
                        RadioLanguage.T(Who + " hängt seit " + n + " Runden hinter dir. Du machst das sauber, weiter so.",
                            Who + " has been behind you for " + n + " laps. You're doing it clean, keep going.",
                            Who + " lleva " + n + " vueltas detrás de ti. Lo estás haciendo limpio, sigue así."),
                        RadioLanguage.T(n + " Runden Druck von " + who + ", und du hältst. Ruhig bleiben, deine Linie.",
                            n + " laps of pressure from " + who + ", and you're holding. Stay calm, your line.",
                            n + " vueltas de presión de " + who + ", y aguantas. Tranquilo, tu línea."),
                        RadioLanguage.T("Seit " + n + " Runden verteidigst du gegen " + who + ". Keine Fehler, dann bleibt es so.",
                            n + " laps defending against " + who + ". No mistakes and it stays that way.",
                            n + " vueltas defendiéndote de " + who + ". Sin errores y seguirá así."),
                        RadioLanguage.T(Who + " probiert es jetzt seit " + n + " Runden. Konzentrier dich auf deine Ausfahrten.",
                            Who + " has been trying for " + n + " laps. Focus on your exits.",
                            Who + " lo intenta desde hace " + n + " vueltas. Céntrate en las salidas."),
                        RadioLanguage.T(n + " Runden " + who + " im Spiegel. Atmen, sauber fahren, Blick nach vorn.",
                            n + " laps with " + who + " in the mirrors. Breathe, drive clean, eyes forward.",
                            n + " vueltas con " + who + " en los retrovisores. Respira, conduce limpio, mirada al frente."),
                        RadioLanguage.T("Das Duell mit " + who + " läuft seit " + n + " Runden. Du gibst nichts her, gut so.",
                            "The fight with " + who + " has run " + n + " laps. You're giving nothing away, good.",
                            "El duelo con " + who + " dura ya " + n + " vueltas. No regalas nada, bien."));
                case Story.Free:
                    return RadioVariants.Pick("story-free",
                        RadioLanguage.T("Den Zweikampf mit " + who + " hast du hinter dir. Jetzt deinen Rhythmus fahren.",
                            "The fight with " + who + " is behind you. Now drive your rhythm.",
                            "El duelo con " + who + " queda atrás. Ahora tu ritmo."),
                        RadioLanguage.T(Who + " ist abgeschüttelt. Gut gemacht, durchatmen.",
                            Who + " is shaken off. Well done, breathe.",
                            Who + " se ha quedado. Bien hecho, respira."),
                        RadioLanguage.T("Das Duell mit " + who + " ist entschieden. Gleichmäßig weiter.",
                            "The duel with " + who + " is settled. Keep it consistent.",
                            "El duelo con " + who + " está decidido. Sigue regular."),
                        RadioLanguage.T("Von " + who + " hast du dich gelöst. Sauber weiter, keine Risiken.",
                            "You've broken away from " + who + ". Clean laps, no risks.",
                            "Te has escapado de " + who + ". Vueltas limpias, sin riesgos."),
                        RadioLanguage.T("Der Druck von " + who + " ist weg. Jetzt saubere Runden.",
                            "The pressure from " + who + " is gone. Now clean laps.",
                            "Ya no tienes la presión de " + who + ". Ahora vueltas limpias."),
                        RadioLanguage.T(Who + " kann nicht mehr folgen. Stark verteidigt.",
                            Who + " can't follow any more. Strong defending.",
                            Who + " ya no puede seguirte. Gran defensa."));
                case Story.Escaped:
                    return RadioVariants.Pick("story-escaped",
                        RadioLanguage.T("Nach dem Zweikampf mit " + who + " erst mal neu ansetzen. Nicht überfahren.",
                            "After the fight with " + who + ", reset first. Don't overdrive.",
                            "Tras el duelo con " + who + ", primero vuelve a empezar. No te pases."),
                        RadioLanguage.T(Who + " ist erst mal weg. Keine Brechstange, deinen Rhythmus finden.",
                            Who + " is gone for now. No forcing it, find your rhythm.",
                            Who + " se ha ido por ahora. Sin forzar, busca tu ritmo."),
                        RadioLanguage.T("Bei " + who + " nicht hinterherhetzen, fahr dein Tempo.",
                            "Don't chase " + who + ", drive your pace.",
                            "No persigas a " + who + ", tu ritmo."),
                        RadioLanguage.T("Der Zweikampf mit " + who + " ist vorbei. Reifen schonen, das zählt später.",
                            "The fight with " + who + " is over. Look after the tyres, it counts later.",
                            "El duelo con " + who + " ha terminado. Cuida los neumáticos, contará más tarde."),
                        RadioLanguage.T(Who + " hat sich gelöst. Ruhig bleiben, jetzt keine Fehler.",
                            Who + " has broken away. Stay calm, no mistakes now.",
                            Who + " se ha escapado. Tranquilo, ahora sin errores."),
                        RadioLanguage.T("Durchatmen nach dem Duell mit " + who + ". Neu ansetzen.",
                            "Breathe after the duel with " + who + ". Reset.",
                            "Respira tras el duelo con " + who + ". Vuelve a empezar."));
                case Story.KeptAhead:
                    return RadioVariants.Pick("story-kept-ahead",
                        RadioLanguage.T("Wie angekündigt: du bist an " + who + " dran.",
                            "As called: you're on " + who + " now.",
                            "Como te dije: ya estás encima de " + who + "."),
                        RadioLanguage.T("Da ist " + who + ", genau wie gerechnet. Jetzt geduldig angreifen.",
                            "There's " + who + ", right on schedule. Attack with patience.",
                            "Ahí está " + who + ", justo según lo calculado. Ataca con paciencia."),
                        RadioLanguage.T("Die Rechnung ging auf, " + who + " ist in Reichweite.",
                            "The maths worked, " + who + " is in range.",
                            "Las cuentas salieron, " + who + " está a tiro."),
                        RadioLanguage.T(Who + " ist da, wie vorhergesagt. Erst ansaugen, dann zuschlagen.",
                            Who + " is there, as predicted. Get the tow first, then strike.",
                            Who + " ya está ahí, como se preveía. Primero el rebufo, luego el ataque."),
                        RadioLanguage.T("Aufgeholt, wie geplant. " + Who + " liegt direkt vor dir.",
                            "Caught, as planned. " + Who + " is right in front of you.",
                            "Alcanzado, como estaba previsto. " + Who + " está justo delante."),
                        RadioLanguage.T("Gut gearbeitet, " + who + " ist eingeholt. Jetzt Druck machen, sauber bleiben.",
                            "Good work, " + who + " is caught. Now pressure, stay clean.",
                            "Buen trabajo, " + who + " alcanzado. Ahora presión, sigue limpio."));
                default:
                    return RadioVariants.Pick("story-kept-behind",
                        RadioLanguage.T("Wie angekündigt ist " + who + " jetzt dran. Linie halten, keine Tür aufmachen.",
                            "As called, " + who + " is with you now. Hold your line, don't open the door.",
                            "Como te dije, " + who + " ya está contigo. Mantén la línea, no abras la puerta."),
                        RadioLanguage.T(Who + " ist da, wie erwartet. Ruhig verteidigen, sauber bremsen.",
                            Who + " is there, as expected. Defend calmly, brake clean.",
                            Who + " ya está ahí, como se esperaba. Defiende tranquilo, frena limpio."),
                        RadioLanguage.T("Jetzt ist " + who + " hinter dir angekommen. Konzentrier dich auf deine Ausfahrten.",
                            Who + " has arrived behind you. Focus on your exits.",
                            Who + " ya ha llegado detrás. Céntrate en las salidas."),
                        RadioLanguage.T("Wie gerechnet: " + who + " sitzt dir im Nacken. Klar fahren, nicht nervös werden.",
                            "As calculated: " + who + " is right on your tail. Drive clear, don't get nervous.",
                            "Como se calculó: " + who + " está pegado a ti. Conduce claro, sin nervios."),
                        RadioLanguage.T(Who + " hat aufgeschlossen. Du bestimmst die Linie, bleib ruhig.",
                            Who + " has closed up. You own the line, stay calm.",
                            Who + " se ha acercado. Tú marcas la línea, tranquilo."),
                        RadioLanguage.T("Da ist " + who + ". Jetzt zählt jede saubere Kurve.",
                            "There's " + who + ". Every clean corner counts now.",
                            "Ahí está " + who + ". Ahora cuenta cada curva limpia."));
            }
        }

        /// <summary>Mik reagiert kurz auf Bens Meldung: ohne Zahlen, ohne Namen, nur Ton.</summary>
        internal static string FollowUpText(Story story)
        {
            if (story == Story.Escaped)
                return RadioVariants.Pick("story-followup-reset",
                    RadioLanguage.T("Neu ansetzen.", "Reset and go again.", "Vuelve a empezar."),
                    RadioLanguage.T("Ruhig, Runde für Runde.", "Easy, lap by lap.", "Tranquilo, vuelta a vuelta."),
                    RadioLanguage.T("Kein Stress, dein Tempo.", "No stress, your pace.", "Sin estrés, tu ritmo."),
                    RadioLanguage.T("Durchatmen, weiter geht's.", "Breathe, on we go.", "Respira, seguimos."),
                    RadioLanguage.T("Sauber bleiben, das zahlt sich aus.", "Stay clean, it pays off.", "Sigue limpio, eso compensa."));
            return RadioVariants.Pick("story-followup-relief",
                RadioLanguage.T("Gut gemacht.", "Nicely done.", "Bien hecho."),
                RadioLanguage.T("Sauber.", "Clean.", "Limpio."),
                RadioLanguage.T("Durchatmen, gut so.", "Breathe, good.", "Respira, bien."),
                RadioLanguage.T("Schön, so weiter.", "Lovely, keep it going.", "Muy bien, sigue así."),
                RadioLanguage.T("Das war stark.", "That was strong.", "Eso fue fuerte."));
        }
    }
}
