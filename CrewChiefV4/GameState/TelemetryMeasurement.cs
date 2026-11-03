using System;
using System.Collections.Generic;

namespace CrewChiefV4.GameState
{
    /// <summary>One immutable, attributed reading. Zero, absence and invalid data are distinct.
    /// UTC denotes the source snapshot, not the time an AI request happened to read it.</summary>
    public sealed class TelemetryMeasurement
    {
        public readonly double? RawValue,Value;
        public readonly string Source,Field,Component,Unit,RawUnit,Conversion,Status;
        public readonly DateTime SampleUtc;
        public TelemetryMeasurement(double? raw,double scale,double offset,string source,string field,string component,
            string rawUnit,string unit,DateTime utc,double minimum,double maximum,bool supported=true,string conversion=null)
        {
            Source=source;Field=field;Component=component;RawUnit=rawUnit;Unit=unit;Conversion=conversion;
            SampleUtc=DateTime.SpecifyKind(utc,DateTimeKind.Utc);
            bool finite=raw.HasValue && !double.IsNaN(raw.Value) && !double.IsInfinity(raw.Value);
            RawValue=finite?raw:null;
            double value=finite?raw.Value*scale+offset:double.NaN;
            Status=!supported || !raw.HasValue?"fehlt":!finite || value<minimum || value>maximum?"ungueltig":"gueltig";
            Value=Status=="gueltig"?(double?)value:null;
        }
        public string StatusAt(DateTime now,bool paused=false)
        {
            if(Status!="gueltig")return Status;
            if(paused)return "pausiert";
            if(SampleUtc==DateTime.MinValue || now<SampleUtc)return "ungueltige_zeitbasis";
            return now-SampleUtc>TimeSpan.FromSeconds(3)?"veraltet":"gueltig";
        }
    }

    public static class TelemetryMeasurements
    {
        public static readonly string[] Corners={"vl","vr","hl","hr"};
        public static void Put(GameStateData gs,string id,double? raw,string source,string field,string component,string unit,
            DateTime utc,double min,double max,bool supported=true,double scale=1,double offset=0,string rawUnit=null,string conversion=null)
        {
            gs.Measurements[id]=new TelemetryMeasurement(raw,scale,offset,source,field,component,rawUnit??unit,unit,utc,min,max,supported,conversion);
        }
        public static float Percentage(GameStateData gs,string id)
        {
            TelemetryMeasurement m;
            return gs.Measurements.TryGetValue(id,out m) && m.Value.HasValue?(float)m.Value.Value:-1;
        }
        public static void DamageFractions(GameStateData gs,float aero,float engine,float[] brakes,float[] suspension,string source,DateTime utc)
        {
            Put(gs,"damage.aero",aero,source,"mAeroDamage","aero","%",utc,0,100,aero!=-1,100,0,"fraction","rohwert * 100");
            Put(gs,"damage.engine",engine,source,"mEngineDamage","motor","%",utc,0,100,engine!=-1,100,0,"fraction","rohwert * 100");
            gs.CarDamageData.AeroDamagePercent=Percentage(gs,"damage.aero");
            gs.CarDamageData.EngineDamagePercent=Percentage(gs,"damage.engine");
            for(int i=0;i<4;i++)
            {
                double? b=brakes!=null && brakes.Length>i?(double?)brakes[i]:null;
                double? s=suspension!=null && suspension.Length>i?(double?)suspension[i]:null;
                Put(gs,"damage.brake."+Corners[i],b,source,"mBrakeDamage["+i+"]",Corners[i],"%",utc,0,100,b.HasValue && b!=-1,100,0,"fraction","rohwert * 100");
                Put(gs,"damage.suspension."+Corners[i],s,source,"mSuspensionDamage["+i+"]",Corners[i],"%",utc,0,100,s.HasValue && s!=-1,100,0,"fraction","rohwert * 100");
                gs.CarDamageData.BrakeDamagePercent[i]=Percentage(gs,"damage.brake."+Corners[i]);
                gs.CarDamageData.SuspensionDamagePercent[i]=Percentage(gs,"damage.suspension."+Corners[i]);
            }
        }
        public static void HealthDamage(GameStateData gs,float aero,float engine,float transmission,string source,DateTime utc)
        {
            string[] ids={"aero","engine","transmission"};float[] values={aero,engine,transmission};
            string[] fields={"CarDamage.Aerodynamics","CarDamage.Engine","CarDamage.Transmission"};
            for(int i=0;i<3;i++)Put(gs,"damage."+ids[i],values[i],source,fields[i],ids[i],"%",utc,0,100,values[i]!=-1,-100,100,"health_fraction","(1 - rohwert) * 100");
            gs.CarDamageData.AeroDamagePercent=Percentage(gs,"damage.aero");
            gs.CarDamageData.EngineDamagePercent=Percentage(gs,"damage.engine");
            gs.CarDamageData.TransmissionDamagePercent=Percentage(gs,"damage.transmission");
        }
    }
}
