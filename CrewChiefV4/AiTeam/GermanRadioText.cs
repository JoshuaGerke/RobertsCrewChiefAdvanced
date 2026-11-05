using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Pronunciation only. Evidence/logs keep the original numeric text and precision.
    /// Limit substitutions to explicit lap clocks and numbers with known units, never car/name digits.</summary>
    public static class GermanRadioText
    {
        private static readonly string[] digits={"null","eins","zwei","drei","vier","fünf","sechs","sieben","acht","neun"};
        private static readonly string[] teens={"zehn","elf","zwölf","dreizehn","vierzehn","fünfzehn","sechzehn","siebzehn","achtzehn","neunzehn"};
        private static readonly string[] tens={"","","zwanzig","dreißig","vierzig","fünfzig","sechzig","siebzig","achtzig","neunzig"};
        private static readonly Regex clock=new Regex(@"(?<![\p{L}\p{N}:])(?<m>[0-5]?\d):(?<s>[0-5]\d)[,.](?<f>\d{1,3})(?![\p{L}\p{N}:,])",RegexOptions.CultureInvariant);
        private static readonly Regex measured=new Regex(@"(?<![\p{L}\p{N}.,])(?<n>-?\d{1,4})(?:,(?<f>\d{1,3}))?\s+(?<u>Sekunden?|Liter|Grad|Prozent|PSI|bar|km/h)\b",RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);

        private static readonly Regex rank = new Regex(@"\b(?<word>Platz|Position) (?<n>[0-9]{1,3})\b", RegexOptions.CultureInvariant);
        private static readonly Regex tenths = new Regex(@"(?<![\p{L}\p{N}.,-])0,(?<n>[1-9]) Sekunden\b", RegexOptions.CultureInvariant);

        private static string Integer(int n)
        {
            if(n<0)return "minus "+Integer(-n);
            if(n<10)return digits[n];
            if(n<20)return teens[n-10];
            if(n<100)return (n%10==0?"":(n%10==1?"ein":digits[n%10])+"und")+tens[n/10];
            if(n<1000)return (n/100==1?"ein":Integer(n/100))+"hundert"+(n%100==0?"":Integer(n%100));
            return (n/1000==1?"ein":Integer(n/1000))+"tausend"+(n%1000==0?"":Integer(n%1000));
        }
        private static string Fraction(string value)
        { return " Komma "+string.Join(" ",Array.ConvertAll(value.ToCharArray(),c=>digits[c-'0'])); }

        public static string Prepare(string text, bool pitWall = false)
        {
            if(string.IsNullOrEmpty(text) || text.Length>8192)return text;
            if (pitWall)
            {
                text = rank.Replace(text, m => m.Groups["word"].Value + " " + Integer(int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture)));
                text = tenths.Replace(text, m => {
                    int value = m.Groups["n"].Value[0] - '0';
                    return (value == 1 ? "ein" : Integer(value)) + " Zehntel";
                });
            }
            string result=clock.Replace(text,m=> {
                int minutes=int.Parse(m.Groups["m"].Value,CultureInfo.InvariantCulture);
                int seconds=int.Parse(m.Groups["s"].Value,CultureInfo.InvariantCulture);
                return (minutes==0?"":minutes==1?"eine Minute ":Integer(minutes)+" Minuten ")+
                    Integer(seconds)+Fraction(m.Groups["f"].Value)+" Sekunden";
            });
            return measured.Replace(result,m=> {
                int n=int.Parse(m.Groups["n"].Value,CultureInfo.InvariantCulture);
                string value=Integer(n);
                if(m.Groups["n"].Value.StartsWith("-",StringComparison.Ordinal) && n==0)value="minus null";
                if(m.Groups["f"].Success)value+=Fraction(m.Groups["f"].Value);
                string unit=m.Groups["u"].Value;
                if(unit.Equals("km/h",StringComparison.OrdinalIgnoreCase))unit="Kilometer pro Stunde";
                return value+" "+unit;
            });
        }
    }
}
