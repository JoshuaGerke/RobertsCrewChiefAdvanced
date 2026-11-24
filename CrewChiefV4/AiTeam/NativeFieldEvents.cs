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
        /// <summary>Schnellste Runde eines Gegners: darf einen Zweikampf, Gelb oder die Startrunde abwarten. RaceRoom Salzburg 24.11.:
        /// Rethemeiers erste Bestzeit wartete in der Startrunde und verfiel nach 25 s (Rob: „wieder eine Bestzeit und ihr meldet sie
        /// nicht“). Sie bleibt jetzt gültig, bis jemand schneller ist.</summary>
        internal const int FastestLapSeconds = 120;
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
        private readonly Dictionary<ObservedRaceCall, NativeFieldFact> saraFacts = new Dictionary<ObservedRaceCall, NativeFieldFact>();
        /// <summary>Einzelmeldungen, die in einer Sammelansage stecken. Kommt die Sammelansage nicht an, gehen sie zurück in die Warteliste.</summary>
        private readonly Dictionary<ObservedRaceCall, List<KeyValuePair<ObservedRaceCall, NativeFieldFact>>> bundled =
            new Dictionary<ObservedRaceCall, List<KeyValuePair<ObservedRaceCall, NativeFieldFact>>>();
        /// <summary>Höchstzahl wartender Feldmeldungen.</summary>
        internal const int MaxPending = 16;
        private int bundleVariant;
        private NativeFieldFact spokenPit;
        private DateTime spokenPitUtc;

        internal bool HasClaimedPit(PitGainEvidence gain, DateTime now)
        {
            return gain != null && ((now >= spokenPitUtc && now - spokenPitUtc <= TimeSpan.FromSeconds(12) && gain.Matches(spokenPit)) ||
                pitFacts.Any(pair => gain.Matches(pair.Value) && (pair.Key.IsReserved || pair.Key.DeliveryStartedUtc != DateTime.MinValue)));
        }
        public void Capture(NativeFieldFact fact)
        {if(fact==null)return;incoming.Enqueue(fact);NativeFieldFact unused;while(incoming.Count>32)incoming.TryDequeue(out unused);}
        public void Reset(){NativeFieldFact f;while(incoming.TryDequeue(out f)){}pending.Clear();pitFacts.Clear();saraFacts.Clear();bundled.Clear();seen.Clear();departing.Clear();ResetLapEvidence();lastRadio=DateTime.MinValue;spokenPit=null;spokenPitUtc=DateTime.MinValue;}

        // AC 20.11.: „Server verlassen ohne Namen.“ Crew Chief meldet Abgänge in AC gar nicht (keine Ausfallliste im Adapter);
        // ein Gegner, der im laufenden Rennen aus den Daten verschwindet oder inaktiv wird, ist nach 3 s bestätigt weg.
        internal static readonly TimeSpan DepartureConfirm = TimeSpan.FromSeconds(3);
        private readonly Dictionary<string, KeyValuePair<DateTime, OpponentData>> departing = new Dictionary<string, KeyValuePair<DateTime, OpponentData>>();

        public void CaptureDepartures(GameStateData before, GameStateData gs, DateTime now)
        {
            if (before == null || gs == null || gs.OpponentData == null || before.OpponentData == null ||
                before.SessionData.SessionId != gs.SessionData.SessionId || before.SessionData.SessionType != gs.SessionData.SessionType ||
                gs.SessionData.IsNewSession || gs.SessionData.SessionType != SessionType.Race ||
                gs.SessionData.SessionPhase != SessionPhase.Green || gs.SessionData.LeaderHasFinishedRace)
            { departing.Clear(); return; }
            foreach (var pair in before.OpponentData)
            {
                var prior = pair.Value;
                if (prior == null || !prior.IsActive || string.IsNullOrWhiteSpace(prior.DriverRawName) || departing.ContainsKey(pair.Key)) continue;
                if (gs.OpponentData.TryGetValue(pair.Key, out var now_) && now_ != null && now_.IsActive) continue;
                if (gs.retriedDriverNames.ContainsKey(prior.DriverRawName) || gs.disqualifiedDriverNames.ContainsKey(prior.DriverRawName)) continue;
                departing[pair.Key] = new KeyValuePair<DateTime, OpponentData>(now, prior);
            }
            foreach (var pair in departing.ToArray())
            {
                if (gs.OpponentData.TryGetValue(pair.Key, out var back) && back != null && back.IsActive) { departing.Remove(pair.Key); continue; }
                if (now < pair.Value.Key || now - pair.Value.Key < DepartureConfirm) continue;
                departing.Remove(pair.Key);
                var prior = pair.Value.Value;
                Capture(new NativeFieldFact("left", pair.Key, prior.CanUseName ? prior.DriverRawName : null, null, "mapped_opponent_left", gs, prior, 0));
            }
            if (departing.Count > 64) departing.Clear();
        }

        private void ResetLapEvidence()
        {
            lapEvidence.Clear();
            fieldChecked.Clear();
            pendingClassRecord = pendingOverallRecord = 0;
            classRecordUtc = overallRecordUtc = DateTime.MinValue;
        }
        public void CaptureMappedPits(GameStateData before,GameStateData gs)
        {
            if(before==null || gs==null || before.SessionData.SessionId!=gs.SessionData.SessionId || before.SessionData.SessionType!=gs.SessionData.SessionType || gs.SessionData.IsNewSession)return;
            // Hockenheim 23.11.: Nach der Zielflagge kam „Walter Becker fährt an die Box. Jetzt hast du freie Bahn, drück ein paar
            // schnelle Runden.“ – nach Rennende ist ein Boxenbesuch kein Rennereignis mehr.
            var phase = gs.SessionData.SessionPhase;
            if (gs.SessionData.SessionType == SessionType.Race && phase != SessionPhase.Green && phase != SessionPhase.FullCourseYellow) return;
            var p=gs.PitData;var old=before.PitData;
            if(p.CarInFrontIsPitting && !old.CarInFrontIsPitting)Capture(NativeFieldFact.Capture("car_in_front_is_pitting",null,gs));
            if(p.CarBehindIsPitting && !old.CarBehindIsPitting)Capture(NativeFieldFact.Capture("car_behind_is_pitting",null,gs));
            if(p.LeaderIsPitting && !old.LeaderIsPitting)Capture(NativeFieldFact.Capture("leader_is_pitting",null,gs));
            if (gs.SessionData.SessionType != SessionType.Race) return;
            // Some mappers expose entry per opponent before setting a dedicated rival flag.
            foreach (var pair in gs.OpponentData)
            {
                var car = pair.Value;
                // Rob 23.11.: „Nicht nur in der Nähe, im gesamten Grid!“ Jede Boxeneinfahrt mit Namen; mehrere gleichzeitig bündelt Take().
                if (car == null || !car.IsActive || !car.InPits || string.IsNullOrWhiteSpace(car.DriverRawName) ||
                    !before.OpponentData.TryGetValue(pair.Key, out var prior) || prior == null || !prior.IsActive ||
                    prior.InPits || RaceReader.Identity(pair.Key, prior) != RaceReader.Identity(pair.Key, car)) continue;
                Capture(NativeFieldFact.Capture("car_is_pitting", new[] { MessageFragment.Opponent(car) }, gs));
            }
            // Rob 23.11. (RaceRoom Hockenheim): „Sara muss mir sofort sagen, wenn jemand ausscheidet, an die Box fährt oder
            // rauskommt. Die Daten haben wir mit Namen in Echtzeit.“ – „Nicht nur in der Nähe, im gesamten Grid!“ Jede
            // Boxenausfahrt mit Namen; mehrere gleichzeitig bündelt Take().
            foreach (var pair in gs.OpponentData)
            {
                var car = pair.Value;
                if (car == null || !car.IsActive || car.InPits || string.IsNullOrWhiteSpace(car.DriverRawName) || !car.CanUseName ||
                    !before.OpponentData.TryGetValue(pair.Key, out var prior) || prior == null || !prior.IsActive ||
                    !prior.InPits || RaceReader.Identity(pair.Key, prior) != RaceReader.Identity(pair.Key, car)) continue;
                Capture(new NativeFieldFact("pit-exit", pair.Key, car.DriverRawName, null, "opponent_left_pit_lane", gs, car, 0));
            }
        }

        /// <summary>Direkte Gegner: bis drei Plätze vor oder hinter Rob, oder der Führende (für Folgen wie Undercut oder freie Bahn).</summary>
        internal const int NearRivalPlaces = 3;
        internal static bool NearRival(int rank, int ownRank) =>
            rank > 0 && ownRank > 0 && rank != ownRank && (rank == 1 || Math.Abs(rank - ownRank) <= NearRivalPlaces);

        /// <summary>Feldnachrichten, die Sara sofort mit Namen sagt: Ausfall, Abgang, Disqualifikation, Box rein und raus.</summary>
        internal static bool SaraNews(string topic) =>
            topic == "field-retired" || topic == "field-left" || topic == "field-dq" || topic == "field-pit" || topic == "field-pit-exit";

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
            CaptureUnconfirmedFastestLaps(gs);
        }

        /// <summary>Gegnerrunden, die schon per Gegenprobe geprüft wurden (Schlüssel → Bestzeit).</summary>
        private readonly Dictionary<string, float> fieldChecked = new Dictionary<string, float>();

        /// <summary>
        /// Rückfallebene, wenn die Klassenbestzeit der Sim die Runde eines Gegners nicht übernimmt: Hat ein Gegner vor 2,5 bis 6 s
        /// seine Bestzeit verbessert und die Sim-Klassenbestzeit zeigt sie noch nicht, entscheidet die Gegenprobe am Feld.
        /// </summary>
        private void CaptureUnconfirmedFastestLaps(GameStateData gs)
        {
            var current = gs.SessionData;
            if (current.SessionPhase != SessionPhase.Green) return;
            foreach (var pair in lapEvidence)
            {
                var e = pair.Value;
                if (e.PriorImprovementUtc == DateTime.MinValue || gs.Now < e.PriorImprovementUtc) continue;
                var age = gs.Now - e.PriorImprovementUtc;
                if (age < TimeSpan.FromSeconds(2.5) || age > TimeSpan.FromSeconds(6)) continue;
                if (fieldChecked.TryGetValue(pair.Key, out var done) && Math.Abs(done - e.PriorImprovementTime) < .001f) continue;
                if (!gs.OpponentData.TryGetValue(pair.Key, out var car) || car == null || !car.IsActive ||
                    RaceReader.Identity(pair.Key, car) != e.Identity || Math.Abs(car.CurrentBestLapTime - e.PriorImprovementTime) >= .005f) continue;
                if (fieldChecked.Count >= TrackAwareness.MaxCars) fieldChecked.Clear();
                fieldChecked[pair.Key] = e.PriorImprovementTime;
                float mapped = current.PlayerClassSessionBestLapTime;
                if (mapped > 10 && Math.Abs(mapped - car.CurrentBestLapTime) < .005f) continue; // die Sim hat es schon übernommen
                if (!FastestInClass(gs, pair.Key, car)) continue;
                LiveDiagnostics.Decision("field-fastest", "cross_checked", "sim_class_best_lags_field", pair.Key);
                Capture(new NativeFieldFact("fastest", pair.Key, car.CanUseName ? car.DriverRawName : null, null,
                    "opponent_lap_beats_class_field", gs, car, car.CurrentBestLapTime));
            }
        }

        /// <summary>
        /// LMU Bahrain 22.11.: Krobber fuhr die schnellste Runde, die Crew schwieg – die Erkennung hing allein an der Klassenbestzeit
        /// der Sim, und die blieb auf Robs Zeit stehen. Gegenprobe direkt am Feld: Die eben beendete Runde des Gegners ist schneller
        /// als jede Bestzeit der anderen Autos seiner Klasse und als Robs eigene. Erst ab der zweiten gezeiteten Runde (der Start zählt nicht).
        /// </summary>
        internal static bool FastestInClass(GameStateData gs, string key, OpponentData car)
        {
            float best = car.CurrentBestLapTime;
            if (gs == null || car == null || !(best > 10) || best >= 3600 || car.CompletedLaps < 2 || car.InPits) return false;
            bool separate = RaceRadioLanguage.SeparateClass(gs.SessionData);
            if (separate && (gs.carClass == null || car.CarClass == null || !CarData.IsCarClassEqual(gs.carClass, car.CarClass, false))) return false;
            float own = gs.SessionData.PlayerLapTimeSessionBest;
            if (own > 10 && own <= best + .001f) return false;
            foreach (var pair in gs.OpponentData)
            {
                var other = pair.Value;
                if (pair.Key == key || other == null || !(other.CurrentBestLapTime > 10)) continue;
                if (separate && (other.CarClass == null || !CarData.IsCarClassEqual(gs.carClass, other.CarClass, false))) continue;
                if (other.CurrentBestLapTime <= best + .001f) return false;
            }
            return true;
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
                if(f.Type==SessionType.Race)Chronicle(f,gs,now);
                seen[id]=now;
                string group = f.Kind == "fastest-overall" ? "race-overall-best" : "race-class-best";
                int variant = fastest ? fastestVariants.Select(group, RadioPhraseCatalog.Current.Count(group)) : 0;
                // Rob 16.11.: Gegner-Bestzeiten klangen jedes Mal gleich. Auch ohne Rückmeldung der Wiedergabe weiterdrehen.
                if (fastest) variant = (variant + fastestAnnounced++) % RadioPhraseCatalog.Current.Count(group);
                string text = Text(f, names, variant);
                if(text==null)continue;
                if(f.Kind=="fastest")text+=" "+SessionLapObserver.GapToOwnAndPush((float)f.Seconds,gs,variant);
                if(fastest && fastestAnnounced%2==0)text=RadioPhraseCatalog.Address(text,AiPersonas.CleanForRadio(RadioName??"",30,false));
                if(memory!=null)memory.AddObservation(text,now,"field-"+f.Kind,f.Key,f.Source);
                // Existing SessionLapObserver already owns non-race class records.
                bool nonRace=SessionLapObserver.Supported(f.Type);
                bool relevant=f.Type==SessionType.Race || nonRace && f.Kind=="pit";
                // AC/RaceRoom 20.11.: Ausfälle und Abgänge immer mit Namen melden, nicht nur direkte Gegner.
                if(f.Kind=="tyres" && (f.Position<1 || Math.Abs(f.Position-gs.SessionData.ClassPosition)>2 && f.Position!=1))relevant=false;
                if(!relevant){LiveDiagnostics.Decision("field-"+f.Kind,"stored","session_or_rival_not_relevant",text);continue;}
                var fact=f;
                if (pending.Count >= MaxPending) MakeRoom(now);
                bool sara=f.Type==SessionType.Race && SaraNews("field-"+f.Kind);
                var call = new ObservedRaceCall {Topic="field-"+f.Kind,Text=text,Evidence="native:"+f.Source+";opponent="+f.Key,SemanticKey="field|"+id,
                    SessionId=f.Session,SessionType=f.Type,Position=-1,Member=sara?TeamMember.Sara:TeamMember.Ben,Priority=sara?5:fastest?4:3,
                    DetectedUtc=now,ExpiresUtc=now.AddSeconds(f.Kind=="retired" || f.Kind=="left" ? 45 : fastest ? FastestLapSeconds : sara ? 15 : 10),AdditionalValidity=PitFlagGrace.For(fact)};
                pending.Add(call);
                if (sara) saraFacts[call] = f;
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
        /// <summary>Feldereignisse für die Rennchronik (Masterplan Schritt 1): Boxenstopp, Ausfall, Serverabgang, neue Führung, Bestzeit.</summary>
        private static void Chronicle(NativeFieldFact f,GameStateData gs,DateTime now)
        {
            ChronicleKind kind;
            switch(f.Kind)
            {
                case "pit": kind=ChronicleKind.RivalPit; break;
                case "retired": kind=ChronicleKind.Retired; break;
                case "dq": kind=ChronicleKind.Retired; break;
                case "left": kind=ChronicleKind.Left; break;
                case "leader": kind=ChronicleKind.LeaderChange; break;
                case "fastest": case "fastest-overall": kind=ChronicleKind.FieldBest; break;
                default: return;
            }
            OpponentData car=null;
            if(f.Key!=null && gs.OpponentData!=null)gs.OpponentData.TryGetValue(f.Key,out car);
            string name=car!=null?(car.CanUseName?RadioPhraseCatalog.SafeRivalName(car.DriverRawName):null):
                string.IsNullOrWhiteSpace(f.Name)?null:RadioPhraseCatalog.SafeRivalName(f.Name);
            RaceChronicle.Current.Add(gs,now,kind,car,f.Key,f.Kind=="dq"?"disqualifiziert":f.Kind=="fastest-overall"?"gesamt":f.Kind=="fastest"?"klasse":null,
                f.Source,DataOrigin.SimulatorReportedEvent,f.Seconds>0?f.Seconds:double.NaN,subjectName:name,dedupe:TimeSpan.FromSeconds(30));
        }

        public static bool StillCurrent(NativeFieldFact f,GameStateData gs)
        {
            if(gs==null || gs.SessionData.SessionId!=f.Session || gs.SessionData.SessionType!=f.Type)return false;
            if(f.Kind=="retired")return gs.retriedDriverNames.ContainsKey(f.Name);
            if(f.Kind=="left")return f.Key!=null && (!gs.OpponentData.TryGetValue(f.Key,out var gone) || gone==null || !gone.IsActive);
            if(f.Kind=="dq")return gs.disqualifiedDriverNames.ContainsKey(f.Name);
            OpponentData op;if(f.Key==null || !gs.OpponentData.TryGetValue(f.Key,out op) || op==null || RaceReader.Identity(f.Key,op)!=f.Identity)return false;
            if(f.Kind=="pit")return op.IsActive && (op.InPits || op.isEnteringPits());
            if(f.Kind=="pit-exit")return op.IsActive && !op.InPits;
            if(f.Kind=="leader")return op.IsActive && op.ClassPosition==1 && gs.SessionData.ClassPosition>1;
            if(f.Kind=="tyres")return op.CurrentTyres.ToString()==f.Detail;
            float reference = f.Kind == "fastest-overall" ? gs.SessionData.OverallSessionBestLapTime : gs.SessionData.PlayerClassSessionBestLapTime;
            // LMU Bahrain 22.11.: Die Klassenbestzeit der Sim kann hinter der Runde des Gegners herhinken. Ungültig wird die Meldung
            // nur, wenn jemand schneller war – nicht, weil der Sim-Wert noch die alte Zeit zeigt.
            // RaceRoom Hockenheim 23.11.: juan ayusos 1:43,935 aus dem Fahrerdatensatz, die Klassenbestzeit der Sim zeigte 1:43,925 –
            // dieselbe Runde, nur anders gerundet. Die Meldung verfiel im selben Moment als „Fakten geändert“. Bis 0,05 s gilt als dieselbe Runde.
            float own = gs.SessionData.PlayerLapTimeSessionBest;
            return (f.Kind=="fastest" || f.Kind=="fastest-overall") && f.Seconds>10 &&
                op.IsActive && op.CurrentBestLapTime>0 && Math.Abs(op.CurrentBestLapTime-f.Seconds)<=.005 &&
                (!(reference > 10) || reference > f.Seconds-SameLapTolerance) && (!(own > 10) || own > f.Seconds-.005);
        }
        /// <summary>
        /// LMU Sebring 23.11.: „Arod Spearhead fährt an die Box“ verfiel als „Fakten geändert“, weil die Sim das Box-Kennzeichen
        /// für einen Augenblick verlor. Ein Aussetzer bis 2 s Spielzeit hält die Meldung; ist das Auto länger draußen, ist sie
        /// überholt und entfällt wie bisher.
        /// </summary>
        internal sealed class PitFlagGrace
        {
            internal static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);
            private readonly NativeFieldFact fact;
            private DateTime outSince;
            private bool out_;
            private PitFlagGrace(NativeFieldFact fact) { this.fact = fact; }

            internal static Func<GameStateData, bool> For(NativeFieldFact fact)
            {
                if (fact.Kind != "pit") return live => StillCurrent(fact, live);
                return new PitFlagGrace(fact).Valid;
            }

            internal bool Valid(GameStateData live)
            {
                if (StillCurrent(fact, live)) { out_ = false; return true; }
                if (live == null || live.SessionData.SessionId != fact.Session || fact.Key == null ||
                    !live.OpponentData.TryGetValue(fact.Key, out var op) || op == null || !op.IsActive ||
                    RaceReader.Identity(fact.Key, op) != fact.Identity) return false;
                if (!out_) { out_ = true; outSince = live.Now; }
                return live.Now - outSince <= Grace;
            }
        }

        private void Remove(ObservedRaceCall call)
        {
            if (pitFacts.TryGetValue(call, out var fact) && call.DeliveryStartedUtc > spokenPitUtc)
            {
                spokenPit = fact;
                spokenPitUtc = call.DeliveryStartedUtc;
            }
            pitFacts.Remove(call);
            saraFacts.Remove(call);
            bundled.Remove(call);
            pending.Remove(call);
        }

        /// <summary>
        /// Review 24.11.: Bei voller Warteliste fiel bisher der älteste Eintrag ohne Logeintrag weg, auch ein Ausfall. Jetzt geht die
        /// unwichtigste Meldung (niedrigster Vorrang, dann die älteste), nie eine, die gerade gesprochen wird, und mit Grund im Log.
        /// </summary>
        private void MakeRoom(DateTime now)
        {
            var victim = pending.Where(c => !c.IsReserved && c.DeliveryStartedUtc == DateTime.MinValue)
                .OrderBy(c => c.Priority).ThenBy(c => c.DetectedUtc).FirstOrDefault();
            if (victim == null) return;
            LiveDiagnostics.Decision(victim.Topic, "suppressed", "queue_full", victim.Text, victim.EventId);
            if (bundled.TryGetValue(victim, out var members))
                foreach (var m in members) LiveDiagnostics.Decision(m.Key.Topic, "suppressed", "queue_full_in_bundle:" + victim.EventId, m.Key.Text, m.Key.EventId);
            Remove(victim);
        }

        /// <summary>
        /// Review 24.11.: Kam eine Sammelansage nicht an, waren alle Namen darin verloren. Ihre Einzelmeldungen, die noch gelten,
        /// gehen zurück in die Warteliste; die übrigen werden mit Grund verworfen.
        /// </summary>
        private void Unbundle(ObservedRaceCall bundle, GameStateData gs, DateTime now)
        {
            if (!bundled.TryGetValue(bundle, out var members)) return;
            bundled.Remove(bundle);
            foreach (var m in members)
            {
                if (pending.Contains(m.Key)) continue;
                if (!m.Key.IsValid(gs, now))
                {
                    LiveDiagnostics.Decision(m.Key.Topic, "suppressed", "expired_with_bundle:" + bundle.EventId, m.Key.Text, m.Key.EventId);
                    continue;
                }
                pending.Add(m.Key);
                saraFacts[m.Key] = m.Value;
                if (m.Value.Kind == "pit") pitFacts[m.Key] = m.Value;
                LiveDiagnostics.Decision(m.Key.Topic, "requeued", "bundle_not_delivered:" + bundle.EventId, m.Key.Text, m.Key.EventId);
            }
        }

        /// <summary>Für Tests: wartende Meldungen und die Einzelmeldungen in Sammelansagen.</summary>
        internal int PendingCount => pending.Count;
        internal int BundledCount => bundled.Values.Sum(l => l.Count);

        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,bool enabled,bool minimal,
            Func<NativeFieldFact, bool> combinedOwnsPit = null)
        {
            foreach(var call in pending.ToArray())
            {
                if(call.DeliveryStartedUtc!=DateTime.MinValue)
                {
                    lastRadio=call.DeliveryStartedUtc;
                    if (bundled.TryGetValue(call, out var spoken))
                        foreach (var m in spoken)
                            LiveDiagnostics.Decision(m.Key.Topic, call.ConsumedOnly ? "suppressed" : "delivered",
                                (call.ConsumedOnly ? "consumed_with_bundle:" : "in_bundle:") + call.EventId, m.Key.Text, m.Key.EventId);
                    Remove(call);
                }
                else if(!call.IsValid(gs,now))
                {
                    LiveDiagnostics.Decision(call.Topic, "suppressed", now > call.ExpiresUtc ? "expired" :
                        call.DeliveryFailed ? "delivery_attempts_exhausted" : "field_facts_changed", call.Evidence, call.EventId);
                    Unbundle(call, gs, now);
                    Remove(call);
                }
            }
            if(!enabled || minimal){pending.Clear();pitFacts.Clear();saraFacts.Clear();bundled.Clear();return null;}
            if(busy || AiRadioSchedule.BusyForRoutine(gs))return null;
            foreach (var call in pending.OrderByDescending(c => c.Priority).ThenByDescending(c => c.DetectedUtc))
            {
                if (!call.CanReserve(now)) continue;
                bool record = call.Topic == "field-fastest" || call.Topic == "field-fastest-overall";
                if (now-lastRadio<TimeSpan.FromSeconds(record || SaraNews(call.Topic) ? 3 : 8)) continue;
                if (TryBundle(call, gs, now)) { if (call.TryReserve(now)) return call; continue; }
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
        /// <summary>Gleichzeitige Sara-Nachrichten derselben Art (Box rein, Box raus, Ausfall/Abgang) innerhalb dieses Fensters werden ein Satz.</summary>
        /// <summary>Gleiche Runde in zwei Datenquellen der Sim, nur unterschiedlich gerundet.</summary>
        internal const float SameLapTolerance = .05f;

        internal static readonly TimeSpan BundleWindow = TimeSpan.FromSeconds(12);

        private static string Family(string kind) => kind == "pit" ? "pit" : kind == "pit-exit" ? "pit-exit" :
            kind == "retired" || kind == "left" || kind == "dq" ? "out" : null;

        /// <summary>
        /// Rob 23.11.: Boxen und Ausfälle im ganzen Grid, aber verständlich. Warten mehrere Nachrichten derselben Art, sagt Sara
        /// sie in einem Satz mit allen Namen („Box: A, B und C fahren rein.“); die übrigen entfallen als gebündelt.
        /// </summary>
        private bool TryBundle(ObservedRaceCall call, GameStateData gs, DateTime now)
        {
            if (!saraFacts.TryGetValue(call, out var fact) || Family(fact.Kind) == null) return false;
            string family = Family(fact.Kind);
            var group = pending.Where(c => c != call && c.DeliveryStartedUtc == DateTime.MinValue && !c.IsReserved && c.CanReserve(now) &&
                    saraFacts.TryGetValue(c, out var f) && Family(f.Kind) == family && Math.Abs((c.DetectedUtc - call.DetectedUtc).TotalSeconds) <= BundleWindow.TotalSeconds)
                .ToList();
            if (group.Count == 0) return false;
            // Schon gebündelte Namen (auch aus einer wartenden Sammelansage in der Gruppe) bleiben im Satz.
            var members = new List<KeyValuePair<ObservedRaceCall, NativeFieldFact>>();
            if (bundled.TryGetValue(call, out var earlier)) members.AddRange(earlier);
            foreach (var c in group)
            {
                members.Add(new KeyValuePair<ObservedRaceCall, NativeFieldFact>(c, saraFacts[c]));
                if (bundled.TryGetValue(c, out var inner)) members.AddRange(inner);
            }
            var names = new[] { fact }.Concat(members.Select(m => m.Value)).Select(f => call.FieldNamesAllowed || fact.Kind != "pit" ? Name(f.Name) : "")
                .Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count < 2) return false;
            call.Text = BundleText(family, names, bundleVariant++);
            call.Evidence += ";bundled=" + (members.Count + 1);
            pitFacts.Remove(call);
            foreach (var other in group)
            {
                LiveDiagnostics.Decision(other.Topic, "suppressed", "bundled_into:" + call.EventId, other.Text, other.EventId);
                Remove(other);
            }
            bundled[call] = members;
            LiveDiagnostics.Decision(call.Topic, "bundled", "sara_same_kind_within_" + (int)BundleWindow.TotalSeconds + "s", call.Text, call.EventId);
            return true;
        }

        internal static string BundleText(string family, IList<string> names, int variant)
        {
            string list = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + RadioLanguage.T(" und ", " and ", " y ") + names[names.Count - 1];
            if (family == "pit")
                return RadioVariants.At(variant,
                    RadioLanguage.T("Box: " + list + " fahren rein.", "Pit lane: " + list + " are coming in.", "Boxes: entran " + list + "."),
                    RadioLanguage.T(list + " gehen gerade an die Box.", list + " are pitting now.", list + " entran ahora a boxes."),
                    RadioLanguage.T("Boxenstopps: " + list + ".", "Pit stops: " + list + ".", "Paradas: " + list + "."),
                    RadioLanguage.T("In der Boxengasse jetzt " + list + ".", "In the pit lane now: " + list + ".", "Ahora en el pit lane: " + list + "."));
            if (family == "pit-exit")
                return RadioVariants.At(variant,
                    RadioLanguage.T(list + " kommen aus der Box.", list + " are out of the pits.", list + " salen de boxes."),
                    RadioLanguage.T("Boxenausfahrt: " + list + ".", "Pit exit: " + list + ".", "Salida de boxes: " + list + "."),
                    RadioLanguage.T("Wieder draußen sind " + list + ".", "Back out on track: " + list + ".", "De vuelta en pista: " + list + "."),
                    RadioLanguage.T(list + " haben ihren Stopp erledigt.", list + " have done their stops.", list + " han hecho su parada."));
            return RadioVariants.At(variant,
                RadioLanguage.T("Raus sind " + list + ".", "Out of the race: " + list + ".", "Fuera de carrera: " + list + "."),
                RadioLanguage.T(list + " sind nicht mehr dabei.", list + " are no longer running.", list + " ya no siguen."),
                RadioLanguage.T("Ausfälle: " + list + ".", "Retirements: " + list + ".", "Abandonos: " + list + "."),
                RadioLanguage.T("Nicht mehr im Rennen: " + list + ".", "No longer in the race: " + list + ".", "Ya no están en carrera: " + list + "."));
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
            if(f.Kind=="pit-exit")return who+RadioVariants.Pick("field-pit-exit",
                RadioLanguage.T(" kommt aus der Box."," is out of the pits."," sale de boxes."),
                RadioLanguage.T(" ist wieder draußen, Boxenausfahrt."," is back out, pit exit."," vuelve a pista, salida de boxes."),
                RadioLanguage.T(" fährt gerade aus der Box raus."," is just leaving the pit lane."," está saliendo del pit lane."),
                RadioLanguage.T(" hat seinen Stopp erledigt und ist zurück auf der Strecke."," has done his stop and is back on track."," ha hecho su parada y vuelve a la pista."),
                RadioLanguage.T(" ist aus der Box zurück, behalt ihn im Blick."," is back from the pits, keep an eye on him."," vuelve de boxes, no le pierdas de vista."));
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
            if(f.Kind=="left")return who+RadioVariants.Pick("field-left",
                RadioLanguage.T(" hat den Server verlassen."," has left the server."," ha abandonado el servidor."),
                RadioLanguage.T(" ist nicht mehr auf dem Server."," is no longer on the server."," ya no está en el servidor."),
                RadioLanguage.T(" hat das Rennen verlassen, ein Auto weniger auf der Strecke."," has left the race, one car fewer on track."," ha dejado la carrera, un coche menos en pista."),
                RadioLanguage.T(" ist vom Server gegangen."," has dropped off the server."," se ha ido del servidor."),
                RadioLanguage.T(" ist raus, er hat den Server verlassen."," is out, he's left the server."," está fuera, ha dejado el servidor."));
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
