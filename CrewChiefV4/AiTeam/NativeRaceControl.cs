using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennleitung aus Crew Chiefs eigener Erkennung (Rob 20.11.2026, LMU: „Du musst viel mehr nativ abgreifen“). Bei „nur das
    /// KI-Team spricht“ wurden Crew Chiefs Meldungen bisher stumm verworfen: gelbe und grüne Sektoren, Strafart und Grund,
    /// Runden bis zum Absitzen, „jetzt an die Box“. Diese Fälle werden hier erkannt und von der Crew in eigenen Worten gesagt.
    /// </summary>
    internal static class NativeRaceControl
    {
        /// <summary>Art der Meldung aus Crew Chiefs Klangordnern, null = nicht von hier.</summary>
        internal static string Classify(IEnumerable<string> folders)
        {
            foreach (string raw in folders ?? new string[0])
            {
                string f = (raw ?? "").Replace('\\', '/').Trim('/');
                if (!f.StartsWith("flags/", StringComparison.Ordinal) && !f.StartsWith("penalties/", StringComparison.Ordinal)) continue;
                for (int s = 1; s <= 3; s++)
                {
                    if (f == "flags/yellow_flag_sector_" + s) return "yellow:" + s;
                    if (f == "flags/double_yellow_flag_sector_" + s) return "dyellow:" + s;
                    if (f == "flags/green_flag_sector_" + s) return "green:" + s;
                }
                if (f == "flags/yellow_flag") return "yellow:0";
                if (f == "flags/double_yellow_flag") return "dyellow:0";
                if (f == "penalties/penalty_three_laps_left") return "pen-laps:3";
                if (f == "penalties/penalty_two_laps_left") return "pen-laps:2";
                if (f.StartsWith("penalties/penalty_one_lap_left", StringComparison.Ordinal) || f.StartsWith("penalties/one_lap_to_serve", StringComparison.Ordinal)) return "pen-laps:1";
                if (f == "penalties/pit_now_drive_through" || f == "penalties/pit_now_stop_go" || f == "penalties/warning_enter_pits_to_serve_penalty") return "pen-pitnow";
                if (f.StartsWith("penalties/drive_through_", StringComparison.Ordinal) || f == "penalties/new_penalty_drivethrough") return "pen-type:dt:" + Cause(f);
                if (f.StartsWith("penalties/stop_go_", StringComparison.Ordinal) || f == "penalties/new_penalty_stopgo") return "pen-type:sg:" + Cause(f);
                if (f == "penalties/time_penalty") return "pen-type:time:";
                if (f == "penalties/blue_move_now_or_be_penalized") return "pen-blue";
                // Platz zurückgeben nach Überholen unter Gelb (RaceRoom meldet es; Crew Chiefs eigene Meldung ging im Filter verloren).
                if (f == "flags/give_one_position_back_first_warning" || f == "flags/give_one_position_back_next_warning") return "giveback:1";
                if (f == "flags/give_positions_back_first_warning_intro" || f == "flags/give_positions_back_next_warning_intro") return "giveback:n";
                if (f == "flags/give_positions_back_completed") return "giveback-done";
            }
            return null;
        }

        private static string Cause(string f)
        {
            if (f.Contains("speeding")) return "speeding";
            if (f.Contains("cut")) return "cut";
            if (f.Contains("blue")) return "blue";
            if (f.Contains("false_start")) return "start";
            if (f.Contains("on_red")) return "red";
            if (f.Contains("overtaking")) return "pass";
            if (f.Contains("stint")) return "stint";
            return "";
        }

        internal static TeamMember Member(string kind) => kind.StartsWith("pen", StringComparison.Ordinal) || kind == "investigation" || kind == "giveback-done" ? TeamMember.Ben : TeamMember.Sara;

        /// <summary>„giveback:n“ bekommt die gemeldete Zahl der Plätze aus dem Spielstand.</summary>
        internal static string Resolve(string kind, GameStateData gs)
        {
            if (kind != "giveback:n" || gs == null) return kind;
            int n = gs.FlagData.numCarsPassedIllegally;
            return n == 1 ? "giveback:1" : n >= 2 && n <= 9 ? "giveback:" + n : kind;
        }

        /// <summary>Zuletzt gemeldete abgesessene Strafe (gegen doppelte Meldung aus Zählung und Rennleitung).</summary>
        internal static DateTime LastServedUtc = DateTime.MinValue;

        internal static int Priority(string kind) => kind.StartsWith("giveback:", StringComparison.Ordinal) ? 9 :
            kind.StartsWith("dyellow", StringComparison.Ordinal) || kind.StartsWith("yellow", StringComparison.Ordinal) ? 8 :
            kind == "pen-pitnow" || kind == "pen-laps:1" ? 8 : kind.StartsWith("pen-type", StringComparison.Ordinal) ? 7 : 5;

        internal static string Topic(string kind)
        {
            string[] part = kind.Split(':');
            return "warning-rc-" + (part[0] == "pen-type" || part.Length < 2 ? part[0] : part[0] + "-" + part[1]);
        }

        /// <summary>Gesprochener Satz (DE/EN/ES, mehrere Formulierungen, zuletzt gehörte werden gemieden).</summary>
        internal static string Text(string kind)
        {
            var options = Options(kind);
            return options == null ? null : RadioVariants.Pick("rc-" + kind, options);
        }

        private static string[] V(string key, params string[] options) => options;

        /// <summary>Alle Formulierungen der aktuellen Sprache (auch für vorbereitete Clips).</summary>
        internal static string[] Options(string kind)
        {
            string[] part = kind.Split(':');
            string n = part.Length > 1 ? part[1] : "";
            switch (part[0])
            {
                case "yellow":
                    if (n == "0") return V("rc-yellow-here",
                        RadioLanguage.T("Gelb, hier in deinem Abschnitt. Nicht überholen.", "Yellow, right here in your section. No overtaking.", "Amarilla aquí en tu zona. No adelantes."),
                        RadioLanguage.T("Achtung, Gelb vor dir.", "Careful, yellow ahead of you.", "Atención, amarilla delante."),
                        RadioLanguage.T("Gelbe Flagge, genau hier. Vorsicht.", "Yellow flag, right here. Careful.", "Bandera amarilla, justo aquí. Cuidado."),
                        RadioLanguage.T("Gelb, Gelb! Tempo anpassen.", "Yellow, yellow! Adjust your speed.", "¡Amarilla, amarilla! Ajusta el ritmo."));
                    return V("rc-yellow",
                        RadioLanguage.T("Gelb in Sektor " + n + ".", "Yellow in sector " + n + ".", "Amarilla en el sector " + n + "."),
                        RadioLanguage.T("Achtung, gelbe Flagge in Sektor " + n + ".", "Heads up, yellow flag in sector " + n + ".", "Atención, bandera amarilla en el sector " + n + "."),
                        RadioLanguage.T("Sektor " + n + " ist gelb, nicht überholen.", "Sector " + n + " is yellow, no overtaking.", "Sector " + n + " en amarilla, no adelantes."),
                        RadioLanguage.T("Gelb im Sektor " + n + ", Vorsicht dort.", "Yellow in sector " + n + ", careful there.", "Amarilla en el sector " + n + ", cuidado ahí."),
                        RadioLanguage.T("Gelbe Flagge, Sektor " + n + ". Tempo anpassen.", "Yellow flag, sector " + n + ". Adjust your speed.", "Bandera amarilla, sector " + n + ". Ajusta el ritmo."));
                case "dyellow":
                    string where = n == "0" ? RadioLanguage.T("hier", "right here", "aquí") : RadioLanguage.T("in Sektor " + n, "in sector " + n, "en el sector " + n);
                    return V("rc-dyellow",
                        RadioLanguage.T("Doppelt Gelb " + where + ", deutlich vom Gas!", "Double yellow " + where + ", big lift!", "¡Doble amarilla " + where + ", levanta mucho!"),
                        RadioLanguage.T("Achtung, doppelt Gelb " + where + ". Bereit zum Anhalten.", "Careful, double yellow " + where + ". Be ready to stop.", "Cuidado, doble amarilla " + where + ". Listo para parar."),
                        RadioLanguage.T("Doppelt Gelb " + where + ", ganz vorsichtig.", "Double yellow " + where + ", very careful.", "Doble amarilla " + where + ", con mucho cuidado."),
                        RadioLanguage.T("Doppelte gelbe Flagge " + where + ". Kein Überholen.", "Double yellow flag " + where + ". No passing.", "Doble bandera amarilla " + where + ". Prohibido adelantar."));
                case "green":
                    return V("rc-green",
                        RadioLanguage.T("Sektor " + n + " wieder grün.", "Sector " + n + " is green again.", "Sector " + n + " otra vez en verde."),
                        RadioLanguage.T("Grün in Sektor " + n + ", Strecke frei.", "Green in sector " + n + ", track clear.", "Verde en el sector " + n + ", pista libre."),
                        RadioLanguage.T("Sektor " + n + " ist wieder frei.", "Sector " + n + " is clear again.", "El sector " + n + " vuelve a estar libre."),
                        RadioLanguage.T("Gelb aufgehoben in Sektor " + n + ".", "Yellow lifted in sector " + n + ".", "Amarilla retirada en el sector " + n + "."));
                case "pen-laps":
                    string laps = n == "1" ? RadioLanguage.T("eine Runde", "one lap", "una vuelta") : n == "2" ? RadioLanguage.T("zwei Runden", "two laps", "dos vueltas") : RadioLanguage.T("drei Runden", "three laps", "tres vueltas");
                    return V("rc-pen-laps",
                        RadioLanguage.T("Noch " + laps + ", um die Strafe abzusitzen.", laps + " left to serve the penalty.", "Quedan " + laps + " para cumplir la penalización."),
                        RadioLanguage.T("Die Strafe muss in " + laps + " erledigt sein.", "The penalty has to be served within " + laps + ".", "Hay que cumplir la penalización en " + laps + "."),
                        RadioLanguage.T("Denk an die Strafe: " + laps + " bleiben dafür.", "Remember the penalty: " + laps + " left for it.", "Recuerda la penalización: quedan " + laps + "."),
                        RadioLanguage.T("Strafe offen, " + laps + " Zeit zum Absitzen.", "Penalty still open, " + laps + " to serve it.", "Penalización pendiente, " + laps + " para cumplirla.")).Select(t => t +
                        (n == "1" ? RadioLanguage.T(" Diese Runde rein!", " Box this lap!", " ¡Entra esta vuelta!") : "")).ToArray();
                case "pen-pitnow":
                    return V("rc-pen-pitnow",
                        RadioLanguage.T("Box jetzt, Strafe absitzen.", "Box now, serve the penalty.", "Entra ya, cumple la penalización."),
                        RadioLanguage.T("Jetzt rein für die Strafe, sonst droht die Disqualifikation.", "In now for the penalty, or we risk a disqualification.", "Entra ahora a cumplir, o nos arriesgamos a la descalificación."),
                        RadioLanguage.T("Diese Runde an die Box, die Strafe muss jetzt erledigt werden.", "Pit this lap, the penalty has to be done now.", "A boxes esta vuelta, hay que cumplir ya."),
                        RadioLanguage.T("Letzte Gelegenheit: Box für die Strafe.", "Last chance: pit for the penalty.", "Última oportunidad: a boxes por la penalización."));
                case "pen-type":
                    var type = n == "dt" ? PenatiesData.DetailedPenaltyType.DRIVE_THROUGH : n == "sg" ? PenatiesData.DetailedPenaltyType.STOP_AND_GO : PenatiesData.DetailedPenaltyType.NONE;
                    string cause = part.Length > 2 ? part[2] : "";
                    var why = cause == "speeding" ? PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE : cause == "cut" ? PenatiesData.DetailedPenaltyCause.CUT_TRACK :
                        cause == "blue" ? PenatiesData.DetailedPenaltyCause.IGNORED_BLUE_FLAG : cause == "start" ? PenatiesData.DetailedPenaltyCause.FALSE_START :
                        cause == "red" ? PenatiesData.DetailedPenaltyCause.EXITING_PITS_UNDER_RED : cause == "pass" ? PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_ROLLING_BEFORE_GREEN :
                        PenatiesData.DetailedPenaltyCause.NONE;
                    string what = n == "time" ? RadioLanguage.T("eine Zeitstrafe", "a time penalty", "una penalización de tiempo") : IncidentWatch.DetailedPenalty(type, why);
                    return V("rc-pen-type",
                        RadioLanguage.T("Die Rennleitung meldet " + what + ".", "Race control reports " + what + ".", "Dirección de carrera indica " + what + "."),
                        RadioLanguage.T("Bestätigt: " + what + ".", "Confirmed: " + what + ".", "Confirmado: " + what + "."),
                        RadioLanguage.T("Das ist " + what + ".", "That's " + what + ".", "Es " + what + "."),
                        RadioLanguage.T("Von der Rennleitung: " + what + ".", "From race control: " + what + ".", "De dirección de carrera: " + what + "."));
                case "investigation":
                    return V("rc-investigation",
                        RadioLanguage.T("Die Rennleitung untersucht einen Vorfall mit dir. Noch keine Strafe.", "Race control is investigating an incident involving you. No penalty yet.", "Dirección de carrera investiga un incidente contigo. Aún sin sanción."),
                        RadioLanguage.T("Achtung, Untersuchung der Rennleitung, du bist beteiligt.", "Heads up, race control investigation, you're involved.", "Atención, investigación de dirección de carrera, estás implicado."),
                        RadioLanguage.T("Die Kommissare schauen sich eine Szene mit dir an. Sauber weiterfahren.", "The stewards are looking at an incident with you. Keep it clean.", "Los comisarios revisan una acción tuya. Sigue limpio."),
                        RadioLanguage.T("Untersuchung angekündigt. Ruhig bleiben, entschieden ist noch nichts.", "Investigation announced. Stay calm, nothing's decided yet.", "Investigación anunciada. Tranquilo, aún no hay nada decidido."));
                case "pen-served":
                    return V("rc-pen-served",
                        RadioLanguage.T("Strafe abgesessen, das ist erledigt.", "Penalty served, that's done.", "Penalización cumplida, asunto cerrado."),
                        RadioLanguage.T("Sauber, die Strafe ist durch. Jetzt wieder Tempo.", "Good, the penalty's done. Back up to speed now.", "Bien, la penalización está cumplida. Ahora a recuperar ritmo."),
                        RadioLanguage.T("Die Rennleitung führt die Strafe als erledigt.", "Race control shows the penalty as served.", "Dirección de carrera da la penalización por cumplida."),
                        RadioLanguage.T("Strafe ist weg. Konzentrier dich wieder aufs Fahren.", "Penalty cleared. Focus back on driving.", "Penalización borrada. Vuelve a centrarte en conducir."));
                case "giveback":
                    string places = n == "1" ? RadioLanguage.T("den Platz", "the place", "la posición") :
                        n == "n" ? RadioLanguage.T("die Plätze", "the places", "las posiciones") :
                        RadioLanguage.T(n + " Plätze", n + " places", n + " posiciones");
                    string him = n == "1" ? RadioLanguage.T("ihn", "him", "") : RadioLanguage.T("sie", "them", "s");
                    return V("rc-giveback",
                        RadioLanguage.T("Unter Gelb überholt, gib " + places + " zurück!", "You passed under yellow, give " + places + " back!", "Adelantaste con amarilla, ¡devuelve " + places + "!"),
                        RadioLanguage.T("Das war unter Gelb: " + places + " zurückgeben.", "That was under yellow: give " + places + " back.", "Fue con amarilla: devuelve " + places + "."),
                        RadioLanguage.T("Achtung, Überholen unter Gelb. Lass " + him + " wieder vorbei.", "Careful, that pass was under yellow. Let " + him + " back past.", "Ojo, adelantamiento con amarilla. Déjale" + him + " pasar otra vez."),
                        RadioLanguage.T("Zurückgeben musst du " + places + ", sonst gibt es eine Strafe.", "You have to give " + places + " back or it's a penalty.", "Tienes que devolver " + places + " o habrá sanción."),
                        RadioLanguage.T("Gelbverstoß: " + places + " zurück, dann ist es erledigt.", "Yellow flag breach: give " + places + " back and it's sorted.", "Infracción con amarilla: devuelve " + places + " y queda resuelto."));
                case "giveback-done":
                    return V("rc-giveback-done",
                        RadioLanguage.T("Platz zurückgegeben, das ist sauber.", "Place given back, that's clean.", "Puesto devuelto, todo limpio."),
                        RadioLanguage.T("Erledigt, die Rennleitung ist zufrieden.", "Done, race control is happy.", "Hecho, dirección de carrera conforme."),
                        RadioLanguage.T("Gut gemacht, der Gelbverstoß ist vom Tisch.", "Well done, the yellow flag issue is closed.", "Bien hecho, asunto de la amarilla cerrado."),
                        RadioLanguage.T("Sauber zurückgegeben, jetzt wieder angreifen.", "Cleanly handed back, now attack again.", "Devuelto limpio, ahora a atacar de nuevo."));
                case "pen-blue":
                    return V("rc-pen-blue",
                        RadioLanguage.T("Blaue Flagge, lass ihn jetzt vorbei, sonst gibt es eine Strafe.", "Blue flag, let him by now or we get a penalty.", "Bandera azul, déjale pasar ya o nos penalizan."),
                        RadioLanguage.T("Platz machen, die Rennleitung droht mit Strafe.", "Make room, race control is threatening a penalty.", "Abre hueco, dirección de carrera amenaza con penalizar."),
                        RadioLanguage.T("Jetzt vorbeilassen, letzte Warnung wegen Blau.", "Let him through now, last warning for blue.", "Déjale pasar ya, último aviso por azul."),
                        RadioLanguage.T("Blau ignoriert droht Strafe, lass ihn durch.", "Ignoring blue means a penalty, let him through.", "Ignorar la azul supone penalización, déjale pasar."));
            }
            return null;
        }

        /// <summary>Kurze Flaggensätze für Sara vorab rendern.</summary>
        internal static IEnumerable<string> PreparedPhrases =>
            new[] { "yellow:0", "dyellow:0", "yellow:1", "yellow:2", "yellow:3", "dyellow:1", "dyellow:2", "dyellow:3", "green:1", "green:2", "green:3" }.SelectMany(Options);

        /// <summary>Gilt die Meldung noch? Gelb/Grün nach dem aktuellen Sektorstand, Strafen solange eine offen ist.</summary>
        internal static bool StillCurrent(string kind, GameStateData gs)
        {
            if (gs == null) return false;
            string[] part = kind.Split(':');
            int sector = part.Length > 1 && int.TryParse(part[1], out int s) ? s : 0;
            var flags = gs.FlagData.sectorFlags;
            FlagEnum flag = sector >= 1 && flags != null && sector <= flags.Length ? flags[sector - 1] : FlagEnum.UNKNOWN;
            switch (part[0])
            {
                // Im eigenen Abschnitt sagt Sara das lokale Gelb schon über die Flaggenbeobachtung – nicht doppelt.
                case "yellow": return sector == 0 ? !gs.FlagData.isLocalYellow : flag == FlagEnum.UNKNOWN || flag == FlagEnum.YELLOW || flag == FlagEnum.DOUBLE_YELLOW;
                case "dyellow": return sector == 0 ? !gs.FlagData.isLocalYellow : flag == FlagEnum.UNKNOWN || flag == FlagEnum.DOUBLE_YELLOW || flag == FlagEnum.YELLOW;
                case "green": return flag == FlagEnum.UNKNOWN || flag == FlagEnum.GREEN;
                case "pen-laps": case "pen-pitnow": return !gs.PitData.InPitlane && gs.PenaltiesData.NumOutstandingPenalties != 0;
                case "giveback": return gs.FlagData.numCarsPassedIllegally > 0;
                default: return true;
            }
        }
    }
}
