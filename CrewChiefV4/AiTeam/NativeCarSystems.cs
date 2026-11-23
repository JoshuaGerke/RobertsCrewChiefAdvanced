using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Fahrzeugsysteme aus Crew Chiefs eigener Erkennung (Rob 23.11.2026: „Alles, was Crew Chief erkennt, sofort melden und mit
    /// vier bis sechs Formulierungen wie eine echte Boxencrew sagen“). Bei „nur das KI-Team spricht“ gingen DRS, Push-to-Pass,
    /// Fahrerwechsel und Batterie bisher stumm verloren. Crew Chief entscheidet weiter, wann etwas zu sagen ist; die Crew sagt es
    /// in eigenen Worten: Mik für Technik und Energie, Ben für Fahrerwechsel und das DRS-Fenster zum Gegner.
    /// </summary>
    internal static class NativeCarSystems
    {
        /// <summary>Crew-Chief-Klangordner je Meldung, in der Reihenfolge der Prüfung (zusammengesetzte Meldungen zuerst).</summary>
        private static readonly string[][] Rules =
        {
            new[] { "overtaking_aids/push_to_pass_now_available", "ptp-ready" },
            new[] { "overtaking_aids/no_activations_remaining", "ptp-left:0" },
            new[] { "overtaking_aids/one_activation_remaining", "ptp-left:1" },
            new[] { "overtaking_aids/three_ptp_activations_remaining", "ptp-left:3" },
            new[] { "overtaking_aids/five_ptp_activations_remaining", "ptp-left:5" },
            new[] { "overtaking_aids/ten_ptp_activations_remaining", "ptp-left:10" },
            new[] { "overtaking_aids/remember_to_use_ptp", "ptp-use" },
            new[] { "overtaking_aids/drs_enabled", "drs-on" },
            new[] { "overtaking_aids/drs_disabled", "drs-off" },
            new[] { "overtaking_aids/dont_forget_drs", "drs-missed" },
            new[] { "overtaking_aids/remember_to_use_kers", "kers-missed" },
            new[] { "overtaking_aids/a_second_off_drs_range", "drs-reach:second" },
            new[] { "overtaking_aids/a_few_tenths_off_drs_range", "drs-reach:tenths" },
            new[] { "overtaking_aids/guy_behind_has_drs", "drs-behind" },
            new[] { "overtaking_aids/no_drs_activations_remaining", "drs-left:0" },
            new[] { "overtaking_aids/one_drs_activation_remaining", "drs-left:1" },
            new[] { "overtaking_aids/drs_activations_remaining", "drs-left" },
            new[] { "driver_swaps/pit_this_lap_driver_change_no_more_stints", "stint-final" },
            new[] { "driver_swaps/pit_this_lap_for_driver_change", "stint-box" },
            new[] { "driver_swaps/pit_now_for_driver_change", "stint-boxnow" },
            new[] { "driver_swaps/15_minutes_left_in_stint", "stint-min:15" },
            new[] { "driver_swaps/10_minutes_left_in_stint", "stint-min:10" },
            new[] { "driver_swaps/5_minutes_left_in_stint", "stint-min:5" },
            new[] { "driver_swaps/2_minutes_left_in_stint", "stint-min:2" },
            new[] { "battery/about_to_run_out", "batt-out" },
            new[] { "battery/critical_battery", "batt-critical" },
            new[] { "battery/low_battery", "batt-low" },
            new[] { "battery/one_lap_battery", "batt-laps:1" },
            new[] { "battery/two_laps_battery", "batt-laps:2" },
            new[] { "battery/three_laps_battery", "batt-laps:3" },
            new[] { "battery/four_laps_battery", "batt-laps:4" },
            new[] { "battery/two_minutes_battery", "batt-min:2" },
            new[] { "battery/five_minutes_battery", "batt-min:5" },
            new[] { "battery/ten_minutes_battery", "batt-min:10" },
            new[] { "battery/half_distance_low_battery", "batt-half:low" },
            new[] { "battery/half_distance_good_battery", "batt-half:good" },
            new[] { "battery/we_estimate", "batt-half:swap" },
            new[] { "battery/half_charge_warning", "batt-halfcharge" },
            new[] { "battery/battery_use_increasing", "batt-use:up" },
            new[] { "battery/battery_use_reducing", "batt-use:down" },
        };

        /// <summary>Teile von Crew Chiefs Antworten auf Sprachbefehle: die beantwortet die Crew selbst, sie sind keine Erkennung.</summary>
        private static readonly string[] AnswerParts =
        {
            "battery/plenty_of_battery", "battery/percent_remaining", "battery/we_will_get_another", "battery/on_last_lap_you_used",
            "battery/percent_of_your_battery", "battery/battery_use_stable",
        };

        /// <summary>Art der Meldung, null = nicht von hier. Zähler (DRS, Push-to-Pass) kommen aus dem Spielstand im Moment der Erkennung.</summary>
        internal static string Classify(IEnumerable<string> folders, string messageName, GameStateData gs)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in folders ?? new string[0]) seen.Add(Normalize(raw));
            if (!string.IsNullOrEmpty(messageName)) seen.Add(Normalize(messageName));
            if (AnswerParts.Any(seen.Contains)) return null;
            foreach (var rule in Rules)
            {
                if (!seen.Contains(rule[0])) continue;
                string kind = rule[1];
                if (kind == "ptp-ready" || kind == "drs-left")
                {
                    int left = gs == null ? -1 : kind == "ptp-ready" ? gs.OvertakingAids.PushToPassActivationsRemaining : gs.OvertakingAids.DrsActivationsRemaining;
                    if (left < 0 || left > 99) return kind == "ptp-ready" ? "ptp-ready:" : null;
                    return kind + ":" + left.ToString(CultureInfo.InvariantCulture);
                }
                if (kind == "batt-out" && seen.Contains("mandatory_pit_stops/pit_this_lap")) return "batt-out:box";
                return kind;
            }
            return null;
        }

        private static string Normalize(string raw) => (raw ?? "").Replace('\\', '/').Trim('/');

        private static string Family(string kind) { int i = kind.IndexOf(':'); return i < 0 ? kind : kind.Substring(0, i); }

        private static string Arg(string kind) { int i = kind.IndexOf(':'); return i < 0 ? "" : kind.Substring(i + 1); }

        /// <summary>Muss jetzt rein oder droht liegenzubleiben: das ist Sicherheit und darf auch unter Gelb und im Zweikampf durch.</summary>
        internal static bool Urgent(string kind)
        {
            string f = Family(kind);
            return f == "stint-box" || f == "stint-boxnow" || f == "stint-final" || f == "batt-out" || f == "batt-critical" ||
                kind == "batt-laps:1" || kind == "batt-min:2";
        }

        internal static string Topic(string kind) => (Urgent(kind) ? "warning-car-" : "car-") + Family(kind);

        internal static TeamMember Member(string kind)
        {
            string f = Family(kind);
            return f.StartsWith("stint", StringComparison.Ordinal) || f == "drs-reach" || f == "drs-behind" ? TeamMember.Ben : TeamMember.Mik;
        }

        internal static int Priority(string kind)
        {
            if (Urgent(kind)) return 8;
            string f = Family(kind);
            if (f == "drs-behind" || f == "batt-low" || f == "batt-laps" || f == "batt-min" || f == "stint-min" && Arg(kind) == "2") return 6;
            if (f == "drs-missed" || f == "kers-missed" || f == "ptp-use" || f == "batt-use") return 3;
            return 5;
        }

        /// <summary>„Box diese Runde“ und vergessenes DRS gelten nur in der Runde, in der Crew Chief es erkannt hat.</summary>
        internal static bool LapBound(string kind)
        {
            string f = Family(kind);
            return f == "stint-box" || f == "stint-final" || f == "batt-out" || f == "drs-reach" || f == "drs-behind";
        }

        /// <summary>Gilt die Meldung noch? Zähler müssen noch stimmen, sonst wäre die Zahl falsch.</summary>
        internal static bool StillCurrent(string kind, GameStateData live)
        {
            if (live == null || live.SessionData.SessionType != SessionType.Race && !Family(kind).StartsWith("batt", StringComparison.Ordinal)) return false;
            string f = Family(kind), n = Arg(kind);
            int count;
            if ((f == "ptp-left" || f == "ptp-ready") && int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                return live.OvertakingAids.PushToPassActivationsRemaining == count;
            if (f == "drs-left" && int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                return live.OvertakingAids.DrsActivationsRemaining == count;
            if (f == "drs-on") return live.OvertakingAids.DrsEnabled;
            if (f == "drs-off") return !live.OvertakingAids.DrsEnabled;
            return true;
        }

        /// <summary>Gesprochener Satz (DE/EN/ES, mehrere Formulierungen, zuletzt gehörte werden gemieden).</summary>
        internal static string Text(string kind)
        {
            var options = Options(kind);
            if (options == null) return null;
            string text = RadioVariants.Pick("car-" + kind, options);
            if (Family(kind) == "ptp-ready")
            {
                string left = Arg(kind);
                if (left.Length > 0)
                {
                    var rest = Options("ptp-left:" + left);
                    if (rest != null) text += " " + RadioVariants.Pick("car-ptp-left:" + left, rest);
                }
            }
            return text;
        }

        private static string T(string de, string en, string es) => RadioLanguage.T(de, en, es);

        /// <summary>Alle Formulierungen der aktuellen Sprache.</summary>
        internal static string[] Options(string kind)
        {
            string n = Arg(kind);
            switch (Family(kind))
            {
                case "drs-on":
                    return new[] {
                        T("DRS ist frei.", "DRS is enabled.", "DRS habilitado."),
                        T("DRS freigegeben, ab jetzt darfst du.", "DRS enabled, you can use it from now on.", "DRS habilitado, ya puedes usarlo."),
                        T("Ab jetzt gibt es DRS.", "DRS from here on.", "A partir de ahora hay DRS."),
                        T("DRS ist offen, nutz es im Fenster.", "DRS is open, use it in the window.", "El DRS está abierto, úsalo en la ventana."),
                        T("Freigabe für DRS ist da.", "We have DRS clearance.", "Tenemos el DRS autorizado.") };
                case "drs-off":
                    return new[] {
                        T("DRS ist gesperrt.", "DRS is disabled.", "DRS deshabilitado."),
                        T("Kein DRS im Moment.", "No DRS for now.", "Ahora mismo no hay DRS."),
                        T("DRS ist zu, bis auf Weiteres.", "DRS is off until further notice.", "DRS cerrado hasta nuevo aviso."),
                        T("DRS deaktiviert, nicht drücken.", "DRS deactivated, don't press it.", "DRS desactivado, no lo pulses.") };
                case "drs-missed":
                    return new[] {
                        T("Du hattest DRS, hast es aber nicht gezogen.", "You had DRS and didn't use it.", "Tenías DRS y no lo usaste."),
                        T("Da war DRS frei, nächstes Mal drücken.", "DRS was there, press it next time.", "Había DRS, la próxima vez púlsalo."),
                        T("DRS war da und blieb ungenutzt. Nicht vergessen.", "DRS was available and unused. Don't forget it.", "DRS disponible y sin usar. No lo olvides."),
                        T("Letzte Runde DRS verschenkt.", "We gave away DRS last lap.", "La última vuelta regalamos el DRS.") };
                case "kers-missed":
                    return new[] {
                        T("Den Boost hast du letzte Runde nicht genutzt.", "You didn't use the boost last lap.", "No usaste el boost la última vuelta."),
                        T("Da war noch Energie zum Abrufen.", "There was energy left to deploy.", "Quedaba energía por desplegar."),
                        T("Vergiss den Boost nicht.", "Don't forget the boost.", "No olvides el boost."),
                        T("Boost war bereit und blieb ungenutzt.", "Boost was ready and went unused.", "El boost estaba listo y no lo usaste.") };
                case "drs-reach":
                    if (n == "second") return new[] {
                        T("Eine Sekunde fehlt dir noch zum DRS.", "You're about a second off DRS range.", "Te falta un segundo para el DRS."),
                        T("Noch etwa eine Sekunde, dann bist du im DRS-Fenster.", "Another second or so and you're in the DRS window.", "Un segundo más y estás en la ventana de DRS."),
                        T("Knapp eine Sekunde bis DRS, dranbleiben.", "Just about a second to DRS, stay on it.", "Casi un segundo para el DRS, sigue ahí."),
                        T("Rück ran, eine Sekunde noch bis zum DRS.", "Close it up, one more second to DRS.", "Acércate, un segundo más para el DRS.") };
                    return new[] {
                        T("Nur noch ein paar Zehntel bis DRS.", "Only a few tenths off DRS.", "Solo unas décimas para el DRS."),
                        T("Ein paar Zehntel, dann hast du DRS.", "A few tenths and you'll have DRS.", "Unas décimas y tendrás DRS."),
                        T("Ganz knapp vor dem DRS-Fenster, bleib dran.", "Right on the edge of the DRS window, stay with them.", "Al borde de la ventana de DRS, sigue con él."),
                        T("Fast im DRS-Fenster, es fehlen nur Zehntel.", "Almost in the DRS window, just tenths now.", "Casi en la ventana de DRS, solo faltan décimas.") };
                case "drs-behind":
                    return new[] {
                        T("Hinter dir hat er DRS.", "The car behind has DRS.", "El de atrás tiene DRS."),
                        T("Achtung, dein Hintermann hat DRS.", "Heads up, the car behind has DRS.", "Atención, el coche de detrás tiene DRS."),
                        T("Der hinter dir ist im DRS-Fenster, Linie halten.", "The one behind is in the DRS window, hold your line.", "El de atrás está en la ventana de DRS, mantén tu línea."),
                        T("DRS für den Hintermann, mach die Gerade zu.", "DRS for the car behind, cover the straight.", "DRS para el de atrás, protege la recta.") };
                case "drs-left":
                    if (n == "0") return new[] {
                        T("DRS ist aufgebraucht.", "DRS is used up.", "DRS agotado."),
                        T("Keine DRS-Aktivierungen mehr.", "No DRS activations left.", "No quedan activaciones de DRS."),
                        T("Das war das letzte DRS.", "That was the last DRS.", "Ese era el último DRS."),
                        T("DRS ist alle, ab jetzt ohne.", "DRS all gone, we're without it now.", "Se acabó el DRS, ahora sin él.") };
                    if (n == "1") return new[] {
                        T("Nur noch eine DRS-Aktivierung.", "Only one DRS activation left.", "Solo queda una activación de DRS."),
                        T("Letztes DRS übrig, gut überlegen.", "Last DRS left, choose well.", "Queda el último DRS, elige bien."),
                        T("Ein DRS hast du noch.", "You've got one DRS left.", "Te queda un DRS."),
                        T("Noch genau einmal DRS.", "Exactly one more DRS.", "Exactamente un DRS más.") };
                    if (n.Length == 0) return null;
                    return new[] {
                        T("Du hast noch " + n + " DRS-Aktivierungen.", "You have " + n + " DRS activations left.", "Te quedan " + n + " activaciones de DRS."),
                        T("Noch " + n + " Mal DRS übrig.", n + " more DRS uses left.", "Quedan " + n + " usos de DRS."),
                        T(n + " DRS-Einsätze bleiben dir.", n + " DRS activations remaining.", n + " activaciones de DRS restantes."),
                        T("DRS: noch " + n + " Aktivierungen, gut einteilen.", "DRS: " + n + " activations left, use them wisely.", "DRS: quedan " + n + " activaciones, adminístralas bien.") };
                case "ptp-ready":
                    return new[] {
                        T("Push-to-Pass wieder bereit.", "Push to pass ready again.", "Push to pass listo otra vez."),
                        T("Push-to-Pass ist wieder geladen.", "Push to pass is charged again.", "El push to pass vuelve a estar cargado."),
                        T("Push-to-Pass steht wieder zur Verfügung.", "Push to pass is available again.", "Vuelves a tener push to pass."),
                        T("Der Boost ist wieder da.", "The boost is back.", "El boost ha vuelto.") };
                case "ptp-left":
                    if (n == "0") return new[] {
                        T("Push-to-Pass ist aufgebraucht.", "Push to pass is used up.", "Push to pass agotado."),
                        T("Kein Push-to-Pass mehr.", "No push to pass left.", "Ya no queda push to pass."),
                        T("Das war der letzte Push-to-Pass.", "That was the last push to pass.", "Ese era el último push to pass."),
                        T("Push-to-Pass ist alle, ab jetzt ohne.", "Push to pass all gone, we're without it now.", "Se acabó el push to pass, ahora sin él.") };
                    if (n == "1") return new[] {
                        T("Nur noch ein Push-to-Pass.", "Only one push to pass left.", "Solo queda un push to pass."),
                        T("Letzter Push-to-Pass übrig, gut einsetzen.", "Last push to pass left, make it count.", "Queda el último push to pass, aprovéchalo."),
                        T("Einen Push-to-Pass hast du noch.", "You've got one push to pass left.", "Te queda un push to pass."),
                        T("Noch genau einmal Push-to-Pass.", "Exactly one more push to pass.", "Exactamente un push to pass más.") };
                    if (n.Length == 0) return null;
                    return new[] {
                        T("Noch " + n + " Push-to-Pass.", n + " push to pass left.", "Quedan " + n + " push to pass."),
                        T("Du hast noch " + n + " Mal Push-to-Pass.", "You've got " + n + " push to pass left.", "Te quedan " + n + " push to pass."),
                        T(n + " Push-to-Pass bleiben dir.", n + " push to pass remaining.", n + " push to pass restantes."),
                        T("Push-to-Pass: noch " + n + " übrig.", "Push to pass: " + n + " to go.", "Push to pass: quedan " + n + ".") };
                case "ptp-use":
                    return new[] {
                        T("Du hast mehr Push-to-Pass als Runden, setz ihn ein.", "You've got more push to pass than laps, use it.", "Tienes más push to pass que vueltas, úsalo."),
                        T("Nutz den Push-to-Pass, wir haben genug für jede Runde.", "Use the push to pass, we have enough for every lap.", "Usa el push to pass, tenemos para cada vuelta."),
                        T("Spar ihn nicht auf, Push-to-Pass reicht für jede Runde.", "Don't save it, there's push to pass for every lap.", "No lo guardes, hay push to pass para cada vuelta."),
                        T("Push-to-Pass ruhig einsetzen, es ist genug übrig.", "Feel free to use push to pass, there's plenty left.", "Usa el push to pass sin miedo, quedan de sobra.") };
                case "stint-min":
                    return new[] {
                        T("Noch " + n + " Minuten in deinem Stint.", n + " minutes left in your stint.", "Te quedan " + n + " minutos de stint."),
                        T(n + " Minuten bis zum Fahrerwechsel.", n + " minutes to the driver change.", n + " minutos para el cambio de piloto."),
                        T("Dein Stint läuft noch " + n + " Minuten.", "Your stint has " + n + " minutes to go.", "A tu stint le quedan " + n + " minutos."),
                        T("In " + n + " Minuten ist Fahrerwechsel.", "Driver change in " + n + " minutes.", "Cambio de piloto en " + n + " minutos.") };
                case "stint-box":
                    return new[] {
                        T("Diese Runde rein zum Fahrerwechsel.", "Box this lap for the driver change.", "Entra a boxes esta vuelta para el cambio de piloto."),
                        T("Box diese Runde, Fahrerwechsel.", "Box this lap, driver change.", "Boxes esta vuelta, cambio de piloto."),
                        T("Dein Stint ist rum, komm diese Runde rein.", "Your stint is done, come in this lap.", "Tu stint ha terminado, entra esta vuelta."),
                        T("Ende dieser Runde Fahrerwechsel, Box.", "End of this lap, driver change, box.", "Al final de esta vuelta, cambio de piloto, a boxes.") };
                case "stint-final":
                    return new[] {
                        T("Box diese Runde, deine Fahrzeit ist dann voll.", "Box this lap, your driving time is used up.", "Boxes esta vuelta, tu tiempo de conducción se agota."),
                        T("Diese Runde rein, du hast deine maximale Fahrzeit erreicht.", "Come in this lap, you've reached your maximum drive time.", "Entra esta vuelta, has llegado a tu tiempo máximo."),
                        T("Komm diese Runde rein, mehr Stints darfst du nicht fahren.", "Box this lap, no more stints allowed for you.", "Entra esta vuelta, no puedes hacer más stints."),
                        T("Das war dein letzter Stint, diese Runde Fahrerwechsel.", "That's your last stint, driver change this lap.", "Es tu último stint, cambio de piloto esta vuelta.") };
                case "stint-boxnow":
                    return new[] {
                        T("Jetzt rein, Fahrerwechsel!", "Box now, driver change!", "¡Entra ya, cambio de piloto!"),
                        T("Box, Box, Fahrerwechsel.", "Box, box, driver change.", "Box, box, cambio de piloto."),
                        T("Nicht vergessen, jetzt in die Box zum Wechsel.", "Don't forget, box now for the change.", "No lo olvides, entra ya para el cambio."),
                        T("Box jetzt, dein Teamkollege wartet.", "Box now, your teammate is waiting.", "Entra ya, tu compañero te espera.") };
                case "batt-out":
                    if (n == "box") return new[] {
                        T("Batterie ist gleich leer, diese Runde rein!", "Battery is about to run out, box this lap!", "¡La batería se acaba, entra esta vuelta!"),
                        T("Die Ladung ist fast weg, Box diese Runde.", "Charge is almost gone, box this lap.", "Casi sin carga, boxes esta vuelta."),
                        T("Batterie am Ende, komm diese Runde rein.", "Battery's at its end, come in this lap.", "Batería al límite, entra esta vuelta."),
                        T("Gleich ist die Batterie leer, rein zum Tausch.", "Battery nearly empty, box for the swap.", "La batería está a punto de vaciarse, entra para el cambio.") };
                    return new[] {
                        T("Batterie ist gleich leer!", "Battery is about to run out!", "¡La batería se acaba!"),
                        T("Die Ladung ist fast weg.", "Charge is almost gone.", "Casi no queda carga."),
                        T("Batterie am Ende, jetzt alles sparen.", "Battery's at its end, save everything now.", "Batería al límite, ahorra todo lo que puedas."),
                        T("Gleich ist die Batterie leer, ganz sanft fahren.", "Battery nearly empty, drive very gently.", "La batería casi vacía, conduce muy suave.") };
                case "batt-critical":
                    return new[] {
                        T("Batterie kritisch!", "Battery critical!", "¡Batería crítica!"),
                        T("Kritischer Batteriestand, sofort sparen.", "Critical battery level, save right now.", "Nivel de batería crítico, ahorra ya."),
                        T("Batterie fast leer, das wird eng.", "Battery almost empty, this is getting tight.", "Batería casi vacía, va a estar justo."),
                        T("Achtung, Batterie im kritischen Bereich.", "Careful, battery is in the critical zone.", "Cuidado, la batería está en zona crítica.") };
                case "batt-low":
                    return new[] {
                        T("Die Batterie wird knapp.", "Battery is getting low.", "La batería se está agotando."),
                        T("Die Ladung geht zur Neige, schon mal sparen.", "Charge is running down, start saving.", "La carga baja, empieza a ahorrar."),
                        T("Niedriger Batteriestand, Verbrauch im Auge behalten.", "Low battery, keep an eye on usage.", "Batería baja, vigila el consumo."),
                        T("Batterie ist niedrig.", "Battery is low.", "La batería está baja.") };
                case "batt-laps":
                    if (n == "1") return new[] {
                        T("Die Batterie reicht nur noch eine Runde.", "Battery is good for one more lap only.", "La batería solo da para una vuelta más."),
                        T("Noch eine Runde Ladung.", "One lap of charge left.", "Queda una vuelta de carga."),
                        T("Eine Runde Batterie, mehr nicht.", "One lap of battery, no more.", "Una vuelta de batería, nada más."),
                        T("Die Ladung hält nur noch eine Runde.", "The charge only lasts one more lap.", "La carga solo aguanta una vuelta más.") };
                    return new[] {
                        T("Die Batterie reicht noch etwa " + n + " Runden.", "Battery is good for about " + n + " more laps.", "La batería da para unas " + n + " vueltas más."),
                        T("Noch " + n + " Runden Ladung.", n + " laps of charge left.", "Quedan " + n + " vueltas de carga."),
                        T("Wir schätzen " + n + " Runden Batterie.", "We estimate " + n + " laps of battery.", "Calculamos " + n + " vueltas de batería."),
                        T("Mit der Ladung kommst du noch " + n + " Runden.", "That charge gets you " + n + " more laps.", "Con esa carga haces " + n + " vueltas más.") };
                case "batt-min":
                    return new[] {
                        T("Die Batterie reicht noch etwa " + n + " Minuten.", "Battery is good for about " + n + " more minutes.", "La batería da para unos " + n + " minutos más."),
                        T("Noch " + n + " Minuten Ladung.", n + " minutes of charge left.", "Quedan " + n + " minutos de carga."),
                        T("Wir schätzen " + n + " Minuten Batterie.", "We estimate " + n + " minutes of battery.", "Calculamos " + n + " minutos de batería."),
                        T("Mit der Ladung kommst du noch " + n + " Minuten.", "That charge gets you " + n + " more minutes.", "Con esa carga aguantas " + n + " minutos más.") };
                case "batt-half":
                    if (n == "good") return new[] {
                        T("Halbzeit, die Batterie sieht gut aus.", "Halfway, the battery looks good.", "Mitad de carrera, la batería va bien."),
                        T("Zur Hälfte passt die Ladung.", "At half distance the charge is fine.", "A mitad de carrera la carga está bien."),
                        T("Halbe Distanz, mit der Batterie kommen wir hin.", "Half distance, the battery will get us there.", "Media distancia, con la batería llegamos."),
                        T("Halbzeit, beim Verbrauch sind wir im Plan.", "Halfway, usage is on plan.", "Mitad de carrera, el consumo va según el plan.") };
                    if (n == "swap") return new[] {
                        T("Halbzeit, die Ladung reicht nicht bis ins Ziel, wir planen den Fahrzeugtausch.", "Halfway, the charge won't reach the end, we're planning the car swap.", "Mitad de carrera, la carga no llega al final, planificamos el cambio de coche."),
                        T("Zur Hälfte ist klar: Wir brauchen den Wechsel.", "At half distance it's clear: we need the swap.", "A mitad de carrera está claro: necesitamos el cambio."),
                        T("Halbe Distanz, die Batterie hält nicht bis zum Ende. Der Tausch kommt.", "Half distance, the battery won't last to the end. The swap is coming.", "Media distancia, la batería no aguanta hasta el final. Llega el cambio."),
                        T("Halbzeit, wir fahren auf den Fahrzeugwechsel hin.", "Halfway, we're working towards the car swap.", "Mitad de carrera, vamos hacia el cambio de coche.") };
                    return new[] {
                        T("Halbzeit, so reicht die Batterie nicht. Verbrauch runter.", "Halfway, the battery won't last like this. Bring usage down.", "Mitad de carrera, así la batería no llega. Baja el consumo."),
                        T("Zur Hälfte zu viel Ladung verbraucht, jetzt sparen.", "Too much charge used by half distance, save now.", "Demasiada carga gastada a mitad de carrera, ahorra ya."),
                        T("Halbe Distanz, mit der Batterie wird es knapp.", "Half distance, it's getting tight on battery.", "Media distancia, vamos justos de batería."),
                        T("Halbzeit, wir müssen beim Verbrauch nachlassen.", "Halfway, we need to ease off on usage.", "Mitad de carrera, hay que reducir el consumo.") };
                case "batt-halfcharge":
                    return new[] {
                        T("Die Hälfte der Ladung ist weg.", "Half of the charge is gone.", "Se ha ido la mitad de la carga."),
                        T("Die Batterie steht bei etwa der Hälfte.", "Battery is at about half.", "La batería está más o menos a la mitad."),
                        T("Halbe Batterie verbraucht.", "Half the battery used.", "Media batería gastada."),
                        T("Nur noch rund die Hälfte Ladung.", "Only around half the charge left.", "Queda más o menos la mitad de la carga.") };
                case "batt-use":
                    if (n == "up") return new[] {
                        T("Dein Batterieverbrauch steigt.", "Your battery usage is going up.", "Tu consumo de batería está subiendo."),
                        T("Du ziehst mehr aus der Batterie als vorher.", "You're pulling more from the battery than before.", "Estás sacando más de la batería que antes."),
                        T("Der Verbrauch geht hoch, etwas sparen.", "Usage is climbing, save a little.", "El consumo sube, ahorra un poco."),
                        T("Mehr Batterieverbrauch in den letzten Runden.", "More battery usage over the last laps.", "Más consumo de batería en las últimas vueltas.") };
                    return new[] {
                        T("Dein Batterieverbrauch geht runter, gut so.", "Your battery usage is coming down, good.", "Tu consumo de batería baja, bien."),
                        T("Du verbrauchst weniger Ladung, sehr gut.", "You're using less charge, very good.", "Gastas menos carga, muy bien."),
                        T("Der Verbrauch sinkt, weiter so.", "Usage is dropping, keep it up.", "El consumo baja, sigue así."),
                        T("Weniger Batterieverbrauch als vorher, passt.", "Less battery usage than before, that works.", "Menos consumo de batería que antes, perfecto.") };
            }
            return null;
        }

        /// <summary>Alle Arten, für die die Crew Formulierungen hat (für Tests und die Übersicht).</summary>
        internal static IEnumerable<string> Kinds()
        {
            foreach (var rule in Rules)
            {
                string kind = rule[1];
                if (kind == "ptp-ready") { yield return "ptp-ready:"; yield return "ptp-ready:4"; continue; }
                if (kind == "drs-left") { yield return "drs-left:3"; continue; }
                yield return kind;
            }
            yield return "batt-out:box";
        }
    }

    /// <summary>Übergabe von Crew Chiefs Fahrzeugsystem-Meldungen an die Crew: abgreifen im Stimmfilter, prüfen, sagen.</summary>
    internal sealed class NativeCarSystemEvents
    {
        private sealed class Native { public string Kind, Source; public int Session, Lap; public SessionType Type; public DateTime Utc; }
        private sealed class Pending
        {
            public string Kind, Topic, Text, Evidence; public TeamMember Member; public int Priority, Session, Lap; public SessionType Type;
            public DateTime CapturedUtc; public ObservedRaceCall Call;
        }

        /// <summary>Dieselbe Meldung (gleiche Art und Zahl) nicht zweimal kurz hintereinander, auch wenn Crew Chief sie erneut erkennt.</summary>
        internal static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(45);
        internal static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(2);
        internal static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(8);

        private readonly ConcurrentQueue<Native> incoming = new ConcurrentQueue<Native>();
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
        private readonly Dictionary<string, DateTime> delivered = new Dictionary<string, DateTime>();

        internal int PendingCount => pending.Count;

        public void Reset()
        {
            Native unused; while (incoming.TryDequeue(out unused)) { }
            pending.Clear(); delivered.Clear();
        }

        /// <summary>Aus dem Stimmfilter (Crew-Chief-Thread): nur Werte, keine Verarbeitung.</summary>
        public bool Capture(IEnumerable<string> folders, string messageName, GameStateData gs, DateTime now)
        {
            if (gs == null) return false;
            string kind = NativeCarSystems.Classify(folders, messageName, gs);
            if (kind == null) return false;
            incoming.Enqueue(new Native { Kind = kind, Source = messageName, Session = gs.SessionData.SessionId, Type = gs.SessionData.SessionType,
                Lap = gs.SessionData.CompletedLaps, Utc = now });
            Native unused; while (incoming.Count > 32) incoming.TryDequeue(out unused);
            LiveDiagnostics.Decision(NativeCarSystems.Topic(kind), "captured", kind, messageName);
            return true;
        }

        public void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            Native n;
            while (incoming.TryDequeue(out n))
            {
                if (n.Session != gs.SessionData.SessionId || n.Type != gs.SessionData.SessionType || now < n.Utc || now - n.Utc > MaximumAge) continue;
                string topic = NativeCarSystems.Topic(n.Kind);
                DateTime last;
                if (delivered.TryGetValue(n.Kind, out last) && now >= last && now - last < RepeatWindow)
                { LiveDiagnostics.Decision(topic, "suppressed", "said_recently", n.Source); continue; }
                Pending same;
                if (pending.TryGetValue(topic, out same) && same.Kind == n.Kind && same.Call != null) continue;
                string text = NativeCarSystems.Text(n.Kind);
                if (string.IsNullOrWhiteSpace(text)) continue;
                pending[topic] = new Pending { Kind = n.Kind, Topic = topic, Text = text, Member = NativeCarSystems.Member(n.Kind),
                    Priority = NativeCarSystems.Priority(n.Kind), Session = n.Session, Type = n.Type,
                    Lap = NativeCarSystems.LapBound(n.Kind) ? n.Lap : -1, CapturedUtc = n.Utc,
                    Evidence = "native:" + n.Source + ";kind=" + n.Kind + ";captured=" + n.Utc.ToString("o") };
                LiveDiagnostics.Decision(topic, "detected", "native:" + n.Source, text);
            }
            foreach (var pair in pending.ToArray())
            {
                var p = pair.Value;
                if (p.Call != null && p.Call.DeliveryStartedUtc != DateTime.MinValue)
                { delivered[p.Kind] = p.Call.DeliveryStartedUtc; pending.Remove(pair.Key); continue; }
                if (gs.SessionData.SessionId != p.Session || gs.SessionData.SessionType != p.Type ||
                    p.Lap >= 0 && gs.SessionData.CompletedLaps != p.Lap || !NativeCarSystems.StillCurrent(p.Kind, gs) ||
                    p.Call == null && now - p.CapturedUtc > Lifetime || p.Call != null && (p.Call.DeliveryFailed || now > p.Call.ExpiresUtc))
                { LiveDiagnostics.Decision(p.Topic, "suppressed", "no_longer_current", p.Evidence); pending.Remove(pair.Key); }
            }
        }

        /// <summary>Dringendes (Box, Batterie leer) wartet nur auf Sara und dringenden Funk, der Rest auf freien Funk.</summary>
        public ObservedRaceCall Take(GameStateData gs, DateTime now, bool urgentBusy, bool busy, Func<TeamMember, bool> enabled)
        {
            if (gs == null) return null;
            foreach (var p in pending.Values.OrderByDescending(p => p.Priority).ThenBy(p => p.CapturedUtc))
            {
                if ((NativeCarSystems.Urgent(p.Kind) ? urgentBusy : busy) || enabled != null && !enabled(p.Member)) continue;
                if (p.Call == null)
                {
                    string kind = p.Kind;
                    p.Call = new ObservedRaceCall { Topic = p.Topic, Text = p.Text, Member = p.Member, Priority = p.Priority, Position = -1,
                        SessionId = p.Session, SessionType = p.Type, CompletedLaps = p.Lap, DetectedUtc = now, ExpiresUtc = now.AddSeconds(8),
                        AdditionalValidity = live => NativeCarSystems.StillCurrent(kind, live), Evidence = p.Evidence };
                }
                if (p.Call.IsValid(gs, now) && p.Call.TryReserve(now)) return p.Call;
            }
            return null;
        }
    }
}
