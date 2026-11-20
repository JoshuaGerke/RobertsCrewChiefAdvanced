using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Endspurt-Briefing (Masterplan Schritt 2, Rob 21.11.2026: „Holst du ihn bis zur Flagge ein?“). Einmal je Rennen, wenn
    /// noch drei bis vier Runden zu fahren sind, ordnet Ben vorn und hinten zusammen ein: Reicht das gemessene Tempo, um den
    /// Vordermann bis zur Flagge zu erreichen? Kommt der Hintermann vorher heran? Grundlage sind nur die vom Rennkopf gemessenen
    /// Abstände und Tempounterschiede je Runde (<see cref="RaceSituation"/>); fehlt eine Messung, sagt Ben dazu nichts. Kennt die
    /// Rennchronik den Hintermann aus einem Duell, kommt der Rückbezug dazu.
    /// </summary>
    internal sealed class EndgameBriefWatch
    {
        private int session = int.MinValue, variant;
        private bool done;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; done = false; call = null; }

        /// <summary>frontName/rearName nur übergeben, wenn Namen erlaubt sind.</summary>
        internal void Observe(GameStateData gs, RaceSituation x, string frontName, string rearName, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (done || s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane || s.CompletedLaps < 2) return;
            int left = LapWatch.LapsLeft(s);
            if (left < 3 || left > 4) return;
            if (x == null || x.SessionId != s.SessionId || !x.Fresh(now)) return;
            done = true;
            string rearStory = rearName != null ? RaceStory.Approaching(s.OpponentKeyBehind, now, variant) : "";
            string text = Compose(x, left, s.ClassPosition, frontName, rearName, rearStory, variant++);
            if (text == null) { LiveDiagnostics.Decision("endgame-brief", "suppressed", "no_measured_gap_or_trend"); return; }
            int position = s.ClassPosition;
            call = new ObservedRaceCall
            {
                Topic = "endgame-brief", Text = text, Evidence = "race_mind_gaps_and_rates_with_laps_left_" + left, Member = TeamMember.Ben, Priority = 5,
                Position = position, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(25),
                AdditionalValidity = live => !live.PitData.InPitlane && live.SessionData.ClassPosition == position
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }

        private static string Pick(int variant, params string[] options) => options[((variant % options.Length) + options.Length) % options.Length];
        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        /// <summary>
        /// Satz aus gemessenen Werten: Einleitung, Lage vorn, Lage hinten. Null, wenn weder vorn noch hinten ein Abstand gemessen ist.
        /// Zum Aufholen bleiben <paramref name="lapsLeft"/> − 1 volle Runden (die laufende zählt nicht ganz).
        /// </summary>
        internal static string Compose(RaceSituation x, int lapsLeft, int position, string frontName, string rearName, string rearStory, int variant)
        {
            int avail = Math.Max(1, lapsLeft - 1);
            string front = position > 1 && x.GapAhead > 0 ? Front(x, avail, frontName, variant) : null;
            string rear = x.GapBehind > 0 ? Rear(x, avail, rearName, variant) : null;
            if (front == null && rear == null) return null;
            string intro = Pick(variant,
                RadioLanguage.T("Endspurt, noch " + lapsLeft + " Runden.", "Final push, " + lapsLeft + " laps to go.", "Recta final, quedan " + lapsLeft + " vueltas."),
                RadioLanguage.T("Noch " + lapsLeft + " Runden, so sieht es aus.", lapsLeft + " laps left, here's the picture.", "Quedan " + lapsLeft + " vueltas, así está la cosa."),
                RadioLanguage.T("Schlussphase, " + lapsLeft + " Runden.", "Closing stages, " + lapsLeft + " laps.", "Fase final, " + lapsLeft + " vueltas."),
                RadioLanguage.T("Bis zur Flagge noch " + lapsLeft + " Runden.", "Flag in " + lapsLeft + " laps.", "Bandera en " + lapsLeft + " vueltas."),
                RadioLanguage.T("Die letzten " + lapsLeft + " Runden.", "The last " + lapsLeft + " laps.", "Las últimas " + lapsLeft + " vueltas."));
            if (position == 1) intro += " " + RadioLanguage.T("Du führst.", "You're leading.", "Vas líder.");
            string text = intro + (front == null ? "" : " " + front) + (rear == null ? "" : " " + rear);
            if (rear != null && !string.IsNullOrEmpty(rearStory)) text += " " + rearStory;
            return text;
        }

        private static string Front(RaceSituation x, int avail, string name, int variant)
        {
            string who = string.IsNullOrWhiteSpace(name) ? RadioLanguage.T("der Vordermann", "the car ahead", "el de delante") : name;
            string gap = GermanRadioText.Interval(x.GapAhead);
            if (x.FrontRate >= GapTrendWatch.MinRate && x.FrontLapsToCatch > 0)
            {
                string rate = GapTrendWatch.Rate(x.FrontRate);
                if (x.FrontLapsToCatch <= avail)
                {
                    string laps = x.FrontLapsToCatch == 1 ? RadioLanguage.T("einer Runde", "one lap", "una vuelta") :
                        RadioLanguage.T("etwa " + x.FrontLapsToCatch + " Runden", "about " + x.FrontLapsToCatch + " laps", "unas " + x.FrontLapsToCatch + " vueltas");
                    return Pick(variant,
                        RadioLanguage.T("Vorn " + who + ", " + gap + ". Du holst " + rate + " pro Runde, das reicht: In " + laps + " bist du dran.",
                            "Ahead " + who + ", " + gap + ". You're gaining " + rate + " a lap, that's enough: on him in " + laps + ".",
                            "Delante " + who + ", a " + gap + ". Recortas " + rate + " por vuelta, alcanza: en " + laps + " lo tienes."),
                        RadioLanguage.T(Cap(who) + " liegt " + gap + " vorn, du bist " + rate + " pro Runde schneller. Bis zur Flagge kommst du ran.",
                            Cap(who) + " is " + gap + " ahead, you're " + rate + " a lap quicker. You'll get there before the flag.",
                            Cap(who) + " va a " + gap + ", eres " + rate + " por vuelta más rápido. Llegas antes de la bandera."),
                        RadioLanguage.T("Auf " + who + " fehlen " + gap + ", bei " + rate + " pro Runde reicht das. Angriff vorbereiten.",
                            gap + " to " + who + ", at " + rate + " a lap that's enough. Get the attack ready.",
                            "Te faltan " + gap + " hasta " + who + ", a " + rate + " por vuelta basta. Prepara el ataque."),
                        RadioLanguage.T(Cap(who) + " ist fällig: " + gap + " Abstand, " + rate + " pro Runde für dich, in " + laps + " an seiner Stoßstange.",
                            Cap(who) + " is there for the taking: " + gap + ", " + rate + " a lap to you, on his bumper in " + laps + ".",
                            Cap(who) + " está a tiro: " + gap + ", " + rate + " por vuelta a tu favor, en su parachoques en " + laps + "."),
                        RadioLanguage.T("Vorn geht was: " + who + " bei " + gap + ", du holst " + rate + " pro Runde.",
                            "There's something ahead: " + who + " at " + gap + ", you're taking " + rate + " a lap.",
                            "Delante hay opción: " + who + " a " + gap + ", le sacas " + rate + " por vuelta."));
                }
                float needed = (x.GapAhead - GapTrendWatch.CatchMargin) / avail;
                string need = GapTrendWatch.Rate(needed);
                return Pick(variant,
                    RadioLanguage.T("Vorn " + who + ", " + gap + ". Mit " + rate + " pro Runde reicht es knapp nicht, du bräuchtest " + need + ".",
                        "Ahead " + who + ", " + gap + ". At " + rate + " a lap it's just short, you'd need " + need + ".",
                        "Delante " + who + ", a " + gap + ". A " + rate + " por vuelta no llega, necesitarías " + need + "."),
                    RadioLanguage.T(Cap(who) + " ist " + gap + " vorn. Du holst " + rate + " pro Runde, für den Angriff bräuchtest du " + need + ".",
                        Cap(who) + " is " + gap + " ahead. You're gaining " + rate + " a lap, to attack you'd need " + need + ".",
                        Cap(who) + " va a " + gap + ". Recortas " + rate + " por vuelta, para atacar necesitarías " + need + "."),
                    RadioLanguage.T("Auf " + who + " fehlen " + gap + ". So wird es eng, " + need + " pro Runde müssten es sein.",
                        gap + " to " + who + ". It's tight, it would take " + need + " a lap.",
                        "Te faltan " + gap + " hasta " + who + ". Va justo, harían falta " + need + " por vuelta."),
                    RadioLanguage.T(Cap(who) + " bei " + gap + ": Du kommst näher, aber bis zur Flagge reicht " + rate + " pro Runde nicht.",
                        Cap(who) + " at " + gap + ": you're closing, but " + rate + " a lap won't do it by the flag.",
                        Cap(who) + " a " + gap + ": te acercas, pero " + rate + " por vuelta no basta antes de la bandera."),
                    RadioLanguage.T("Vorn " + who + ", " + gap + ". Du bist schneller, nur nicht genug: " + need + " pro Runde wären nötig.",
                        "Ahead " + who + ", " + gap + ". You're quicker, just not enough: " + need + " a lap would be needed.",
                        "Delante " + who + ", a " + gap + ". Eres más rápido, pero no basta: harían falta " + need + " por vuelta."));
            }
            if (x.FrontRate <= -GapTrendWatch.MinRate)
                return Pick(variant,
                    RadioLanguage.T(Cap(who) + " zieht weg, " + gap + " vorn.", Cap(who) + " is pulling away, " + gap + " ahead.", Cap(who) + " se escapa, a " + gap + "."),
                    RadioLanguage.T("Vorn " + who + " mit " + gap + ", er ist gerade schneller.", "Ahead " + who + " at " + gap + ", he's quicker right now.", "Delante " + who + " a " + gap + ", ahora va más rápido."),
                    RadioLanguage.T("Nach vorn " + gap + " auf " + who + ", der Abstand wächst.", gap + " to " + who + ", the gap is growing.", gap + " hasta " + who + ", la diferencia crece."),
                    RadioLanguage.T(Cap(who) + " ist " + gap + " vorn und legt zu.", Cap(who) + " is " + gap + " ahead and stretching it.", Cap(who) + " va a " + gap + " y amplía."));
            if (x.GapAhead <= 1.2f)
                return Pick(variant,
                    RadioLanguage.T(Cap(who) + " ist in Reichweite, " + gap + ". Deine Chance kommt.", Cap(who) + " is in range, " + gap + ". Your chance is coming.", Cap(who) + " está a tiro, " + gap + ". Tu ocasión llegará."),
                    RadioLanguage.T("Vorn " + who + ", nur " + gap + ". Druck machen, er muss fehlerfrei bleiben.", "Ahead " + who + ", just " + gap + ". Keep the pressure on, he has to stay perfect.", "Delante " + who + ", solo " + gap + ". Presiona, tiene que no fallar."),
                    RadioLanguage.T(gap + " auf " + who + ". Ein Fehler von ihm reicht.", gap + " to " + who + ". One mistake from him is enough.", gap + " hasta " + who + ". Le basta un error."),
                    RadioLanguage.T(Cap(who) + " direkt vor dir, " + gap + ". Bleib dran.", Cap(who) + " right in front, " + gap + ". Stay with him.", Cap(who) + " justo delante, " + gap + ". No lo sueltes."));
            return Pick(variant,
                RadioLanguage.T("Vorn " + who + " bei " + gap + ", das bleibt ungefähr so.", "Ahead " + who + " at " + gap + ", that's about stable.", "Delante " + who + " a " + gap + ", más o menos estable."),
                RadioLanguage.T("Auf " + who + " fehlen " + gap + ", gleiches Tempo.", gap + " to " + who + ", same pace.", "Te faltan " + gap + " hasta " + who + ", mismo ritmo."),
                RadioLanguage.T(Cap(who) + " liegt " + gap + " vorn, stabil.", Cap(who) + " is " + gap + " ahead, steady.", Cap(who) + " va a " + gap + ", estable."),
                RadioLanguage.T("Nach vorn " + gap + " auf " + who + ", keine Bewegung.", gap + " to " + who + ", no movement.", gap + " hasta " + who + ", sin cambios."));
        }

        private static string Rear(RaceSituation x, int avail, string name, int variant)
        {
            string who = string.IsNullOrWhiteSpace(name) ? RadioLanguage.T("der Hintermann", "the car behind", "el de detrás") : name;
            string gap = GermanRadioText.Interval(x.GapBehind);
            if (x.RearRate >= GapTrendWatch.MinRate && x.RearLapsToCatch > 0)
            {
                string rate = GapTrendWatch.Rate(x.RearRate);
                if (x.RearLapsToCatch <= avail)
                    return Pick(variant,
                        RadioLanguage.T("Hinten " + who + ", " + gap + ", er kommt " + rate + " pro Runde näher. Er wird vor der Flagge dran sein, verteidigen.",
                            "Behind, " + who + ", " + gap + ", closing " + rate + " a lap. He'll be on you before the flag, defend.",
                            "Detrás " + who + ", a " + gap + ", se acerca " + rate + " por vuelta. Te alcanzará antes de la bandera, defiende."),
                        RadioLanguage.T(Cap(who) + " holt " + rate + " pro Runde, " + gap + " hinter dir. Linie sauber, keine Lücke lassen.",
                            Cap(who) + " is taking " + rate + " a lap, " + gap + " behind. Clean lines, leave no gap.",
                            Cap(who) + " recorta " + rate + " por vuelta, a " + gap + ". Trazada limpia, sin huecos."),
                        RadioLanguage.T("Von hinten kommt " + who + ", " + gap + ", " + rate + " pro Runde schneller. Bremspunkte sauber treffen.",
                            who + " is coming from behind, " + gap + ", " + rate + " a lap quicker. Hit your braking points.",
                            "Por detrás viene " + who + ", a " + gap + ", " + rate + " por vuelta más rápido. Clava las frenadas."),
                        RadioLanguage.T(Cap(who) + " schließt auf, " + gap + ". Das wird ein Kampf bis zur Linie.",
                            Cap(who) + " is closing, " + gap + ". This will be a fight to the line.",
                            Cap(who) + " se acerca, a " + gap + ". Será una pelea hasta la línea."),
                        RadioLanguage.T("Hinten wird es eng: " + who + " bei " + gap + ", " + rate + " pro Runde. Ruhig verteidigen.",
                            "It's getting tight behind: " + who + " at " + gap + ", " + rate + " a lap. Defend calmly.",
                            "Detrás se complica: " + who + " a " + gap + ", " + rate + " por vuelta. Defiende con calma."));
                return Pick(variant,
                    RadioLanguage.T("Hinten " + who + " kommt " + rate + " pro Runde näher, " + gap + ". Bis zur Flagge reicht es ihm nicht.",
                        "Behind, " + who + " is closing " + rate + " a lap, " + gap + ". It won't be enough for him by the flag.",
                        "Detrás " + who + " se acerca " + rate + " por vuelta, a " + gap + ". No le llega antes de la bandera."),
                    RadioLanguage.T(Cap(who) + " ist " + gap + " zurück und etwas schneller, das hält.",
                        Cap(who) + " is " + gap + " back and a little quicker, it'll hold.",
                        Cap(who) + " está a " + gap + " y algo más rápido, aguanta."),
                    RadioLanguage.T("Von hinten " + who + ", " + gap + ". Er holt auf, aber dein Polster reicht.",
                        "From behind " + who + ", " + gap + ". He's gaining, but your cushion is enough.",
                        "Por detrás " + who + ", a " + gap + ". Recorta, pero tu colchón basta."),
                    RadioLanguage.T(Cap(who) + " bei " + gap + ", " + rate + " pro Runde. Fehlerfrei fahren, dann kommt er nicht ran.",
                        Cap(who) + " at " + gap + ", " + rate + " a lap. Stay clean and he won't get there.",
                        Cap(who) + " a " + gap + ", " + rate + " por vuelta. Sin errores no llega."),
                    RadioLanguage.T("Hinten " + gap + " auf " + who + ". Er wird nicht rechtzeitig dran sein.",
                        gap + " back to " + who + ". He won't get there in time.",
                        gap + " sobre " + who + ". No llegará a tiempo."));
            }
            if (x.GapBehind <= 1.2f)
                return Pick(variant,
                    RadioLanguage.T(Cap(who) + " sitzt dir mit " + gap + " im Nacken.", Cap(who) + " is right on your tail at " + gap + ".", Cap(who) + " te pisa los talones a " + gap + "."),
                    RadioLanguage.T("Hinten " + who + ", " + gap + ". Keine Fehler jetzt.", "Behind " + who + ", " + gap + ". No mistakes now.", "Detrás " + who + ", a " + gap + ". Nada de errores ahora."),
                    RadioLanguage.T(Cap(who) + " " + gap + " hinter dir, Spiegel im Blick.", Cap(who) + " " + gap + " behind, eyes in the mirrors.", Cap(who) + " a " + gap + ", ojo a los retrovisores."),
                    RadioLanguage.T("Nur " + gap + " auf " + who + ". Bremspunkte sauber.", "Just " + gap + " to " + who + ". Clean braking.", "Solo " + gap + " sobre " + who + ". Frenadas limpias."));
            return Pick(variant,
                RadioLanguage.T("Hinten hast du " + gap + " Luft.", "You've got " + gap + " behind.", "Detrás tienes " + gap + " de margen."),
                RadioLanguage.T("Nach hinten " + gap + ", das reicht.", gap + " back, that's enough.", gap + " por detrás, basta."),
                RadioLanguage.T(Cap(who) + " ist " + gap + " zurück, ruhig nach Hause bringen.", Cap(who) + " is " + gap + " back, bring it home calmly.", Cap(who) + " está a " + gap + ", llévalo a casa con calma."),
                RadioLanguage.T("Hinten " + gap + " Polster.", "A " + gap + " cushion behind.", "Detrás " + gap + " de colchón."));
        }
    }
}
