using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennverständnis (Masterplan Schritt 2, Rob 21.11.2026): Die Crew erinnert sich an die Geschichte mit einem Gegner.
    /// Grundlage ist ausschließlich die <see cref="RaceChronicle"/>: Hat Rob diesen Gegner schon einmal auf der Strecke überholt
    /// oder wurde er von ihm überholt, bekommt die aktuelle Meldung einen kurzen Rückbezug („Revanche für Runde 2“). Ohne belegte
    /// Vorgeschichte bleibt der Satz leer – nichts wird erfunden. Platzwechsel durch Boxenstopps zählen nicht als Duell.
    /// </summary>
    internal static class RaceStory
    {
        /// <summary>Einträge jünger als diese Zeit gehören zum gerade gemeldeten Ereignis selbst, nicht zur Vorgeschichte.</summary>
        internal static readonly TimeSpan Fresh = TimeSpan.FromSeconds(4);

        private static List<ChronicleEntry> Duels(RaceChronicle chronicle, string key, DateTime now) =>
            key == null ? new List<ChronicleEntry>() : chronicle.With(key).Where(e => now - e.Utc > Fresh &&
                (e.Kind == ChronicleKind.PositionGained || e.Kind == ChronicleKind.PositionLost) && e.Detail == ChronicleRecorder.OnTrack).ToList();

        private static string Pick(int variant, params string[] options) => options[((variant % options.Length) + options.Length) % options.Length];

        /// <summary>Runde, wie Rob sie zählt (die laufende Runde, nicht die abgeschlossenen).</summary>
        private static int LapNumber(ChronicleEntry e) => e.Lap + 1;

        /// <summary>Kurzer Satz „an X vorbei“ für eine bestätigte Überholung auf der Strecke (Name nur, wenn erlaubt).</summary>
        internal static string Passed(string name, int variant)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T("An " + name + " vorbei.", "Past " + name + ".", "Has pasado a " + name + ".");
                case 1: return RadioLanguage.T(name + " ist hinter dir.", name + " is behind you now.", name + " ya está detrás.");
                case 2: return RadioLanguage.T("Das war " + name + ".", "That was " + name + ".", "Ese era " + name + ".");
                case 3: return RadioLanguage.T(name + " ist abgehakt.", name + " is done.", name + ", superado.");
                default: return RadioLanguage.T("Vorbei an " + name + ".", "Through on " + name + ".", "Adelantado " + name + ".");
            }
        }

        /// <summary>Kurzer Satz „X ist vorbei“, wenn Rob auf der Strecke einen Platz an einen bekannten Gegner verliert.</summary>
        internal static string PassedBy(string name, int variant)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T("Das war " + name + ".", "That was " + name + ".", "Ese era " + name + ".");
                case 1: return RadioLanguage.T(name + " ist vorbei.", name + " is through.", name + " ha pasado.");
                case 2: return RadioLanguage.T(name + " hat dich erwischt.", name + " got you.", name + " te ha pillado.");
                case 3: return RadioLanguage.T(name + " geht durch.", name + " goes through.", name + " se cuela.");
                default: return RadioLanguage.T("Vorbei ist " + name + ".", name + " made it past.", name + " se ha ido por delante.");
            }
        }

        /// <summary>
        /// Rückbezug nach einem eigenen Überholmanöver auf der Strecke: Revanche, wenn dieser Gegner Rob zuletzt überholt hatte;
        /// „drittes Duell“, wenn die beiden schon mehrfach getauscht haben. Sonst leer.
        /// </summary>
        internal static string AfterGain(string key, DateTime now, int variant, RaceChronicle chronicle = null)
        {
            var duels = Duels(chronicle ?? RaceChronicle.Current, key, now);
            if (duels.Count == 0) return "";
            var last = duels[duels.Count - 1];
            int lap = LapNumber(last);
            if (last.Kind == ChronicleKind.PositionLost)
                switch (((variant % 5) + 5) % 5)
                {
                    case 0: return RadioLanguage.T("Revanche für Runde " + lap + ".", "Payback for lap " + lap + ".", "Venganza por la vuelta " + lap + ".");
                    case 1: return RadioLanguage.T("Er hatte dich in Runde " + lap + " geholt, jetzt bist du wieder vorn.", "He got you on lap " + lap + ", now you're back ahead.", "Te pasó en la vuelta " + lap + ", ahora vuelves a ir delante.");
                    case 2: return RadioLanguage.T("Zurückgeholt, was du in Runde " + lap + " verloren hast.", "That's the place from lap " + lap + " back.", "Recuperado lo que perdiste en la vuelta " + lap + ".");
                    case 3: return RadioLanguage.T("Damit ist Runde " + lap + " ausgeglichen.", "That squares lap " + lap + ".", "Con esto queda saldada la vuelta " + lap + ".");
                    default: return RadioLanguage.T("Die Antwort auf Runde " + lap + ".", "The answer to lap " + lap + ".", "La respuesta a la vuelta " + lap + ".");
                }
            if (duels.Count < 2) return "";
            int round = duels.Count + 1;
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T("Das ist euer " + round + ". Platztausch.", "That's swap number " + round + " between you two.", "Es vuestro intercambio número " + round + ".");
                case 1: return RadioLanguage.T("Ihr zwei schenkt euch nichts, Tausch Nummer " + round + ".", "You two are giving nothing away, swap " + round + ".", "No os regaláis nada, intercambio " + round + ".");
                case 2: return RadioLanguage.T("Schon wieder ihr beide, zum " + round + ". Mal.", "You two again, for the " + Ordinal(round) + " time.", "Otra vez vosotros dos, por " + round + "ª vez.");
                case 3: return RadioLanguage.T("Das Duell geht weiter, Runde " + round + " für euch zwei.", "The duel goes on, round " + round + " between you.", "El duelo sigue, asalto " + round + " entre vosotros.");
                default: return RadioLanguage.T("Platztausch Nummer " + round + " mit ihm.", "Swap number " + round + " with him.", "Intercambio número " + round + " con él.");
            }
        }

        /// <summary>Rückbezug nach einem Platzverlust auf der Strecke: Er holt sich zurück, was Rob ihm abgenommen hatte.</summary>
        internal static string AfterLoss(string key, DateTime now, int variant, RaceChronicle chronicle = null)
        {
            var duels = Duels(chronicle ?? RaceChronicle.Current, key, now);
            if (duels.Count == 0) return "";
            var last = duels[duels.Count - 1];
            int lap = LapNumber(last);
            if (last.Kind == ChronicleKind.PositionGained)
                switch (((variant % 5) + 5) % 5)
                {
                    case 0: return RadioLanguage.T("Er holt sich den Platz aus Runde " + lap + " zurück.", "He takes back the place from lap " + lap + ".", "Recupera el puesto de la vuelta " + lap + ".");
                    case 1: return RadioLanguage.T("Seine Antwort auf Runde " + lap + ". Bleib dran.", "His answer to lap " + lap + ". Stay with him.", "Su respuesta a la vuelta " + lap + ". Sigue pegado.");
                    case 2: return RadioLanguage.T("Den hattest du in Runde " + lap + " schon, das geht wieder.", "You had him on lap " + lap + ", you can do it again.", "Ya lo pasaste en la vuelta " + lap + ", puedes repetirlo.");
                    case 3: return RadioLanguage.T("Runde " + lap + " ist damit ausgeglichen, jetzt bist du dran.", "That evens out lap " + lap + ", your move now.", "Empate con la vuelta " + lap + ", ahora te toca a ti.");
                    default: return RadioLanguage.T("Er kontert Runde " + lap + ". Ruhig, die Chance kommt.", "He's countered lap " + lap + ". Stay calm, the chance will come.", "Contraataca lo de la vuelta " + lap + ". Calma, llegará la ocasión.");
                }
            if (duels.Count < 2) return "";
            int round = duels.Count + 1;
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T("Platztausch Nummer " + round + " mit ihm.", "Swap number " + round + " with him.", "Intercambio número " + round + " con él.");
                case 1: return RadioLanguage.T("Ihr zwei schenkt euch nichts, das war Nummer " + round + ".", "You two give nothing away, that was number " + round + ".", "No os regaláis nada, ese fue el número " + round + ".");
                case 2: return RadioLanguage.T("Das Duell läuft weiter, zum " + round + ". Mal.", "The duel goes on, for the " + Ordinal(round) + " time.", "El duelo sigue, por " + round + "ª vez.");
                case 3: return RadioLanguage.T("Schon wieder er, Tausch Nummer " + round + ".", "Him again, swap number " + round + ".", "Otra vez él, intercambio " + round + ".");
                default: return RadioLanguage.T("Euer " + round + ". Platztausch, das ist noch nicht vorbei.", "Your " + Ordinal(round) + " swap, this isn't over.", "Vuestro intercambio número " + round + ", esto no ha terminado.");
            }
        }

        /// <summary>Rückbezug, wenn ein bekannter Gegner von hinten aufschließt („den hast du in Runde 2 überholt“).</summary>
        internal static string Approaching(string key, DateTime now, int variant, RaceChronicle chronicle = null)
        {
            var duels = Duels(chronicle ?? RaceChronicle.Current, key, now);
            if (duels.Count == 0) return "";
            var last = duels[duels.Count - 1];
            int lap = LapNumber(last);
            bool passedHim = last.Kind == ChronicleKind.PositionGained;
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return passedHim ? RadioLanguage.T("Den hast du in Runde " + lap + " überholt.", "You passed him on lap " + lap + ".", "Lo adelantaste en la vuelta " + lap + ".")
                    : RadioLanguage.T("Er hatte dich in Runde " + lap + " schon geholt.", "He already got you on lap " + lap + ".", "Ya te pasó en la vuelta " + lap + ".");
                case 1: return passedHim ? RadioLanguage.T("Er will den Platz aus Runde " + lap + " zurück.", "He wants the place from lap " + lap + " back.", "Quiere recuperar el puesto de la vuelta " + lap + ".")
                    : RadioLanguage.T("Derselbe wie in Runde " + lap + ", diesmal zumachen.", "Same one as lap " + lap + ", close the door this time.", "El mismo de la vuelta " + lap + ", esta vez cierra.");
                case 2: return passedHim ? RadioLanguage.T("Seit Runde " + lap + " liegt er hinter dir, so bleibt das.", "He's been behind you since lap " + lap + ", keep it that way.", "Va detrás de ti desde la vuelta " + lap + ", que siga así.")
                    : RadioLanguage.T("Du kennst ihn aus Runde " + lap + ", er greift wieder an.", "You know him from lap " + lap + ", he's coming again.", "Lo conoces de la vuelta " + lap + ", vuelve a atacar.");
                case 3: return passedHim ? RadioLanguage.T("Das ist dein Gegner aus Runde " + lap + ".", "That's your rival from lap " + lap + ".", "Es tu rival de la vuelta " + lap + ".")
                    : RadioLanguage.T("Das ist der aus Runde " + lap + ", Linie halten.", "That's the one from lap " + lap + ", hold your line.", "Es el de la vuelta " + lap + ", mantén la trazada.");
                default: return passedHim ? RadioLanguage.T("Runde " + lap + " hat er nicht vergessen.", "He hasn't forgotten lap " + lap + ".", "No ha olvidado la vuelta " + lap + ".")
                    : RadioLanguage.T("Wie in Runde " + lap + ", nur diesmal bist du vorbereitet.", "Like lap " + lap + ", only this time you're ready.", "Como en la vuelta " + lap + ", pero esta vez estás preparado.");
            }
        }

        /// <summary>
        /// Namen zum Startfazit: an wem Rob in der ersten Runde auf der Strecke vorbeigegangen ist (bzw. wer an ihm), nach dem
        /// letzten Stand je Gegner – wer den Platz in derselben Runde zurückgeholt hat, zählt nicht. Leer ohne belegte Namen.
        /// </summary>
        internal static string StartNames(RaceChronicle chronicle, int variant)
        {
            var lapOne = (chronicle ?? RaceChronicle.Current).All().Where(e => e.Lap == 0 && e.SubjectKey != null &&
                (e.Kind == ChronicleKind.PositionGained || e.Kind == ChronicleKind.PositionLost) && e.Detail == ChronicleRecorder.OnTrack).ToList();
            var net = lapOne.GroupBy(e => e.SubjectKey).Select(g => g.Last()).Where(e => e.Subject != null).ToList();
            var passed = net.Where(e => e.Kind == ChronicleKind.PositionGained).Select(e => e.Subject).Take(3).ToList();
            var lostTo = net.Where(e => e.Kind == ChronicleKind.PositionLost).Select(e => e.Subject).Take(3).ToList();
            if (passed.Count > 0)
            {
                string who = Join(passed);
                return Pick(variant,
                    RadioLanguage.T("An " + who + " vorbei.", "Past " + who + ".", "Has pasado a " + who + "."),
                    RadioLanguage.T(Cap(who) + (passed.Count == 1 ? " liegt" : " liegen") + " jetzt hinter dir.", Cap(who) + (passed.Count == 1 ? " is" : " are") + " behind you now.", Cap(who) + (passed.Count == 1 ? " va" : " van") + " ahora detrás."),
                    RadioLanguage.T("Geschnappt: " + who + ".", "Picked off: " + who + ".", "Cazados: " + who + "."),
                    RadioLanguage.T("Du hast " + who + " eingesammelt.", "You've picked up " + who + ".", "Te has llevado a " + who + "."));
            }
            if (lostTo.Count > 0)
            {
                string who = Join(lostTo);
                return Pick(variant,
                    RadioLanguage.T(Cap(who) + (lostTo.Count == 1 ? " ist" : " sind") + " vorbeigekommen.", Cap(who) + (lostTo.Count == 1 ? " got" : " got") + " through.", Cap(who) + (lostTo.Count == 1 ? " ha pasado." : " han pasado.")),
                    RadioLanguage.T("Vorbei " + (lostTo.Count == 1 ? "ist " : "sind ") + who + ".", who + (lostTo.Count == 1 ? " made it past." : " made it past."), "Te " + (lostTo.Count == 1 ? "ha" : "han") + " pasado " + who + "."),
                    RadioLanguage.T("Verloren an " + who + ".", "Lost out to " + who + ".", "Perdido frente a " + who + "."),
                    RadioLanguage.T(Cap(who) + (lostTo.Count == 1 ? " hat" : " haben") + " dich erwischt.", Cap(who) + " caught you.", Cap(who) + (lostTo.Count == 1 ? " te ha pillado." : " te han pillado.")));
            }
            return "";
        }

        private static string Join(List<string> names)
        {
            if (names.Count == 1) return names[0];
            string and = RadioLanguage.T(" und ", " and ", " y ");
            return string.Join(", ", names.Take(names.Count - 1)) + and + names[names.Count - 1];
        }

        private static string Ordinal(int n) => n == 2 ? "second" : n == 3 ? "third" : n + "th";

        /// <summary>
        /// Bilanz der Überholmanöver auf der Strecke aus der Chronik: gewonnen, verloren und die meistgenannten Gegner. Für
        /// Zwischenstand und Abschluss (nur belegte Duelle, keine Boxenstopps).
        /// </summary>
        internal static void Tally(RaceChronicle chronicle, out int gained, out int lost, out string rival, out int rivalSwaps)
        {
            var duels = (chronicle ?? RaceChronicle.Current).All().Where(e => (e.Kind == ChronicleKind.PositionGained || e.Kind == ChronicleKind.PositionLost) &&
                e.Detail == ChronicleRecorder.OnTrack).ToList();
            gained = duels.Count(e => e.Kind == ChronicleKind.PositionGained);
            lost = duels.Count(e => e.Kind == ChronicleKind.PositionLost);
            var top = duels.Where(e => e.SubjectKey != null && e.Subject != null).GroupBy(e => e.SubjectKey)
                .Select(g => new { Name = g.Last().Subject, Count = g.Count() }).OrderByDescending(g => g.Count).FirstOrDefault();
            rival = top != null && top.Count >= 2 ? top.Name : null;
            rivalSwaps = top == null ? 0 : top.Count;
        }

        /// <summary>
        /// Satz für den Zwischenstand: Überholbilanz auf der Strecke und der Hauptrivale. Leer, wenn noch nichts passiert ist.
        /// </summary>
        internal static string TallyText(RaceChronicle chronicle, bool names, int variant)
        {
            Tally(chronicle, out int gained, out int lost, out string rival, out int swaps);
            if (gained == 0 && lost == 0) return "";
            string g = gained == 1 ? RadioLanguage.T("ein Überholmanöver", "one pass", "un adelantamiento") :
                gained + RadioLanguage.T(" Überholmanöver", " passes", " adelantamientos");
            string l = lost == 1 ? RadioLanguage.T("einmal überholt worden", "passed once", "te han pasado una vez") :
                RadioLanguage.T(lost + "-mal überholt worden", "passed " + lost + " times", "te han pasado " + lost + " veces");
            string balance;
            if (lost == 0) balance = Pick(variant,
                RadioLanguage.T("Auf der Strecke bisher " + g + ", kein Platz hergegeben.", "On track so far " + g + ", no place given away.", "En pista llevas " + g + ", sin ceder ninguna posición."),
                RadioLanguage.T("Bisher " + g + " auf der Strecke und keiner an dir vorbei.", "So far " + g + " on track and nobody past you.", "Hasta ahora " + g + " en pista y nadie te ha pasado."),
                RadioLanguage.T(Cap(g) + " auf der Strecke, sauber verteidigt.", Cap(g) + " on track, defended cleanly.", Cap(g) + " en pista, defensa limpia."),
                RadioLanguage.T("Bilanz auf der Strecke: " + g + ", nichts verloren.", "Track tally: " + g + ", nothing lost.", "Balance en pista: " + g + ", nada perdido."));
            else if (gained == 0) balance = Pick(variant,
                RadioLanguage.T("Auf der Strecke bisher " + l + ".", "On track you've been " + l + " so far.", "En pista " + l + " hasta ahora."),
                RadioLanguage.T("Bilanz auf der Strecke: " + l + ", da holen wir uns was zurück.", "Track tally: " + l + ", we'll win some back.", "Balance en pista: " + l + ", algo recuperaremos."),
                RadioLanguage.T("Bisher " + l + ", noch kein eigenes Manöver.", "So far " + l + ", no move of your own yet.", "Hasta ahora " + l + ", todavía sin adelantar."),
                RadioLanguage.T("Auf der Strecke " + l + ", ruhig bleiben, die Chancen kommen.", "On track " + l + ", stay calm, chances will come.", "En pista " + l + ", calma, llegarán ocasiones."));
            else balance = Pick(variant,
                RadioLanguage.T("Auf der Strecke " + g + ", " + l + ".", "On track " + g + ", " + l + ".", "En pista " + g + ", " + l + "."),
                RadioLanguage.T("Bilanz auf der Strecke: " + g + " und " + l + ".", "Track tally: " + g + " and " + l + ".", "Balance en pista: " + g + " y " + l + "."),
                RadioLanguage.T("Bisher " + g + ", dafür " + l + ".", "So far " + g + ", and " + l + ".", "Hasta ahora " + g + ", y " + l + "."),
                RadioLanguage.T("Viel los: " + g + ", " + l + ".", "Busy race: " + g + ", " + l + ".", "Mucho movimiento: " + g + ", " + l + "."));
            if (!names || rival == null) return balance;
            return balance + " " + Pick(variant,
                RadioLanguage.T("Dein Hauptgegner ist " + rival + ", " + swaps + " Platztausche.", "Your main rival is " + rival + ", " + swaps + " swaps.", "Tu rival principal es " + rival + ", " + swaps + " intercambios."),
                RadioLanguage.T("Mit " + rival + " hast du " + swaps + "-mal getauscht.", "You've swapped " + swaps + " times with " + rival + ".", "Con " + rival + " has intercambiado " + swaps + " veces."),
                RadioLanguage.T(rival + " ist dein Dauerduell, " + swaps + " Wechsel.", rival + " is your running duel, " + swaps + " swaps.", rival + " es tu duelo constante, " + swaps + " cambios."),
                RadioLanguage.T("Das Duell mit " + rival + " zählt " + swaps + " Wechsel.", "The duel with " + rival + " stands at " + swaps + " swaps.", "El duelo con " + rival + " va por " + swaps + " cambios."));
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);
    }
}
