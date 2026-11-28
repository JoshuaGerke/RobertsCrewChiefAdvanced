using System;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Wählt einen von zwölf voneinander unterscheidbaren Gesprächseinstiegen für
    /// die bestehende KI-Begrüßung. Alle Fakten kommen separat aus dem GameState;
    /// die Sprachvarianten enthalten absichtlich keine Telemetriebehauptungen.
    /// Es werden weder Audio noch Cloud-Anfragen aus dieser Klasse ausgelöst.
    /// </summary>
    public sealed class SessionGreetingDirector
    {
        // Rob 17.11.2026: „So begrüßt keiner seinen Rennfahrer.“ Kein Wohlfühl-Text mehr, sondern wie am Kommandostand:
        // kurzer Funkcheck, dann Strecke, Session, Bedingungen, Feld und ein Satz, der anschiebt.
        private static readonly string[] OpeningsDe =
        {
            "Funkcheck, Rob. Ben hier, die Crew steht.",
            "Rob, Ben am Funk. Wir sind startklar.",
            "Okay Rob, Ben hier. Daten laufen, die Box ist besetzt.",
            "Rob, hier ist Ben. Auf unserer Seite ist alles bereit.",
            "Hallo Rob, Ben am Funk.",
            "Rob, Ben. Funk steht, Telemetrie ist da.",
            "Willkommen zurück im Auto, Rob. Ben hier.",
            "Rob, Ben hier. Das Auto ist vorbereitet, jetzt bist du dran.",
            "Alles klar, Rob. Ben am Kommandostand.",
            "Rob, Ben hier. Die Crew ist auf Position.",
            "Na dann, Rob. Ben am Funk, wir legen los.",
            "Rob, Ben hier. Wir sind bereit, wenn du es bist."
        };
        // "Rob" und "Ben" werden in CreateCall ersetzt; die Übersetzungen enthalten sie deshalb wörtlich.
        private static readonly string[] OpeningsEn =
        {
            "Radio check, Rob. Ben here, the crew is set.",
            "Rob, Ben on the radio. We're good to go.",
            "Okay Rob, Ben here. Data's flowing, the box is manned.",
            "Rob, this is Ben. Everything's ready on our side.",
            "Hello Rob, Ben on the radio.",
            "Rob, Ben. Radio's up, telemetry's in.",
            "Welcome back in the car, Rob. Ben here.",
            "Rob, Ben here. The car is prepared, over to you.",
            "All right, Rob. Ben on the pit wall.",
            "Rob, Ben here. The crew is in position.",
            "Here we go, Rob. Ben on the radio, let's get to work.",
            "Rob, Ben here. We're ready when you are."
        };
        private static readonly string[] OpeningsEs =
        {
            "Prueba de radio, Rob. Aquí Ben, el equipo está listo.",
            "Rob, Ben en la radio. Todo preparado.",
            "Vale Rob, aquí Ben. Los datos entran, el box está ocupado.",
            "Rob, soy Ben. Por nuestra parte está todo listo.",
            "Hola Rob, Ben en la radio.",
            "Rob, Ben. Radio funcionando, telemetría recibida.",
            "Bienvenido de nuevo al coche, Rob. Aquí Ben.",
            "Rob, aquí Ben. El coche está preparado, te toca a ti.",
            "Muy bien, Rob. Ben en el muro.",
            "Rob, aquí Ben. El equipo está en posición.",
            "Vamos allá, Rob. Ben en la radio, a trabajar.",
            "Rob, aquí Ben. Estamos listos cuando tú lo estés."
        };

        /// <summary>Ein Satz, der anschiebt – passend zur Session, ohne Behauptungen über Daten.</summary>
        internal static string Motivation(SessionType type, int variant)
        {
            bool race = type == SessionType.Race, quali = type == SessionType.Qualify || type == SessionType.PrivateQualify;
            string[] de = race ? new[] { "Sauber durch Kurve eins, entschieden wird hinten raus.", "Kopf frei, wir haben das Tempo. Lass uns Plätze holen.",
                    "Konzentriert bleiben, die Chancen kommen. Wir sind jede Runde bei dir.", "Guter Start, saubere erste Runde, dann greifen wir an." }
                : quali ? new[] { "Eine saubere Runde reicht, und die hast du drauf.", "Reifen ins Fenster bringen, dann Attacke.",
                    "Jetzt zählt's: volle Konzentration auf die eine Runde.", "Du kennst die Strecke, du kennst das Auto. Hol sie dir." }
                : new[] { "Erst Reifen und Bremsen auf Temperatur, dann holen wir uns die Zeit.", "Runde für Runde schneller, so bauen wir das heute auf.",
                    "Taste dich an die Grenze ran, wir liefern dir die Zahlen.", "Lass uns heute ein schnelles Auto draus machen." };
            string[] en = race ? new[] { "Clean through turn one, this gets decided late.", "Clear head, we've got the pace. Let's gain some places.",
                    "Stay focused, the chances will come. We're with you every lap.", "Good start, clean first lap, then we attack." }
                : quali ? new[] { "One clean lap is all it takes, and you've got it in you.", "Get the tyres in the window, then go for it.",
                    "This is it: full focus on that one lap.", "You know the track, you know the car. Go get it." }
                : new[] { "Tyres and brakes up to temperature first, then we go for time.", "Quicker lap by lap, that's how we build it today.",
                    "Work your way up to the limit, we'll give you the numbers.", "Let's turn this into a fast car today." };
            string[] es = race ? new[] { "Limpio en la primera curva, esto se decide al final.", "Cabeza fría, tenemos ritmo. Vamos a ganar posiciones.",
                    "Concentrado, las ocasiones llegarán. Estamos contigo cada vuelta.", "Buena salida, primera vuelta limpia y luego atacamos." }
                : quali ? new[] { "Basta una vuelta limpia, y la tienes.", "Neumáticos en su ventana y luego a por todas.",
                    "Ahora cuenta: toda la concentración en esa vuelta.", "Conoces la pista, conoces el coche. Ve a por ella." }
                : new[] { "Primero neumáticos y frenos a temperatura, luego vamos a por el tiempo.", "Vuelta a vuelta más rápido, así lo construimos hoy.",
                    "Acércate al límite poco a poco, nosotros te damos los números.", "Hoy vamos a hacer de esto un coche rápido." };
            var pool = RadioLanguage.Pick(de, en, es);
            return pool[((variant % pool.Length) + pool.Length) % pool.Length];
        }

        /// <summary>Feldgröße und – außerhalb des Rennens – wie viele gerade draußen sind; leer ohne Gegnerdaten.</summary>
        /// <summary>Feldgröße aus dem Lagebild direkt aus der Sim (größte gemeldete Zahl aus Fahrerliste und Platz), 0 = keines.</summary>
        internal static int NativeFieldSize()
        {
            var v = Field.FieldService.Fresh(2);
            return v == null ? 0 : Math.Max(v.NumCars, v.MaxPlace);
        }

        /// <summary>
        /// Monza 21.11.: „Startplatz 24 von 21“ – Crew Chief kannte 20 Gegner, im Feld standen 31 Autos. Die Sim selbst zählt:
        /// die größere der beiden Angaben gilt, und nie kleiner als der eigene Platz.
        /// </summary>
        internal static int FieldSize(GameStateData gs)
        {
            int mapped = gs?.OpponentData == null ? 1 : gs.OpponentData.Values.Count(o => o != null && o.IsActive) + 1;
            // iRacing 27.11.: Crew Chief meldet im Training Platz 1000 als Platzhalter – daraus wurden „1000 Autos im Feld“.
            int own = gs == null ? 0 : Math.Max(Plausible(gs.SessionData.ClassPosition), Plausible(gs.SessionData.OverallPosition));
            return Math.Max(Math.Max(mapped, NativeFieldSize()), own);
        }

        private static int Plausible(int position) => position > 0 && position < MaxPlausibleField ? position : 0;
        internal const int MaxPlausibleField = 200;

        internal static string FieldLine(GameStateData gs)
        {
            if (gs?.OpponentData == null) return "";
            var active = gs.OpponentData.Values.Where(o => o != null && o.IsActive).ToList();
            if (active.Count == 0 && NativeFieldSize() <= 1) return ""; // beim Laden fehlen Gegnerdaten oft noch – dann lieber nichts behaupten
            int cars = FieldSize(gs);
            // RaceRoom Laguna 27.11.: „2 Autos im Feld“ beim Laden, es waren 16. Ein so kleines Feld außerhalb des Rennens ist meist noch
            // nicht geladen – dann lieber nichts behaupten.
            if (cars <= 2 && gs.SessionData.SessionType != SessionType.Race) return "";
            string c = cars.ToString(RadioLanguage.Culture);
            if (gs.SessionData.SessionType == SessionType.Race)
                return gs.SessionData.ClassPosition > 0 ? "" : RadioLanguage.T(c + " Autos am Start. ", c + " cars on the grid. ", c + " coches en parrilla. ");
            int inPits = active.Count(o => o.InPits);
            // InPits setzen nicht alle Sims; ohne ein einziges Auto in der Box nennen wir nur die Feldgröße.
            if (inPits == 0) return RadioLanguage.T(c + " Autos im Feld. ", c + " cars in the session. ", c + " coches en la sesión. ");
            // Monza 21.11.: „21 Autos im Feld, 0 davon gerade draußen“ – die Null klingt falsch. Dann: alle noch in der Box.
            if (inPits >= active.Count) return RadioLanguage.T(c + " Autos im Feld, noch alle in der Box. ", c + " cars in the session, all still in the pits. ",
                c + " coches en la sesión, todos aún en boxes. ");
            string o2 = (active.Count - inPits).ToString(RadioLanguage.Culture);
            return RadioLanguage.T(c + " Autos im Feld, " + o2 + " davon gerade draußen. ", c + " cars in the session, " + o2 + " of them out on track. ",
                c + " coches en la sesión, " + o2 + " ahora mismo en pista. ");
        }

        private static string[] Openings => RadioLanguage.Pick(OpeningsDe, OpeningsEn, OpeningsEs);

        private readonly Random random = new Random();
        private readonly System.Collections.Generic.Queue<int> variantBag = new System.Collections.Generic.Queue<int>();
        private int previousOpening = -1;
        public const int VariantCount = 12;

        private int NextVariant()
        {
            // Einmaliger Shuffle-Beutel: alle 12 Varianten bevor eine wiederholt wird.
            // Am Beutelübergang keine direkte Wiederholung.
            if (variantBag.Count == 0)
            {
                var indices = new int[Openings.Length];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                for (int i = indices.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    int temp = indices[i]; indices[i] = indices[j]; indices[j] = temp;
                }
                if (indices[0] == previousOpening)
                {
                    int swap = indices[0]; indices[0] = indices[1]; indices[1] = swap;
                }
                foreach (int index in indices) variantBag.Enqueue(index);
            }
            previousOpening = variantBag.Dequeue();
            return previousOpening;
        }

        /// <summary>Small, factual greeting independent of changing gaps, RPM, laps and cloud reasoning.</summary>
        public ObservedRaceCall CreateCall(RaceDataContext context, DateTime windowEnd, string driverName, TeamMember speaker)
        {
            var gs = context == null ? null : context.GameState;
            if (gs == null || context.TelemetryAgeSeconds < 0 || context.TelemetryAgeSeconds > 1 ||
                context.NowUtc > windowEnd || !SafeToGreet(gs.SessionData.SessionType, gs.SessionData.SessionPhase,
                    gs.PitData.InPitlane, gs.PositionAndMotionData.CarSpeed)) return null;
            if (gs.SessionData.SessionType == SessionType.Race && gs.SessionData.SessionPhase == SessionPhase.Countdown)
                return null;
            string track = gs.SessionData.TrackDefinition == null ? null : gs.SessionData.TrackDefinition.name;
            if (string.IsNullOrWhiteSpace(track)) return null;
            string trackForRadio = track.Split(':')[0].Replace('_', ' ').Trim();
            string name = string.IsNullOrWhiteSpace(driverName) ? "Rob" : driverName.Trim();
            string opening = Openings[NextVariant()].Replace("Rob", name).Replace("Ben", TeamRouter.DisplayName(speaker));
            var type = gs.SessionData.SessionType;
            bool race = type == SessionType.Race, qualifying = type == SessionType.Qualify || type == SessionType.PrivateQualify;
            string place = " " + SpokenTrack(track) + ", " + RadioLanguage.T(race ? "Rennen" : qualifying ? "Qualifying" : "Training",
                race ? "race" : qualifying ? "qualifying" : "practice", race ? "carrera" : qualifying ? "clasificación" : "libres") + ". ";
            string weather = GreetingWeather(context);
            string field = FieldLine(gs);
            int grid = FieldSize(gs);
            string start = race && gs.SessionData.ClassPosition > 0 ? (RadioLanguage.IsGerman ? "Startplatz " + gs.SessionData.ClassPosition +
                (RaceRadioLanguage.SeparateClass(gs.SessionData) ? " in deiner Klasse. " : grid > 1 ? " von " + grid + ". " : ". ") :
                RadioLanguage.T("", "Starting ", "Sales ") + RaceRadioLanguage.Rank(gs.SessionData, gs.SessionData.ClassPosition) +
                (!RaceRadioLanguage.SeparateClass(gs.SessionData) && grid > 1 ? RadioLanguage.T("", " of " + grid, " de " + grid) : "") + ". ") : "";
            string encouragement = Motivation(type, random.Next(4));
            return new ObservedRaceCall {
                Topic = "greeting", Text = opening + place + weather + field + start + encouragement,
                Evidence = "confirmed_track_and_session; weather_only_when_fresh", Member = speaker, Priority = 3,
                SessionId = gs.SessionData.SessionId, SessionType = type, Position = -1, AllowPitlane = true,
                DetectedUtc = context.NowUtc, ExpiresUtc = windowEnd,
                AdditionalValidity = live => live.SessionData.TrackDefinition != null && live.SessionData.TrackDefinition.name == track &&
                    (type != SessionType.Race || live.SessionData.SessionPhase != SessionPhase.Countdown) &&
                    (start.Length == 0 || live.SessionData.ClassPosition == gs.SessionData.ClassPosition) &&
                    (weather.Length == 0 || weather == GreetingWeather(new RaceDataContext { GameState = live, NowUtc = DateTime.UtcNow }))
            };
        }

        private static string GreetingWeather(RaceDataContext context)
        {
            var c = context.GameState.Conditions.LatestConditions;
            if (c == null || !WeatherWatch.Fresh(context.NowUtc, c.Time)) return "";
            var parts = new System.Collections.Generic.List<string>();
            // Rob 17.11.: Luft und Strecke gehören zusammen in die Begrüßung, dazu Regen und Wind.
            if (TrackAwareness.InRange(c.AmbientTemperature, -30, 60).HasValue)
                parts.Add(RadioLanguage.T("Luft ", "air ", "aire ") + Math.Round(c.AmbientTemperature).ToString(RadioLanguage.Culture) + RadioLanguage.T(" Grad", " degrees", " grados"));
            if (TrackAwareness.InRange(c.TrackTemperature, -30, 85).HasValue)
                parts.Add(RadioLanguage.T("Strecke ", "track ", "pista ") + Math.Round(c.TrackTemperature).ToString(RadioLanguage.Culture) + RadioLanguage.T(" Grad", " degrees", " grados"));
            if (c.RainDensity >= .01 && c.RainDensity <= 1) parts.Add(RadioLanguage.T("Regen wird gemeldet", "rain reported", "se informa de lluvia"));
            if (TrackAwareness.InRange(c.WindSpeed, .5, 150).HasValue)
                parts.Add(c.WindSpeed * 3.6 < 8 ? RadioLanguage.T("kaum Wind", "hardly any wind", "apenas viento") :
                    RadioLanguage.T("Wind ", "wind ", "viento ") + Math.Round(c.WindSpeed * 3.6).ToString(RadioLanguage.Culture) +
                    RadioLanguage.T(" Kilometer pro Stunde", " kilometres per hour", " kilómetros por hora"));
            // Zero rain does not establish sunshine, cloud cover or a dry track.
            if (parts.Count == 0) return "";
            string text = string.Join(", ", parts) + ". ";
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        public static bool SafeToGreet(SessionType type, SessionPhase phase, bool inPitlane, float speedMetresPerSecond)
        {
            if (type == SessionType.Unavailable || phase == SessionPhase.Unavailable ||
                phase == SessionPhase.Checkered || phase == SessionPhase.Finished ||
                phase == SessionPhase.FullCourseYellow || float.IsNaN(speedMetresPerSecond) ||
                float.IsInfinity(speedMetresPerSecond) || speedMetresPerSecond < 0)
                return false;
            if (phase == SessionPhase.Garage || phase == SessionPhase.Gridwalk ||
                phase == SessionPhase.Countdown)
                return true;
            if (phase == SessionPhase.Formation)
                return speedMetresPerSecond < 2.0f; // nicht in die rollende Formation funken
            // Beim laufenden Rennen kein Begrüßungsfunkspruch nach dem Rennstart.
            // Training / Qualifying dürfen in der Box bzw. im Stand begrüßt werden.
            if (phase != SessionPhase.Green) return false;
            // Rennstart aus der Box: nur im Stand, innerhalb des kurzen Begrüßungsfensters
            // (Zeitfenster wird vom AiTeamService kontrolliert).
            if (type == SessionType.Race) return inPitlane && speedMetresPerSecond < 1.0f;
            return speedMetresPerSecond < 1.0f || inPitlane && speedMetresPerSecond <= 8.3f;
        }

        public string NextCue(SessionType type, SessionPhase phase, bool inPitlane, float speedMetresPerSecond)
        {
            // Vorhersagbare, testbare Variation ohne instabile Hashcodes oder Cloud-Abfragen.
            int next = NextVariant();
            string context = phase == SessionPhase.Garage ? "Garage" :
                phase == SessionPhase.Gridwalk || phase == SessionPhase.Countdown || phase == SessionPhase.Formation ? "Startaufstellung/Startvorbereitung" :
                inPitlane ? "Boxengasse" : "stehendes Fahrzeug, genauer Standort nicht bestätigt";
            string session = type == SessionType.Race ? "Rennen" :
                type == SessionType.Qualify || type == SessionType.PrivateQualify ? "Qualifikation" : "Training bzw. freie Fahrt";
            return "Begrüßungsvorlage (sinngemäß, natürlich umformulieren, nicht wörtlich ablesen): '" + Openings[next] +
                "'. Sicher erkannter Sessiontyp: " + session + ". Standortklassifikation: " + context +
                ". Verwende eine zeitneutrale Begrüßung, keine erfundene Tageszeit. Keine Behauptung über Wetter, Grip, Training oder Strategie ohne bestätigte Daten. " +
                "Ben begrüßt zuerst; Mik ergänzt nur bei einem Rennen und nur bei einem echten strategischen Befund. " +
                "Keine Grußwiederholung und keine Aufzählung aller Telemetriegrößen.";
        }

        /// <summary>Interne Streckennamen wie "Monza:Monza_2020" so, wie man sie spricht ("Monza").</summary>
        internal static string SpokenTrack(string track)
        {
            // iRacing 27.11.: der Name kam mit Anführungszeichen ("snetterton 300").
            string name = (RadioPhraseCatalog.RepairEncoding(track) ?? "").Split(':')[0].Trim().Trim('"', '\'').Trim();
            // AC 20.11.: „ks nurburgring“ – Kunos- und Mod-Kürzel vor dem Namen sind kein Streckenname.
            name = System.Text.RegularExpressions.Regex.Replace(name, @"^(?:ks|rt|acu|rss|vrc|rj|sx)_", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            name = name.Replace('_', ' ').Trim();
            if (name.Length > 0 && name == name.ToLowerInvariant())
            {
                var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < words.Length; i++)
                {
                    string w = words[i];
                    words[i] = w == "nurburgring" ? "Nürburgring" : w == "nordschleife" ? "Nordschleife" :
                        char.ToUpperInvariant(w[0]) + w.Substring(1);
                }
                name = string.Join(" ", words);
            }
            return name.Length > 0 ? name : track;
        }
    }
}
