using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Begrüßung schon im Boxenmenü (Rob 21.11.2026: „Rob, heute sind wir in Monza, 29 Fahrer, vor uns liegen 10 Minuten
    /// Practice … dann das Rennen 20 Minuten. Dann starten wir mal unser Programm, viel Erfolg, leg los.“).
    /// RaceRoom meldet im Menü Strecke, Feld, Art und Länge der laufenden Session und die Renndauer; Ben sagt nur das, was die
    /// Sim gemeldet hat – keine Temperatur, keine Quali-Länge, wenn sie fehlen.
    /// </summary>
    internal static class MenuBriefing
    {
        internal const int Variants = 5;

        /// <summary>Kennung einer Session im Menü; dieselbe Kennung wird nur einmal begrüßt.</summary>
        internal static string Key(FieldView v) => v == null ? null : v.Sim + "|" + v.Track + "|" + v.Layout + "|" + v.Session;

        /// <summary>Steht Rob im Menü bzw. in der Garage einer bekannten Session?</summary>
        internal static bool Ready(FieldView v) => v != null && !string.IsNullOrWhiteSpace(v.Track) && v.Session != FieldSession.Unknown &&
            v.Player != null && !v.InReplay && (v.InMenus || v.PlayerInGarage || v.Phase == FieldPhase.Garage);

        /// <summary>
        /// AC Monza 21.11.: Nach der schwarzen Flagge stellte AC Rob in die Garage, das Rennen lief weiter (Phase Green,
        /// noch 4 Runden) – und Ben begrüßte ihn zum Rennen. Ein Rennen, das schon läuft oder vorbei ist, wird nicht begrüßt.
        /// </summary>
        internal static bool RaceUnderway(FieldView v) => v != null && v.Session == FieldSession.Race &&
            (v.Phase == FieldPhase.Checkered || v.Phase == FieldPhase.Green && v.Player != null &&
             (v.Player.CompletedLaps > 0 || v.Player.Motion == CarMotion.Finished || v.Player.Motion == CarMotion.Retired));

        internal static int FieldSize(FieldView v) => v == null ? 0 : Math.Max(v.NumCars, v.MaxPlace);

        private static string SessionName(FieldSession s)
        {
            switch (s)
            {
                case FieldSession.Practice: return RadioLanguage.T("Training", "practice", "entrenamiento");
                case FieldSession.Qualify: return RadioLanguage.T("Qualifying", "qualifying", "clasificación");
                case FieldSession.Warmup: return RadioLanguage.T("Warm-up", "warm-up", "warm-up");
                case FieldSession.Race: return RadioLanguage.T("Rennen", "race", "carrera");
                default: return null;
            }
        }

        private static string Minutes(int m) => m == 1 ? RadioLanguage.T("eine Minute", "one minute", "un minuto") :
            RadioLanguage.T(m + " Minuten", m + " minutes", m + " minutos");
        private static string Laps(int l) => l == 1 ? RadioLanguage.T("eine Runde", "one lap", "una vuelta") :
            RadioLanguage.T(l + " Runden", l + " laps", l + " vueltas");

        /// <summary>Länge der laufenden Session als gesprochener Ausdruck, null = unbekannt.</summary>
        internal static string Length(FieldView v)
        {
            if (v.TimedSession && !float.IsNaN(v.TimeDuration) && v.TimeDuration >= 30)
                return Minutes((int)Math.Round(v.TimeDuration / 60f));
            if (v.LapsSession && v.SessionLaps > 0) return Laps(v.SessionLaps);
            return null;
        }

        /// <summary>Die Rennen des Events (Dauer je Rennen), leer = keine Angabe.</summary>
        internal static List<string> Races(FieldView v)
        {
            var list = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                if (v.RaceMinutes != null && v.RaceMinutes[i] > 0) list.Add(Minutes(v.RaceMinutes[i]));
                else if (v.RaceLaps != null && v.RaceLaps[i] > 0) list.Add(Laps(v.RaceLaps[i]));
            }
            return list;
        }

        internal static string Compose(FieldView v, string driverName, string speakerName, int variant)
        {
            if (!Ready(v)) return null;
            string session = SessionName(v.Session);
            if (session == null) return null;
            string name = string.IsNullOrWhiteSpace(driverName) ? RadioLanguage.T("Fahrer", "driver", "piloto") : driverName.Trim();
            string track = SessionGreetingDirector.SpokenTrack(v.Track);
            int vv = Math.Abs(variant) % Variants;
            string[] intro = RadioLanguage.Pick(
                new[] { name + ", heute sind wir in " + track + ".", "Hallo " + name + ", " + speakerName + " hier. Wir sind in " + track + ".",
                        name + ", willkommen in " + track + ".", "Okay " + name + ", heute geht es nach " + track + ".", name + ", der Funk steht. Heute " + track + "." },
                new[] { name + ", today we're at " + track + ".", "Hello " + name + ", " + speakerName + " here. We're at " + track + ".",
                        name + ", welcome to " + track + ".", "Okay " + name + ", " + track + " today.", name + ", radio's up. Today it's " + track + "." },
                new[] { name + ", hoy estamos en " + track + ".", "Hola " + name + ", aquí " + speakerName + ". Estamos en " + track + ".",
                        name + ", bienvenido a " + track + ".", "Vale " + name + ", hoy toca " + track + ".", name + ", la radio funciona. Hoy, " + track + "." });
            var parts = new List<string> { intro[vv] };
            string temps = Temperatures(v);
            if (temps != null) parts.Add(temps);
            int field = FieldSize(v);
            if (field > 1) parts.Add(RadioLanguage.T(field + " Fahrer im Feld.", field + " drivers in the field.", field + " pilotos inscritos."));
            string len = Length(v);
            string now = len == null
                ? RadioLanguage.T("Jetzt steht das " + session + " an.", "Up now: " + session + ".", "Ahora toca " + session + ".")
                : RadioLanguage.T("Vor uns liegen " + len + " " + session + ".", "Ahead of us: " + len + " of " + session + ".", "Por delante: " + len + " de " + session + ".");
            if (v.Session == FieldSession.Race && len != null)
                now = RadioLanguage.T("Das Rennen geht über " + len + ".", "The race runs for " + len + ".", "La carrera dura " + len + ".");
            parts.Add(now);
            var races = Races(v);
            string next = Next(v);
            if (next != null) parts.Add(next);
            else if (v.Session != FieldSession.Race && races.Count == 1)
                parts.Add(RadioLanguage.T("Danach das Rennen über " + races[0] + ".", "Then the race, " + races[0] + ".", "Después, la carrera de " + races[0] + "."));
            else if (v.Session != FieldSession.Race && races.Count > 1)
                parts.Add(RadioLanguage.T(races.Count + " Rennen danach: " + Join(races, "und") + ".", races.Count + " races after that: " + Join(races, "and") + ".",
                    races.Count + " carreras después: " + Join(races, "y") + "."));
            string[] close = RadioLanguage.Pick(
                new[] { "Dann starten wir mal unser Programm. Viel Erfolg, leg los!", "Wir arbeiten unser Programm Schritt für Schritt ab. Viel Erfolg!",
                        "Wir sind bereit, wenn du es bist. Leg los!", "Ruhig reinfinden, dann angreifen. Wir begleiten dich.", "Alles bereit. Viel Erfolg, wir sind am Funk." },
                new[] { "Then let's start our programme. Good luck, go for it!", "We'll work through the programme step by step. Good luck!",
                        "We're ready when you are. Off you go!", "Settle in, then attack. We're with you.", "All set. Good luck, we're on the radio." },
                new[] { "Pues empezamos nuestro programa. ¡Suerte, adelante!", "Vamos paso a paso con el programa. ¡Suerte!",
                        "Estamos listos cuando tú lo estés. ¡Adelante!", "Entra con calma y luego ataca. Estamos contigo.", "Todo listo. Suerte, estamos en la radio." });
            parts.Add(close[(vv + 2) % close.Length]);
            return string.Join(" ", parts);
        }

        /// <summary>„Luft 24 Grad, Strecke 36 Grad.“ – nur, was die Sim meldet (RaceRoom meldet keine Temperatur).</summary>
        internal static string Temperatures(FieldView v)
        {
            bool air = !float.IsNaN(v.AirTempC), track = !float.IsNaN(v.TrackTempC);
            if (!air && !track) return null;
            string a = air ? RadioLanguage.Num(Math.Round(v.AirTempC), "0") : null, t = track ? RadioLanguage.Num(Math.Round(v.TrackTempC), "0") : null;
            if (air && track) return RadioLanguage.T("Luft " + a + " Grad, Strecke " + t + " Grad.", "Air " + a + " degrees, track " + t + ".", "Aire a " + a + " grados, pista a " + t + ".");
            return air ? RadioLanguage.T("Luft " + a + " Grad.", "Air " + a + " degrees.", "Aire a " + a + " grados.")
                : RadioLanguage.T("Strecke " + t + " Grad.", "Track " + t + " degrees.", "Pista a " + t + " grados.");
        }

        /// <summary>Der weitere Ablauf, wenn die Sim alle Sessions meldet (iRacing): „Danach Qualifying über 10 Minuten und das Rennen über 20 Minuten.“</summary>
        internal static string Next(FieldView v)
        {
            if (v.Upcoming == null || v.Upcoming.Length == 0) return null;
            var items = new List<string>();
            foreach (var u in v.Upcoming.Take(4))
            {
                string name = SessionName(u.Session);
                if (name == null) continue;
                string len = u.Minutes > 0 ? Minutes(u.Minutes) : u.Laps > 0 ? Laps(u.Laps) : null;
                string article = u.Session == FieldSession.Race ? RadioLanguage.T("das ", "the ", "la ") : "";
                items.Add(len == null ? article + name : RadioLanguage.T(article + name + " über " + len, article + name + " over " + len, article + name + " de " + len));
            }
            if (items.Count == 0) return null;
            string joined = Join(items, RadioLanguage.T("und", "and", "y"));
            return RadioLanguage.T("Danach " + joined + ".", "Then " + joined + ".", "Después, " + joined + ".");
        }

        private static string Join(List<string> items, string and) =>
            items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " " + and + " " + items.Last();
    }
}
