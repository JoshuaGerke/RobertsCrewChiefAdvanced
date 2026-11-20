using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    // A bounded, non-blocking hand-off BEFORE native voice suppression. No audio/network work here.
    public static class NativeTrafficBridge
    {
        public sealed class Detection
        {
            public int SessionId;
            public SessionType Type;
            public DateTime Utc;
            public bool Behind;
            public string[] Names;
        }
        private static readonly ConcurrentQueue<Detection> queue = new ConcurrentQueue<Detection>();
        public static volatile bool Enabled;
        public static void Publish(GameStateData gs, IEnumerable<string> names, bool behind)
        {
            if (!Enabled || gs == null || gs.SessionData.SessionType != SessionType.Race || names == null) return;
            var copy = names.Where(n=>!string.IsNullOrWhiteSpace(n) && n.Length<=128).Take(16).ToArray();
            if (copy.Length == 0) return;
            queue.Enqueue(new Detection { SessionId=gs.SessionData.SessionId, Type=gs.SessionData.SessionType,
                Utc=DateTime.UtcNow, Behind=behind, Names=copy });
            while (queue.Count > 32) { Detection unused; queue.TryDequeue(out unused); }
        }
        public static bool TryTake(out Detection detection) { return queue.TryDequeue(out detection); }
        public static void Clear() { Detection unused; while (queue.TryDequeue(out unused)) { } }
    }

    /// <summary>Local race traffic, independent of LLMs and optional reporting timers.
    /// Own previously observed speed at the SAME route section distinguishes slow cars from normal corner braking.
    /// Side/clear calls remain exclusively with the existing Sara spotter.</summary>
    public sealed class RaceTrafficWatch
    {
        private static readonly string[] StoppedPhrasesDe = {
            "Vorsicht, stehendes Fahrzeug voraus.", "Achtung, vor dir steht ein Wagen.", "Stehender Wagen voraus, bleib aufmerksam.",
            "Wagen steht auf der Strecke voraus. Weite Linie, Tempo raus.",
            "Vorsicht, liegengebliebenes Auto voraus.",
            "Achtung, da vorne steht einer. Rechtzeitig ausweichen."
        };
        private static readonly string[] StoppedPhrasesEn = {
            "Careful, stopped car ahead.", "Watch out, there's a car stopped ahead of you.", "Stationary car ahead, stay alert.",
            "Car stopped on track ahead. Go wide, ease off.",
            "Careful, stranded car ahead.",
            "Heads up, someone's stopped ahead. Move over in time."
        };
        private static readonly string[] StoppedPhrasesEs = {
            "Cuidado, vehículo parado delante.", "Atención, hay un coche parado delante de ti.", "Coche detenido delante, mantente atento.",
            "Coche parado en pista delante. Abre la trazada y afloja.",
            "Cuidado, coche averiado delante.",
            "Atención, hay uno parado delante. Esquiva a tiempo."
        };
        public static string[] StoppedPhrases => RadioLanguage.Pick(StoppedPhrasesDe, StoppedPhrasesEn, StoppedPhrasesEs);
        // Rennen 20.11. (Sebring): „Vor mir hat sich einer gedreht und ist stehen geblieben. Es kam gar nichts.“ Der Wagen fiel
        // in 4 s von 66 auf 13 m/s, 30–80 m vor Rob; die Prüfung „deutlich langsamer als hier üblich“ griff erst, als er schon vorbei war.
        private static readonly string[] SpinPhrasesDe = {
            "Achtung, Dreher vor dir!", "Vorsicht, vorne dreht sich einer!", "Achtung, das Auto vor dir hat sich gedreht!",
            "Dreher voraus, Augen auf!", "Vorsicht, da vorne ist einer abgeflogen!"
        };
        private static readonly string[] SpinPhrasesEn = {
            "Careful, spin ahead!", "Watch out, car spinning in front!", "Heads up, the car ahead has spun!",
            "Spin ahead, eyes up!", "Careful, someone's gone off ahead!"
        };
        private static readonly string[] SpinPhrasesEs = {
            "¡Cuidado, trompo delante!", "¡Atención, un coche está trompeando delante!", "¡Ojo, el coche de delante ha hecho un trompo!",
            "¡Trompo delante, atento!", "¡Cuidado, alguien se ha salido delante!"
        };
        public static string[] SpinPhrases => RadioLanguage.Pick(SpinPhrasesDe, SpinPhrasesEn, SpinPhrasesEs);

        /// <summary>
        /// Plötzlicher Einbruch: in den letzten 3 s um mindestens 20 m/s auf höchstens 60 % gefallen und deutlich langsamer als
        /// Robs eigene Fahrt an dieser Stelle (ohne Vergleich: halb so schnell wie Rob). Normales Anbremsen fällt so nicht auf,
        /// weil Rob an derselben Stelle ähnlich langsam ist.
        /// </summary>
        internal static bool SuddenSlowdown(IList<KeyValuePair<DateTime, float>> history, float speed, DateTime now, float? normalHere, float playerSpeed)
        {
            if (history == null || speed < 0 || speed > 30) return false;
            float peak = 0;
            foreach (var h in history)
                if (now - h.Key <= TimeSpan.FromSeconds(3) && now - h.Key >= TimeSpan.FromMilliseconds(300) && h.Value > peak) peak = h.Value;
            if (peak - speed < 20 || speed > peak * .6f) return false;
            return normalHere.HasValue && normalHere.Value >= 15 ? speed < normalHere.Value * .7f : speed < playerSpeed * .5f;
        }

        private readonly Dictionary<string, List<KeyValuePair<DateTime, float>>> motion = new Dictionary<string, List<KeyValuePair<DateTime, float>>>();
        private readonly HashSet<string> spinning = new HashSet<string>();

        private void RecordMotion(TrackPositionSnapshot s, DateTime now)
        {
            foreach (var c in s.Cars)
            {
                if (c.Key == null || !c.SpeedMps.HasValue) continue;
                if (!motion.TryGetValue(c.Key, out var list)) motion[c.Key] = list = new List<KeyValuePair<DateTime, float>>();
                if (list.Count > 0 && now < list[list.Count - 1].Key) list.Clear();
                list.Add(new KeyValuePair<DateTime, float>(now, (float)c.SpeedMps.Value));
                list.RemoveAll(p => now - p.Key > TimeSpan.FromSeconds(4));
            }
            if (motion.Count > 256) motion.Clear();
        }

        private float? NormalSpeedAt(TrackCarPosition c, GameStateData gs, DateTime now)
        {
            Reference normal;
            return c.LapDistanceM.HasValue && reference.TryGetValue((int)(c.LapDistanceM.Value / 100), out normal) &&
                normal.Lap < gs.SessionData.CompletedLaps && now >= normal.Utc && now - normal.Utc < TimeSpan.FromMinutes(10) ? normal.Speed : (float?)null;
        }
        private static readonly string[] SlowPhrasesDe = {
            "Achtung, sehr langsames Fahrzeug voraus.", "Vor dir fährt ein Wagen deutlich langsamer. Bleib aufmerksam.",
            "Vorsicht, langsamer Verkehr direkt voraus.", "Pass auf, der Wagen vor dir ist sehr langsam.",
            "Achtung voraus, du läufst auf einen sehr langsamen Wagen auf."
        };
        private static readonly string[] SlowPhrasesEn = {
            "Watch out, very slow car ahead.", "Car ahead is going much slower. Stay alert.",
            "Careful, slow traffic right ahead.",
            "Watch out, the car ahead is very slow.",
            "Heads up ahead, you're closing on a very slow car."
        };
        private static readonly string[] SlowPhrasesEs = {
            "Atención, vehículo muy lento delante.", "Delante va un coche mucho más lento. Mantente atento.",
            "Cuidado, tráfico lento justo delante.",
            "Ojo, el coche de delante va muy lento.",
            "Atención delante, te acercas a un coche muy lento."
        };
        public static string[] SlowPhrases => RadioLanguage.Pick(SlowPhrasesDe, SlowPhrasesEn, SlowPhrasesEs);
        private static readonly string[] RearPhrasesDe = {
            "Schnelleres Fahrzeug von hinten. Bleib berechenbar.", "Von hinten kommt schneller Verkehr. Behalte deine Linie bei.",
            "Ein schnellerer Wagen läuft von hinten auf. Bleib berechenbar.",
            "Schneller Verkehr kommt näher. Bleib auf einer berechenbaren Linie.",
            "Achte auf den Spiegel, ein schnellerer Wagen kommt von hinten.",
            "Von hinten kommt einer mit mehr Tempo. Ruhig auf deiner Linie bleiben."
        };
        private static readonly string[] RearPhrasesEn = {
            "Faster car from behind. Stay predictable.", "Fast traffic coming from behind. Hold your line.",
            "A faster car is closing from behind. Stay predictable.",
            "Fast traffic closing. Keep your line steady.",
            "Check your mirrors, quicker car coming from behind.",
            "Someone with more pace is coming. Stay calm on your line."
        };
        private static readonly string[] RearPhrasesEs = {
            "Coche más rápido por detrás. Sé predecible.", "Llega tráfico rápido por detrás. Mantén tu trazada.",
            "Un coche más rápido se acerca por detrás. Sé predecible.",
            "Tráfico rápido acercándose. Mantén una trazada predecible.",
            "Mira los espejos, viene un coche más rápido por detrás.",
            "Viene alguien con más ritmo. Tranquilo en tu trazada."
        };
        public static string[] RearPhrases => RadioLanguage.Pick(RearPhrasesDe, RearPhrasesEn, RearPhrasesEs);
        private static readonly string[] RearGroupPhrasesDe = {
            "Mehrere schnellere Fahrzeuge kommen von hinten. Bleib berechenbar.",
            "Schnellere Gruppe von hinten. Behalte deine Linie bei.",
            "Da kommt ein ganzer Pulk von hinten. Linie halten, sie suchen sich ihren Weg.",
            "Mehrere schnelle Autos im Spiegel. Berechenbar bleiben, nicht ausweichen.",
            "Schnellere Klasse kommt in der Gruppe. Ruhig weiterfahren, die gehen vorbei."
        };
        private static readonly string[] RearGroupPhrasesEn = {
            "Several faster cars coming from behind. Stay predictable.",
            "Faster group from behind. Hold your line.",
            "A whole bunch coming from behind. Hold your line, they'll find a way.",
            "Several quick cars in the mirrors. Stay predictable, don't dodge.",
            "Faster class arriving as a group. Keep driving, they'll go by."
        };
        private static readonly string[] RearGroupPhrasesEs = {
            "Llegan varios coches más rápidos por detrás. Sé predecible.",
            "Grupo más rápido por detrás. Mantén tu trazada.",
            "Llega un grupo entero por detrás. Mantén la trazada, ellos buscarán hueco.",
            "Varios coches rápidos en los espejos. Sé predecible, no te apartes.",
            "La clase más rápida llega en grupo. Sigue rodando, ya pasarán."
        };
        public static string[] RearGroupPhrases => RadioLanguage.Pick(RearGroupPhrasesDe, RearGroupPhrasesEn, RearGroupPhrasesEs);
        public static string DistancePhrase(int metres,bool stopped) => RadioLanguage.T(
            "Vorsicht, in etwa "+metres+" Metern "+(stopped?"steht ein Fahrzeug.":"ein sehr langsames Fahrzeug."),
            "Careful, "+(stopped?"stopped car":"very slow car")+" in about "+metres+" metres.",
            "Cuidado, a unos "+metres+" metros hay un coche "+(stopped?"parado.":"muy lento."));
        internal static string SectionPhrase(string section) => RadioLanguage.T(
            "Achtung, Gefahr voraus. Abschnitt " + section + ".",
            "Careful, hazard ahead. Section " + section + ".",
            "Cuidado, peligro delante. Tramo " + section + ".");

        internal static IEnumerable<string> SectionPhrases(TrackDefinition track, double startingDistance)
        {
            if (track?.trackLandmarks == null || track.trackLength < 100 || !RaceFactContext.Finite(track.trackLength) ||
                !TrackAwareness.ValidDistance(startingDistance, track.trackLength).HasValue) return Enumerable.Empty<string>();
            return track.trackLandmarks.Where(landmark => !landmark.isStraight && !string.IsNullOrWhiteSpace(landmark.landmarkNames?.FirstOrDefault()) &&
                    TrackAwareness.ValidDistance(landmark.distanceRoundLapStart, track.trackLength).HasValue)
                .OrderBy(landmark => (landmark.distanceRoundLapStart - startingDistance + track.trackLength) % track.trackLength)
                .Select(landmark => SectionPhrase(CornerCoach.FriendlyName(landmark.landmarkNames[0], 0)))
                .Distinct().Take(8);
        }

        public static string[] DistancePhrases => Enumerable.Range(1,12).SelectMany(i=>new[]{DistancePhrase(i*20,true),DistancePhrase(i*20,false)}).ToArray();
        public static IEnumerable<string> PreparedPhrases { get { return DistancePhrases.Concat(StoppedPhrases).Concat(SpinPhrases).Concat(SlowPhrases).Concat(RearPhrases).Concat(RearGroupPhrases).Concat(StillTherePhrases); } }
        internal static string ForecastMerge(RaceDataContext context, TrackPositionSnapshot map, double exit, double horizon, out int covered)
        {
            covered = 0;
            var state = context.GameState;
            double length = map.LengthM.GetValueOrDefault();
            if (!TrackAwareness.ValidDistance(exit, length).HasValue || !TrackAwareness.InRange(horizon, 0, 100).HasValue) return "unknown";
            int expected = 0;
            bool close = false, marginal = false, unresolvedPitTraffic = false;
            double allowance = 3 + horizon * .18;
            foreach (var car in map.Cars)
            {
                if (car.InPits) { unresolvedPitTraffic = true; continue; }
                expected++;
                if (!car.SpatiallyConsistent || !car.LapDistanceM.HasValue || car.OpponentSampleUtc == DateTime.MinValue ||
                    context.NowUtc < car.OpponentSampleUtc || context.NowUtc-car.OpponentSampleUtc > TimeSpan.FromSeconds(1) ||
                    !state.OpponentData.TryGetValue(car.Key, out var opponent) || opponent == null ||
                    RaceReader.Identity("", opponent) != car.NativeIdentity)
                    continue;
                var laps = opponent.OpponentLapData.Where(l => l.IsValid && !l.InLap && !l.OutLap &&
                    TrackAwareness.InRange(l.LapTime, 15, 3600).HasValue).Reverse().Take(2).ToArray();
                if (laps.Length != 2 || Math.Abs(laps[0].LapTime-laps[1].LapTime) > 5 || car.SpeedMps.GetValueOrDefault() < 10)
                    continue;
                double period = laps.Average(l => l.LapTime);
                var speed = TrackAwareness.InRange(length / period, 10, 120);
                if (!speed.HasValue || horizon > period) continue;
                covered++;
                double distance = (exit-car.LapDistanceM.Value+length)%length;
                double untilMerge = distance / speed.Value;
                double closest = Math.Min(Math.Abs(untilMerge-horizon), Math.Abs(untilMerge+period-horizon));
                close |= closest <= allowance;
                marginal |= closest <= allowance + 4;
            }
            // Incomplete coverage may confirm a traffic risk, but can never certify an empty gap.
            if (close) return "traffic";
            return expected > 0 && covered == expected && !marginal && !unresolvedPitTraffic &&
                state.SessionData.NumCarsOverall > 1 && map.Cars.Count >= state.SessionData.NumCarsOverall - 1 ? "clear" : "unknown";
        }

        private sealed class Reference { public float Speed; public int Lap; public DateTime Utc; }
        private sealed class Facts { public DateTime Utc; public int Epoch, Session; public TrackCarPosition[] Cars; }
        private Facts facts;
        private readonly Dictionary<int,Reference> reference = new Dictionary<int,Reference>();
        private readonly Dictionary<string,DateTime> reported = new Dictionary<string,DateTime>();
        private readonly Dictionary<string,DateTime> nativeRear = new Dictionary<string,DateTime>();
        // Rob 16.11.: Ein Auto, das dauerhaft an derselben Stelle steht, wird einmal gemeldet, dann einmal kurz erinnert, dann nicht mehr.
        private sealed class Parked { public int Cell; public int Reports; }
        private readonly Dictionary<string,Parked> parked = new Dictionary<string,Parked>();
        private static readonly string[] StillThereDe = { "Der stehende Wagen ist noch an derselben Stelle.", "Das Auto steht immer noch da, gleiche Stelle.",
            "Der Wagen steht weiter dort. Gleiche Stelle wie eben.",
            "Unverändert: Das Auto steht noch auf der Strecke.",
            "Denk dran, der stehende Wagen ist noch da."
        };
        private static readonly string[] StillThereEn = { "The stopped car is still in the same place.", "That car is still stopped, same spot.",
            "That car is still there. Same place as before.",
            "Unchanged: the car is still on track.",
            "Remember, the stopped car is still there."
        };
        private static readonly string[] StillThereEs = { "El coche parado sigue en el mismo sitio.", "Ese coche sigue parado, mismo lugar.",
            "Ese coche sigue ahí. En el mismo sitio.",
            "Sin cambios: el coche sigue en pista.",
            "Recuerda, el coche parado sigue ahí."
        };
        public static string[] StillTherePhrases => RadioLanguage.Pick(StillThereDe, StillThereEn, StillThereEs);
        private TrackAwareness tracks;
        private ObservedRaceCall pending;
        private string pendingKey, track;
        private int session=int.MinValue, variant, epoch;
        private DateTime lastTick, lastRadio;
        public void Reset()
        {
            reference.Clear(); reported.Clear(); nativeRear.Clear(); parked.Clear(); motion.Clear(); spinning.Clear(); pending=null; tracks=null;
            session=int.MinValue; track=null; lastTick=lastRadio=lastRear=lastRearGroup=DateTime.MinValue; epoch++;
            Volatile.Write(ref facts,null);
        }
        internal const double RearSameCarSeconds = 300, RearAnySeconds = 45;
        private DateTime lastRear = DateTime.MinValue, lastRearGroup = DateTime.MinValue;
        private bool pendingGroup, pendingOtherClass;

        public void Observe(TrackAwareness observer, GameStateData gs, DateTime now)
        {
            if (gs == null || observer == null || observer.Snapshot == null) return;
            var s=observer.Snapshot;
            if (session!=s.SessionId || track!=s.Track || now<lastTick ||
                lastTick!=DateTime.MinValue && now-lastTick>TimeSpan.FromSeconds(2))
            { Reset(); session=s.SessionId; track=s.Track; }
            tracks=observer; lastTick=now;
            if (pending!=null && pending.DeliveryStartedUtc!=DateTime.MinValue)
            {
                reported[pendingKey]=lastRadio=pending.DeliveryStartedUtc;
                if (pending.Topic=="race-traffic-rear") { lastRear=pending.DeliveryStartedUtc; if (pendingGroup) lastRearGroup=lastRear; }
                if (pendingOtherClass && pending.Topic=="race-traffic-rear") MulticlassTraffic.LastRearClassCallUtc=pending.DeliveryStartedUtc;
                pending=null;
            }
            if (pending!=null && !pending.IsValid(gs,now)) pending=null;
            NativeTrafficBridge.Detection d;
            while (NativeTrafficBridge.TryTake(out d))
                if (d.SessionId==s.SessionId && d.Type==s.SessionType && d.Behind && now>=d.Utc && now-d.Utc<TimeSpan.FromSeconds(3))
                    foreach (string name in d.Names) nativeRear[name]=d.Utc;
            foreach (string name in nativeRear.Where(p=>now-p.Value>TimeSpan.FromSeconds(3)).Select(p=>p.Key).ToArray()) nativeRear.Remove(name);
            if (s.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green ||
                gs.PitData.InPitlane || !s.PlayerSpeedMps.HasValue || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1))
            { Volatile.Write(ref facts,null); return; }
            if (s.PlayerDistanceM.HasValue && s.PlayerSpeedMps>=5 && !gs.PenaltiesData.IsOffRacingSurface)
            {
                int cell=(int)(s.PlayerDistanceM.Value/100);
                Reference old;
                // Retain the last lap, not a same-tick reference which merely duplicates the player speed.
                if (!reference.TryGetValue(cell,out old) || old.Lap<gs.SessionData.CompletedLaps)
                    reference[cell]=new Reference {Speed=(float)s.PlayerSpeedMps.Value,Lap=gs.SessionData.CompletedLaps,Utc=now};
                if (reference.Count>2000) reference.Clear();
            }
            RecordMotion(s, now);
            spinning.Clear();
            var candidates=s.Cars.Where(c=>!c.IsSafetyCar && Relevant(c,gs,s,now)).OrderBy(c=>c.RouteGapM>0?0:1)
                .ThenBy(c=>Math.Abs(c.RouteGapM.Value)/Math.Max(1,c.ClosingMps??1)).ToArray();
            Volatile.Write(ref facts,new Facts {Utc=now,Epoch=epoch,Session=s.SessionId,Cars=candidates});
            if (pending!=null || now-lastRadio<TimeSpan.FromSeconds(3)) return;
            foreach (var car in candidates)
            {
                bool rear=car.RouteGapM<0, stopped=!rear && car.SpeedMps<=1, otherClass=MulticlassTraffic.OtherClass(car);
                bool spin=!rear && !stopped && spinning.Contains(car.Key);
                bool classAhead=!rear && !stopped && !spin && otherClass && !SlowAgainstReference(car,gs,s,now);
                string key=car.Id+"|"+(rear?"rear":"hazard"); DateTime previous;
                // Rob 17.11., Bathurst: ~25 Warnungen „schneller Verkehr von hinten“ beim Überrunden. Jetzt: dasselbe Auto
                // höchstens alle 5 Minuten, Verkehr von hinten insgesamt höchstens alle 45 s, und nichts, solange die blaue
                // Flagge weht – die sagt Sara bereits an.
                if (reported.TryGetValue(key,out previous) && now-previous<TimeSpan.FromSeconds(rear ? RearSameCarSeconds : 60)) continue;
                // Klassenfremder Verkehr wird auch bei blauer Flagge genannt (LMU 18.11.): Die Klasse ist die Information, die
                // Sara mit „Blaue Flagge“ nicht gibt; FlagWatch lässt ihre allgemeine Ansage dann weg.
                if (rear && (now-lastRear<TimeSpan.FromSeconds(RearAnySeconds) || gs.SessionData.Flag==FlagEnum.BLUE && !otherClass)) continue;
                var rearCars=candidates.Where(c=>c.RouteGapM<0).ToArray();
                bool group=rear && rearCars.Length>1;
                if (group && now-lastRearGroup<TimeSpan.FromSeconds(120)) continue;
                bool classGroup=group && rearCars.All(MulticlassTraffic.OtherClass);
                if (group && !classGroup) otherClass=false;
                string groupLabel=classGroup && rearCars.Select(c=>c.ClassLabel).Distinct().Count()==1 ? rearCars[0].ClassLabel : null;
                var phrases=rear?(group?RearGroupPhrases:RearPhrases):stopped?StoppedPhrases:spin?SpinPhrases:SlowPhrases;
                bool reminder=false;
                if (stopped && car.LapDistanceM.HasValue)
                {
                    int cell=(int)(car.LapDistanceM.Value/100); Parked spot;
                    if (parked.TryGetValue(car.Key,out spot) && Math.Abs(spot.Cell-cell)<=1)
                    {
                        if (spot.Reports>=2) continue;      // bekannt und unverändert: nicht mehr melden
                        reminder=spot.Reports==1;           // zweites Mal: nur kurz
                        spot.Reports++;
                    }
                    else parked[car.Key]=new Parked{Cell=cell,Reports=1};
                    if (reminder) phrases=StillTherePhrases;
                }
                else if (!rear && parked.ContainsKey(car.Key)) parked.Remove(car.Key); // fährt wieder
                int id=car.Id, generation=epoch; string opponentKey=car.Key;
                pending=new ObservedRaceCall {
                    Topic=rear?"race-traffic-rear":classAhead?"race-class-ahead":"race-hazard-ahead",
                    Text=rear && otherClass ? (group ? MulticlassTraffic.RearGroup(groupLabel,variant++) : MulticlassTraffic.Rear(car.ClassLabel,variant++)) :
                        classAhead ? MulticlassTraffic.Ahead(car.ClassLabel,variant++) : phrases[variant++%phrases.Length],
                    Member=TeamMember.Ben, Priority=7, Position=-1, SessionId=s.SessionId, SessionType=s.SessionType,
                    DetectedUtc=now, ExpiresUtc=now.AddSeconds(3),
                    HazardIdentity=rear || classAhead ? null : car.NativeIdentity,
                    Evidence="local_route_motion;opponent="+opponentKey+";source="+(NativeConfirmed(car,now)?"native_multiclass_and_positions":"measured_positions"),
                    TimedValidity=(live,utc)=>generation==epoch && ValidNow(live,utc,opponentKey,id,stopped,group)
                };
                // Bei klassenfremdem Verkehr zählt die Klasse, nicht der Name (Rob 18.11.: „lass ihn durch, andere Klasse“).
                if (!group && !reminder && !otherClass && !string.IsNullOrWhiteSpace(car.Name))
                    pending.OpponentNameClip = NameClipText(car.Name);
                // Dreher: sofort der kurze, vorbereitete Satz – keine Abstandsangabe, die erst gerechnet werden muss. Ein sich
                // drehendes Auto passt oft kurz nicht zur Strecke (Lage/Distanz uneinig); die Warnung gilt deshalb 2 s ab Erkennung.
                if (spin) { DateTime seenAt=now; pending.TimedValidity=(live,utc)=>generation==epoch && utc>=seenAt && utc-seenAt<=TimeSpan.FromSeconds(2) &&
                    live.SessionData.SessionPhase==SessionPhase.Green && !live.PitData.InPitlane; }
                if(!rear && !reminder && !classAhead && !spin) pending.PreparedDetail=(live,at)=>generation==epoch?HazardDetail(live,at,opponentKey,id,stopped):null;
                pendingKey=key; pendingGroup=group; pendingOtherClass=otherClass; LiveDiagnostics.Decision(pending.Topic,"detected",pending.Evidence,pending.Text,pending.EventId); break;
            }
            if (reported.Count>512) reported.Clear();
        }
        internal static string NameClipText(string name) => RadioLanguage.T("Das ist ", "That's ", "Es ") + name + ".";

        private PreparedRadioDetail HazardDetail(GameStateData gs,DateTime now,string key,int id,bool stopped)
        {
            if(!ValidNow(gs,now,key,id,stopped,false))return null;
            var sample=Volatile.Read(ref facts);
            if(sample==null || now<sample.Utc || now-sample.Utc>TimeSpan.FromMilliseconds(500))return null;
            var car=sample.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
            if (car == null || !car.RouteGapM.HasValue) return null;
            string section = TrackAwareness.ConfirmedSection(gs.SessionData.TrackDefinition, car.LapDistanceM);
            if (section != null)
                return new PreparedRadioDetail { Text = SectionPhrase(section), StillCurrent = (live, at) =>
                    ValidNow(live, at, key, id, stopped, false) &&
                    live.OpponentData.TryGetValue(key, out var opponent) &&
                    TrackAwareness.ConfirmedSection(live.SessionData.TrackDefinition, opponent.DistanceRoundTrack) == section };
            if (car.RouteGapM < 40 || car.RouteGapM > 240) return null;
            double measured=car.RouteGapM.Value;
            int rounded=(int)Math.Round(measured/20,MidpointRounding.AwayFromZero)*20;
            return new PreparedRadioDetail {Text=DistancePhrase(rounded,stopped),StillCurrent=(live,at)=>{
                if(!ValidNow(live,at,key,id,stopped,false))return false;
                var fresh=Volatile.Read(ref facts);
                if(fresh==null || at<fresh.Utc || at-fresh.Utc>TimeSpan.FromMilliseconds(500))return false;
                var same=fresh.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
                return same!=null && same.RouteGapM.HasValue && Math.Abs(same.RouteGapM.Value-measured)<=15;
            }};
        }
        private bool NativeConfirmed(TrackCarPosition car,DateTime now)
        {
            DateTime utc;
            return (nativeRear.TryGetValue(car.Key,out utc) || car.NativeName!=null && nativeRear.TryGetValue(car.NativeName,out utc)) &&
                now>=utc && now-utc<TimeSpan.FromSeconds(3);
        }
        public Func<string,bool> CoveredAhead;
        private bool Relevant(TrackCarPosition c,GameStateData gs,TrackPositionSnapshot s,DateTime now)
        {
            if (c.InPits || !c.SpatiallyConsistent || !c.RouteGapM.HasValue || !c.SpeedMps.HasValue ||
                !s.PlayerSpeedMps.HasValue || s.PlayerSpeedMps<5 || Math.Abs(c.RouteGapM.Value)<15) return false;
            bool rear=c.RouteGapM<0;
            if (!rear && (c.RouteGapM > 600 || CoveredAhead != null && CoveredAhead(c.Key))) return false;
            if (rear && NativeConfirmed(c,now) && c.SpeedMps>=s.PlayerSpeedMps+3 && Math.Abs(c.RouteGapM.Value)<=500) return true;
            // Dreher voraus (Rennen 20.11.): plötzlicher Einbruch zählt auch ohne Abfang-Zeitfenster, bis 400 m voraus.
            if (!rear && c.RouteGapM <= 400 && c.SpeedMps < s.PlayerSpeedMps && motion.TryGetValue(c.Key ?? "", out var seen) &&
                SuddenSlowdown(seen, (float)c.SpeedMps.Value, now, NormalSpeedAt(c, gs, now), (float)s.PlayerSpeedMps.Value))
            { spinning.Add(c.Key); return true; }
            if (!c.ClosingMps.HasValue || !TrackSituationWatch.MotionPlausible(c,s.PlayerSpeedMps.Value,!rear)) return false;
            double closing=c.ClosingMps.Value;
            if (closing<(rear?3:8)) return false;
            double intercept=Math.Abs(c.RouteGapM.Value)/closing;
            if (intercept<.5 || intercept>(rear?10:7)) return false;
            if (rear) return c.ClassComparisonKnown && (!c.SameClass || c.LapOffset>0) && c.SpeedMps>s.PlayerSpeedMps*1.08;
            if (c.SpeedMps<=5) return true;
            if (SlowAgainstReference(c,gs,s,now)) return true;
            // Langsamere Klasse voraus, noch nicht überrundet (Überrundete meldet BackmarkerWatch).
            return MulticlassTraffic.OtherClass(c) && (c.LapOffset ?? 0) >= 0 && closing >= 4 && intercept <= 6 && c.SpeedMps < s.PlayerSpeedMps;
        }

        private bool SlowAgainstReference(TrackCarPosition c,GameStateData gs,TrackPositionSnapshot s,DateTime now)
        {
            Reference normal;
            return c.LapDistanceM.HasValue && c.SpeedMps.HasValue && s.PlayerSpeedMps.HasValue && reference.TryGetValue((int)(c.LapDistanceM.Value/100),out normal) &&
                normal.Lap<gs.SessionData.CompletedLaps && now>=normal.Utc && now-normal.Utc<TimeSpan.FromMinutes(10) &&
                normal.Speed>=15 && c.SpeedMps<normal.Speed*.45 && c.SpeedMps<s.PlayerSpeedMps*.6;
        }
        private bool ValidNow(GameStateData gs,DateTime now,string key,int id,bool stopped,bool group)
        {
            var s=tracks==null?null:tracks.Snapshot;
            var current=Volatile.Read(ref facts);
            if (gs==null || s==null || s.SessionId!=gs.SessionData.SessionId || s.SessionType!=SessionType.Race ||
                gs.SessionData.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green ||
                gs.PitData.InPitlane || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) || current==null ||
                current.Epoch!=epoch || current.Session!=s.SessionId || now<current.Utc || now-current.Utc>TimeSpan.FromSeconds(1)) return false;
            var car=current.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
            OpponentData live;
            return car!=null && gs.OpponentData.TryGetValue(key,out live) && live.IsActive && !live.InPits && RaceReader.Identity("",live)==car.NativeIdentity &&
                (!stopped || live.Speed<=1 && car.SpeedMps<=1) && (!group || current.Cars.Count(c=>c.RouteGapM<0)>1);
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,bool memberEnabled)
        {
            if (pending==null || busy || !memberEnabled || !pending.IsValid(gs,now)) return null;
            return pending.TryReserve(now)?pending:null;
        }
    }
}
