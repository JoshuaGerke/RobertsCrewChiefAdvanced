using System;
using System.Globalization;
using System.Threading;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Sprache des Funks (Deutsch, Englisch, Spanisch). Alle gesprochenen Texte wählen hierüber ihre Fassung.
    /// Deutsch bleibt die Ausgangssprache; Tests und bestehende Profile verhalten sich unverändert.
    /// </summary>
    public static class RadioLanguage
    {
        public const string German = "de", English = "en", Spanish = "es";
        public const string Auto = "auto";
        private static string current = German;

        /// <summary>Aktuelle Funksprache: "de", "en" oder "es".</summary>
        public static string Current
        {
            get { return Volatile.Read(ref current); }
            set { Volatile.Write(ref current, Normalize(value)); }
        }

        public static bool IsGerman => Current == German;

        /// <summary>"auto" oder leer übernimmt die Windows-Sprache; alles außer DE/ES wird Englisch.</summary>
        public static string Resolve(string setting)
        {
            string value = (setting ?? "").Trim().ToLowerInvariant();
            if (value == German || value == English || value == Spanish) return value;
            return Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }

        private static string Normalize(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            return value == German || value == Spanish ? value : value == English ? English : value.StartsWith("de") ? German : value.StartsWith("es") ? Spanish : English;
        }

        /// <summary>Text in der aktuellen Funksprache.</summary>
        public static string T(string de, string en, string es)
        {
            string lang = Current;
            return lang == English ? en : lang == Spanish ? es : de;
        }

        /// <summary>Auswahl (z. B. Satzvarianten) in der aktuellen Funksprache.</summary>
        public static T Pick<T>(T de, T en, T es)
        {
            string lang = Current;
            return lang == English ? en : lang == Spanish ? es : de;
        }

        /// <summary>Zahlenformat der Funksprache (Komma in DE/ES, Punkt in EN).</summary>
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Current == English ? "en-GB" : Current == Spanish ? "es-ES" : "de-DE");

        public static string Num(double value, string format) => value.ToString(format, Culture);
        public static string Num(float value, string format) => value.ToString(format, Culture);

        /// <summary>Sprachcode für die Spracherkennung.</summary>
        public static string SpeechCode => Current;

        /// <summary>Name der Funksprache für Anweisungen an das Sprachmodell (auf Deutsch, weil die Anweisungen deutsch sind).</summary>
        public static string PromptName => T("Deutsch", "Englisch", "Spanisch");
    }
}
