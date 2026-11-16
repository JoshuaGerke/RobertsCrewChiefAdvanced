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
        private sealed class Values { public int Routine=1, Gaps=1; public bool QualifyingSectors=true; public int RearTrafficSeconds=DefaultRearTrafficSeconds; }
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
                if (schema < 1 || schema > 3 || routine < 0 || routine > 2 || gaps < 0 || gaps > 2 ||
                    schema >= 2 && json["qualifying_sectors"]?.Type != JTokenType.Boolean ||
                    schema == 3 && json["rear_traffic_seconds"]?.Type != JTokenType.Integer) throw new InvalidDataException();
                int rear = schema == 3 ? (int)json["rear_traffic_seconds"] : DefaultRearTrafficSeconds;
                if (rear < 2 || rear > 6) throw new InvalidDataException();
                values=new Values {Routine=routine,Gaps=gaps,QualifyingSectors=schema==1 || (bool)json["qualifying_sectors"],
                    RearTrafficSeconds=rear};
            } catch(Exception e) when(e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException || e is ArgumentException || e is FormatException || e is InvalidCastException || e is OverflowException) { invalid=true; }
        }
        private static double Scale(int level) => level==2?2:level==0?.75:1;
        public double IntervalScale(string topic)
        {
            var current=Volatile.Read(ref values);
            return Scale(current.Routine)*((topic=="ahead" || topic=="behind")?Scale(current.Gaps):1);
        }
        private static string Label(int level) => level==2?"seltener":level==0?"häufiger":"normal";
        public bool QualifyingSectors => Volatile.Read(ref values).QualifyingSectors;
        public int RearTrafficSeconds => Volatile.Read(ref values).RearTrafficSeconds;
        public string Describe()
        {
            var current=Volatile.Read(ref values);
            return invalid?"Funkwünsche-Datei nicht lesbar; Standardintervalle aktiv.":
                "Routineberichte "+Label(current.Routine)+". Abstände "+Label(current.Gaps)+
                ". Sektorvergleiche im Quali "+(current.QualifyingSectors?"an.":"aus.") +
                " Schneller Verkehr von hinten ab etwa " + current.RearTrafficSeconds + " Sekunden.";
        }
        internal static string Normalize(string question) => Regex.Replace((question??"").ToLowerInvariant()
            .Replace("ä","ae").Replace("ö","oe").Replace("ü","ue").Replace("ß","ss").Trim().TrimEnd('.','!','?'),@"\s+"," ");
        internal static bool IsCommand(string question) => Parse(question).Success;
        private static Match Parse(string question) => Regex.Match(Normalize(question),
            @"^(?:(?:bitte|ab jetzt|kuenftig|merke dir[:,]?) )*(?:(?:(?:melde(?: mir)?|ich moechte) )?(?<topic>routinefunk|routineberichte|abstandsmeldungen|abstandsberichte|abstaende) (?:bitte )?(?<level>seltener|haeufiger|normal)(?: hoeren)?|(?:sektorvergleiche|sektoransagen) im qual(?:i|ifying) (?<sectors>an|aus)|ich moechte im qual(?:i|ifying) (?:(?<no>keine) )?(?:sektorvergleiche|sektoransagen) hoeren|(?:schneller verkehr von hinten|schnellere (?:autos|gegner) von hinten) (?:bitte )?(?:erst )?bei (?<rear>[2-6]|zwei|drei|vier|fuenf|sechs) sekunden|(?<reset>funkwuensche zuruecksetzen)|(?<query>welche funkwuensche sind gespeichert|funkwuensche anzeigen))$");
        public bool TryCommand(string question,out string reply)
        {
            reply=null; var match=Parse(question); if(!match.Success)return false;
            lock(sync) {
                if(match.Groups["query"].Success) { reply=Describe(); return true; }
                if(invalid && !match.Groups["reset"].Success) { reply="Die Funkwünsche-Datei ist nicht lesbar. Ich habe nichts verändert. Zum Wiederherstellen: Funkwünsche zurücksetzen."; return true; }
                var old=Volatile.Read(ref values);
                var next=new Values {Routine=old.Routine,Gaps=old.Gaps,QualifyingSectors=old.QualifyingSectors,RearTrafficSeconds=old.RearTrafficSeconds};
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
                    if(match.Groups["topic"].Value.StartsWith("routine"))next.Routine=value;else next.Gaps=value;
                }
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    File.WriteAllText(temporary,new JObject { ["schema"]=3,["routine"]=next.Routine,["gaps"]=next.Gaps,
                        ["qualifying_sectors"]=next.QualifyingSectors,["rear_traffic_seconds"]=next.RearTrafficSeconds }.ToString(),new UTF8Encoding(false));
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
