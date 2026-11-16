using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Einfache Standardfragen, die ohne KI-Aufruf zuverlässig aus der Telemetrie beantwortet werden
    /// (spart Latenz und Kosten). Alles mit "warum", "Strategie", "analysier" usw. geht an die KI.
    /// </summary>
    public static class LocalAnswers
    {
        private static readonly string[] complexMarkers = { "warum", "wieso", "weshalb", "analys", "strategie", "sollten", "sollen wir", "meinst du",
            "undercut", "overcut", "vergleich", "besser", "was hältst", "wie kann ich", "was kann ich", "reicht", "schaffen", "kommen wir", "kriege ich", "krieg ich",
            // Englisch
            "why", "analyz", "strategy", "should we", "should i", "do you think", "compare", "better", "how can i", "what can i", "enough", "make it", "will we", "can we",
            // Spanisch
            "por qué", "por que", "analiz", "estrategia", "deberíamos", "deberiamos", "debería", "deberia", "crees", "compar", "mejor", "opinas",
            "cómo puedo", "como puedo", "qué puedo", "que puedo", "alcanza", "llega", "llegamos", "llego" };

        private static readonly char[] trimChars = { ' ', '?', '.', '!', '¿', '¡' };

        private static bool AnyOf(string q, params string[] parts) => parts.Any(q.Contains);
        private static readonly CultureInfo de = CultureInfo.GetCultureInfo("de-DE");
        internal static bool AsksAboutRecentRaceAction(string question) => Regex.IsMatch(question ?? "",
            @"\b(?:überholt|überholen|vorbeigegangen|vorbeigekommen|platzgewinn|positionswechsel)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string RecentOwnPass(string question, RaceDataContext ctx)
        {
            if (!AsksAboutRecentRaceAction(question) || ctx?.Memory == null ||
                ctx.GameState?.SessionData.SessionType != SessionType.Race || ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1 ||
                !Regex.IsMatch(question, @"\b(?:ich\s+hab(?:e)?|hab(?:e)?\s+ich|ich\s+bin|bin\s+ich)\b", RegexOptions.IgnoreCase))
                return null;
            var gs = ctx.GameState;
            var requested = RaceFieldTools.NamedInQuestion(question, gs);
            if (requested.Count > 1 || requested.Count > 0 && !ctx.SendDriverNames) return null;
            string key = requested.Count == 1 ? gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, requested[0])).Key : null;
            if (requested.Count == 1 && key == null) return null;
            var events = ctx.Memory.GetEvents(TimeSpan.FromSeconds(45), ctx.NowUtc)
                .Where(e => e.TimeUtc <= ctx.NowUtc && e.Topic == "confirmed-on-track-pass" && (key == null || e.SubjectKey == key))
                .ToArray();
            if (events.Length == 0 || key == null && events.Length > 1) return null;
            string rank = gs.SessionData.ClassPosition > 0 ? " Jetzt " + RaceRadioLanguage.Rank(gs.SessionData, gs.SessionData.ClassPosition) + "." : "";
            return "Ja. " + (ctx.SendDriverNames ? events.Last().Text : "Du bist auf der Strecke an einem Klassengegner vorbeigegangen.") + rank;
        }

        internal static string FuelToFinish(string question, RaceDataContext ctx)
        {
            string q = DriverRadioPreferences.Normalize(question);
            bool fuel = Regex.IsMatch(q, @"\b(?:sprit|tank|kraftstoff|verbrauch|reichweite|liter)\b");
            bool range = Regex.IsMatch(q, @"\b(?:reicht|reichen|reicht's|hin|auskommen|schaff\w*|ziel|rennende|durchkommen|reichweite)\b");
            if (!fuel || !range || ctx?.GameState?.SessionData.SessionType != SessionType.Race ||
                Regex.IsMatch(q, @"\b(?:merke|speicher\w*|wunsch|strategie|undercut|overcut)\b")) return null;
            if (ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1) return "Für die Spritrechnung fehlen frische Daten.";
            var gs = ctx.GameState;
            var f = gs.FuelData;
            if (!f.FuelUseActive || !TrackAwareness.InRange(f.FuelLeft, 0, 10000).HasValue)
                return "Ich habe gerade keinen verlässlichen Tankstand.";
            float consumption = FuelEstimate.Consumption(ctx.Memory, gs, out bool provisional, out _);
            if (!(consumption > 0)) return "Noch " + FuelWatch.Litres(f.FuelLeft) + ". Für die Reichweite fehlt ein gemessener Verbrauch.";
            int requestedLaps = 0;
            var explicitLaps = Regex.Match(q, @"\bfuer (\d{1,3}) runden\b");
            if (explicitLaps.Success) int.TryParse(explicitLaps.Groups[1].Value, out requestedLaps);
            var estimate = FuelEstimate.From(ctx, minimumMeasuredLaps: 1);
            string basis = provisional ? "Hochgerechnet etwa " : "Rechnerisch etwa ";
            string rangeText = basis + (f.FuelLeft / consumption).ToString("0.0", de) + " Runden Sprit.";
            if (requestedLaps <= 0 && estimate == null) return rangeText + " Die Restdistanz bis ins Ziel ist noch offen.";
            float balance = requestedLaps > 0 ? f.FuelLeft - requestedLaps * consumption : -estimate.AdditionalWithoutReserve;
            float reserve = requestedLaps > 0 ? consumption * FuelWatch.SafetyMarginLaps : estimate.ReserveLitres;
            string distance = requestedLaps > 0 ? "für " + requestedLaps + " Runden" : "bis ins Ziel";
            if (balance < 0) return "Nach aktueller Rechnung fehlen " + FuelWatch.Litres(-balance) + " " + distance + ".";
            if (balance < reserve) return "Der Sprit reicht rechnerisch " + distance + ". Die Reserve ist knapp.";
            return "Der Sprit reicht rechnerisch " + distance + ". Etwa " + FuelWatch.Litres(balance) + " bleiben übrig.";
        }

        internal static string PreviousStintFuel(string question, RaceDataContext ctx)
        {
            if (ctx?.GameState == null || string.IsNullOrWhiteSpace(question) ||
                !Regex.IsMatch(question,@"\b(?:verbrauch|verbrauch\w*|liter pro runde|sprit pro runde)\b",RegexOptions.IgnoreCase) ||
                complexMarkers.Any(question.ToLowerInvariant().Contains) ||
                Regex.IsMatch(question,@"\b(?:gegner|vordermann|hintermann)\b",RegexOptions.IgnoreCase) ||
                RaceFieldTools.NamedInQuestion(question,ctx.GameState).Count != 0) return null;
            var stint = DriverMemory.PreviousStint(ctx);
            double? litres = (double?)stint?["gemessener_sprit_pro_runde_l"];
            return litres.HasValue ? "Aus dieser Sitzung fehlt mir noch eine stabile Messung. Beim letzten Stint hier waren es " +
                litres.Value.ToString("0.0",de) + " Liter pro Runde." : null;
        }
        private static readonly Regex TrackNeighbourQuestion = new Regex(
            @"^(?:wer (?:ist|liegt|fährt)|(?:fährt|ist|liegt) (?:aktuell |gerade |noch )?(?:jemand|ein (?:auto|wagen|fahrzeug))|wie weit (?:ist|liegt)|wie (?:groß|knapp) ist der abstand|abstand|wie viel (?:abstand|zeit))\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex PresenceQuestion = new Regex(@"^(?:fährt|ist|liegt) (?:aktuell |gerade |noch )?(?:jemand|ein (?:auto|wagen|fahrzeug))\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex Ahead = new Regex(@"\b(?:vor mir|vor uns|nach vorn(?:e)?|vordermann|vorne)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex Behind = new Regex(@"\b(?:hinter mir|hinter uns|nach hinten|hintermann|hinten)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        internal static bool NeighbourReplyStillCurrent((string Text, string Evidence) original, (string Text, string Evidence) live)
        {
            if (original.Equals(live)) return true;
            if (original.Evidence == null || original.Evidence != live.Evidence || live.Text == null) return false;
            const string interval = @"(?<v>\d+,\d+) Sekunden";
            var a = Regex.Matches(original.Text, interval);
            var b = Regex.Matches(live.Text, interval);
            if (a.Count == 0 || a.Count != b.Count || Regex.Replace(original.Text, interval, "") != Regex.Replace(live.Text, interval, ""))
                return false;
            for (int i = 0; i < a.Count; i++)
                if (Math.Abs(double.Parse(a[i].Groups["v"].Value, de) - double.Parse(b[i].Groups["v"].Value, de)) > .4)
                    return false;
            return true;
        }

        internal static string TrackNeighbour(string question, RaceDataContext ctx) => TrackNeighbour(question, ctx, out _);

        internal static string TrackNeighbour(string question, RaceDataContext ctx, out string evidence)
        {
            evidence = null;
            if (string.IsNullOrWhiteSpace(question) || !TrackNeighbourQuestion.IsMatch(question.Trim()) ||
                complexMarkers.Any(marker => question.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) ||
                Regex.IsMatch(question, @"\bbox\w*\b|\b(?:stopp|reifen|sektor|unfall|überrund)\w*\b", RegexOptions.IgnoreCase))
                return null;
            bool ahead = Ahead.IsMatch(question), behind = Behind.IsMatch(question);
            if (!ahead && !behind) return null;
            if (ctx?.GameState == null) return "Ich habe gerade keine aktuellen Verkehrsdaten.";
            var gs = ctx.GameState;
            DateTime source = gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc;
            if (source == DateTime.MinValue || ctx.NowUtc < source || ctx.NowUtc - source > TimeSpan.FromSeconds(1))
                return "Ich habe gerade keine aktuellen Verkehrsdaten.";

            bool physical = gs.SessionData.SessionType != SessionType.Race ||
                PresenceQuestion.IsMatch(question.Trim()) ||
                question.IndexOf("auf der strecke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                question.IndexOf("in metern", StringComparison.OrdinalIgnoreCase) >= 0;
            bool presence = PresenceQuestion.IsMatch(question.Trim());
            bool metresOnly = question.IndexOf("in metern", StringComparison.OrdinalIgnoreCase) >= 0;
            string result = NeighbourAnswer(ctx, ahead, physical, presence, metresOnly, out evidence);
            if (!ahead || !behind) return result;
            string rear = NeighbourAnswer(ctx, false, physical, presence, metresOnly, out var rearEvidence);
            evidence = evidence + "|" + rearEvidence;
            return result + " " + rear;
        }

        private static string NeighbourAnswer(RaceDataContext ctx, bool ahead, bool physical, bool presence, bool metresOnly, out string evidence)
        {
            evidence = null;
            var gs = ctx.GameState;
            string direction = ahead ? "vor dir" : "hinter dir";
            if (physical)
            {
                var snapshot = ctx.Tracks?.Snapshot;
                var car = snapshot != null && snapshot.IsCurrent(ctx)
                    ? snapshot.Cars.Where(c => !c.InPits && c.SpatiallyConsistent &&
                        (ahead ? c.RouteGapM > 0 : c.RouteGapM < 0) &&
                        gs.OpponentData.TryGetValue(c.Key, out var live) && live != null && live.IsActive && !live.InPits &&
                        RaceReader.Identity("", live) == c.NativeIdentity)
                        .OrderBy(c => Math.Abs(c.RouteGapM.Value)).FirstOrDefault()
                    : null;
                if (car == null) return "Ich kann das nächste Auto " + direction + " auf der Strecke gerade nicht sicher zuordnen.";
                evidence = car.NativeIdentity;
                string name = ctx.SendDriverNames ? car.Name : null;
                if (presence) return (string.IsNullOrWhiteSpace(name) ? "Da fährt ein Wagen" : name + " fährt") + " " + direction + ".";
                if (!metresOnly && car.TrackIntervalSeconds.HasValue)
                    return (string.IsNullOrWhiteSpace(name) ? "Ein Wagen" : name) + " ist auf der Strecke ungefähr " +
                        car.TrackIntervalSeconds.Value.ToString("0.0", de) + " Sekunden " + direction + ".";
                double distance = Math.Round(Math.Abs(car.RouteGapM.Value) / 5) * 5;
                return (string.IsNullOrWhiteSpace(name) ? "Ein Wagen" : name) +
                    " ist auf der Strecke " + (distance < 5 ? "direkt " : "etwa " + distance.ToString("0", de) + " Meter ") + direction + ".";
            }

            var rival = RaceDataTools.Neighbour(gs, ahead);
            if (rival == null) return "Ich kann den Nachbarn " + direction + " in der Wertung gerade nicht sicher zuordnen.";
            evidence = RaceReader.Identity(ahead ? gs.SessionData.OpponentKeyInFront : gs.SessionData.OpponentKeyBehind, rival);
            string who = ctx.SendDriverNames && rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
            var gap = RaceDataTools.ReadGap(ctx, rival, ahead);
            string basis = RaceRadioLanguage.SeparateClass(gs.SessionData) ? "In deiner Klasse " : "In der Wertung ";
            if (!gap.IsRaceGap || gap.Kind == RaceGapKind.TrackDistanceEstimate)
                return basis + "liegt " + (string.IsNullOrWhiteSpace(who) ? "ein Gegner" : who) + " " + direction +
                    ". Einen Zeitabstand habe ich gerade nicht sicher.";
            string interval = (gap.IsEstimated ? "etwa " : "") + gap.Seconds.ToString("0.0", de) + " Sekunden";
            return RaceRadioLanguage.MeasuredGap(ahead, who, interval, RaceRadioLanguage.SeparateClass(gs.SessionData));
        }

        internal static string CompareRivalLap(string question, RaceDataContext ctx)
        {
            if (ctx?.GameState == null || string.IsNullOrWhiteSpace(question) || ctx.TelemetryAgeSeconds < 0 ||
                ctx.TelemetryAgeSeconds > 1 || !Regex.IsMatch(question, @"\b(rundenzeitvergleich|rundenzeit(en)?|bestzeit(en)?|letzte runde)\b",
                    RegexOptions.IgnoreCase) || !Regex.IsMatch(question, @"\b(vergleich\w*|schneller|langsamer|rundenzeitvergleich|gegen|gegenüberstell\w*|unterschied|differenz)\b",
                    RegexOptions.IgnoreCase)) return null;
            var gs = ctx.GameState;
            var matches = RaceFieldTools.NamedInQuestion(question, gs);
            if (matches.Count > 0 && !ctx.SendDriverNames) return "Die Namensfreigabe ist ausgeschaltet.";
            if (matches.Count > 1) return "Welche Startnummer meinst du?";
            bool ahead = Ahead.IsMatch(question), behind = Behind.IsMatch(question);
            var car = matches.Count == 1 ? matches[0] : ahead != behind && gs.SessionData.SessionType == SessionType.Race ?
                RaceDataTools.Neighbour(gs, ahead) : null;
            if (car == null || !car.IsActive || car.InPits) return null;
            if (ctx.Tracks != null && !ctx.Tracks.OpponentFresh(ctx, car))
                return "Für den Vergleich fehlen mir gerade frische Daten zu diesem Fahrer.";
            bool best = Regex.IsMatch(question, @"\bbestzeit(en)?\b", RegexOptions.IgnoreCase);
            float own = best ? gs.SessionData.PlayerLapTimeSessionBest : gs.SessionData.LapTimePrevious;
            float rival = best ? car.CurrentBestLapTime : car.LastLapTime;
            if (!best && (!gs.SessionData.PreviousLapWasValid || !car.LastLapValid) ||
                !TrackAwareness.InRange(own, 10, 3600).HasValue || !TrackAwareness.InRange(rival, 10, 3600).HasValue)
                return "Für den Rundenzeitvergleich fehlen gültige Zeiten.";
            string name = ctx.SendDriverNames && car.CanUseName ? RadioPhraseCatalog.SafeRivalName(car.DriverRawName) : "";
            if (name.Length == 0) name = "Der Gegner";
            double delta = rival - own;
            if (Math.Abs(delta) < .05) return (best ? "Eure Bestzeiten" : "Eure letzten gültigen Runden") + " liegen fast gleichauf.";
            return (best ? "Bei der Bestzeit liegt " + name + " " + GermanRadioText.Interval(Math.Abs(delta)) +
                (delta < 0 ? " vorn." : " hinter dir.") : "Letzte Runde war " + name + " " +
                GermanRadioText.Interval(Math.Abs(delta)) + (delta < 0 ? " schneller." : " langsamer."));
        }

        public static string TryAnswer(string question, GameStateData gs, AiRaceMemory memory, bool allowDriverNames = true)
        {
            if (gs == null || string.IsNullOrWhiteSpace(question))
            {
                return null;
            }
            string q = question.ToLowerInvariant();
            if (q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 10 || complexMarkers.Any(m => q.Contains(m)))
            {
                return null;
            }

            string trimmed = q.Trim(trimChars);
            if (q.Contains("wo bin ich") || q.Contains("wo auf der strecke") || q.Contains("welchem sektor") ||
                trimmed == "where am i" || trimmed == "where am i on track" || AnyOf(q, "which sector", "what sector") ||
                trimmed == "dónde estoy" || trimmed == "donde estoy" || AnyOf(q, "qué sector", "que sector"))
            {
                int sector = gs.SessionData.SectorNumber;
                return gs.PitData.InPitlane ? RadioLanguage.T("Du bist in der Boxengasse.", "You're in the pit lane.", "Estás en el carril de boxes.") :
                    sector >= 1 && sector <= 3 ? RadioLanguage.T("Du bist in Sektor ", "You're in sector ", "Estás en el sector ") + sector + "." :
                    RadioLanguage.T("Den aktuellen Streckensektor liefert die Simulation gerade nicht sicher.", "The sim isn't giving a reliable sector right now.",
                        "El simulador no da el sector con seguridad ahora mismo.");
            }

            bool lapTimeWords = AnyOf(q, "rundenzeit", "letzte runde", "lap time", "laptime", "last lap", "tiempo de vuelta", "última vuelta", "ultima vuelta");
            var named = RaceFieldTools.NamedInQuestion(question, gs);
            if (named.Count > 0 && !allowDriverNames)
                return RadioLanguage.T("Die Namensfreigabe ist ausgeschaltet.", "Driver names are switched off.", "Los nombres de pilotos están desactivados.");
            if (named.Count > 1)
                return RadioLanguage.T("Der Fahrername ist nicht eindeutig. Welche Startnummer meinst du?", "That name isn't unique. Which car number?",
                    "Ese nombre no es único. ¿Qué dorsal?");
            if (named.Count == 1)
            {
                var opponent = named[0];
                if (lapTimeWords)
                    return opponent.LastLapTime > 0 && opponent.LastLapTime < 3600 ?
                        opponent.DriverRawName + RadioLanguage.T(": letzte Runde ", ": last lap ", ": última vuelta ") + LapTime(opponent.LastLapTime) + "." :
                        RadioLanguage.T("Für diesen Fahrer liegt noch keine Rundenzeit vor.", "No lap time for that driver yet.", "Todavía no hay tiempo de vuelta de ese piloto.");
                if (AnyOf(q, "position", "platz", "place", "posición", "posicion", "puesto"))
                    return opponent.ClassPosition > 0 ?
                        opponent.DriverRawName + RadioLanguage.T(": Klassenposition ", ": class position ", ": posición en su clase ") + opponent.ClassPosition + "." :
                        RadioLanguage.T("Für diesen Fahrer fehlt gerade eine verlässliche Position.", "No reliable position for that driver right now.",
                            "Ahora mismo no tengo una posición fiable de ese piloto.");
                return null;
            }
            if (AnyOf(q, "sprit", "benzin", "kraftstoff", "tank", "fuel", "combustible", "gasolina", "depósito", "deposito") &&
                AnyOf(q, "wie viel", "wieviel", "stand", "noch", "how much", "left", "remaining", "level", "cuánt", "cuant", "queda", "nivel"))
            {
                // Unknown rival names must never fall through to our own tank value.
                if (q.Contains(" hat ") || q.Contains(" haben die ") || q.Contains("gegner") || q.Contains("vordermann") || q.Contains("hintermann") ||
                    AnyOf(q, " has ", " does ", " have they", "opponent", "rival", "car ahead", "car in front", "car behind", " tiene ", " tienen ",
                        "el de delante", "el de detrás", "el de detras")) return null;
                if (!gs.FuelData.FuelUseActive || float.IsNaN(gs.FuelData.FuelLeft) || float.IsInfinity(gs.FuelData.FuelLeft) || gs.FuelData.FuelLeft < 0)
                {
                    return RadioLanguage.T("Gerade kein verlässlicher Tankstand.", "No reliable fuel reading right now.", "Ahora mismo no hay un nivel de combustible fiable.");
                }
                string litres = RadioLanguage.Num(gs.FuelData.FuelLeft, "0");
                float perLap = FuelEstimate.Consumption(memory, gs, out bool provisional, out _);
                if (perLap > 0)
                {
                    int laps = (int)Math.Floor(gs.FuelData.FuelLeft / perLap);
                    return RadioLanguage.T("Noch " + litres + " Liter. " + (provisional ? "Erste Schätzung: etwa " : "Das reicht für etwa ") + laps + (laps == 1 ? " Runde." : " Runden."),
                        litres + " litres left. " + (provisional ? "First estimate: about " : "Good for about ") + laps + (laps == 1 ? " lap." : " laps."),
                        "Quedan " + litres + " litros. " + (provisional ? "Primera estimación: unas " : "Dan para unas ") + laps + (laps == 1 ? " vuelta." : " vueltas."));
                }
                return RadioLanguage.T("Noch " + litres + " Liter im Tank. Den Verbrauch hab ich noch nicht sauber gemessen.",
                    litres + " litres in the tank. I haven't measured consumption cleanly yet.",
                    "Quedan " + litres + " litros en el depósito. Aún no tengo bien medido el consumo.");
            }

            // Only explicitly self-referential short questions qualify for the own-car shortcut.
            bool ownQuestion = q.Contains("mein ") || q.Contains("meine") || q.Contains("unsere") || q.Contains("bin ich") || q.Contains("sind wir") ||
                q.Trim(' ', '?', '.', '!') == "letzte runde" || q.Trim(' ', '?', '.', '!') == "wie war die letzte runde" || q.Trim(' ', '?', '.', '!') == "rundenzeit" ||
                new[] { "my", "our", "am i", "are we", "mi", "mis", "nuestro", "nuestra", "estoy", "vamos", "voy" }.Any(w => TeamRouter.HasWord(q, w)) ||
                new[] { "last lap", "lap time", "how was the last lap", "última vuelta", "ultima vuelta", "tiempo de vuelta",
                    "cómo fue la última vuelta", "como fue la ultima vuelta" }.Contains(trimmed);
            if (!ownQuestion) return null;
            if (OwnTyreAnswer.Matches(question)) return OwnTyreAnswer.Answer(gs, DateTime.UtcNow);

            if (q.Contains("gesamtplatz") || q.Contains("klassenplatz") || q.Contains("position") || q.Contains("welcher platz") || q.Contains("welchem platz") || q.Contains("wievielter") || q.Contains("wie vielter") ||
                AnyOf(q, "what place", "which place", "posición", "posicion", "puesto", "qué lugar", "que lugar"))
            {
                var session = gs.SessionData;
                if (session.SessionType == SessionType.Race)
                {
                    if (AnyOf(q, "gesamt", "overall", "general"))
                        return session.OverallPosition > 0 ? RadioLanguage.T("Gesamtplatz " + session.OverallPosition + ".", "P" + session.OverallPosition + " overall.",
                                "Posición " + session.OverallPosition + " en la general.") :
                            RadioLanguage.T("Deinen Gesamtplatz habe ich gerade nicht sicher.", "I don't have your overall position for sure right now.",
                                "Ahora mismo no tengo seguro tu puesto en la general.");
                    if (AnyOf(q, "klasse", "class", "clase"))
                        return session.ClassPosition > 0 ? RadioLanguage.T("Klassenplatz " + session.ClassPosition + ".", "P" + session.ClassPosition + " in class.",
                                "Posición " + session.ClassPosition + " en tu clase.") :
                            RadioLanguage.T("Deinen Klassenplatz habe ich gerade nicht sicher.", "I don't have your class position for sure right now.",
                                "Ahora mismo no tengo seguro tu puesto en la clase.");
                    if (RaceRadioLanguage.SeparateClass(session)) return RaceRadioLanguage.RaceRanks(session) + ".";
                }
                int pos = session.ClassPosition > 0 ? session.ClassPosition : session.OverallPosition;
                if (pos <= 0)
                {
                    return RadioLanguage.T("Deinen Platz hab ich gerade nicht sicher.", "I'm not sure of your position right now.", "Ahora mismo no tengo seguro tu puesto.");
                }
                int cars = gs.SessionData.NumCarsInPlayerClass;
                return cars > 0 ? RadioLanguage.T("Platz " + pos + " von " + cars + ".", "P" + pos + " of " + cars + ".", "Posición " + pos + " de " + cars + ".") :
                    RadioLanguage.T("Platz " + pos + ".", "P" + pos + ".", "Posición " + pos + ".");
            }

            if (lapTimeWords)
            {
                float last = gs.SessionData.LapTimePrevious;
                if (float.IsNaN(last) || float.IsInfinity(last) || last <= 0 || last >= 3600)
                {
                    return RadioLanguage.T("Noch keine gezeitete Runde.", "No timed lap yet.", "Todavía no hay vuelta cronometrada.");
                }
                string answer = RadioLanguage.T("Letzte Runde ", "Last lap ", "Última vuelta ") + LapTime(last) +
                    (gs.SessionData.PreviousLapWasValid ? "." : RadioLanguage.T(", aber ungültig.", ", but invalid.", ", pero no válida."));
                float best = gs.SessionData.PlayerLapTimeSessionBest;
                if (best > 0 && best < last - 0.05f)
                {
                    answer += RadioLanguage.T(" Bestzeit ", " Best ", " Mejor vuelta ") + LapTime(best) + ".";
                }
                return answer;
            }
            return null;
        }

        public static string LapTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds >= 86400)
                return RadioLanguage.T("Zeit nicht verfügbar", "Time not available", "Tiempo no disponible");
            var culture = RadioLanguage.Culture;
            long milliseconds = (long)Math.Round((double)seconds * 1000, MidpointRounding.AwayFromZero);
            long minutes = milliseconds / 60000;
            decimal remainder = (milliseconds % 60000) / 1000m;
            return minutes > 0 ? minutes + ":" + remainder.ToString("00.000", culture) : remainder.ToString("0.000", culture);
        }
    }
}
