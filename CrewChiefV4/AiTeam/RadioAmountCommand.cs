using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rob 23.11.: Funkmenge während der Fahrt per Funk: „Ruhe bitte“, „mehr Infos“, „viel mehr Infos“ und „Funk normal“.
    /// Wirkt sofort und bleibt gespeichert, wie der Regler im Cockpit. Sara und alle Sicherheitsmeldungen sind nie betroffen.
    /// Fragen („Gibt es mehr Infos?“) ändern nichts.
    /// </summary>
    internal static class RadioAmountCommand
    {
        private const string Lead = @"^(?:(?:ben|mik|crew|team|jungs|leute|guys|chicos|equipo|ok|okay|so|jetzt|ab jetzt|bitte|please|por favor)[, ]+)*";
        private const string Tail = @"(?:[, ]+(?:bitte|please|por favor|danke|thanks|gracias|jetzt|now|ahora|ab jetzt|from now on))*$";

        private static readonly Regex MuchMore = new Regex(Lead +
            @"(?:viel mehr (?:infos?|informationen|funk|daten)|noch (?:viel )?mehr (?:infos?|informationen|funk)|alle infos|" +
            @"(?:much|lots|a lot) more (?:info|information|radio|data)|all the info|" +
            @"mucha mas (?:info|informacion)|muchas mas infos?|mucho mas (?:radio|datos|info))" + Tail);
        private static readonly Regex More = new Regex(Lead +
            @"(?:mehr (?:infos?|informationen|funk|daten)|gib mir mehr infos?|more (?:info|information|radio|data)|give me more info|" +
            @"mas (?:info|infos|informacion|radio|datos)|dame mas info)" + Tail);
        private static readonly Regex Quiet = new Regex(Lead +
            @"(?:ruhe|ruhig|funkruhe|weniger (?:infos?|informationen|funk|daten)|seid (?:mal )?ruhig|sei (?:mal )?ruhig|" +
            @"quiet|be quiet|keep quiet|less (?:info|information|radio|data)|radio silence|" +
            @"silencio|menos (?:info|infos|informacion|radio|datos)|callaos|calla)" + Tail);
        private static readonly Regex Normal = new Regex(Lead +
            @"(?:(?:funk|infos?|funkmenge|radio) (?:wieder )?normal|(?:wieder )?normal(?:er)? funk|zurueck auf normal|wieder normal|normal|" +
            @"(?:radio|info) (?:back )?(?:to )?normal|back to normal|normal radio|" +
            @"radio normal|vuelve a normal|otra vez normal|volvemos a lo normal)" + Tail);

        /// <summary>Erkannter Wunsch oder null.</summary>
        internal static RadioAmount? Parse(string text)
        {
            string raw = (text ?? "").Trim();
            if (raw.Length == 0 || raw.Length > 80 || raw.EndsWith("?")) return null;
            string n = DriverRadioPreferences.Normalize(raw).Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
            n = Regex.Replace(n, @"[!.,;:]+", ",").Trim(',', ' ');
            n = Regex.Replace(n, @"\s*,\s*", ", ");
            if (MuchMore.IsMatch(n)) return RadioAmount.MuchMore;
            if (More.IsMatch(n)) return RadioAmount.More;
            if (Quiet.IsMatch(n)) return RadioAmount.Quiet;
            if (Normal.IsMatch(n)) return RadioAmount.Normal;
            return null;
        }

        /// <summary>Kurze Bestätigung in der Funksprache, vier Formulierungen je Stufe.</summary>
        internal static string Reply(RadioAmount amount, int variant)
        {
            switch (amount)
            {
                case RadioAmount.Quiet:
                    return AiRadioSchedule.Of(variant,
                        RadioLanguage.T("Verstanden, Ruhe am Funk. Wichtiges und Sara kommen weiter.", "Copy, quiet on the radio. Anything important and Sara still come through.", "Entendido, silencio en la radio. Lo importante y Sara siguen llegando."),
                        RadioLanguage.T("Alles klar, wir halten uns zurück. Nur noch das Nötigste.", "Got it, we'll hold back. Only the essentials.", "De acuerdo, nos contenemos. Solo lo esencial."),
                        RadioLanguage.T("Okay, Funkruhe. Sag „Funk normal“, wenn du wieder mehr hören willst.", "Okay, radio quiet. Say \"radio normal\" when you want more again.", "Vale, silencio de radio. Di \"radio normal\" cuando quieras más."),
                        RadioLanguage.T("Verstanden, keine Routineberichte mehr. Gefahren meldet Sara wie immer.", "Copy, no more routine reports. Sara calls the hazards as always.", "Entendido, se acabaron los informes de rutina. Sara avisa de los peligros como siempre."));
                case RadioAmount.More:
                    return AiRadioSchedule.Of(variant,
                        RadioLanguage.T("Verstanden, ab jetzt mehr Infos.", "Copy, more info from now on.", "Entendido, a partir de ahora más información."),
                        RadioLanguage.T("Alles klar, du hörst mehr von uns, vor allem zu vorne und hinten.", "Got it, you'll hear more from us, mostly about ahead and behind.", "De acuerdo, nos oirás más, sobre todo de delante y detrás."),
                        RadioLanguage.T("Okay, wir legen eine Schippe drauf.", "Okay, we'll step it up.", "Vale, subimos un punto."),
                        RadioLanguage.T("Verstanden, mehr Daten, wenn Luft ist.", "Copy, more data whenever there's room.", "Entendido, más datos cuando haya hueco."));
                case RadioAmount.MuchMore:
                    return AiRadioSchedule.Of(variant,
                        RadioLanguage.T("Verstanden, volles Programm am Funk.", "Copy, full programme on the radio.", "Entendido, programa completo por radio."),
                        RadioLanguage.T("Alles klar, viel mehr Infos. Sag „Funk normal“, wenn es zu viel wird.", "Got it, lots more info. Say \"radio normal\" if it gets too much.", "De acuerdo, mucha más información. Di \"radio normal\" si es demasiado."),
                        RadioLanguage.T("Okay, wir reden ab jetzt deutlich mehr mit dir.", "Okay, we'll be talking to you a lot more now.", "Vale, a partir de ahora hablamos bastante más contigo."),
                        RadioLanguage.T("Verstanden, du bekommst alles, was wir sehen.", "Copy, you get everything we see.", "Entendido, te pasamos todo lo que vemos."));
                default:
                    return AiRadioSchedule.Of(variant,
                        RadioLanguage.T("Verstanden, Funk wieder normal.", "Copy, radio back to normal.", "Entendido, radio otra vez normal."),
                        RadioLanguage.T("Alles klar, zurück auf normal.", "Got it, back to normal.", "De acuerdo, volvemos a lo normal."),
                        RadioLanguage.T("Okay, normaler Funk ab jetzt.", "Okay, normal radio from now on.", "Vale, radio normal a partir de ahora."),
                        RadioLanguage.T("Verstanden, wir funken wieder im normalen Rhythmus.", "Copy, back to our normal rhythm.", "Entendido, volvemos a nuestro ritmo normal."));
            }
        }
    }
    /// <summary>
    /// Rob 23.11.: „Spotter aus“ / „Spotter ein“. Abgeschaltet werden nur die Nahbereichsansagen (links, rechts, Auto daneben,
    /// frei). Gelb, Unfälle, langsame oder stehende Autos, Safety Car und FCY bleiben immer an – sie laufen nicht über den
    /// Spotter, sondern über die Rennleitung und Saras Gefahrenmeldungen. Gilt bis „Spotter ein“ oder zum nächsten Start.
    /// </summary>
    internal static class SpotterCommand
    {
        private static readonly Regex Off = new Regex(@"^(?:(?:sara|ok|okay|bitte|please|por favor)[, ]+)*(?:spotter (?:aus|ausschalten|off|apagado|fuera)|" +
            @"(?:schalte?|mach) (?:den )?spotter aus|kein spotter|no spotter|turn (?:the )?spotter off|switch (?:the )?spotter off|" +
            @"(?:desactiva|apaga|quita) (?:el )?spotter|sin spotter)(?:[, ]+(?:bitte|please|por favor|danke|thanks|gracias))*$");
        private static readonly Regex On = new Regex(@"^(?:(?:sara|ok|okay|bitte|please|por favor)[, ]+)*(?:spotter (?:ein|an|einschalten|on|encendido|activo)|" +
            @"(?:schalte?|mach) (?:den )?spotter (?:ein|an)|spotter wieder (?:ein|an)|turn (?:the )?spotter (?:back )?on|switch (?:the )?spotter (?:back )?on|spotter back on|" +
            @"(?:activa|enciende|pon) (?:el )?spotter)(?:[, ]+(?:bitte|please|por favor|danke|thanks|gracias))*$");

        /// <summary>true = Spotter ein, false = aus, null = kein Spotterbefehl.</summary>
        internal static bool? Parse(string text)
        {
            string raw = (text ?? "").Trim();
            if (raw.Length == 0 || raw.Length > 60 || raw.EndsWith("?")) return null;
            string n = DriverRadioPreferences.Normalize(raw).Replace("é", "e").Replace("í", "i").Replace("ó", "o");
            n = Regex.Replace(n, @"[!.,;:]+", ",").Trim(',', ' ');
            n = Regex.Replace(n, @"\s*,\s*", ", ");
            if (Off.IsMatch(n)) return false;
            if (On.IsMatch(n)) return true;
            return null;
        }

        /// <summary>Kurze Bestätigung, fünf Formulierungen je Richtung.</summary>
        internal static string Reply(bool on, int variant) => on
            ? AiRadioSchedule.Of(variant,
                RadioLanguage.T("Spotter ist wieder an. Ich sag dir links und rechts.", "Spotter back on. I'll call left and right.", "Spotter activado otra vez. Te canto izquierda y derecha."),
                RadioLanguage.T("Verstanden, ich spotte wieder.", "Copy, I'm spotting again.", "Entendido, vuelvo a hacer de spotter."),
                RadioLanguage.T("Okay, Spotter an. Ich hab deine Seiten im Blick.", "Okay, spotter on. I've got your sides.", "Vale, spotter activado. Vigilo tus lados."),
                RadioLanguage.T("Alles klar, links, rechts und frei kommen wieder.", "Got it, left, right and clear are back.", "De acuerdo, vuelven izquierda, derecha y libre."),
                RadioLanguage.T("Spotter läuft wieder. Ich bin bei dir.", "Spotter running again. I'm with you.", "El spotter vuelve a funcionar. Estoy contigo."))
            : AiRadioSchedule.Of(variant,
                RadioLanguage.T("Spotter aus. Gelb, Unfälle und Safety Car melde ich weiter.", "Spotter off. I'll still call yellows, crashes and the safety car.", "Spotter desactivado. Sigo avisando de amarillas, accidentes y safety car."),
                RadioLanguage.T("Verstanden, kein links und rechts mehr. Gefahren sage ich trotzdem.", "Copy, no more left and right. I'll still call hazards.", "Entendido, se acabó izquierda y derecha. Los peligros te los sigo diciendo."),
                RadioLanguage.T("Okay, Spotter ist aus. Sag „Spotter ein“, wenn du mich wieder brauchst.", "Okay, spotter is off. Say \"spotter on\" when you need me again.", "Vale, spotter desactivado. Di \"spotter activado\" cuando me necesites."),
                RadioLanguage.T("Alles klar, ich halte mich bei Seitenansagen raus. Gelb und Unfälle kommen weiter.", "Got it, I'll stay off the side calls. Yellows and crashes still come.", "De acuerdo, me salto los avisos laterales. Amarillas y accidentes siguen llegando."),
                RadioLanguage.T("Spotter aus. Für Safety Car und Gefahren bin ich weiter da.", "Spotter off. I'm still here for the safety car and hazards.", "Spotter desactivado. Sigo aquí para el safety car y los peligros."));
    }
}
