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
        public readonly string Kind,Key,Name,Detail,Source,CarNumber,Identity;
        public readonly int CarId;
        public readonly int Session,Lap,Position;
        public readonly SessionType Type;
        public readonly DateTime Utc;
        public readonly double Seconds;
        internal NativeFieldFact(string kind,string key,string name,string detail,string source,GameStateData gs,OpponentData op,double seconds)
        {Kind=kind;Key=key;Name=name;Detail=detail;Source=source;Session=gs.SessionData.SessionId;Type=gs.SessionData.SessionType;
            Lap=op==null?-1:op.CompletedLaps;Position=op==null?-1:op.ClassPosition;Utc=gs.Now;Seconds=seconds;CarNumber=op?.CarNumber;CarId=op?.CostId??-1;
            Identity=op==null?null:RaceReader.Identity(key,op);}
        public static string Classify(string source)
        {
            if(source=="leader_is_pitting" || source=="car_in_front_is_pitting" || source=="car_behind_is_pitting" || source=="car_is_pitting")return "pit";
            if(source=="new_leader")return "leader";
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
        private readonly RadioVariantCursor fastestVariants = new RadioVariantCursor();
        private int fastestAnnounced;
        /// <summary>Funkname des Fahrers; Gegner-Bestzeiten sprechen ihn im Wechsel persönlich an.</summary>
        internal string RadioName = "Rob";
        private struct LapEvidence
        {
            public string Identity;
            public int Completed;
            public float Best;
            public DateTime PriorImprovementUtc;
            public float PriorImprovementTime;
        }
        private readonly Dictionary<string, LapEvidence> lapEvidence = new Dictionary<string, LapEvidence>();
        private float pendingClassRecord, pendingOverallRecord;
        private DateTime classRecordUtc, overallRecordUtc;
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
        public void Reset(){NativeFieldFact f;while(incoming.TryDequeue(out f)){}pending.Clear();pitFacts.Clear();seen.Clear();ResetLapEvidence();lastRadio=DateTime.MinValue;spokenPit=null;spokenPitUtc=DateTime.MinValue;}

        private void ResetLapEvidence()
        {
            lapEvidence.Clear();
            pendingClassRecord = pendingOverallRecord = 0;
            classRecordUtc = overallRecordUtc = DateTime.MinValue;
        }
        public void CaptureMappedPits(GameStateData before,GameStateData gs)
        {
            if(before==null || gs==null || before.SessionData.SessionId!=gs.SessionData.SessionId || before.SessionData.SessionType!=gs.SessionData.SessionType || gs.SessionData.IsNewSession)return;
            var p=gs.PitData;var old=before.PitData;
            if(p.CarInFrontIsPitting && !old.CarInFrontIsPitting)Capture(NativeFieldFact.Capture("car_in_front_is_pitting",null,gs));
            if(p.CarBehindIsPitting && !old.CarBehindIsPitting)Capture(NativeFieldFact.Capture("car_behind_is_pitting",null,gs));
            if(p.LeaderIsPitting && !old.LeaderIsPitting)Capture(NativeFieldFact.Capture("leader_is_pitting",null,gs));
            if (gs.SessionData.SessionType != SessionType.Race) return;
            // Some mappers expose entry per opponent before setting a dedicated rival flag.
            foreach (var pair in gs.OpponentData)
            {
                var car = pair.Value;
                if (car == null || !car.IsActive || !car.InPits || string.IsNullOrWhiteSpace(car.DriverRawName) ||
                    gs.carClass == null || car.CarClass == null || !CarData.IsCarClassEqual(gs.carClass, car.CarClass, false) ||
                    !before.OpponentData.TryGetValue(pair.Key, out var prior) || prior == null || !prior.IsActive ||
                    prior.InPits || RaceReader.Identity(pair.Key, prior) != RaceReader.Identity(pair.Key, car)) continue;
                int rank = prior.ClassPosition;
                int ownRank = before.SessionData.ClassPosition;
                if (rank <= 0 || ownRank <= 0 || rank == ownRank || rank != 1 && Math.Abs(rank - ownRank) > 1) continue;
                Capture(NativeFieldFact.Capture("car_is_pitting", new[] { MessageFragment.Opponent(car) }, gs));
            }
        }

        public void CaptureMappedFastestLap(GameStateData before, GameStateData gs)
        {
            if (gs == null) return;
            var current = gs.SessionData;
            var previous = before?.SessionData;
            if (previous != null && current.SessionId == previous.SessionId && !current.IsNewSession &&
                current.SessionType == SessionType.Race && previous.SessionType == SessionType.Race)
            {
                bool separateClass = RaceRadioLanguage.SeparateClass(current);
                UpdateRecordWindow(previous.PlayerClassSessionBestLapTime, current.PlayerClassSessionBestLapTime, gs.Now,
                    ref pendingClassRecord, ref classRecordUtc);
                if (separateClass)
                    UpdateRecordWindow(previous.OverallSessionBestLapTime, current.OverallSessionBestLapTime, gs.Now,
                        ref pendingOverallRecord, ref overallRecordUtc);

                bool overall = separateClass && CaptureMappedRecord(before, gs, true, pendingOverallRecord, overallRecordUtc);
                if (overall) pendingOverallRecord = 0;
                if (!overall || Math.Abs(current.OverallSessionBestLapTime - current.PlayerClassSessionBestLapTime) >= .005f)
                {
                    if (CaptureMappedRecord(before, gs, false, pendingClassRecord, classRecordUtc)) pendingClassRecord = 0;
                }
                else pendingClassRecord = 0;
            }
            // RaceRoom rewrites OpponentData in place. Keep the prior scalar values rather than
            // reading the already-updated car through the previous GameState.
            if (current.IsNewSession || previous == null || previous.SessionId != current.SessionId ||
                previous.SessionType != current.SessionType) ResetLapEvidence();
            if (current.SessionType != SessionType.Race) return;

            int observed = 0;
            foreach (var pair in gs.OpponentData)
            {
                var car = pair.Value;
                if (car == null || !car.IsActive) continue;
                if (++observed > TrackAwareness.MaxCars) break;
                bool hadEvidence = lapEvidence.TryGetValue(pair.Key, out var old);
                if (hadEvidence)
                {
                    if (car.CompletedLaps == old.Completed && car.CurrentBestLapTime == old.Best && !car.IsNewLap) continue;
                }
                else if (lapEvidence.Count >= TrackAwareness.MaxCars) continue;
                string identity = RaceReader.Identity(pair.Key, car);
                bool sameDriver = hadEvidence && identity == old.Identity;
                bool improved = sameDriver && !car.InPits && CompletedLap(car, old) && car.LastLapValid &&
                    Math.Abs(car.LastLapTime - car.CurrentBestLapTime) < .005f &&
                    (!(old.Best > 10) || car.CurrentBestLapTime < old.Best - .001f);
                lapEvidence[pair.Key] = new LapEvidence { Identity = identity,
                    Completed = car.CompletedLaps, Best = car.CurrentBestLapTime,
                    PriorImprovementUtc = improved ? gs.Now : sameDriver ? old.PriorImprovementUtc : DateTime.MinValue,
                    PriorImprovementTime = improved ? car.CurrentBestLapTime : sameDriver ? old.PriorImprovementTime : 0 };
            }
        }

        private static void UpdateRecordWindow(float previous, float best, DateTime now, ref float pendingBest, ref DateTime pendingUtc)
        {
            if (best > 10 && best < 3600 && (!(previous > 10) || best < previous - .001f))
            {
                pendingBest = best;
                pendingUtc = now;
            }
            else if (now < pendingUtc || now - pendingUtc > TimeSpan.FromSeconds(2) || best != pendingBest)
                pendingBest = 0;
        }

        private static bool CompletedLap(OpponentData car, LapEvidence before)
        {
            return car.CompletedLaps == before.Completed + 1 ||
                car.IsNewLap && car.CompletedLaps == before.Completed;
        }

        private bool CaptureMappedRecord(GameStateData before, GameStateData gs, bool overall, float pendingBest, DateTime pendingUtc)
        {
            var current = gs.SessionData;
            var previous = before.SessionData;
            float best = overall ? current.OverallSessionBestLapTime : current.PlayerClassSessionBestLapTime;
            if (pendingBest <= 10 || current.SessionPhase != SessionPhase.Green || previous.SessionPhase != SessionPhase.Green ||
                !overall && gs.carClass == null || Math.Abs(best - pendingBest) >= .005f ||
                gs.Now < pendingUtc || gs.Now - pendingUtc > TimeSpan.FromSeconds(2) ||
                current.PreviousLapWasValid && current.CompletedLaps > previous.CompletedLaps &&
                    Math.Abs(current.LapTimePrevious - best) < .005f) return false;

            var candidates = gs.OpponentData.Where(pair => {
                var rival = pair.Value;
                if (rival == null || !rival.IsActive || rival.InPits || (!overall &&
                    (rival.CarClass == null || !CarData.IsCarClassEqual(gs.carClass, rival.CarClass, false))) ||
                    !(Math.Abs(rival.CurrentBestLapTime - best) < .005f)) return false;
                if (!lapEvidence.TryGetValue(pair.Key, out var old))
                {
                    if (ReferenceEquals(before.OpponentData, gs.OpponentData) ||
                        !before.OpponentData.TryGetValue(pair.Key, out var prior) || prior == null || !prior.IsActive) return false;
                    old = new LapEvidence { Identity = RaceReader.Identity(pair.Key, prior), Completed = prior.CompletedLaps, Best = prior.CurrentBestLapTime };
                }
                if (RaceReader.Identity(pair.Key, rival) != old.Identity) return false;
                if (gs.Now >= old.PriorImprovementUtc && gs.Now - old.PriorImprovementUtc <= TimeSpan.FromSeconds(2) &&
                    Math.Abs(old.PriorImprovementTime - best) < .005f) return true;
                return CompletedLap(rival, old) && rival.LastLapValid && Math.Abs(rival.LastLapTime - best) < .005f &&
                    (!(old.Best > 10) || old.Best > best + .001f);
            }).Take(2).ToArray();
            if (candidates.Length != 1) return false;
            var selected = candidates[0];
            Capture(new NativeFieldFact(overall ? "fastest-overall" : "fastest", selected.Key,
                selected.Value.CanUseName ? selected.Value.DriverRawName : null, null,
                overall ? "mapped_field_best_lap" : "mapped_class_best_lap", gs, selected.Value, best));
            return true;
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
                if (f.Kind == "fastest" && RaceRadioLanguage.SeparateClass(gs.SessionData) &&
                    Math.Abs(gs.SessionData.OverallSessionBestLapTime - f.Seconds) <= .005 &&
                    StillCurrent(f, gs) && gs.OpponentData.Values.Count(o => o != null && o.IsActive &&
                        Math.Abs(o.CurrentBestLapTime - f.Seconds) <= .005) == 1 &&
                    Math.Abs(gs.SessionData.PlayerLapTimeSessionBest - f.Seconds) > .005)
                {
                    f = new NativeFieldFact("fastest-overall", f.Key, f.Name, f.Detail, f.Source, gs,
                        gs.OpponentData[f.Key], f.Seconds);
                }
                bool fastest = f.Kind == "fastest" || f.Kind == "fastest-overall";
                if (f.Kind == "pit" && !StillCurrent(f, gs)) continue;
                string id=(fastest?"fastest":f.Kind)+"|"+(f.Identity??f.Name)+"|"+(fastest?
                    Math.Round(f.Seconds,3).ToString("0.000",System.Globalization.CultureInfo.InvariantCulture):
                    f.Lap+"|"+f.Detail+"|"+f.Seconds);
                if(seen.ContainsKey(id))continue;
                if(seen.Count>=128)seen.Remove(seen.OrderBy(p=>p.Value).First().Key);
                seen[id]=now;
                string group = f.Kind == "fastest-overall" ? "race-overall-best" : "race-class-best";
                int variant = fastest ? fastestVariants.Select(group, RadioPhraseCatalog.Current.Count(group)) : 0;
                // Rob 16.11.: Gegner-Bestzeiten klangen jedes Mal gleich. Auch ohne Rückmeldung der Wiedergabe weiterdrehen.
                if (fastest) variant = (variant + fastestAnnounced++) % RadioPhraseCatalog.Current.Count(group);
                string text = Text(f, names, variant);
                if(text==null)continue;
                if(f.Kind=="fastest")text+=" "+SessionLapObserver.GapAndPush((float)f.Seconds,gs.SessionData.PlayerLapTimeSessionBest,variant);
                if(fastest && fastestAnnounced%2==0)text=RadioPhraseCatalog.Address(text,AiPersonas.CleanForRadio(RadioName??"",30,false));
                if(memory!=null)memory.AddObservation(text,now,"field-"+f.Kind,f.Key,f.Source);
                // Existing SessionLapObserver already owns non-race class records.
                bool nonRace=SessionLapObserver.Supported(f.Type);
                bool relevant=f.Type==SessionType.Race || nonRace && f.Kind=="pit";
                if((f.Kind=="retired" || f.Kind=="dq" || f.Kind=="tyres") && (f.Position<1 || Math.Abs(f.Position-gs.SessionData.ClassPosition)>2 && f.Position!=1))relevant=false;
                if(!relevant){LiveDiagnostics.Decision("field-"+f.Kind,"stored","session_or_rival_not_relevant",text);continue;}
                var fact=f;
                if (pending.Count >= 16) Remove(pending[0]);
                var call = new ObservedRaceCall {Topic="field-"+f.Kind,Text=text,Evidence="native:"+f.Source+";opponent="+f.Key,
                    SessionId=f.Session,SessionType=f.Type,Position=-1,Member=TeamMember.Ben,Priority=fastest?4:3,
                    DetectedUtc=now,ExpiresUtc=now.AddSeconds(10),AdditionalValidity=live=>StillCurrent(fact,live)};
                pending.Add(call);
                if (f.Kind == "pit")
                {
                    pitFacts.Add(call, f);
                    call.VariantCursor = pitVariants;
                    call.FieldNamesAllowed = names;
                }
                else if (fastest)
                {
                    call.FieldNamesAllowed = names && !string.IsNullOrWhiteSpace(f.Name);
                    call.BindVariant(fastestVariants, group, variant);
                }
                LiveDiagnostics.Decision("field-"+f.Kind,"detected","native_semantic_handoff",text,call.EventId);
            }
        }
        public static bool StillCurrent(NativeFieldFact f,GameStateData gs)
        {
            if(gs==null || gs.SessionData.SessionId!=f.Session || gs.SessionData.SessionType!=f.Type)return false;
            if(f.Kind=="retired")return gs.retriedDriverNames.ContainsKey(f.Name);
            if(f.Kind=="dq")return gs.disqualifiedDriverNames.ContainsKey(f.Name);
            OpponentData op;if(f.Key==null || !gs.OpponentData.TryGetValue(f.Key,out op) || op==null || RaceReader.Identity(f.Key,op)!=f.Identity)return false;
            if(f.Kind=="pit")return op.IsActive && (op.InPits || op.isEnteringPits());
            if(f.Kind=="leader")return op.IsActive && op.ClassPosition==1 && gs.SessionData.ClassPosition>1;
            if(f.Kind=="tyres")return op.CurrentTyres.ToString()==f.Detail;
            float reference = f.Kind == "fastest-overall" ? gs.SessionData.OverallSessionBestLapTime : gs.SessionData.PlayerClassSessionBestLapTime;
            return (f.Kind=="fastest" || f.Kind=="fastest-overall") && f.Seconds>10 &&
                op.IsActive && op.CurrentBestLapTime>0 && Math.Abs(op.CurrentBestLapTime-f.Seconds)<=.005 &&
                (!(reference > 10) || Math.Abs(reference-f.Seconds)<=.005);
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
                else if(!call.IsValid(gs,now))
                {
                    LiveDiagnostics.Decision(call.Topic, "suppressed", now > call.ExpiresUtc ? "expired" :
                        call.DeliveryFailed ? "delivery_attempts_exhausted" : "field_facts_changed", call.Evidence, call.EventId);
                    Remove(call);
                }
            }
            if(!enabled || minimal){pending.Clear();pitFacts.Clear();return null;}
            if(busy || AiRadioSchedule.BusyForRoutine(gs))return null;
            foreach (var call in pending.OrderByDescending(c => c.Priority).ThenByDescending(c => c.DetectedUtc))
            {
                if (!call.CanReserve(now)) continue;
                bool record = call.Topic == "field-fastest" || call.Topic == "field-fastest-overall";
                if (now-lastRadio<TimeSpan.FromSeconds(record ? 3 : 8)) continue;
                if (pitFacts.TryGetValue(call, out var pit))
                {
                    if (combinedOwnsPit != null && (combinedOwnsPit(pit) ||
                        (pit.Source == "car_in_front_is_pitting" || pit.Position == gs.SessionData.ClassPosition - 1) &&
                        now - pit.Utc < TimeSpan.FromSeconds(1.8))) continue;
                    if (pitFacts.Keys.Any(other => other.IsReserved)) continue;
                    const string group = "pit-rival";
                    int index = pitVariants.Select(group, RadioPhraseCatalog.Current.Count(group));
                    call.Text = Text(pit, call.FieldNamesAllowed, index);
                    call.BindVariant(pitVariants, group, index);
                }
                if (call.TryReserve(now)) return call;
            }
            return null;
        }
        /// <summary>
        /// Renningenieur (Rob 17.11.): Was bedeutet der Stopp des direkten Nachbarn für Rob? Vordermann in der Box: freie Bahn,
        /// jetzt schnelle Runden. Hintermann in der Box: möglicher Undercut, Tempo hochhalten. Nur im Rennen.
        /// </summary>
        internal static string PitConsequence(string source, SessionType type, int variant)
        {
            if (type != SessionType.Race) return "";
            if (source == "car_in_front_is_pitting")
                return " " + RadioVariants.At(variant,
                    RadioLanguage.T("Jetzt hast du freie Bahn, drück ein paar schnelle Runden.", "You've got a clear track now, push for a few quick laps.", "Ahora tienes pista libre, aprieta unas vueltas rápidas."),
                    RadioLanguage.T("Freie Strecke vor dir, das ist die Zeit für schnelle Runden.", "Clear track ahead, this is the time for quick laps.", "Pista libre delante, es el momento de vueltas rápidas."),
                    RadioLanguage.T("Nutz die freie Luft, jede Zehntel zählt jetzt.", "Use the clean air, every tenth counts now.", "Aprovecha el aire limpio, cada décima cuenta ahora."),
                    RadioLanguage.T("Das ist dein Fenster: Qualirunden, solange er in der Box steht.", "This is your window: quali laps while he's in the pits.", "Esta es tu ventana: vueltas de clasificación mientras él está en boxes."));
            if (source == "car_behind_is_pitting")
                return " " + RadioVariants.At(variant,
                    RadioLanguage.T("Er versucht womöglich den Undercut, halt das Tempo hoch.", "He may be trying the undercut, keep the pace up.", "Puede que intente el undercut, mantén el ritmo."),
                    RadioLanguage.T("Achtung Undercut, die nächsten Runden zählen.", "Watch the undercut, the next laps count.", "Ojo al undercut, las próximas vueltas cuentan."),
                    RadioLanguage.T("Jetzt keine Zeit verschenken, saubere schnelle Runden.", "Don't give away any time now, clean quick laps.", "No regales tiempo ahora, vueltas rápidas y limpias."),
                    RadioLanguage.T("Mit frischen Reifen wird er schnell sein – jetzt Polster aufbauen.", "On fresh tyres he'll be quick – build a cushion now.", "Con neumáticos nuevos irá rápido: haz colchón ahora."));
            return "";
        }

        private static string Text(NativeFieldFact f,bool names,int variant)
        {
            string who=names?Name(f.Name):"";
            if(who.Length==0)who=RadioLanguage.T("Ein Gegner","A rival","Un rival");
            if (f.Kind == "pit") return RadioPhraseCatalog.Current.Format("pit-rival", variant, who) + PitConsequence(f.Source, f.Type, variant);
            if(f.Kind=="leader")return who+RadioVariants.Pick("field-leader",
                RadioLanguage.T(" führt jetzt unsere Klasse an."," now leads our class."," lidera ahora nuestra clase."),
                RadioLanguage.T(" ist neuer Klassenführender."," is the new class leader."," es el nuevo líder de la clase."),
                RadioLanguage.T(" übernimmt die Spitze in unserer Klasse."," takes the lead in our class."," toma el mando en nuestra clase."),
                RadioLanguage.T(" liegt jetzt vorn in der Klasse."," is now in front in our class."," va ahora primero en la clase."));
            if(f.Kind=="retired")return who+RadioVariants.Pick("field-retired",
                RadioLanguage.T(" ist ausgefallen."," is out of the race."," ha abandonado."),
                RadioLanguage.T(" ist raus aus dem Rennen."," has retired."," se ha retirado."),
                RadioLanguage.T(" hat aufgegeben, ein Gegner weniger."," has retired, one rival fewer."," ha abandonado, un rival menos."),
                RadioLanguage.T(" ist nicht mehr dabei."," is no longer running."," ya no sigue en carrera."));
            if(f.Kind=="dq")return who+RadioVariants.Pick("field-dq",
                RadioLanguage.T(" wurde disqualifiziert."," has been disqualified."," ha sido descalificado."),
                RadioLanguage.T(" ist disqualifiziert worden."," is disqualified."," queda descalificado."),
                RadioLanguage.T(" bekommt die Disqualifikation."," has been handed a disqualification."," recibe la descalificación."),
                RadioLanguage.T(" ist aus der Wertung genommen."," has been removed from the results."," queda fuera de la clasificación."));
            if(f.Kind=="tyres")return who+RadioVariants.Pick("field-tyres",
                RadioLanguage.T(" hat die Reifenmischung gewechselt."," has changed tyre compound."," ha cambiado de compuesto de neumáticos."),
                RadioLanguage.T(" ist jetzt auf einer anderen Mischung unterwegs."," is now on a different compound."," rueda ahora con otro compuesto."),
                RadioLanguage.T(" hat andere Reifen aufziehen lassen."," has gone for a different tyre."," ha montado otros neumáticos."),
                RadioLanguage.T(" fährt jetzt eine andere Reifenmischung."," is running a different compound now."," lleva ahora otro compuesto."));
            if(f.Kind=="fastest" && f.Seconds>10 && f.Seconds<3600)
                return RadioPhraseCatalog.Current.Format("race-class-best", variant, who, LocalAnswers.LapTime((float)f.Seconds));
            if(f.Kind=="fastest-overall" && f.Seconds>10 && f.Seconds<3600)
                return RadioPhraseCatalog.Current.Format("race-overall-best", variant, who, LocalAnswers.LapTime((float)f.Seconds));
            return null;
        }
    }
}
