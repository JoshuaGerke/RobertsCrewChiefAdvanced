using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Version-independent text memory. Historic feedback never replaces current measured race data.</summary>
    public sealed class DriverMemoryStore : IDisposable
    {
        private readonly object sync = new object();
        private readonly List<string> feedback = new List<string>();
        private string[] secrets;
        private readonly DiagnosticRecorder journal;
        private int sessionEpoch;
        public readonly string Folder;
        public string NotesPath { get { return Path.Combine(Folder,"notizen.txt"); } }
        public string Status { get { return journal==null ? "Textarchiv voll oder nicht beschreibbar; Notizen bleiben erhalten" : journal.Status; } }
        public long Dropped { get { return journal==null ? 0 : journal.Dropped; } }
        private const int NoteLimit = 128 * 1024;
        private static readonly Regex wishes = new Regex(@"\b(merk[e]? dir|erinnere dich|ich möchte|ich will|mir ist wichtig|du sollst|bitte immer|bitte künftig|in zukunft|beim nächsten mal|das war falsch|stimmt nicht)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public DriverMemoryStore(string folder, string[] knownSecrets = null, string legacyNotes = null, DiagnosticLimits limits = null)
        {
            Folder=Path.GetFullPath(folder); secrets=knownSecrets ?? new string[0];
            Directory.CreateDirectory(Folder);
            ImportLegacy(legacyNotes);
            RecoverFeedback();
            try {
                journal=new DiagnosticRecorder(Path.Combine(Folder,"Gespraeche"), limits ?? new DiagnosticLimits {
                    QueueCapacity=128, SegmentBytes=1024*1024, CaptureBytes=8*1024*1024, FolderBytes=128*1024*1024, RecordResources=false
                },secrets);
            } catch(IOException) { /* Existing notes and historical context remain usable at the archive limit. */ }
        }

        private string Clean(string text)
        { return DiagnosticRecorder.RedactText(text ?? "",Volatile.Read(ref secrets)).Replace("\r"," ").Replace("\n"," ").Replace("\0","").Trim(); }

        public void RememberSecret(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return;
            lock(sync) {
                if(!secrets.Contains(value))Volatile.Write(ref secrets,secrets.Concat(new[]{value}).Reverse().Take(16).Reverse().ToArray());
            }
            if(journal!=null)journal.RememberSecret(value);
        }

        private void ImportLegacy(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source) || Path.GetFullPath(source)==NotesPath || new FileInfo(source).Length>NoteLimit) return;
            var existing=File.Exists(NotesPath) ? new HashSet<string>(File.ReadAllLines(NotesPath,Encoding.UTF8)) : new HashSet<string>();
            foreach(string line in File.ReadLines(source,Encoding.UTF8))
            {
                string safe=Clean(line);
                if(safe.Length>0 && existing.Add(safe)) AppendLine(safe);
            }
        }

        private void AppendLine(string text)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(text+Environment.NewLine);
            if ((File.Exists(NotesPath) ? new FileInfo(NotesPath).Length : 0)+bytes.Length>NoteLimit)
                throw new IOException("Notizlimit erreicht. Bestehende Notizen selbst archivieren; nichts wurde gelöscht.");
            using(var file=new FileStream(NotesPath,FileMode.Append,FileAccess.Write,FileShare.Read)) file.Write(bytes,0,bytes.Length);
        }

        public bool AddNote(string note)
        {
            string safe=Clean(note);
            if(safe.Length==0)return false;
            lock(sync) AppendLine(DateTime.Now.ToString("dd.MM.yyyy")+": "+safe);
            return true;
        }

        public void BeginSession(string game,string track,string car,string sessionType,int sessionId)
        {
            int epoch=Interlocked.Increment(ref sessionEpoch);
            if(journal!=null)journal.TryRecord("memory.session",new Dictionary<string,object> {
                {"game",game},{"track",track},{"car",car},{"session_type",sessionType}
            },sessionId:sessionId,epoch:epoch);
        }

        public bool RecordDialogue(TeamMember member, string question, string proposedAnswer, string game=null, string track=null, int sessionId=int.MinValue)
        {
            string q=Clean(question), a=Clean(proposedAnswer);
            bool candidate=wishes.IsMatch(q);
            bool accepted=journal!=null && journal.TryRecord(proposedAnswer==null?"memory.driver_input":"memory.dialogue", new Dictionary<string,object> {
                {"speaker",member.ToString()},{"transcript",q},{"text",a},{"game",game},{"track",track},
                {"source",candidate?"driver_feedback":"dialogue"},{"result",proposedAnswer==null?"driver_input_only":"generated_or_queued_not_audible_proof"}
            },sessionId:sessionId,epoch:Volatile.Read(ref sessionEpoch));
            if(accepted && candidate) RememberFeedback(FeedbackText(q,game,track));
            return accepted;
        }

        public bool RecordLap(string game, string track, string car, LapRecord lap, int sessionId)
        {
            if(lap==null)return false;
            return journal!=null && journal.TryRecord("memory.lap",new Dictionary<string,object> {
                {"game",game},{"track",track},{"car",car},{"lap",lap.LapNumber},{"lap_time_s",lap.LapTime},
                {"lap_valid",lap.Valid},{"in_pit",lap.InPitLap},{"fuel_l",lap.FuelAtEnd},{"fuel_used_l",lap.FuelUsed},
                {"tyre_wear_pct",lap.TyreWear},{"source","historical_measured_lap_not_current_race_state"}
            },sessionId:sessionId,epoch:Volatile.Read(ref sessionEpoch));
        }

        public bool RecordObservation(string topic,string state,string reason,string text,int sessionId)
        {
            return journal!=null && journal.TryRecord("memory.observation",new Dictionary<string,object> {
                {"topic",topic},{"status",state},{"reason",reason},{"text",text},{"source","historical_local_decision_not_current_measurement"}
            },sessionId:sessionId,epoch:Volatile.Read(ref sessionEpoch));
        }

        public bool RecordRadioStarted(TeamMember member,string text,int sessionId)
        {
            return journal!=null && journal.TryRecord("memory.radio_start",new Dictionary<string,object> {
                {"speaker",member.ToString()},{"text",text},{"source","audio_backend_start"},{"result","not_proof_of_completed_or_audible_speech"}
            },sessionId:sessionId,epoch:Volatile.Read(ref sessionEpoch));
        }

        private string FeedbackText(string text,string game,string track)
        {
            string label=string.Join(" / ",new[]{Clean(game),Clean(track)}.Where(s=>s.Length>0));
            return (label.Length==0?"":"["+label+"] ")+text;
        }

        private void RememberFeedback(string text)
        {
            lock(sync)
            {
                feedback.RemoveAll(s=>string.Equals(s,text,StringComparison.OrdinalIgnoreCase));
                feedback.Add(text);
                if(feedback.Count>30) feedback.RemoveAt(0);
            }
        }

        private void RecoverFeedback()
        {
            string folder=Path.Combine(Folder,"Gespraeche");
            if(!Directory.Exists(folder))return;
            // Empty captures from repeated upgrades must not hide an older actual conversation.
            // Disk history is quota-bound; only thirty feedback items are kept in memory.
            foreach(string file in Directory.EnumerateFiles(folder,"*.jsonl").OrderBy(File.GetLastWriteTimeUtc).ThenBy(p=>p))
            {
                using(var reader=new StreamReader(new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite),Encoding.UTF8))
                {
                    string line;
                    while((line=reader.ReadLine())!=null)
                    {
                        if(line.Length>8192)continue;
                        try {
                            var record=JObject.Parse(line);
                            if(((string)record["kind"]=="memory.dialogue" || (string)record["kind"]=="memory.driver_input") && (string)record["data"]?["source"]=="driver_feedback")
                                RememberFeedback(FeedbackText(Clean((string)record["data"]?["transcript"]),(string)record["data"]?["game"],(string)record["data"]?["track"]));
                        } catch(Newtonsoft.Json.JsonException) { }
                    }
                }
            }
        }

        public string PromptBlock(string question)
        {
            string notes="";
            lock(sync)
            {
                if(File.Exists(NotesPath) && new FileInfo(NotesPath).Length<=NoteLimit)
                    notes=string.Join("\n",File.ReadAllLines(NotesPath,Encoding.UTF8).Where(s=>!string.IsNullOrWhiteSpace(s)).Reverse().Take(30).Reverse().Select(Clean));
                var words=new HashSet<string>(Regex.Matches(question ?? "",@"\p{L}{4,}").Cast<Match>().Select(m=>m.Value.ToLowerInvariant()));
                var relevant=feedback.Select((text,index)=>new {text,index,score=words.Count(w=>text.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0)})
                    .OrderByDescending(x=>x.score).ThenByDescending(x=>x.index).Take(4).Select(x=>x.text.Length>220?x.text.Substring(0,220):x.text);
                if(notes.Length>1800)notes=notes.Substring(notes.Length-1800);
                string history=string.Join("\n",relevant);
                if(notes.Length==0 && history.Length==0)return "";
                return "\n\nDauerhaftes Fahrergedächtnis (historischer Kontext, keine aktuellen Messwerte und keine erfolgte Programmänderung):\n"+
                    new JObject { ["notizen"]=notes,["aufgezeichnetes_fahrerfeedback_unbestaetigt"]=history }.ToString(Newtonsoft.Json.Formatting.None);
            }
        }

        public void Dispose() { if(journal!=null)journal.Dispose(); }
    }

    public static class DriverMemory
    {
        private static readonly object sync=new object();
        private static DriverMemoryStore store;
        private static Task initialization;
        private static int generation;
        private static volatile bool enabled=true;
        private static string status="wird vorbereitet";
        private static string noteError;
        public static bool Enabled { get { return enabled; } }
        public static string Folder { get { return ResolveFolder(DataFiles.IsolatedRoot,Environment.GetEnvironmentVariable("RCCA_MEMORY_ROOT"),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); } }
        public static string NotesPath { get { return Path.Combine(Folder,"notizen.txt"); } }
        public static string Status { get { var current=Volatile.Read(ref store);return enabled ? !string.IsNullOrEmpty(noteError)?noteError:current==null?status:current.Status=="recording"?"bereit – Texte lokal gespeichert":current.Status : "ausgeschaltet"; } }

        public static string ResolveFolder(string isolatedRoot, string sharedOverride, string documents)
        {
            if(!string.IsNullOrWhiteSpace(sharedOverride))return Path.GetFullPath(sharedOverride);
            return isolatedRoot!=null ? Path.Combine(Path.GetFullPath(isolatedRoot),"DriverMemory") : Path.Combine(documents,"RRCA","Fahrergedaechtnis");
        }

        public static void Configure(bool on)
        {
            enabled=on;
            lock(sync)
            {
                if(!on)
                {
                    Interlocked.Increment(ref generation);initialization=null;
                    var previous=Interlocked.Exchange(ref store,null);
                    if(previous!=null)Task.Run(()=>previous.Dispose());
                    return;
                }
                if(store!=null || initialization!=null)return;
                int expected=Volatile.Read(ref generation);
                initialization=Task.Run(()=> {
                    try {
                        var created=new DriverMemoryStore(Folder,new[]{AiSecretStore.Load(),AiSecretStore.LoadElevenLabs()},Path.Combine(AiSecretStore.Folder,"notizen.txt"));
                        lock(sync) {
                            if(!enabled || expected!=Volatile.Read(ref generation)){created.Dispose();return;}
                            Volatile.Write(ref store,created);status="bereit";
                        }
                    } catch(Exception e) { status="nicht bereit ("+e.GetType().Name+") – vorhandene Dateien bleiben erhalten"; }
                });
            }
        }

        public static string PromptBlock(string question=null)
        {
            if(!enabled)return "";
            Task task;lock(sync)task=initialization;
            if(task!=null && !task.IsCompleted)task.Wait(250); // model worker only, never the telemetry or spotter thread
            try { var current=Volatile.Read(ref store);return current==null?"":current.PromptBlock(question); }
            catch(IOException) { return ""; }
        }

        public static bool TryAddNote(string note)
        {
            if(!enabled)return false;
            Task task;lock(sync)task=initialization;
            if(task!=null && !task.IsCompleted)task.Wait(3000);
            var current=Volatile.Read(ref store);
            try { bool saved=current!=null && current.AddNote(note);if(saved)noteError=null;return saved; }
            catch(IOException) { noteError="Notiz konnte nicht gespeichert werden; Speicher und Ordnerzugriff prüfen";return false; }
            catch(UnauthorizedAccessException) { noteError="Notizordner ist nicht beschreibbar";return false; }
        }

        public static void BeginSession(string game,string track,string car,string sessionType,int sessionId)
        { var current=Volatile.Read(ref store);if(enabled && current!=null)current.BeginSession(game,track,car,sessionType,sessionId); }

        public static void RememberDialogue(TeamMember member,string question,string answer,string game,string track,int sessionId)
        { var current=Volatile.Read(ref store);if(enabled && current!=null)current.RecordDialogue(member,question,answer,game,track,sessionId); }

        public static void RememberLap(string game,string track,string car,LapRecord lap,int sessionId)
        { var current=Volatile.Read(ref store);if(enabled && current!=null)current.RecordLap(game,track,car,lap,sessionId); }

        public static void RememberRadio(TeamMember member,string text,int sessionId)
        { var current=Volatile.Read(ref store);if(enabled && current!=null)current.RecordRadioStarted(member,text,sessionId); }

        public static void RememberObservation(string topic,string state,string reason,string text,int sessionId)
        { var current=Volatile.Read(ref store);if(enabled && current!=null)current.RecordObservation(topic,state,reason,text,sessionId); }

        public static void RememberSecret(string secret)
        { var current=Volatile.Read(ref store);if(current!=null)current.RememberSecret(secret); }

        public static void Stop()
        {
            enabled=false;Interlocked.Increment(ref generation);
            var current=Interlocked.Exchange(ref store,null);if(current!=null)current.Dispose();
        }
    }
}
