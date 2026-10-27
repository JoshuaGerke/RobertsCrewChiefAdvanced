using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Event-driven observations; never reads optional reporting intervals.</summary>
    public sealed class RaceSituationWatch
    {
        private readonly Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();
        public void Reset() { last.Clear(); }
        public List<ObservedRaceCall> Evaluate(GameStateData before, GameStateData gs, DateTime now)
        {
            var calls=new List<ObservedRaceCall>();
            if(before==null || gs==null || gs.SessionData.SessionPhase!=SessionPhase.Green || gs.SessionData.SessionType!=SessionType.Race || gs.PitData.InPitlane) return calls;
            if(before.SessionData.SessionPhase==SessionPhase.Countdown || before.SessionData.SessionPhase==SessionPhase.Formation)
                Add(calls,"race-start","Das Rennen läuft. Konzentrier dich auf deinen Start.",gs,now,TeamMember.Ben,6,null);
            var e=gs.EngineData;var p=before.EngineData;
            if(e.EngineWaterTempWarning && !p.EngineWaterTempWarning)
                Add(calls,"engine-water","Warnung für die Motortemperatur. Behalte die Anzeige im Blick.",gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineWaterTempWarning);
            if(e.EngineOilPressureWarning && !p.EngineOilPressureWarning)
                Add(calls,"engine-oil","Öldruckwarnung. Motor schonen und die Anzeige prüfen.",gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineOilPressureWarning);
            if(e.EngineFuelPressureWarning && !p.EngineFuelPressureWarning)
                Add(calls,"engine-fuel-pressure","Die Simulation meldet ein Problem mit dem Kraftstoffdruck.",gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineFuelPressureWarning);
            if(!before.PitData.CarInFrontIsPitting && gs.PitData.CarInFrontIsPitting)
                Add(calls,"opponent-pit","Dein bisheriger Vordermann fährt an die Box. Ich beobachte die Reihenfolge.",gs,now,TeamMember.Ben,3,null);
            return calls;
        }
        private void Add(List<ObservedRaceCall> calls,string topic,string text,GameStateData gs,DateTime now,TeamMember member,int priority,Func<GameStateData,bool> valid)
        {
            DateTime previous;if(last.TryGetValue(topic,out previous) && now-previous<TimeSpan.FromSeconds(20))return;
            last[topic]=now;
            calls.Add(new ObservedRaceCall { Topic=topic,Text=text,Evidence="mapped_state_transition",Member=member,Priority=priority,
                Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=now,ExpiresUtc=now.AddSeconds(6),AdditionalValidity=valid });
        }
    }
}
