using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    public sealed class ThermalProfile
    {
        public string Source;
        public float[] TyreCookingC,BrakeCookingC;
        public float[] TyreNormalMinC, TyreNormalMaxC, BrakeNormalMinC, BrakeNormalMaxC;
        public float OilLimitC,WaterLimitC;
        public static ThermalProfile FromCar(GameStateData gs)
        {
            if(gs==null || gs.carClass==null || gs.carClass.carClassEnum==CarData.CarClassEnum.UNKNOWN_RACE ||
                gs.carClass.carClassEnum==CarData.CarClassEnum.USER_CREATED)return null;
            var t=gs.TyreData;
            TyreType[] types={t.FrontLeftTyreType,t.FrontRightTyreType,t.RearLeftTyreType,t.RearRightTyreType};
            var profile=new ThermalProfile {Source="CrewChief.CarData/"+gs.carClass.carClassEnum+"/"+gs.carClass.brakeType,
                TyreCookingC=new float[4],BrakeCookingC=new float[4], TyreNormalMinC=new float[4], TyreNormalMaxC=new float[4],
                BrakeNormalMinC=new float[4], BrakeNormalMaxC=new float[4],
                OilLimitC=gs.carClass.maxSafeOilTemp,WaterLimitC=gs.carClass.maxSafeWaterTemp};
            var brakes=CarData.getBrakeTempThresholds(gs.carClass);
            var brake=brakes.FirstOrDefault(v=>v.e.Equals(BrakeTemp.COOKING));
            var normalBrake=brakes.FirstOrDefault(v=>v.e.Equals(BrakeTemp.WARM));
            for(int i=0;i<4;i++)
            {
                if(types[i]!=TyreType.Unknown_Race)
                {
                    var tyres=CarData.getTyreTempThresholds(gs.carClass,types[i]);
                    var tyre=tyres.FirstOrDefault(v=>v.e.Equals(TyreTemp.COOKING));
                    if(tyre!=null)profile.TyreCookingC[i]=tyre.lowerThreshold;
                    var normalTyre=tyres.FirstOrDefault(v=>v.e.Equals(TyreTemp.WARM));
                    if(normalTyre!=null){profile.TyreNormalMinC[i]=normalTyre.lowerThreshold;profile.TyreNormalMaxC[i]=normalTyre.upperThreshold;}
                }
                if(brake!=null)profile.BrakeCookingC[i]=brake.lowerThreshold;
                if(normalBrake!=null){profile.BrakeNormalMinC[i]=normalBrake.lowerThreshold;profile.BrakeNormalMaxC[i]=normalBrake.upperThreshold;}
            }
            return profile;
        }
    }

    /// <summary>Live temperatures, not per-lap peaks. Profile-specific Celsius limits,
    /// persistence and rearm hysteresis prevent normal braking spikes from becoming alarms.</summary>
    public sealed class ThermalWatch
    {
        private readonly Func<GameStateData,ThermalProfile> resolver;
        private sealed class HotSample
        {
            internal DateTime Utc;
            internal string Vehicle;
            internal int Tyres, Brakes;
        }
        private HotSample hotSample;

        internal int SustainedHeat(GameStateData state, DateTime now)
        {
            var active = System.Threading.Volatile.Read(ref hotSample);
            if (active == null || !GreenLap(state) || !Fresh(state, now) || active.Vehicle != Key(state) ||
                now < active.Utc || now - active.Utc > TimeSpan.FromSeconds(1)) return 0;
            return (active.Tyres != 0 ? 1 : 0) | (active.Brakes != 0 ? 2 : 0);
        }
        private readonly Dictionary<string,DateTime> since=new Dictionary<string,DateTime>();
        private sealed class Recovery
        {
            public DateTime WarningUtc;
            public float Severity;
            public int WarningLap;
            public int FirstBoundary = -1, LastBoundary = -1;
            public void BreakRun() { FirstBoundary = LastBoundary = -1; }
        }
        private readonly Dictionary<string,Recovery> recovery = new Dictionary<string,Recovery>();
        private string identity;
        private DateTime lastTick, identitySince;
        private int variant;
        private bool engineSuppressionLogged;
        public ThermalWatch(Func<GameStateData,ThermalProfile> profileResolver=null){resolver=profileResolver??ThermalProfile.FromCar;}
        private static readonly int[] groups={1,2,4,8,3,12,15};
        private static readonly string[] tyres={"vorn links","vorn rechts","hinten links","hinten rechts","vorn","hinten","an allen vier Rädern"};
        private static readonly string[] tyresEn={"front-left","front-right","rear-left","rear-right","front","rear","all four"};
        private static readonly string[] tyresEs={"delantero izquierdo","delantero derecho","trasero izquierdo","trasero derecho","delanteros","traseros",""};
        // EN/ES: "the front-left tyre" / "el neumático delantero izquierdo", "all four brakes" / "los cuatro frenos".
        private static string PartEn(bool brake,int i){return (i==6?"":"the ")+tyresEn[i]+" "+(brake?"brake":"tyre")+(i<4?"":"s");}
        private static string PartEs(bool brake,int i){string noun=brake?"freno":"neumático";return i<4?"el "+noun+" "+tyresEs[i]:i==6?"los cuatro "+noun+"s":"los "+noun+"s "+tyresEs[i];}
        public static string Phrase(bool brake,int mask,int variant)
        {
            int i=Array.IndexOf(groups,mask);if(i<0)throw new ArgumentException("Unsupported corner group.");
            bool single=i<4;
            if(RadioLanguage.Current==RadioLanguage.English)
            {
                string en=PartEn(brake,i)+(single?" is ":" are ");
                return variant%3==0?"Careful, "+en+"overheating. Take the next corners carefully.":
                    variant%3==1?"Watch it, "+en+"too hot. Take the next corners gently.":
                    "Careful, "+en+"overheating. Stay careful and avoid extra load.";
            }
            if(RadioLanguage.Current==RadioLanguage.Spanish)
            {
                string es=PartEs(brake,i)+(single?" está ":" están "),pl=single?"":"s";
                return variant%3==0?"Atención, "+es+"sobrecalentado"+pl+". Toma las próximas curvas con cuidado.":
                    variant%3==1?"Cuidado, "+es+"demasiado caliente"+pl+". Afronta las próximas curvas con calma.":
                    "Atención, "+es+"sobrecalentado"+pl+". Sigue con cuidado y evita más carga.";
            }
            string part=single?(brake?"die Bremse ":"der Reifen ")+tyres[i]:
                (brake?"die Bremsen ":"die Reifen ")+tyres[i];
            string verb=single?" ist ":" sind ";
            return variant%3==0?"Achtung, "+part+verb+"überhitzt. Fahr die nächsten Kurven vorsichtig.":
                variant%3==1?"Vorsicht, "+part+verb+"zu heiß. Geh die nächsten Kurven ruhig an.":
                "Achtung, "+part+verb+"überhitzt. Bleib vorsichtig und vermeide weitere Belastung.";
        }
        public static string ClearPhrase(bool brake,int mask)
        {
            int i=Array.IndexOf(groups,mask);
            if(RadioLanguage.Current==RadioLanguage.English)return "The temperature of "+PartEn(brake,i)+" has settled again.";
            if(RadioLanguage.Current==RadioLanguage.Spanish)return "La temperatura "+(i<4?"d"+PartEs(brake,i):"de "+PartEs(brake,i))+" se ha estabilizado.";
            return "Die Temperatur "+(i<4?(brake?"der Bremse ":"des Reifens "):(brake?"der Bremsen ":"der Reifen "))+tyres[i]+" hat sich wieder beruhigt.";
        }
        public static IEnumerable<string> PreparedPhrases
        {
            get {foreach(bool b in new[]{false,true})foreach(int g in groups){for(int v=0;v<3;v++)yield return Phrase(b,g,v);yield return ClearPhrase(b,g);}}
        }
        public void Reset(){System.Threading.Volatile.Write(ref hotSample,null);since.Clear();recovery.Clear();identity=null;lastTick=identitySince=DateTime.MinValue;engineSuppressionLogged=false;}
        private static string Key(GameStateData gs)
        {return gs.SessionData.SessionId+"|"+gs.SessionData.SessionType+"|"+gs.carName+"|"+(gs.carClass==null?"":gs.carClass.carClassEnum.ToString())+"|"+gs.TyreData.FrontLeftTyreType+"|"+gs.TyreData.FrontRightTyreType+"|"+gs.TyreData.RearLeftTyreType+"|"+gs.TyreData.RearRightTyreType;}
        private static bool Good(float v,float max){return !float.IsNaN(v) && !float.IsInfinity(v) && v>1 && v<max;}
        private static float Average(float a,float b,float c)
        {return Good(a,500) && Good(b,500) && Good(c,500)?(a+b+c)/3:-1;}
        private static float[] Temperatures(GameStateData gs,bool brakes)
        {
            var t=gs.TyreData;
            return brakes?new[]{t.LeftFrontBrakeTemp,t.RightFrontBrakeTemp,t.LeftRearBrakeTemp,t.RightRearBrakeTemp}:
                new[]{Average(t.FrontLeft_LeftTemp,t.FrontLeft_CenterTemp,t.FrontLeft_RightTemp),
                    Average(t.FrontRight_LeftTemp,t.FrontRight_CenterTemp,t.FrontRight_RightTemp),
                    Average(t.RearLeft_LeftTemp,t.RearLeft_CenterTemp,t.RearLeft_RightTemp),
                    Average(t.RearRight_LeftTemp,t.RearRight_CenterTemp,t.RearRight_RightTemp)};
        }
        private static bool Fresh(GameStateData gs,DateTime now)
        {
            DateTime source=gs.SourceTelemetryUtc==DateTime.MinValue?DateTime.SpecifyKind(gs.Now,DateTimeKind.Utc):gs.SourceTelemetryUtc;
            return source!=DateTime.MinValue && now>=source && now-source<=TimeSpan.FromSeconds(1);
        }
        private static bool AtLimit(float[] values,bool brake,int mask,float[] limits)
        {
            for(int i=0;i<4;i++)if((mask&(1<<i))!=0)
            {
                if(limits==null || limits.Length<4 || !Good(limits[i],brake?2000:500) || !Good(values[i],brake?2000:500))return false;
                if(values[i]<limits[i])return false;
            }
            return true;
        }
        private static float AboveLimit(float[] values, float[] limits, int mask)
        {
            if (limits?.Length != 4) return 0;
            float max = 0;
            for (int i = 0; i < 4; i++)
                if ((mask & (1 << i)) != 0 && Good(values[i], 2000) && Good(limits[i], 2000))
                    max = Math.Max(max, values[i] - limits[i]);
            return max;
        }

        private static bool Normal(float[] values, bool brake, int mask, ThermalProfile profile)
        {
            var minimum = brake ? profile.BrakeNormalMinC : profile.TyreNormalMinC;
            var maximum = brake ? profile.BrakeNormalMaxC : profile.TyreNormalMaxC;
            if (minimum?.Length != 4 || maximum?.Length != 4) return false;
            for (int i=0;i<4;i++)
                if ((mask & (1 << i)) != 0 && (!Good(maximum[i],2000) || !Good(values[i],2000) ||
                    !(minimum[i] >= 0) || maximum[i] <= minimum[i] || values[i] < minimum[i] || values[i] >= maximum[i]))
                    return false;
            return true;
        }
        private static bool GreenLap(GameStateData state) => state.SessionData.SessionPhase == SessionPhase.Green &&
            state.SessionData.CurrentLapIsValid && !state.FlagData.isLocalYellow && !state.PitData.InPitlane &&
            !state.PitData.OnInLap && !state.PitData.OnOutLap;
        private static int Group(int mask)
        {if(mask==15)return 15;if((mask&12)==12)return 12;if((mask&3)==3)return 3;foreach(int bit in new[]{1,2,4,8})if((mask&bit)!=0)return bit;return 0;}
        public void Observe(GameStateData gs,DateTime now,LiveWarningDispatcher output)
        {
            if(gs==null || output==null)return;
            string key=Key(gs);
            if(key!=identity || now<lastTick) {Reset();identity=key;identitySince=now;}
            if(lastTick!=DateTime.MinValue && now-lastTick>TimeSpan.FromSeconds(2))
            {since.Clear();foreach(var episode in recovery.Values)episode.BreakRun();}
            lastTick=now;
            if(gs.PitData.InPitlane || !Fresh(gs,now) ||
                gs.SessionData.SessionPhase!=SessionPhase.Green && gs.SessionData.SessionPhase!=SessionPhase.FullCourseYellow)
            {System.Threading.Volatile.Write(ref hotSample,null);since.Clear();foreach(var episode in recovery.Values)episode.BreakRun();return;}
            var profile=resolver(gs);
            if(profile==null){System.Threading.Volatile.Write(ref hotSample,null);foreach(var episode in recovery.Values)episode.BreakRun();return;}
            int tyres = 0, brakes = 0;
            foreach(bool brake in new[]{false,true})
            {
                var limits=brake?profile.BrakeCookingC:profile.TyreCookingC;int ready=0;
                var currentTemps = Temperatures(gs, brake);
                for(int i=0;i<4;i++)
                {
                    string part=(brake?"b":"t")+i;DateTime start;
                    if(!AtLimit(currentTemps,brake,1<<i,limits)){since.Remove(part);continue;}
                    if(!since.TryGetValue(part,out start))since[part]=start=now;
                    if(now-start>=TimeSpan.FromSeconds(brake?4:2))ready|=1<<i;
                }
                int mask=Group(ready);
                if (brake) brakes = mask; else tyres = mask;
                if(mask!=0)
                {
                    bool b=brake;int group=mask;float[] checkedLimits=(float[])limits.Clone();string checkedKey=key;
                    string topic="warning-temperature-"+(b?"brakes-":"tyres-")+mask;
                    var last = output.LastDelivered(topic);
                    float severity = AboveLimit(currentTemps, limits, group);
                    if (!recovery.TryGetValue(topic, out var prior) || output.LastDelivered(topic + "-clear") >= last ||
                        gs.SessionData.CompletedLaps - prior.WarningLap >= 3 || severity >= prior.Severity + (brake ? 40 : 5))
                    {
                        output.Offer(topic,Phrase(b,group,variant++),TeamMember.Mik,7,gs,now,
                            live=>Key(live)==checkedKey && AtLimit(Temperatures(live,b),b,group,checkedLimits),
                            profile.Source+";layer="+(b?"brake_celsius":"mean_current_left_center_right_celsius")+";mask="+mask,
                            timed:(live,utc)=>Fresh(live,utc));
                    }
                }
                foreach(int group in groups)
                {
                    string topic="warning-temperature-"+(brake?"brakes-":"tyres-")+group;
                    DateTime delivered=output.LastDelivered(topic);
                    if(delivered==DateTime.MinValue || delivered<identitySince || output.LastDelivered(topic+"-clear")>=delivered)continue;
                    if(!recovery.TryGetValue(topic,out var episode) || delivered>episode.WarningUtc)
                        recovery[topic]=episode=new Recovery{WarningUtc=delivered,
                            WarningLap=gs.SessionData.CompletedLaps, Severity=AboveLimit(currentTemps, limits, group)};
                    if(!GreenLap(gs) || !Normal(currentTemps,brake,group,profile))
                    {episode.BreakRun();continue;}
                    var session = gs.SessionData;
                    if (session.IsNewLap && session.CompletedLaps != episode.LastBoundary)
                    {
                        int lap = session.CompletedLaps;
                        if (episode.FirstBoundary < 0 || lap != episode.LastBoundary + 1 || !session.PreviousLapWasValid)
                            episode.FirstBoundary = lap;
                        episode.LastBoundary = lap;
                    }
                    if (episode.FirstBoundary < 0 || episode.LastBoundary - episode.FirstBoundary < 2) continue;
                    bool b=brake;int g=group;string checkedKey=key;
                    output.Offer(topic+"-clear",ClearPhrase(b,g),TeamMember.Mik,3,gs,now,
                        live=>Key(live)==checkedKey && GreenLap(live) && Normal(Temperatures(live,b),b,g,profile),
                        profile.Source+";two_clean_laps_in_operating_range",
                        timed:(live,utc)=>Fresh(live,utc));
                }
            }
            System.Threading.Volatile.Write(ref hotSample, tyres == 0 && brakes == 0 ? null :
                new HotSample { Utc=now, Vehicle=key, Tyres=tyres, Brakes=brakes });
            if(gs.EngineData.EngineRpm>0)
            {
                bool oil=EngineHot(gs,true,profile),water=EngineHot(gs,false,profile);string name=oil&&water?"both":oil?"oil":water?"water":null;
                if(name==null){since.Remove("engine");return;}
                DateTime start;if(!since.TryGetValue("engine",out start))since["engine"]=start=now;
                if(now-start>=TimeSpan.FromSeconds(5))
                {
                    // RaceRoom Norisring 22.11.: dieselbe Sperre wie für Crew Chiefs EngineMonitor (vorher alle 30 s eine Warnung).
                    float oilC=gs.EngineData.EngineOilTemp,waterC=gs.EngineData.EngineWaterTemp;
                    if(!output.EngineWarningDue(name,oilC,waterC,now))
                    {
                        if(!engineSuppressionLogged)LiveDiagnostics.Decision("warning-engine-"+name,"suppressed","engine_warning_recently_given",profile.Source);
                        engineSuppressionLogged=true;return;
                    }
                    engineSuppressionLogged=false;
                    var phrases=name=="both"?LiveWarningDispatcher.BothPhrases:oil?LiveWarningDispatcher.OilPhrases:LiveWarningDispatcher.WaterPhrases;
                    bool checkOil=oil,checkWater=water;string checkedKey=key,kind=name;
                    output.Offer("warning-engine-"+name,phrases[variant++%phrases.Length],TeamMember.Mik,9,gs,now,
                        live=>Key(live)==checkedKey && (!checkOil || EngineHot(live,true,profile)) && (!checkWater || EngineHot(live,false,profile)),
                        profile.Source+";profiled_engine_temperature;oil="+oilC.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+
                            ";water="+waterC.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+
                            ";limits="+profile.OilLimitC.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+"/"+
                            profile.WaterLimitC.ToString("0",System.Globalization.CultureInfo.InvariantCulture),
                        timed:(live,utc)=>Fresh(live,utc),
                        onDelivered:utc=>output.EngineWarningSpoken(kind,oilC,waterC,utc));
                }
            }
        }
        private static bool EngineHot(GameStateData gs,bool oil,ThermalProfile profile)
        {float value=oil?gs.EngineData.EngineOilTemp:gs.EngineData.EngineWaterTemp,limit=oil?profile.OilLimitC:profile.WaterLimitC;return Good(value,400) && Good(limit,400) && value>limit;}
    }
}
