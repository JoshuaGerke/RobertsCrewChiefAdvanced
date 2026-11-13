using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Accumulates material changes below the legacy damage-level thresholds.
    /// A published damage percentage is not a percentage loss of braking force or grip.</summary>
    public sealed class NumericDamageWatch
    {
        private static readonly string[] Names={"Aero","Motor","Getriebe","Bremse vorne links","Bremse vorne rechts","Bremse hinten links","Bremse hinten rechts",
            "Aufhängung vorne links","Aufhängung vorne rechts","Aufhängung hinten links","Aufhängung hinten rechts"};
        private static readonly string[] NamesEn={"Aero","Engine","Gearbox","Front-left brake","Front-right brake","Rear-left brake","Rear-right brake",
            "Front-left suspension","Front-right suspension","Rear-left suspension","Rear-right suspension"};
        private static readonly string[] NamesEs={"de la aerodinámica","del motor","de la caja de cambios","del freno delantero izquierdo","del freno delantero derecho","del freno trasero izquierdo","del freno trasero derecho",
            "de la suspensión delantera izquierda","de la suspensión delantera derecha","de la suspensión trasera izquierda","de la suspensión trasera derecha"};
        private readonly float[] baseline=Enumerable.Repeat(-1f,11).ToArray();
        private int session=int.MinValue;
        private SessionType type;
        public void Reset(){for(int i=0;i<baseline.Length;i++)baseline[i]=-1;session=int.MinValue;}
        private static bool Good(float n){return !float.IsNaN(n) && !float.IsInfinity(n) && n>=0 && n<=100;}
        private static float At(float[] a,int i){return a!=null && i<a.Length?a[i]:-1;}
        private static float[] Values(CarDamageData d)
        {return new[]{d.AeroDamagePercent,d.EngineDamagePercent,d.TransmissionDamagePercent,
            At(d.BrakeDamagePercent,0),At(d.BrakeDamagePercent,1),At(d.BrakeDamagePercent,2),At(d.BrakeDamagePercent,3),
            At(d.SuspensionDamagePercent,0),At(d.SuspensionDamagePercent,1),At(d.SuspensionDamagePercent,2),At(d.SuspensionDamagePercent,3)};}
        private static string Part(int i,float value)
        {
            string n=RadioLanguage.Num(value,"0.0");
            return RadioLanguage.T(Names[i]+"-Schadenswert "+n+" Prozent",NamesEn[i]+" damage value "+n+" percent","Valor de daño "+NamesEs[i]+" "+n+" por ciento");
        }
        public static string Report(CarDamageData d)
        {
            var values=Values(d);var parts=new List<string>();
            for(int i=0;i<values.Length;i++)if(Good(values[i]) && values[i]>=.05f)parts.Add(Part(i,values[i]));
            return parts.Count==0?null:string.Join(", ",parts)+".";
        }
        public void Observe(GameStateData gs,DateTime now,LiveWarningDispatcher output)
        {
            if(gs==null)return;
            if(session!=gs.SessionData.SessionId || type!=gs.SessionData.SessionType || gs.SessionData.IsNewSession)
            {Reset();session=gs.SessionData.SessionId;type=gs.SessionData.SessionType;}
            if(gs.SourceTelemetryUtc!=DateTime.MinValue && (now<gs.SourceTelemetryUtc || now-gs.SourceTelemetryUtc>TimeSpan.FromSeconds(1)))return;
            var values=Values(gs.CarDamageData);
            var changed=new List<int>();
            for(int i=0;i<values.Length;i++)
            {
                if(!Good(values[i])){baseline[i]=-1;continue;}
                // Initial availability, repairs and a garage state establish a new baseline, not an incident.
                if(!Good(baseline[i]) || values[i]<baseline[i] || gs.PitData.InPitlane || gs.SessionData.SessionPhase!=SessionPhase.Green)
                {baseline[i]=values[i];continue;}
                if(values[i]-baseline[i]>=.9999f)changed.Add(i);
            }
            if(changed.Count==0)return;
            var snapshot=(float[])values.Clone();var indices=changed.ToArray();
            string report=string.Join(", ",indices.Take(3).Select(i=>Part(i,snapshot[i])))+".";
            if(indices.Length>3)report+=RadioLanguage.T(" Weitere Komponenten melden ebenfalls mehr Schaden."," Other components are reporting more damage too."," Otros componentes también marcan más daño.");
            output.Offer("warning-numeric-damage",report,TeamMember.Mik,7,gs,now,
                live=>!live.PitData.InPitlane && indices.All(i=>Good(Values(live.CarDamageData)[i]) && Math.Abs(Values(live.CarDamageData)[i]-snapshot[i])<=.1f),
                "numeric_damage_increase_at_least_1_percentage_point;not_force_loss",-1,
                (live,utc)=>live.SourceTelemetryUtc==DateTime.MinValue || utc>=live.SourceTelemetryUtc && utc-live.SourceTelemetryUtc<=TimeSpan.FromSeconds(1),
                delivered=>Array.Copy(snapshot,baseline,baseline.Length));
        }
    }
}
