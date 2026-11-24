using System;
using CrewChiefV4.GameState;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rob 24.11.: Die Funkstufen (Ruhig, Normal, Mehr, Viel mehr) sind entfernt. „Das bringt ihn nur durcheinander“: Die Crew funkt
    /// immer nach ihrer eigenen Lage-Logik (Renndauer, Spannung, Zweikampf, Ereignisse). Die alten Befehle werden noch erkannt,
    /// damit die Crew ehrlich sagt, dass sich nichts ändert, statt dass das Sprachmodell eine Umstellung bestätigt.
    /// Fragen („Gibt es mehr Infos?“) zählen nicht.
    /// </summary>
    internal static class RadioAmountCommand
    {
        private const string Lead = @"^(?:(?:ben|mik|crew|team|jungs|leute|guys|chicos|equipo|ok|okay|so|jetzt|ab jetzt|bitte|please|por favor|ich moechte|ich will|ich haette gerne?|i want|i.?d like|quiero)[, ]+)*";
        private const string Tail = @"(?:[, ]+(?:bitte|please|por favor|danke|thanks|gracias|jetzt|now|ahora|ab jetzt|from now on))*$";

        private static readonly Regex MuchMore = new Regex(Lead +
            @"(?:viel mehr (?:infos?|informationen|funk|daten)|noch (?:viel )?mehr (?:infos?|informationen|funk)|alle infos|alle informationen|" +
            @"volle (?:infos?|informationen)(?:,? (?:insbesondere|vor allem|besonders) [a-z ]+)?|wieder (?:volle|alle) (?:infos?|informationen)|volles programm|wieder alles|alles wieder|" +
            @"full (?:info|information|radio)|all (?:the )?information|everything|" +
            @"toda la (?:info|informacion)|informacion completa|todo|" +
            @"(?:much|lots|a lot) more (?:info|information|radio|data)|all the info|" +
            @"mucha mas (?:info|informacion)|muchas mas infos?|mucho mas (?:radio|datos|info))" + Tail);
        private static readonly Regex More = new Regex(Lead +
            @"(?:mehr (?:infos?|informationen|funk|daten)|gib mir mehr infos?|more (?:info|information|radio|data)|give me more info|" +
            @"mas (?:info|infos|informacion|radio|datos)|dame mas info)" + Tail);
        private static readonly Regex Quiet = new Regex(Lead +
            @"(?:ruhe|ruhig|funkruhe|seid (?:mal )?ruhig|sei (?:mal )?ruhig|" +
            @"quiet|be quiet|keep quiet|radio silence|" +
            @"silencio|callaos|calla)" + Tail);
        /// <summary>LMU Sebring 23.11.: „Weniger Infos bitte“ aus „Viel mehr Infos“ schaltete ganz auf Ruhig. „Weniger“ ist eine Stufe weniger.</summary>
        private static readonly Regex Less = new Regex(Lead +
            @"(?:weniger (?:infos?|informationen|funk|daten)|etwas weniger|less (?:info|information|radio|data)|a bit less|" +
            @"menos (?:info|infos|informacion|radio|datos))" + Tail);
        /// <summary>Der frühere Funkwunsch „Routineberichte seltener/häufiger/normal“ war ebenfalls eine Funkstufe.</summary>
        private static readonly Regex Routine = new Regex(Lead +
            @"(?:(?:melde(?: mir)? )?(?:routinefunk|routineberichte|routine) (?:bitte )?(?:seltener|haeufiger|oefter|normal)(?: hoeren)?|" +
            @"(?:mehr|weniger) routine(?:funk|berichte)?|(?:more|less|fewer) routine (?:reports|calls)|(?:mas|menos) informes de rutina)" + Tail);
        private static readonly Regex Normal = new Regex(Lead +
            @"(?:(?:funk|infos?|funkmenge|radio) (?:wieder )?normal|(?:funk|infos?|informationen|ansagen) wieder (?:an|ein)|wieder (?:infos?|informationen|ansagen|funk)|" +
            @"(?:radio|info) back on|(?:wieder )?normal(?:er)? funk|zurueck auf normal|wieder normal|normal|" +
            @"(?:radio|info) (?:back )?(?:to )?normal|back to normal|normal radio|" +
            @"radio normal|vuelve a normal|otra vez normal|volvemos a lo normal)" + Tail);

        /// <summary>Ein alter Funkstufen-Befehl („Ruhe bitte“, „mehr Infos“, „viel mehr Infos“, „weniger Infos“, „Funk normal“).</summary>
        internal static bool IsAmountWish(string text)
        {
            string raw = (text ?? "").Trim();
            if (raw.Length == 0 || raw.Length > 80 || raw.EndsWith("?")) return false;
            string n = DriverRadioPreferences.Normalize(raw).Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
            n = Regex.Replace(n, @"[!.,;:]+", ",").Trim(',', ' ');
            n = Regex.Replace(n, @"\s*,\s*", ", ");
            return MuchMore.IsMatch(n) || More.IsMatch(n) || Quiet.IsMatch(n) || Less.IsMatch(n) || Normal.IsMatch(n) || Routine.IsMatch(n);
        }

        /// <summary>Kurze, ehrliche Antwort: Es gibt keine Stufen mehr, die Crew entscheidet nach Lage. Vier Formulierungen.</summary>
        internal static string NoLevelsReply(int variant) => AiRadioSchedule.Of(variant,
            RadioLanguage.T("Verstanden. Wir funken immer nach Lage: wenn etwas passiert, sofort, sonst im Rhythmus des Rennens.",
                "Copy. We always call it as the race goes: straight away when something happens, otherwise in the race's rhythm.",
                "Entendido. Siempre hablamos según la carrera: al momento si pasa algo, si no al ritmo de la carrera."),
            RadioLanguage.T("Alles klar. Eine Funkstufe gibt es nicht mehr, wir melden uns, wenn es etwas zu sagen gibt.",
                "Got it. There's no radio level any more, we'll call when there's something to say.",
                "De acuerdo. Ya no hay niveles de radio, te avisamos cuando haya algo que decir."),
            RadioLanguage.T("Okay. Du bekommst alles Wichtige, wir entscheiden nach dem Rennen, wann.",
                "Okay. You get everything important, the race decides when.",
                "Vale. Te llega todo lo importante, la carrera decide cuándo."),
            RadioLanguage.T("Verstanden. Vorne, hinten und alles, was passiert, kommt wie immer von uns.",
                "Copy. Ahead, behind and anything that happens still comes from us as always.",
                "Entendido. Delante, detrás y todo lo que pase te lo seguimos diciendo como siempre."));
    }
    /// <summary>
    /// Rob 23.11.: „Spotter aus“ / „Spotter ein“. Abgeschaltet werden nur die Nahbereichsansagen (links, rechts, Auto daneben,
    /// frei). Gelb, Unfälle, langsame oder stehende Autos, Safety Car und FCY bleiben immer an – sie laufen nicht über den
    /// Spotter, sondern über die Rennleitung und Saras Gefahrenmeldungen. Gilt bis „Spotter ein“ oder zum nächsten Start.
    /// </summary>
    internal static class SpotterCommand
    {
        private static readonly Regex Off = new Regex(@"^(?:(?:sara|ok|okay|bitte|please|por favor)[, ]+)*(?:spotter (?:aus|ausschalten|off|apagado|desactivado|fuera)|" +
            @"(?:schalte?|mach) (?:den )?spotter aus|kein spotter|no spotter|turn (?:the )?spotter off|switch (?:the )?spotter off|" +
            @"(?:desactiva|apaga|quita) (?:el )?spotter|sin spotter)(?:[, ]+(?:bitte|please|por favor|danke|thanks|gracias))*$");
        private static readonly Regex On = new Regex(@"^(?:(?:sara|ok|okay|bitte|please|por favor)[, ]+)*(?:spotter (?:ein|an|einschalten|on|encendido|activo|activado)|" +
            @"(?:schalte?|mach) (?:den )?spotter (?:ein|an)|spotter wieder (?:ein|an)|turn (?:the )?spotter (?:back )?on|switch (?:the )?spotter (?:back )?on|spotter back on|" +
            @"(?:activa|enciende|pon) (?:el )?spotter)(?:[, ]+(?:bitte|please|por favor|danke|thanks|gracias))*$");

        /// <summary>
        /// Wirkt sofort und bleibt bis zum Neustart von Crew Chief, auch beim Wechsel Training → Qualifying → Rennen und im
        /// Spottermodus Realistisch. Der Spielzyklus startet oder stoppt den Spotter beim nächsten Takt.
        /// </summary>
        internal static void Apply(bool on)
        {
            GlobalBehaviourSettings.driverSpotterChoice = on;
            GlobalBehaviourSettings.spotterEnabled = on;
            Console.WriteLine("RRCA Spotter: per Funk " + (on ? "eingeschaltet" : "ausgeschaltet"));
        }

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
