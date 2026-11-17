using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Der Renningenieur liest das Rennen (Rob 16.11.2026: „Der Hauptingenieur muss logisch besser denken, selbstständig das Rennen
    /// lesen und mich informieren“). Ändert sich die Lage des Rennkopfs dauerhaft – jetzt Angriff, jetzt Verteidigung, sicher
    /// ankommen, Schlussphase oder wieder Ruhe –, erklärt Ben, warum (Abstand, Tempo pro Runde, Hochrechnung, Vorfallpunkte) und
    /// was jetzt zählt. Nur aus gemessenen Werten des Rennkopfs, mit wechselnden Formulierungen, höchstens jede Minute.
    /// </summary>
    internal sealed class FocusBriefWatch
    {
        internal static readonly TimeSpan Settle = TimeSpan.FromSeconds(15), MinGap = TimeSpan.FromSeconds(60);
        private int session = int.MinValue;
        private RaceFocus stable = RaceFocus.None, candidate = RaceFocus.None;
        private DateTime candidateSince = DateTime.MinValue, lastCall = DateTime.MinValue;
        private readonly int[] variants = new int[6];
        private ObservedRaceCall call;

        internal void Reset()
        {
            session = int.MinValue; stable = candidate = RaceFocus.None; candidateSince = lastCall = DateTime.MinValue; call = null;
            for (int i = 0; i < variants.Length; i++) variants[i] = 0;
        }

        /// <summary>frontName/rearName nur übergeben, wenn Namen erlaubt sind.</summary>
        internal void Observe(GameStateData gs, RaceSituation x, string frontName, string rearName, string driver, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (x == null || x.SessionId != s.SessionId || !x.Fresh(now)) return;
            if (x.Phase == RacePhase.PreStart || x.Phase == RacePhase.Start || x.Phase == RacePhase.Finished || x.Phase == RacePhase.NotRacing) return;
            if (x.Focus != candidate) { candidate = x.Focus; candidateSince = now; return; }
            if (candidate == stable || now - candidateSince < Settle) return;
            var previous = stable;
            stable = candidate;
            // Die erste Lage nach dem Start erklärt das Startfazit; danach nur echte Wechsel.
            if (previous == RaceFocus.None || stable == RaceFocus.None || now - lastCall < MinGap || call != null && call.IsValid(gs, now)) return;
            string text = Compose(x, stable, previous, frontName, rearName, driver, variants[(int)stable]++);
            if (text == null) return;
            lastCall = now;
            var focus = stable;
            call = new ObservedRaceCall
            {
                Topic = "focus-brief", Text = text, Evidence = "race_mind_focus_change_" + previous + "_to_" + focus, Member = TeamMember.Ben, Priority = 4,
                Position = s.ClassPosition, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(15),
                AdditionalValidity = live => !live.PitData.InPitlane && (RaceMind.Latest == null || RaceMind.Latest.Focus == focus)
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        private static string Gap(float s) => GermanRadioText.Interval(s);
        private static string Who(string name, string fallbackDe, string fallbackEn, string fallbackEs) =>
            string.IsNullOrWhiteSpace(name) ? RadioLanguage.T(fallbackDe, fallbackEn, fallbackEs) : name;
        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal static string Compose(RaceSituation x, RaceFocus focus, RaceFocus previous, string frontName, string rearName, string driver, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? "" : driver;
            int v = ((variant % 5) + 5) % 5; // Rob 17.11.: mehr Abwechslung, fünf Formulierungen je Lage
            string front = Who(frontName, "der Vordermann", "the car ahead", "el de delante");
            string rear = Who(rearName, "der Hintermann", "the car behind", "el de detrás");
            switch (focus)
            {
                case RaceFocus.Attack:
                {
                    if (!(x.GapAhead > 0)) return null;
                    string rate = x.FrontRate >= GapTrendWatch.MinRate ? RadioLanguage.T(", du bist " + GapTrendWatch.Rate(x.FrontRate) + " pro Runde schneller",
                        ", you're " + GapTrendWatch.Rate(x.FrontRate) + " a lap quicker", ", eres " + GapTrendWatch.Rate(x.FrontRate) + " por vuelta más rápido") : "";
                    string catchUp = x.FrontLapsToCatch > 1 ? RadioLanguage.T(" Rechnerisch in etwa " + x.FrontLapsToCatch + " Runden dran.",
                        " On paper you're on him in about " + x.FrontLapsToCatch + " laps.", " En teoría lo alcanzas en unas " + x.FrontLapsToCatch + " vueltas.") : "";
                    if (v == 0) return RadioLanguage.T((n.Length > 0 ? n + ", jetzt" : "Jetzt") + " geht's nach vorn: " + front + " liegt " + Gap(x.GapAhead) + " vor dir" + rate + "." + catchUp + " Druck machen, aber sauber.",
                        (n.Length > 0 ? n + ", now" : "Now") + " it's time to go forward: " + front + " is " + Gap(x.GapAhead) + " ahead" + rate + "." + catchUp + " Apply pressure, keep it clean.",
                        (n.Length > 0 ? n + ", ahora" : "Ahora") + " toca ir hacia delante: " + front + " está a " + Gap(x.GapAhead) + rate + "." + catchUp + " Presiona, pero limpio.");
                    if (v == 1) return RadioLanguage.T(Cap(front) + " ist in Reichweite, " + Gap(x.GapAhead) + rate + "." + catchUp + " Jetzt zählen saubere Kurvenausgänge.",
                        Cap(front) + " is in range, " + Gap(x.GapAhead) + rate + "." + catchUp + " Clean corner exits matter now.",
                        Cap(front) + " está a tiro, " + Gap(x.GapAhead) + rate + "." + catchUp + " Ahora cuentan las salidas limpias.");
                    string frontAcc = Who(frontName, "den Vordermann", "the car ahead", "el de delante");
                    if (v == 3) return RadioLanguage.T("Da vorne geht was" + (n.Length > 0 ? ", " + n : "") + ": " + Gap(x.GapAhead) + " auf " + frontAcc + rate + "." + catchUp + " Geduld, die Chance kommt.",
                        "There's something on" + (n.Length > 0 ? ", " + n : "") + ": " + Gap(x.GapAhead) + " to " + frontAcc + rate + "." + catchUp + " Be patient, the chance will come.",
                        "Ahí delante hay algo" + (n.Length > 0 ? ", " + n : "") + ": " + Gap(x.GapAhead) + " hasta " + frontAcc + rate + "." + catchUp + " Paciencia, la ocasión llegará.");
                    if (v == 4) return RadioLanguage.T("Jagdmodus. " + Cap(front) + " fährt " + Gap(x.GapAhead) + " vor dir" + rate + "." + catchUp + " Bremspunkte treffen, dann kommst du ran.",
                        "Hunting mode. " + Cap(front) + " is " + Gap(x.GapAhead) + " ahead" + rate + "." + catchUp + " Hit your braking points and you'll close in.",
                        "Modo caza. " + Cap(front) + " va a " + Gap(x.GapAhead) + rate + "." + catchUp + " Clava las frenadas y te acercarás.");
                    return RadioLanguage.T("Neue Lage: Angriff. " + Gap(x.GapAhead) + " auf " + frontAcc + rate + "." + catchUp + " Hol ihn dir.",
                        "New situation: attack. " + Gap(x.GapAhead) + " to " + front + rate + "." + catchUp + " Go and get him.",
                        "Nueva situación: ataque. " + Gap(x.GapAhead) + " hasta " + front + rate + "." + catchUp + " A por él.");
                }
                case RaceFocus.Defend:
                {
                    if (!(x.GapBehind > 0)) return null;
                    string rate = x.RearRate >= GapTrendWatch.MinRate ? RadioLanguage.T(" und ist " + GapTrendWatch.Rate(x.RearRate) + " pro Runde schneller",
                        " and is " + GapTrendWatch.Rate(x.RearRate) + " a lap quicker", " y es " + GapTrendWatch.Rate(x.RearRate) + " por vuelta más rápido") : "";
                    if (v == 0) return RadioLanguage.T("Achtung" + (n.Length > 0 ? ", " + n : "") + ": " + rear + " kommt, " + Gap(x.GapBehind) + " hinter dir" + rate + ". Saubere Linien, Bremspunkte treffen.",
                        "Heads up" + (n.Length > 0 ? ", " + n : "") + ": " + rear + " is coming, " + Gap(x.GapBehind) + " behind" + rate + ". Clean lines, hit your braking points.",
                        "Atención" + (n.Length > 0 ? ", " + n : "") + ": " + rear + " viene, a " + Gap(x.GapBehind) + rate + ". Trazadas limpias, clava las frenadas.");
                    if (v == 1) return RadioLanguage.T("Jetzt heißt es verteidigen. " + Cap(rear) + " ist " + Gap(x.GapBehind) + " hinter dir" + rate + ". Keine Fehler, dann kommt er nicht vorbei.",
                        "Time to defend. " + Cap(rear) + " is " + Gap(x.GapBehind) + " behind" + rate + ". No mistakes and he won't get by.",
                        "Toca defender. " + Cap(rear) + " está a " + Gap(x.GapBehind) + rate + ". Sin errores no pasará.");
                    if (v == 3) return RadioLanguage.T("Spiegel im Blick" + (n.Length > 0 ? ", " + n : "") + ". " + Cap(rear) + " ist " + Gap(x.GapBehind) + " hinter dir" + rate + ". Früh die Linie wählen und dabei bleiben.",
                        "Eyes on the mirrors" + (n.Length > 0 ? ", " + n : "") + ". " + Cap(rear) + " is " + Gap(x.GapBehind) + " behind" + rate + ". Pick your line early and stick to it.",
                        "Ojo a los espejos" + (n.Length > 0 ? ", " + n : "") + ". " + Cap(rear) + " está a " + Gap(x.GapBehind) + rate + ". Elige la trazada pronto y mantenla.");
                    if (v == 4) return RadioLanguage.T("Verteidigungsmodus. " + Cap(rear) + ", " + Gap(x.GapBehind) + rate + ". Gute Ausfahrten sind deine beste Waffe.",
                        "Defence mode. " + Cap(rear) + ", " + Gap(x.GapBehind) + rate + ". Good exits are your best weapon.",
                        "Modo defensa. " + Cap(rear) + ", a " + Gap(x.GapBehind) + rate + ". Las buenas salidas son tu mejor arma.");
                    return RadioLanguage.T(Cap(rear) + " macht Druck von hinten, " + Gap(x.GapBehind) + rate + ". Bleib ruhig und fahr deine Linie.",
                        Cap(rear) + " is pushing from behind, " + Gap(x.GapBehind) + rate + ". Stay calm and drive your line.",
                        Cap(rear) + " aprieta por detrás, " + Gap(x.GapBehind) + rate + ". Tranquilo, tu trazada.");
                }
                case RaceFocus.Safe:
                    return v % 2 == 0
                        ? RadioLanguage.T("Die Vorfallpunkte werden knapp. Ab jetzt zählt nur sicher ankommen, kein Risiko mehr.", "Incident points are getting tight. From now on it's only about getting home safely, no more risks.",
                            "Los puntos de incidente van justos. Desde ahora solo cuenta llegar, sin riesgos.")
                        : RadioLanguage.T((n.Length > 0 ? n + ", wir" : "Wir") + " schalten auf sicher: wenig Vorfallpunkte übrig. Lieber eine Zehntel liegen lassen.",
                            (n.Length > 0 ? n + ", we" : "We") + " switch to safe mode: few incident points left. Better to leave a tenth.",
                            (n.Length > 0 ? n + ", pasamos" : "Pasamos") + " a modo seguro: quedan pocos puntos de incidente. Mejor dejar una décima.");
                case RaceFocus.BringHome:
                {
                    string ahead = x.GapAhead > 0 ? RadioLanguage.T(" Nach vorn " + Gap(x.GapAhead) + ".", " " + Gap(x.GapAhead) + " to the car ahead.", " " + Gap(x.GapAhead) + " al de delante.") : "";
                    string behind = x.GapBehind > 0 ? RadioLanguage.T(" Hinten " + Gap(x.GapBehind) + " Luft.", " " + Gap(x.GapBehind) + " of margin behind.", " " + Gap(x.GapBehind) + " de margen detrás.") : "";
                    if (v == 3) return RadioLanguage.T("Die letzten Runden laufen." + ahead + behind + " Konzentration hoch, keine unnötigen Risiken.",
                        "The final laps are on." + ahead + behind + " Keep the concentration up, no needless risks.",
                        "Últimas vueltas en marcha." + ahead + behind + " Concentración alta, sin riesgos innecesarios.");
                    if (v == 4) return RadioLanguage.T((n.Length > 0 ? n + ", das" : "Das") + " Ziel ist in Sicht." + ahead + behind + " Saubere Runden, den Rest erledigen die anderen.",
                        (n.Length > 0 ? n + ", the" : "The") + " flag is in sight." + ahead + behind + " Clean laps, let the others make the mistakes.",
                        (n.Length > 0 ? n + ", la" : "La") + " meta está a la vista." + ahead + behind + " Vueltas limpias, que fallen los demás.");
                    return v % 2 == 0
                        ? RadioLanguage.T("Schlussphase" + (n.Length > 0 ? ", " + n : "") + "." + ahead + behind + " Jetzt sauber nach Hause bringen.",
                            "Closing laps" + (n.Length > 0 ? ", " + n : "") + "." + ahead + behind + " Bring it home cleanly.",
                            "Últimas vueltas" + (n.Length > 0 ? ", " + n : "") + "." + ahead + behind + " Llévalo a casa limpio.")
                        : RadioLanguage.T("Es geht ins Ziel." + ahead + behind + " Keine Experimente mehr, den Platz sichern.",
                            "Heading for the flag." + ahead + behind + " No experiments now, secure the place.",
                            "Rumbo a meta." + ahead + behind + " Nada de experimentos, asegura el puesto.");
                }
                case RaceFocus.Rhythm:
                {
                    string why = previous == RaceFocus.Defend ? RadioLanguage.T("Der Druck von hinten ist weg. ", "The pressure from behind is gone. ", "Ya no hay presión detrás. ")
                        : previous == RaceFocus.Attack ? RadioLanguage.T("Vorne ist gerade nichts zu holen. ", "Nothing to gain ahead right now. ", "Ahora mismo no hay nada que ganar delante. ") : "";
                    string ahead = x.GapAhead > 0 ? RadioLanguage.T("Vorne " + Gap(x.GapAhead), Gap(x.GapAhead) + " ahead", "Delante " + Gap(x.GapAhead)) : "";
                    string behind = x.GapBehind > 0 ? RadioLanguage.T("hinten " + Gap(x.GapBehind), Gap(x.GapBehind) + " behind", "detrás " + Gap(x.GapBehind)) : "";
                    string gaps = ahead.Length > 0 && behind.Length > 0 ? ahead + ", " + behind + ". " : ahead.Length + behind.Length > 0 ? Cap(ahead + behind) + ". " : "";
                    if (v == 3) return RadioLanguage.T(why + gaps + "Zeit zum Durchatmen, saubere Runden fahren.", why + gaps + "Time to breathe, string some clean laps together.", why + gaps + "Momento de respirar, encadena vueltas limpias.");
                    if (v == 4) return RadioLanguage.T(why + gaps + "Jetzt in den Rhythmus kommen und Zeit gutmachen, wo es geht.", why + gaps + "Now settle into a rhythm and gain time where you can.", why + gaps + "Ahora a coger ritmo y ganar tiempo donde se pueda.");
                    return v % 2 == 0
                        ? RadioLanguage.T(why + gaps + "Jetzt gleichmäßige Runden, Rhythmus finden.", why + gaps + "Now consistent laps, find your rhythm.", why + gaps + "Ahora vueltas constantes, busca el ritmo.")
                        : RadioLanguage.T(why + gaps + "Ruhig weiter, die Reifen schonen.", why + gaps + "Keep it calm, look after the tyres.", why + gaps + "Tranquilo, cuida los neumáticos.");
                }
                default: return null;
            }
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
