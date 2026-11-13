using System;
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
        private static readonly string[] OpeningsDe =
        {
            "Hey Rob, schön, dass wir wieder zusammen unterwegs sind.",
            "Hey Rob, Ben hier. Wir sind für die Session bereit.",
            "Rob, da sind wir wieder. Wir gehen das heute konzentriert an.",
            "Willkommen zurück, Rob. Wir haben alles im Blick.",
            "Rob, Ben hier. Ich bin am Funk, sobald du mich brauchst.",
            "Hallo Rob. Wir kümmern uns um die Daten, du ums Fahren.",
            "Na Rob, auf geht's. Wir bleiben ruhig und arbeiten uns rein.",
            "Rob, schön dich wieder im Cockpit zu haben. Wir sind bereit.",
            "Hallo Rob, Ben am Funk. Wir halten die Augen auf die Technik.",
            "Rob, wir sind wieder am Start. Ich begleite deine Session.",
            "Hey Rob. Schön, dass du da bist. Wir schauen gemeinsam auf den Rhythmus.",
            "Rob, Ben hier. Wir starten strukturiert und ohne Hektik."
        };
        // "Rob" und "Ben" werden in CreateCall ersetzt; die Übersetzungen enthalten sie deshalb wörtlich.
        private static readonly string[] OpeningsEn =
        {
            "Hey Rob, good to be out together again.",
            "Hey Rob, Ben here. We're ready for the session.",
            "Rob, here we are again. Let's stay focused today.",
            "Welcome back, Rob. We've got everything covered.",
            "Rob, Ben here. I'm on the radio whenever you need me.",
            "Hello Rob. We handle the data, you handle the driving.",
            "Right Rob, let's go. We stay calm and work our way in.",
            "Rob, good to have you back in the cockpit. We're ready.",
            "Hello Rob, Ben on the radio. We're keeping an eye on the car.",
            "Rob, we're back at it. I'll be with you through the session.",
            "Hey Rob. Good to have you here. Let's work on the rhythm together.",
            "Rob, Ben here. We start methodically, no rush."
        };
        private static readonly string[] OpeningsEs =
        {
            "Hola Rob, qué bien volver a estar juntos en pista.",
            "Hola Rob, aquí Ben. Estamos listos para la sesión.",
            "Rob, aquí estamos otra vez. Hoy vamos concentrados.",
            "Bienvenido de nuevo, Rob. Lo tenemos todo controlado.",
            "Rob, aquí Ben. Estoy en la radio cuando me necesites.",
            "Hola Rob. Nosotros nos ocupamos de los datos, tú de pilotar.",
            "Venga Rob, vamos allá. Con calma y poco a poco.",
            "Rob, qué bien tenerte de nuevo en el coche. Estamos listos.",
            "Hola Rob, Ben en la radio. Vigilamos la parte técnica.",
            "Rob, volvemos a la carga. Te acompaño durante la sesión.",
            "Hola Rob. Qué bien que estés aquí. Vamos a trabajar juntos el ritmo.",
            "Rob, aquí Ben. Empezamos con orden y sin prisas."
        };
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
            string track = gs.SessionData.TrackDefinition == null ? null : gs.SessionData.TrackDefinition.name;
            if (string.IsNullOrWhiteSpace(track)) return null;
            string name = string.IsNullOrWhiteSpace(driverName) ? "Rob" : driverName.Trim();
            string opening = Openings[NextVariant()].Replace("Rob", name).Replace("Ben", TeamRouter.DisplayName(speaker));
            var type = gs.SessionData.SessionType;
            bool race = type == SessionType.Race, qualifying = type == SessionType.Qualify || type == SessionType.PrivateQualify;
            string place = RadioLanguage.T(
                " Wir sind in " + track + " zum " + (race ? "Rennen" : qualifying ? "Qualifying" : "Training") + ". ",
                " We're at " + track + " for " + (race ? "the race" : qualifying ? "qualifying" : "practice") + ". ",
                " Estamos en " + track + " para " + (race ? "la carrera" : qualifying ? "la clasificación" : "los libres") + ". ");
            string weather = GreetingWeather(context);
            string encouragement = race ? RadioLanguage.T("Ruhig bleiben, sauber durch die erste Kurve.", "Stay calm, clean through turn one.", "Tranquilo, limpio en la primera curva.") :
                qualifying ? RadioLanguage.T("Finde deinen Rhythmus, dann setzen wir eine saubere Zeit.", "Find your rhythm, then we'll set a clean time.", "Encuentra tu ritmo y luego marcamos un tiempo limpio.") :
                RadioLanguage.T("Komm in Ruhe auf Tempo.", "Build up to speed at your own pace.", "Coge ritmo con calma.");
            return new ObservedRaceCall {
                Topic = "greeting", Text = opening + place + weather + encouragement,
                Evidence = "confirmed_track_and_session; weather_only_when_fresh", Member = speaker, Priority = 2,
                SessionId = gs.SessionData.SessionId, SessionType = type, Position = -1, AllowPitlane = true,
                DetectedUtc = context.NowUtc, ExpiresUtc = windowEnd,
                AdditionalValidity = live => live.SessionData.TrackDefinition != null && live.SessionData.TrackDefinition.name == track &&
                    (weather.Length == 0 || weather == GreetingWeather(new RaceDataContext { GameState = live, NowUtc = DateTime.UtcNow }))
            };
        }

        private static string GreetingWeather(RaceDataContext context)
        {
            var c = context.GameState.Conditions.LatestConditions;
            if (c == null || !WeatherWatch.Fresh(context.NowUtc, c.Time)) return "";
            var parts = new System.Collections.Generic.List<string>();
            if (!float.IsNaN(c.AmbientTemperature) && c.AmbientTemperature >= -30 && c.AmbientTemperature <= 60)
                parts.Add(RadioLanguage.T("Luft ", "air ", "aire ") + Math.Round(c.AmbientTemperature).ToString(RadioLanguage.Culture) + RadioLanguage.T(" Grad", " degrees", " grados"));
            if (!float.IsNaN(c.TrackTemperature) && c.TrackTemperature >= -30 && c.TrackTemperature <= 85)
                parts.Add(RadioLanguage.T("Strecke ", "track ", "pista ") + Math.Round(c.TrackTemperature).ToString(RadioLanguage.Culture) + RadioLanguage.T(" Grad", " degrees", " grados"));
            if (c.RainDensity >= .01 && c.RainDensity <= 1) parts.Add(RadioLanguage.T("Regen wird gemeldet", "rain reported", "se informa de lluvia"));
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
    }
}
