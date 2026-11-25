using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Verkehr aus anderen Klassen im Multiclass-Rennen (LMU-Test 18.11.2026, Interlagos, GT3 gegen LMP2 und Hypercar).
    /// Rob: „Das hättest du erkennen sollen: Hinter dir kommt ein schnellerer Wagen einer anderen Klasse, lass ihn durch.“
    /// und „Rob, hinter dir kommt eine ganze Kolonne der LMP2-Fahrer. Lass sie vorbei.“ Die Klasse wird nur genannt,
    /// wenn das Spiel sie für beide Autos liefert und sie sich unterscheidet; sonst bleibt es beim allgemeinen Verkehrshinweis.
    /// </summary>
    internal static class MulticlassTraffic
    {
        /// <summary>Wann die Crew zuletzt klassenfremden Verkehr von hinten angesagt hat (für die blaue Flagge).</summary>
        internal static DateTime LastRearClassCallUtc = DateTime.MinValue;
        internal static readonly TimeSpan BlueCoveredFor = TimeSpan.FromSeconds(20);

        /// <summary>Sprechbarer Klassenname oder null, wenn die Klasse unbekannt ist.</summary>
        internal static string Label(CarData.CarClass c)
        {
            if (c == null) return null;
            switch (c.carClassEnum)
            {
                case CarData.CarClassEnum.HYPER_CAR:
                case CarData.CarClassEnum.HYPER_CAR_RACE: return "Hypercar";
                case CarData.CarClassEnum.LMP2: return "LMP2";
                case CarData.CarClassEnum.LMP3: return "LMP3";
                case CarData.CarClassEnum.GT3: return "GT3";
                case CarData.CarClassEnum.GTE: return "GTE";
            }
            string id = (c.getClassIdentifier() ?? "").ToUpperInvariant();
            if (id.Contains("HYPER")) return "Hypercar";
            if (id.Contains("LMP2")) return "LMP2";
            if (id.Contains("LMP3")) return "LMP3";
            if (id.Contains("GT3")) return "GT3";
            if (id.Contains("GTE")) return "GTE";
            return null;
        }

        internal static readonly string[] SpokenLabels = { null, "Hypercar", "LMP2", "LMP3", "GT3", "GTE" };

        /// <summary>Alle Sätze für die bekannten Klassen, vorab als Originalclip erzeugt (Verkehr von hinten braucht keine Wartezeit).</summary>
        internal static System.Collections.Generic.IEnumerable<string> PreparedPhrases
        {
            get
            {
                foreach (string label in SpokenLabels)
                    for (int v = 0; v < 5; v++) { yield return Rear(label, v); yield return RearGroup(label, v); yield return Ahead(label, v); }
            }
        }

        /// <summary>Andere Klasse, und beide Klassen sind bekannt.</summary>
        internal static bool OtherClass(TrackCarPosition car) => car != null && car.ClassComparisonKnown && !car.SameClass;

        private static string Pick(string[] options, int variant) => options[((variant % options.Length) + options.Length) % options.Length];

        /// <summary>Ein schnelleres Auto einer anderen Klasse läuft von hinten auf.</summary>
        internal static string Rear(string label, int variant)
        {
            bool n = !string.IsNullOrEmpty(label);
            string de = n ? label : "ein Wagen aus einer schnelleren Klasse", en = n ? "a " + label : "a car from a faster class", es = n ? "un " + label : "un coche de una clase más rápida";
            string De = n ? label : "Ein Wagen aus einer schnelleren Klasse", En = n ? "A " + label : "A car from a faster class", Es = n ? "Un " + label : "Un coche de una clase más rápida";
            return Pick(new[]
            {
                RadioLanguage.T(De + " kommt von hinten, andere Klasse. Lass ihn durch, das ist nicht dein Gegner.",
                    En + " is coming from behind, different class. Let him through, he's not your fight.",
                    Es + " viene por detrás, otra clase. Déjale pasar, no es tu rival."),
                RadioLanguage.T("Im Spiegel: " + de + ", schnellere Klasse. Linie halten und ihn vorbeilassen.",
                    "Mirrors: " + en + ", faster class. Hold your line and let him by.",
                    "Por los espejos: " + es + ", clase más rápida. Mantén la trazada y déjale pasar."),
                RadioLanguage.T("Klassenfremder Verkehr von hinten: " + de + ". Berechenbar bleiben, er geht gleich vorbei.",
                    "Other-class traffic behind: " + en + ". Stay predictable, he'll be past in a moment.",
                    "Tráfico de otra clase detrás: " + es + ". Sé predecible, pasará enseguida."),
                RadioLanguage.T(De + " läuft auf, deutlich schneller. Kein Kampf, Platz lassen und weiter deinen Rhythmus fahren.",
                    En + " is closing, much quicker. No fight, give him room and keep your rhythm.",
                    Es + " se acerca, mucho más rápido. Sin pelea, deja hueco y sigue a tu ritmo."),
                RadioLanguage.T("Achtung hinten, " + de + " aus der schnelleren Klasse. Lass ihn auf der Geraden vorbei, nicht in der Kurve.",
                    "Heads up behind, " + en + " from the faster class. Let him by on the straight, not in the corner.",
                    "Atención detrás, " + es + " de la clase más rápida. Déjale pasar en la recta, no en la curva."),
            }, variant);
        }

        /// <summary>Mehrere schnellere Autos einer anderen Klasse kommen als Gruppe.</summary>
        internal static string RearGroup(string label, int variant)
        {
            bool n = !string.IsNullOrEmpty(label);
            string de = n ? label + "-Autos" : "Autos der schnelleren Klasse", en = n ? label + " cars" : "faster-class cars", es = n ? "coches " + label : "coches de la clase más rápida";
            return Pick(new[]
            {
                RadioLanguage.T("Hinter dir kommt eine ganze Kolonne " + de + ". Lass sie vorbei, die sind deutlich schneller.",
                    "A whole train of " + en + " is coming up behind. Let them through, they're a lot quicker.",
                    "Viene detrás toda una fila de " + es + ". Déjales pasar, son bastante más rápidos."),
                RadioLanguage.T("Mehrere " + de + " im Spiegel, andere Klasse. Linie halten, sie suchen sich ihren Weg.",
                    "Several " + en + " in the mirrors, different class. Hold your line, they'll find their way.",
                    "Varios " + es + " en los espejos, otra clase. Mantén la trazada, ellos buscarán hueco."),
                RadioLanguage.T("Da kommt eine Gruppe " + de + " von hinten. Ruhig bleiben, berechenbar fahren, dann sind sie schnell durch.",
                    "Group of " + en + " coming from behind. Stay calm and predictable, they'll be through quickly.",
                    "Llega un grupo de " + es + " por detrás. Tranquilo y predecible, pasarán rápido."),
                RadioLanguage.T("Kolonne der schnelleren Klasse: " + de + ". Kein Positionskampf, Platz lassen.",
                    "Faster class arriving in a pack: " + en + ". No position fight, give them room.",
                    "La clase rápida llega en grupo: " + es + ". No es pelea por posición, deja espacio."),
                RadioLanguage.T("Achtung, " + de + " im Pulk hinter dir. Lass sie durch und fahr danach wieder dein Tempo.",
                    "Watch out, a bunch of " + en + " right behind. Let them go, then back to your pace.",
                    "Atención, un grupo de " + es + " justo detrás. Déjales pasar y luego vuelve a tu ritmo."),
            }, variant);
        }

        /// <summary>Du läufst auf ein langsameres Auto einer anderen Klasse auf.</summary>
        internal static string Ahead(string label, int variant)
        {
            bool n = !string.IsNullOrEmpty(label);
            string de = n ? label : "ein Wagen aus einer langsameren Klasse", en = n ? "a " + label : "a slower-class car", es = n ? "un " + label : "un coche de una clase más lenta";
            string De = n ? label : "Ein Wagen aus einer langsameren Klasse", En = n ? "A " + label : "A slower-class car", Es = n ? "Un " + label : "Un coche de una clase más lenta";
            return Pick(new[]
            {
                RadioLanguage.T(De + " vor dir, langsamere Klasse. Früh entscheiden, wo du vorbeigehst.",
                    En + " ahead, slower class. Decide early where you go by.",
                    Es + " delante, clase más lenta. Decide pronto dónde pasar."),
                RadioLanguage.T("Du läufst auf " + de + " auf, andere Klasse. Sauber vorbei, am besten auf der Geraden.",
                    "You're catching " + en + ", different class. Clean pass, ideally on the straight.",
                    "Te acercas a " + es + ", otra clase. Adelanta limpio, mejor en la recta."),
                // Astra-Prüfbericht 25.11.: „der macht dir Platz“ ist eine Behauptung über den anderen Fahrer, die keiner messen kann.
                RadioLanguage.T("Klassenfremder Verkehr voraus: " + de + ". Kein Risiko, rechne nicht damit, dass er Platz macht.",
                    "Other-class traffic ahead: " + en + ". No risk, don't count on him making room.",
                    "Tráfico de otra clase delante: " + es + ". Sin riesgos, no cuentes con que te deje sitio."),
                RadioLanguage.T(De + " kommt näher. Zeig dich früh, dann ist er berechenbar.",
                    En + " coming up. Show yourself early so he knows you're there.",
                    Es + " se acerca. Déjate ver pronto para que sepa que estás ahí."),
                RadioLanguage.T("Vorne " + de + ", langsamere Klasse. Nicht in der Bremszone hängen bleiben.",
                    "Up ahead " + en + ", slower class. Don't get stuck in the braking zone.",
                    "Delante " + es + ", clase más lenta. No te quedes atascado en la frenada."),
            }, variant);
        }
    }
}
