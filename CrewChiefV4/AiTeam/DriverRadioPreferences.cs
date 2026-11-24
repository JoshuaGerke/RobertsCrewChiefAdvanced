using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Explicit, reversible wishes for optional race radio.</summary>
    internal sealed class DriverRadioPreferences
    {
        private sealed class Values { public int Routine=1, Gaps=1, Density=1; public bool QualifyingSectors=true; public int RearTrafficSeconds=DefaultRearTrafficSeconds; }
        internal const int DefaultRearTrafficSeconds = 3;
        private Values values = new Values();
        private readonly string path;
        private readonly object sync = new object();
        private bool invalid;
        public DriverRadioPreferences(string folder)
        {
            path=Path.Combine(folder,"funkwuensche.json");
            if(!File.Exists(path))return;
            try {
                if(new FileInfo(path).Length>4096)throw new InvalidDataException();
                var json=JObject.Parse(File.ReadAllText(path,Encoding.UTF8));
                foreach(string field in new[]{"schema","routine","gaps"})
                    if(json[field]==null || json[field].Type!=JTokenType.Integer)throw new InvalidDataException();
                int routine=(int)json["routine"], gaps=(int)json["gaps"];
                int schema = (int)json["schema"];
                if (schema < 1 || schema > 4 || routine < 0 || routine > 2 || gaps < 0 || gaps > 2 ||
                    schema >= 2 && json["qualifying_sectors"]?.Type != JTokenType.Boolean ||
                    schema >= 3 && json["rear_traffic_seconds"]?.Type != JTokenType.Integer ||
                    schema == 4 && json["radio_density"]?.Type != JTokenType.Integer) throw new InvalidDataException();
                int rear = schema >= 3 ? (int)json["rear_traffic_seconds"] : DefaultRearTrafficSeconds;
                int density = schema == 4 ? (int)json["radio_density"] : 1;
                if (density < 0 || density > 2) throw new InvalidDataException();
                if (rear < 2 || rear > 6) throw new InvalidDataException();
                values=new Values {Routine=routine,Gaps=gaps,QualifyingSectors=schema==1 || (bool)json["qualifying_sectors"],
                    RearTrafficSeconds=rear,Density=density};
            } catch(Exception e) when(e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException || e is ArgumentException || e is FormatException || e is InvalidCastException || e is OverflowException) { invalid=true; }
        }
        private static double Scale(int level) => level==2?2:level==0?.75:1;
        /// <summary>Abstände „häufiger“ halbiert das Intervall (Rob, Monza 22.11.: ein Viertel kürzer war nicht spürbar).</summary>
        internal static double GapScale(int level) => level==2?2:level==0?.5:1;
        public double IntervalScale(string topic)
        {
            var current=Volatile.Read(ref values);
            // Rob 24.11.: Keine Funkstufen mehr. Ein früher gespeichertes „Routineberichte seltener“ dämpft nichts mehr; nur der
            // gezielte Wunsch „Abstände häufiger/seltener“ (Monza 22.11.) gilt weiter.
            return (topic=="ahead" || topic=="behind")?GapScale(current.Gaps):1;
        }
        private static string Label(int level) => level==2?"seltener":level==0?"häufiger":"normal";
        public bool QualifyingSectors => Volatile.Read(ref values).QualifyingSectors;
        public int RearTrafficSeconds => Volatile.Read(ref values).RearTrafficSeconds;
        /// <summary>Funkdichte im Rennen: 0 = mehr Infos, 1 = normal, 2 = weniger (steuert die Funklast für Daten).</summary>
        public int Density => Volatile.Read(ref values).Density;
        public string Describe()
        {
            var current=Volatile.Read(ref values);
            return invalid?"Funkwünsche-Datei nicht lesbar; Standardintervalle aktiv.":
                "Abstände "+Label(current.Gaps)+
                ". Sektorvergleiche im Quali "+(current.QualifyingSectors?"an.":"aus.") +
                " Schneller Verkehr von hinten ab etwa " + current.RearTrafficSeconds + " Sekunden.";
        }
        internal static string Normalize(string question) => Regex.Replace((question??"").ToLowerInvariant()
            .Replace("ä","ae").Replace("ö","oe").Replace("ü","ue").Replace("ß","ss").Replace("á","a").Trim().TrimEnd('.','!','?'),@"\s+"," ");
        internal static bool IsCommand(string question) => Parse(question).Success;
        private static Match Parse(string question)
        {
            var strict = Regex.Match(Normalize(question), CommandPattern);
            if (strict.Success) return strict;
            string loose = LooseGapWish(question);
            return loose == null ? strict : Regex.Match(loose, CommandPattern);
        }

        /// <summary>
        /// RaceRoom Monza 22.11. (Rob an Ben): „Das muss häufiger kommen, Ben. … wie die Abstände sind zum Hintermann, zum
        /// Vordermann. Das muss häufiger kommen.“ Ben antwortete „automatische Häufigkeit bleibt unverändert“ – erkannt wurde nur
        /// der feste Satz „Abstände häufiger“. Ein klarer Wunsch mit „Abstand“ und „häufiger/öfter“ (oder „seltener“) zählt jetzt
        /// auch frei formuliert. Fragen („Kommen die Abstände häufiger?“) ändern nichts.
        /// </summary>
        internal static string LooseGapWish(string question)
        {
            string raw = (question ?? "").Trim();
            if (raw.Length == 0 || raw.Length > 400 || raw.EndsWith("?")) return null;
            string n = Normalize(raw);
            if (Regex.IsMatch(n, @"^(wie|was|warum|wieso|wann|kommen|kommt|gibt|ist|sind|kannst|koenntest|how|what|why|when|do|does|are|is)\b")) return null;
            if (!Regex.IsMatch(n, @"\b(abstand|abstaende|abstaenden|gaps?|gap calls|distancias?)\b")) return null;
            // „nicht häufiger“ und Bedingungen („nur wenn ich führe“) bleiben mehrdeutig – außer Robs Fall „wenn ich im Zweikampf bin“.
            if (Regex.IsMatch(n, @"\b(nicht|kein|keine|nie|not|no|never)\b")) return null;
            if (Regex.IsMatch(n, @"\b(wenn|nur|aber|ausser|when|only|but|unless)\b") && !Regex.IsMatch(n, @"\b(zweikampf\w*|duell\w*|battle\w*)\b")) return null;
            bool more = Regex.IsMatch(n, @"\b(haeufiger|oefter|oefters|more often|mas a menudo|mas seguido)\b");
            bool less = Regex.IsMatch(n, @"\b(seltener|less often|menos a menudo)\b");
            if (more == less) return null;
            return more ? "abstaende haeufiger" : "abstaende seltener";
        }

        private const string CommandPattern =
            @"^(?:(?:bitte|ab jetzt|kuenftig|merke dir[:,]?) )*(?:(?:(?:melde(?: mir)?|ich moechte) )?(?<topic>abstandsmeldungen|abstandsberichte|abstaende) (?:bitte )?(?<level>seltener|haeufiger|normal)(?: hoeren)?|(?:sektorvergleiche|sektoransagen) im qual(?:i|ifying) (?<sectors>an|aus)|ich moechte im qual(?:i|ifying) (?:(?<no>keine) )?(?:sektorvergleiche|sektoransagen) hoeren|(?:schneller verkehr von hinten|schnellere (?:autos|gegner) von hinten) (?:bitte )?(?:erst )?bei (?<rear>[2-6]|zwei|drei|vier|fuenf|sechs) sekunden|(?<reset>funkwuensche zuruecksetzen)|(?<query>welche funkwuensche sind gespeichert|funkwuensche anzeigen))$";
        public bool TryCommand(string question,out string reply)
        {
            reply=null; var match=Parse(question); if(!match.Success)return false;
            lock(sync) {
                if(match.Groups["query"].Success) { reply=Describe(); return true; }
                if(invalid && !match.Groups["reset"].Success) { reply="Die Funkwünsche-Datei ist nicht lesbar. Ich habe nichts verändert. Zum Wiederherstellen: Funkwünsche zurücksetzen."; return true; }
                var old=Volatile.Read(ref values);
                var next=new Values {Routine=old.Routine,Gaps=old.Gaps,QualifyingSectors=old.QualifyingSectors,RearTrafficSeconds=old.RearTrafficSeconds,Density=old.Density};
                bool rearPreference = match.Groups["rear"].Success;
                bool sectorPreference = !rearPreference && !match.Groups["topic"].Success && !match.Groups["reset"].Success;
                if(match.Groups["reset"].Success)next=new Values();
                else if(rearPreference)
                {
                    string value = match.Groups["rear"].Value;
                    next.RearTrafficSeconds = int.TryParse(value, out int seconds) ? seconds :
                        2 + Array.IndexOf(new[] { "zwei", "drei", "vier", "fuenf", "sechs" }, value);
                }
                else if(sectorPreference)next.QualifyingSectors=!match.Groups["no"].Success && match.Groups["sectors"].Value!="aus";
                else {
                    string level=match.Groups["level"].Value;
                    int value=level=="seltener"?2:level=="haeufiger"?0:1;
                    next.Gaps=value;
                }
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    File.WriteAllText(temporary,new JObject { ["schema"]=4,["routine"]=next.Routine,["gaps"]=next.Gaps,
                        ["qualifying_sectors"]=next.QualifyingSectors,["rear_traffic_seconds"]=next.RearTrafficSeconds,["radio_density"]=next.Density }.ToString(),new UTF8Encoding(false));
                    if(File.Exists(path)) {
                        if(invalid)File.Copy(path,path+".corrupt."+Guid.NewGuid().ToString("N")+".bak");
                        if(!File.Exists(path+".initial.bak"))File.Copy(path,path+".initial.bak");
                        File.Replace(temporary,path,null);
                    } else File.Move(temporary,path);
                    Volatile.Write(ref values,next);
                    invalid=false;
                    reply=rearPreference ? "Gespeichert. Schnelleren Verkehr von hinten melde ich bei etwa " +
                        next.RearTrafficSeconds + " Sekunden oder näher." : sectorPreference?"Gespeichert. Sektorvergleiche im Quali "+(next.QualifyingSectors?"an.":"aus."):
                        "Gespeichert, ab dem nächsten neu geplanten Intervall. "+Describe();
                } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) { reply="Ich konnte den Funkwunsch nicht speichern. Die bisherigen Intervalle bleiben aktiv."; }
                return true;
            }
        }
    }
}
