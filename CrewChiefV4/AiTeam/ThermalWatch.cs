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
        public float OilLimitC,WaterLimitC;
        public static ThermalProfile FromCar(GameStateData gs)
        {
            if(gs==null || gs.carClass==null || gs.carClass.carClassEnum==CarData.CarClassEnum.UNKNOWN_RACE ||
                gs.carClass.carClassEnum==CarData.CarClassEnum.USER_CREATED)return null;
            var t=gs.TyreData;
            TyreType[] types={t.FrontLeftTyreType,t.FrontRightTyreType,t.RearLeftTyreType,t.RearRightTyreType};
            var profile=new ThermalProfile {Source="CrewChief.CarData/"+gs.carClass.carClassEnum+"/"+gs.carClass.brakeType,
                TyreCookingC=new float[4],BrakeCookingC=new float[4],OilLimitC=gs.carClass.maxSafeOilTemp,WaterLimitC=gs.carClass.maxSafeWaterTemp};
            var brake=CarData.getBrakeTempThresholds(gs.carClass).FirstOrDefault(v=>v.e.Equals(BrakeTemp.COOKING));
            for(int i=0;i<4;i++)
            {
                if(types[i]!=TyreType.Unknown_Race)
                {
                    var tyre=CarData.getTyreTempThresholds(gs.carClass,types[i]).FirstOrDefault(v=>v.e.Equals(TyreTemp.COOKING));
                    if(tyre!=null)profile.TyreCookingC[i]=tyre.lowerThreshold;
                }
                if(brake!=null)profile.BrakeCookingC[i]=brake.lowerThreshold;
            }
            return profile;
        }
    }

    /// <summary>Live temperatures, not per-lap peaks. Profile-specific Celsius limits,
    /// persistence and rearm hysteresis prevent normal braking spikes from becoming alarms.</summary>
    public sealed class ThermalWatch
    {
        private readonly Func<GameStateData,ThermalProfile> resolver;
        private readonly Dictionary<string,DateTime> since=new Dictionary<string,DateTime>();
        private readonly Dictionary<string,DateTime> coolSince=new Dictionary<string,DateTime>();
        private readonly Dictionary<string,DateTime> acknowledged=new Dictionary<string,DateTime>();
        private string identity;
        private DateTime lastTick;
        private int variant;
        public ThermalWatch(Func<GameStateData,ThermalProfile> profileResolver=null){resolver=profileResolver??ThermalProfile.FromCar;}
        private static readonly int[] groups={1,2,4,8,3,12,15};
        private static readonly string[] tyres={"vorn links","vorn rechts","hinten links","hinten rechts","vorn","hinten","an allen vier Rädern"};
        public static string Phrase(bool brake,int mask,int variant)
        {
            int i=Array.IndexOf(groups,mask);if(i<0)throw new ArgumentException("Unsupported corner group.");
            bool single=i<4;
            string part=single?(brake?"die Bremse ":"der Reifen ")+tyres[i]:
                (brake?"die Bremsen ":"die Reifen ")+tyres[i];
            string verb=single?" ist ":" sind ";
            return variant%3==0?"Achtung, "+part+verb+"überhitzt. Fahr die nächsten Kurven vorsichtig.":
                variant%3==1?"Vorsicht, "+part+verb+"zu heiß. Geh die nächsten Kurven ruhig an.":
                "Achtung, "+part+verb+"überhitzt. Bleib vorsichtig und vermeide weitere Belastung.";
        }
        public static string ClearPhrase(bool brake,int mask)
        {int i=Array.IndexOf(groups,mask);return "Die Temperatur "+(i<4?(brake?"der Bremse ":"des Reifens "):(brake?"der Bremsen ":"der Reifen "))+tyres[i]+" hat sich wieder beruhigt.";}
        public static IEnumerable<string> PreparedPhrases
        {
            get {foreach(bool b in new[]{false,true})foreach(int g in groups){for(int v=0;v<3;v++)yield return Phrase(b,g,v);yield return ClearPhrase(b,g);}}
        }
        public void Reset(){since.Clear();coolSince.Clear();acknowledged.Clear();identity=null;lastTick=DateTime.MinValue;}
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
        private static bool AtLimit(GameStateData gs,bool brake,int mask,float[] limits,bool cooled)
        {
            var values=Temperatures(gs,brake);
            for(int i=0;i<4;i++)if((mask&(1<<i))!=0)
            {
                if(limits==null || limits.Length<4 || !Good(limits[i],brake?2000:500) || !Good(values[i],brake?2000:500))return false;
                float margin=Math.Max(2,limits[i]*.02f);
                if(cooled?values[i]>=limits[i]-margin:values[i]<limits[i])return false;
            }
            return true;
        }
        private static int Group(int mask)
        {if(mask==15)return 15;if((mask&12)==12)return 12;if((mask&3)==3)return 3;foreach(int bit in new[]{1,2,4,8})if((mask&bit)!=0)return bit;return 0;}
        public void Observe(GameStateData gs,DateTime now,LiveWarningDispatcher output)
        {
            if(gs==null || output==null)return;
            string key=Key(gs);
            if(key!=identity || now<lastTick || lastTick!=DateTime.MinValue && now-lastTick>TimeSpan.FromSeconds(2))
            {Reset();identity=key;}
            lastTick=now;
            if(gs.PitData.InPitlane || !Fresh(gs,now) ||
                gs.SessionData.SessionPhase!=SessionPhase.Green && gs.SessionData.SessionPhase!=SessionPhase.FullCourseYellow)
            {since.Clear();coolSince.Clear();return;}
            var profile=resolver(gs);if(profile==null)return;
            foreach(bool brake in new[]{false,true})
            {
                var limits=brake?profile.BrakeCookingC:profile.TyreCookingC;int ready=0;
                for(int i=0;i<4;i++)
                {
                    string part=(brake?"b":"t")+i;DateTime start;
                    if(!AtLimit(gs,brake,1<<i,limits,false)){since.Remove(part);continue;}
                    if(!since.TryGetValue(part,out start))since[part]=start=now;
                    if(now-start>=TimeSpan.FromSeconds(brake?4:2))ready|=1<<i;
                }
                int mask=Group(ready);
                if(mask!=0)
                {
                    bool b=brake;int group=mask;float[] checkedLimits=(float[])limits.Clone();string checkedKey=key;
                    string topic="warning-temperature-"+(b?"brakes-":"tyres-")+mask;
                    output.Offer(topic,Phrase(b,group,variant++),TeamMember.Mik,7,gs,now,
                        live=>Key(live)==checkedKey && AtLimit(live,b,group,checkedLimits,false),
                        profile.Source+";layer="+(b?"brake_celsius":"mean_current_left_center_right_celsius")+";mask="+mask,
                        timed:(live,utc)=>Fresh(live,utc));
                }
                foreach(int group in groups)
                {
                    string topic="warning-temperature-"+(brake?"brakes-":"tyres-")+group;
                    DateTime delivered=output.LastDelivered(topic),seen;
                    if(delivered==DateTime.MinValue)continue;
                    if(!acknowledged.TryGetValue(topic,out seen) || delivered>seen){acknowledged[topic]=delivered;coolSince.Remove(topic);}
                    if(!AtLimit(gs,brake,group,limits,true)){coolSince.Remove(topic);continue;}
                    DateTime cool;if(!coolSince.TryGetValue(topic,out cool))coolSince[topic]=cool=now;
                    if(now-cool<TimeSpan.FromSeconds(10))continue;
                    bool b=brake;int g=group;float[] checkedLimits=(float[])limits.Clone();string checkedKey=key;
                    output.Offer(topic+"-clear",ClearPhrase(b,g),TeamMember.Mik,3,gs,now,
                        live=>Key(live)==checkedKey && AtLimit(live,b,g,checkedLimits,true),profile.Source+";confirmed_cooling",
                        timed:(live,utc)=>Fresh(live,utc));
                    coolSince[topic]=now.AddYears(1); // One recovery call for this delivered episode.
                }
            }
            if(gs.EngineData.EngineRpm>0)
            {
                bool oil=EngineHot(gs,true,profile),water=EngineHot(gs,false,profile);string name=oil&&water?"both":oil?"oil":water?"water":null;
                if(name==null){since.Remove("engine");return;}
                DateTime start;if(!since.TryGetValue("engine",out start))since["engine"]=start=now;
                if(now-start>=TimeSpan.FromSeconds(5))
                {
                    var phrases=name=="both"?LiveWarningDispatcher.BothPhrases:oil?LiveWarningDispatcher.OilPhrases:LiveWarningDispatcher.WaterPhrases;
                    bool checkOil=oil,checkWater=water;string checkedKey=key;
                    output.Offer("warning-engine-"+name,phrases[variant++%phrases.Length],TeamMember.Mik,9,gs,now,
                        live=>Key(live)==checkedKey && (!checkOil || EngineHot(live,true,profile)) && (!checkWater || EngineHot(live,false,profile)),
                        profile.Source+";profiled_engine_temperature",timed:(live,utc)=>Fresh(live,utc));
                }
            }
        }
        private static bool EngineHot(GameStateData gs,bool oil,ThermalProfile profile)
        {float value=oil?gs.EngineData.EngineOilTemp:gs.EngineData.EngineWaterTemp,limit=oil?profile.OilLimitC:profile.WaterLimitC;return Good(value,400) && Good(limit,400) && value>limit;}
    }
}
