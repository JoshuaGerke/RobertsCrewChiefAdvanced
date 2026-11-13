using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Value-only snapshot taken BEFORE native fragments become sound paths.
    /// No sound-name parsing, retained OpponentData, network calls or native validator side effects.</summary>
    public sealed class NativeFieldFact
    {
        public readonly string Kind,Key,Name,Detail,Source,CarNumber;
        public readonly int CarId;
        public readonly int Session,Lap,Position;
        public readonly SessionType Type;
        public readonly DateTime Utc;
        public readonly double Seconds;
        private NativeFieldFact(string kind,string key,string name,string detail,string source,GameStateData gs,OpponentData op,double seconds)
        {Kind=kind;Key=key;Name=name;Detail=detail;Source=source;Session=gs.SessionData.SessionId;Type=gs.SessionData.SessionType;
            Lap=op==null?-1:op.CompletedLaps;Position=op==null?-1:op.ClassPosition;Utc=gs.Now;Seconds=seconds;CarNumber=op?.CarNumber;CarId=op?.CostId??-1;}
        public static string Classify(string source)
        {
            if(source=="leader_is_pitting" || source=="car_in_front_is_pitting" || source=="car_behind_is_pitting" || source=="car_is_pitting")return "pit";
            if(source=="new_leader")return "leader";
            if(source=="new_fastest_lap")return "fastest";
            if(source=="retirement")return "retired";
            if(source=="disqualified")return "dq";
            if((source??"").StartsWith("opponent_tyre_change_",StringComparison.Ordinal))return "tyres";
            return null;
        }
        public static NativeFieldFact Capture(string source,IEnumerable<MessageFragment> fragments,GameStateData gs)
        {
            string kind=Classify(source);if(kind==null || gs==null)return null;
            var list=(fragments??Enumerable.Empty<MessageFragment>()).Where(f=>f!=null).ToArray();
            var op=list.Where(f=>f.type==FragmentType.Opponent).Select(f=>f.opponent).FirstOrDefault(o=>o!=null);
            if(op==null && source=="leader_is_pitting")op=gs.PitData.OpponentForLeaderPitting;
            if(op==null && source=="car_in_front_is_pitting")op=gs.PitData.OpponentForCarAheadPitting;
            if(op==null && source=="car_behind_is_pitting")op=gs.PitData.OpponentForCarBehindPitting;
            string name=op==null?null:op.DriverRawName;
            if(name==null && (kind=="retired" || kind=="dq"))
            {
                var names=kind=="retired"?gs.retriedDriverNames:gs.disqualifiedDriverNames;
                var matches=names.Keys.Where(n=>list.Any(f=>f.type==FragmentType.Text && f.text==DriverNameHelper.getUsableDriverName(n))).Take(2).ToArray();
                if(matches.Length==1)name=matches[0];
            }
            if(string.IsNullOrWhiteSpace(name))return null; // Unknown identity is never guessed from a queue label.
            if(op==null)
            {
                var matches=gs.OpponentData.Values.Where(o=>o.DriverRawName==name).Take(2).ToArray();
                if(matches.Length==1)op=matches[0];
            }
            string key=op==null?null:gs.OpponentData.Where(p=>p.Value==op).Select(p=>p.Key).FirstOrDefault();
            if(op!=null && key==null)return null;
            double seconds=list.Where(f=>f.type==FragmentType.Time && f.timeSpan!=null).Select(f=>f.timeSpan.timeSpan.TotalSeconds).FirstOrDefault();
            return new NativeFieldFact(kind,key,name,op==null?null:op.CurrentTyres.ToString(),source,gs,op,seconds);
        }
    }

    /// <summary>Bounded handoff from native decisions to session-aware radio and race memory.</summary>
    public sealed class NativeFieldEvents
    {
        private readonly ConcurrentQueue<NativeFieldFact> incoming=new ConcurrentQueue<NativeFieldFact>();
        private readonly List<ObservedRaceCall> pending=new List<ObservedRaceCall>();
        private readonly Dictionary<string,DateTime> seen=new Dictionary<string,DateTime>();
        private DateTime lastRadio=DateTime.MinValue;
        private readonly RadioVariantCursor pitVariants = new RadioVariantCursor();
        private readonly Dictionary<ObservedRaceCall, NativeFieldFact> pitFacts = new Dictionary<ObservedRaceCall, NativeFieldFact>();
        private NativeFieldFact spokenPit;
        private DateTime spokenPitUtc;

        internal bool HasClaimedPit(PitGainEvidence gain, DateTime now)
        {
            return gain != null && ((now >= spokenPitUtc && now - spokenPitUtc <= TimeSpan.FromSeconds(12) && gain.Matches(spokenPit)) ||
                pitFacts.Any(pair => gain.Matches(pair.Value) && (pair.Key.IsReserved || pair.Key.DeliveryStartedUtc != DateTime.MinValue)));
        }
        public void Capture(NativeFieldFact fact)
        {if(fact==null)return;incoming.Enqueue(fact);NativeFieldFact unused;while(incoming.Count>32)incoming.TryDequeue(out unused);}
        public void Reset(){NativeFieldFact f;while(incoming.TryDequeue(out f)){}pending.Clear();pitFacts.Clear();seen.Clear();lastRadio=DateTime.MinValue;spokenPit=null;spokenPitUtc=DateTime.MinValue;}
        public void CaptureMappedPits(GameStateData before,GameStateData gs)
        {
            if(before==null || gs==null || before.SessionData.SessionId!=gs.SessionData.SessionId || before.SessionData.SessionType!=gs.SessionData.SessionType || gs.SessionData.IsNewSession)return;
            var p=gs.PitData;var old=before.PitData;
            if(p.CarInFrontIsPitting && !old.CarInFrontIsPitting)Capture(NativeFieldFact.Capture("car_in_front_is_pitting",null,gs));
            if(p.CarBehindIsPitting && !old.CarBehindIsPitting)Capture(NativeFieldFact.Capture("car_behind_is_pitting",null,gs));
            if(p.LeaderIsPitting && !old.LeaderIsPitting)Capture(NativeFieldFact.Capture("leader_is_pitting",null,gs));
        }
        private static string Name(string text)
        {return new string((text??"").Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='\'').Take(60).ToArray()).Trim();}
        public void Observe(GameStateData gs,DateTime now,AiRaceMemory memory,bool names)
        {
            NativeFieldFact f;
            foreach(var key in seen.Where(p=>now-p.Value>TimeSpan.FromMinutes(2)).Select(p=>p.Key).ToArray())seen.Remove(key);
            while(incoming.TryDequeue(out f))
            {
                if(gs==null || f.Session!=gs.SessionData.SessionId || f.Type!=gs.SessionData.SessionType || now<f.Utc || now-f.Utc>TimeSpan.FromSeconds(2))continue;
                string id=f.Kind+"|"+(f.Key??f.Name)+"|"+f.Lap+"|"+f.Detail+"|"+f.Seconds;
                if(seen.ContainsKey(id))continue;
                if(seen.Count>=128)seen.Remove(seen.OrderBy(p=>p.Value).First().Key);
                seen[id]=now;
                string text = Text(f, names, 0);
                if(text==null)continue;
                if(memory!=null)memory.AddObservation(text,now,"field-"+f.Kind,f.Key,f.Source);
                // Existing SessionLapObserver already owns non-race class records.
                bool nonRace=SessionLapObserver.Supported(f.Type);
                bool relevant=f.Type==SessionType.Race || nonRace && f.Kind=="pit";
                if((f.Kind=="retired" || f.Kind=="dq" || f.Kind=="tyres") && (f.Position<1 || Math.Abs(f.Position-gs.SessionData.ClassPosition)>2 && f.Position!=1))relevant=false;
                if(!relevant){LiveDiagnostics.Decision("field-"+f.Kind,"stored","session_or_rival_not_relevant",text);continue;}
                var fact=f;
                if (pending.Count >= 16) Remove(pending[0]);
                var call = new ObservedRaceCall {Topic="field-"+f.Kind,Text=text,Evidence="native:"+f.Source+";opponent="+f.Key,
                    SessionId=f.Session,SessionType=f.Type,Position=-1,Member=TeamMember.Ben,Priority=3,
                    DetectedUtc=now,ExpiresUtc=now.AddSeconds(10),AdditionalValidity=live=>StillCurrent(fact,live)};
                pending.Add(call);
                if (f.Kind == "pit")
                {
                    pitFacts.Add(call, f);
                    call.VariantCursor = pitVariants;
                    call.FieldNamesAllowed = names;
                }
                LiveDiagnostics.Decision("field-"+f.Kind,"detected","native_semantic_handoff",text);
            }
        }
        public static bool StillCurrent(NativeFieldFact f,GameStateData gs)
        {
            if(gs==null || gs.SessionData.SessionId!=f.Session || gs.SessionData.SessionType!=f.Type)return false;
            if(f.Kind=="retired")return gs.retriedDriverNames.ContainsKey(f.Name);
            if(f.Kind=="dq")return gs.disqualifiedDriverNames.ContainsKey(f.Name);
            OpponentData op;if(f.Key==null || !gs.OpponentData.TryGetValue(f.Key,out op) || op.DriverRawName!=f.Name)return false;
            if(f.Kind=="pit")return op.CarNumber==f.CarNumber && op.CostId==f.CarId && (op.InPits || op.isEnteringPits());
            if(f.Kind=="leader")return op.IsActive && op.ClassPosition==1 && gs.SessionData.ClassPosition>1;
            if(f.Kind=="tyres")return op.CurrentTyres.ToString()==f.Detail;
            return f.Kind=="fastest" && f.Seconds>10 && op.CurrentBestLapTime>0 && op.CurrentBestLapTime<=f.Seconds+.01;
        }
        private void Remove(ObservedRaceCall call)
        {
            if (pitFacts.TryGetValue(call, out var fact) && call.DeliveryStartedUtc > spokenPitUtc)
            {
                spokenPit = fact;
                spokenPitUtc = call.DeliveryStartedUtc;
            }
            pitFacts.Remove(call);
            pending.Remove(call);
        }

        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,bool enabled,bool minimal,
            Func<NativeFieldFact, bool> combinedOwnsPit = null)
        {
            foreach(var call in pending.ToArray())
            {
                if(call.DeliveryStartedUtc!=DateTime.MinValue){lastRadio=call.DeliveryStartedUtc;Remove(call);}
                else if(!call.IsValid(gs,now))Remove(call);
            }
            if(!enabled || minimal){pending.Clear();pitFacts.Clear();return null;}
            if(busy || AiRadioSchedule.BusyForRoutine(gs) || now-lastRadio<TimeSpan.FromSeconds(8))return null;
            foreach (var call in pending)
            {
                if (!call.CanReserve(now)) continue;
                if (pitFacts.TryGetValue(call, out var pit))
                {
                    if (combinedOwnsPit != null && (combinedOwnsPit(pit) ||
                        (pit.Source == "car_in_front_is_pitting" || pit.Position == gs.SessionData.ClassPosition - 1) &&
                        now - pit.Utc < TimeSpan.FromSeconds(1.8))) continue;
                    if (pitFacts.Keys.Any(other => other.IsReserved)) continue;
                    call.Text = Text(pit, call.FieldNamesAllowed, pitVariants.Heard);
                }
                if (call.TryReserve(now)) return call;
            }
            return null;
        }
        private static string Text(NativeFieldFact f,bool names,int variant)
        {
            string who=names?Name(f.Name):RadioLanguage.T("Ein Gegner","A rival","Un rival");if(who.Length==0)return null;
            if (f.Kind == "pit") return RadioPhraseCatalog.Current.Format("pit-rival", variant, who);
            if(f.Kind=="leader")return who+RadioLanguage.T(" führt jetzt unsere Klasse an."," now leads our class."," lidera ahora nuestra clase.");
            if(f.Kind=="retired")return who+RadioLanguage.T(" ist ausgefallen."," is out of the race."," ha abandonado.");
            if(f.Kind=="dq")return who+RadioLanguage.T(" wurde disqualifiziert."," has been disqualified."," ha sido descalificado.");
            if(f.Kind=="tyres")return who+RadioLanguage.T(" hat die Reifenmischung gewechselt."," has changed tyre compound."," ha cambiado de compuesto de neumáticos.");
            if(f.Kind=="fastest" && f.Seconds>10 && f.Seconds<3600)return who+RadioLanguage.T(" setzt die Klassenbestzeit: "," sets the class fastest lap: "," marca la vuelta rápida de la clase: ")+LocalAnswers.LapTime((float)f.Seconds)+".";
            return null;
        }
    }
}
