using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Event-driven observations; never reads optional reporting intervals.</summary>
    public sealed class RaceSituationWatch
    {
        private readonly Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();
        private readonly Random random = new Random();
        private int session = int.MinValue, formationCount, previousVariant = -1;
        private DateTime nextFormation;
        private static readonly int R3eCarsOffset = Marshal.OffsetOf(typeof(CrewChiefV4.RaceRoom.RaceRoomData.RaceRoomShared), "NumCars").ToInt32();
        private static readonly int R3eDriverSize = Marshal.SizeOf(typeof(CrewChiefV4.RaceRoom.RaceRoomData.DriverData));
        [StructLayout(LayoutKind.Explicit)]
        private struct IntBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Integer;
        }

        // RaceRoom 3.6 uses reserved 3.5 slots for integer start information. Keep the
        // original public struct fields for old traces, and interpret their bits only
        // for the documented 3.6 layout and an unambiguous current vehicle slot.
        private static int IntegerBits(float value) => new IntBits { Float = value }.Integer;
        internal static bool RaceRoomStart(GameStateData gs, DateTime now, out int mode, out int phase)
        {
            mode = phase = -1;
            var wrapper = gs?.rawGameData as CrewChiefV4.RaceRoom.R3ESharedMemoryReader.R3EStructWrapper;
            if (wrapper == null || wrapper.data.VersionMajor != 3 || wrapper.data.VersionMinor != 6) return false;
            var raw = wrapper.data;
            long source = wrapper.sourceSampleTicks>0 ? wrapper.sourceSampleTicks : wrapper.ticksWhenRead;
            if(now.Ticks<source || now.Ticks-source>TimeSpan.TicksPerSecond) return true;
            if (wrapper.ticksWhenRead <= 0 || now.Ticks < wrapper.ticksWhenRead ||
                now.Ticks - wrapper.ticksWhenRead > TimeSpan.TicksPerSecond || raw.GamePaused != 0 || raw.GameInReplay != 0 ||
                raw.AllDriversOffset != R3eCarsOffset || raw.DriverDataSize != R3eDriverSize) return true;
            int reportedMode = IntegerBits(raw.EventUnused1);
            if (reportedMode >= 0 && reportedMode <= 2) mode = reportedMode;
            if (raw.VehicleInfo.SlotId < 0 || raw.NumCars < 1 || raw.DriverData == null || raw.NumCars > raw.DriverData.Length) return true;
            int matches = 0;
            for (int i = 0; i < raw.NumCars; i++)
                if (raw.DriverData[i].DriverInfo.SlotId == raw.VehicleInfo.SlotId)
                {
                    matches++;
                    int reportedPhase = IntegerBits(raw.DriverData[i].Unused2);
                    phase = reportedPhase >= 0 && reportedPhase <= 2 ? reportedPhase : -1;
                }
            if (matches != 1) phase = -1;
            return true;
        }

        internal static bool InFormation(GameStateData gs, DateTime now)
        {
            if (gs == null || gs.SessionData.SessionType != SessionType.Race) return false;
            int mode, phase;
            bool r3e = RaceRoomStart(gs, now, out mode, out phase);
            if (gs.SessionData.SessionPhase == SessionPhase.Formation) return !r3e || phase != 2;
            return gs.SessionData.SessionPhase == SessionPhase.Green && r3e &&
                (mode == 1 || mode == 2) && (phase == 0 || phase == 1);
        }
        // Own wording for confirmed phases; no coaching, forecasts or invented race facts.
        private static readonly string[] FormationPhrasesDe = {
            "Formation. Ruhig bleiben und sauber in Position halten.",
            "Konzentriert in den Start. Bleib aufmerksam, wir sind bei dir.",
            "Noch Formation. Lass dir Raum und geh ohne unnötiges Risiko in den Start.",
            "Ruhig durch die Formation. Wir behalten den Verkehr im Blick.",
            "Fokus auf den Start. Bleib bei deinem Rhythmus und lass dir Platz.",
            "Wir sind bei dir. Formation sauber zu Ende bringen, dann geht es los."
        };
        private static readonly string[] FormationPhrasesEn = {
            "Formation. Stay calm and hold your position cleanly.",
            "Focused into the start. Stay alert, we're with you.",
            "Still formation. Give yourself room and go into the start without needless risk.",
            "Easy through the formation. We're watching the traffic.",
            "Focus on the start. Keep your rhythm and leave yourself space.",
            "We're with you. Finish the formation cleanly, then we go."
        };
        private static readonly string[] FormationPhrasesEs = {
            "Vuelta de formación. Tranquilo y mantén la posición limpio.",
            "Concentrado para la salida. Atento, estamos contigo.",
            "Todavía en formación. Déjate espacio y sal sin riesgos innecesarios.",
            "Tranquilo en la formación. Vigilamos el tráfico.",
            "Céntrate en la salida. Mantén tu ritmo y déjate espacio.",
            "Estamos contigo. Termina bien la formación y luego empezamos."
        };
        private static string[] FormationPhrases => RadioLanguage.Pick(FormationPhrasesDe, FormationPhrasesEn, FormationPhrasesEs);
        private static readonly string[] StartPhrasesDe = {
            "grün. Sauber durch die ersten Kurven.",
            "das Rennen läuft. Bleib konzentriert.",
            "der Start ist frei. Lass dir Platz.",
            "grün. Ruhig bleiben, Blick nach vorn.",
            "jetzt geht's los. Bleib bei deinem Rhythmus."
        };
        private static readonly string[] StartPhrasesEn = {
            "green. Clean through the first corners.",
            "the race is on. Stay focused.",
            "the start is clear. Give yourself room.",
            "green. Stay calm, eyes forward.",
            "here we go. Keep your rhythm."
        };
        private static readonly string[] StartPhrasesEs = {
            "verde. Limpio en las primeras curvas.",
            "empieza la carrera. Concentrado.",
            "salida libre. Déjate espacio.",
            "verde. Tranquilo, mirada al frente.",
            "allá vamos. Mantén tu ritmo."
        };
        private static string[] StartPhrases => RadioLanguage.Pick(StartPhrasesDe, StartPhrasesEn, StartPhrasesEs);

        internal static bool AnswerMatchesStartPhase(GameStateData questionState, GameStateData current, DateTime now)
        {
            if (questionState?.SessionData.SessionType != SessionType.Race) return true;
            var phase = questionState.SessionData.SessionPhase;
            if (phase != SessionPhase.Gridwalk && phase != SessionPhase.Countdown && phase != SessionPhase.Formation &&
                !InFormation(questionState, questionState.Now)) return true;
            if (current?.SessionData.SessionType != SessionType.Race) return false;
            var currentPhase = current.SessionData.SessionPhase;
            return currentPhase == SessionPhase.Gridwalk || currentPhase == SessionPhase.Countdown ||
                currentPhase == SessionPhase.Formation || InFormation(current, now);
        }
        private static string RadioName(string name)
        {
            string clean = new string((name ?? "Rob").Where(c => char.IsLetter(c) || c == ' ' || c == '-').Take(30).ToArray()).Trim();
            return clean.Length == 0 ? "Rob" : clean;
        }
        public static IEnumerable<string> PreparedPhrases(string driverName)
        { return FormationPhrases.Concat(StartPhrases).Select(p => RadioName(driverName) + ", " + p); }
        public static bool IsPreparedPhrase(string text)
        { return text != null && FormationPhrases.Concat(StartPhrases).Any(p => text.EndsWith(", " + p, StringComparison.Ordinal)); }
        public void Reset() { last.Clear(); session = int.MinValue; formationCount = 0; nextFormation = DateTime.MinValue; }
        private string FormationText(string name)
        {
            int index = random.Next(FormationPhrases.Length - (previousVariant >= 0 ? 1 : 0));
            if (previousVariant >= 0 && index >= previousVariant) index++;
            previousVariant = index;
            return RadioName(name) + ", " + FormationPhrases[index];
        }
        public List<ObservedRaceCall> Evaluate(GameStateData before, GameStateData gs, DateTime now, string driverName = "Rob")
        {
            var calls=new List<ObservedRaceCall>();
            if(gs==null || gs.SessionData.SessionType!=SessionType.Race) { Reset(); return calls; }
            if(session!=gs.SessionData.SessionId || gs.SessionData.IsNewSession && (before==null || !before.SessionData.IsNewSession))
            { Reset(); session=gs.SessionData.SessionId; }
            if(gs.PitData.InPitlane) return calls;
            if(InFormation(gs,now))
            {
                float speed=gs.PositionAndMotionData.CarSpeed;
                if(!float.IsNaN(speed) && !float.IsInfinity(speed) && speed>=0 && speed<80 && formationCount<3 && now>=nextFormation)
                {
                    Add(calls,"formation",FormationText(driverName),gs,now,TeamMember.Ben,3,null);
                    formationCount++; nextFormation=now.AddSeconds(45+random.Next(-7,9));
                }
                if(gs.SessionData.SessionPhase==SessionPhase.Formation) return calls;
            }
            if(before==null || before.SessionData.SessionId!=gs.SessionData.SessionId || before.SessionData.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green) return calls;
            int mode, phase, previousMode, previousPhase;
            bool isR3e36 = RaceRoomStart(gs,now,out mode,out phase);
            bool hadR3e36 = RaceRoomStart(before,now,out previousMode,out previousPhase);
            bool globalStart = before.SessionData.SessionPhase==SessionPhase.Countdown || before.SessionData.SessionPhase==SessionPhase.Formation;
            bool ownGroupStart = isR3e36 && phase==2 && hadR3e36 && (previousPhase==0 || previousPhase==1);
            if ((!isR3e36 || phase==2) && (globalStart || ownGroupStart))
            {
                var call=Add(calls,"race-start",RadioName(driverName)+", "+StartPhrases[random.Next(StartPhrases.Length)],gs,now,TeamMember.Ben,6,null);
                if(call!=null && isR3e36) {
                    call.Evidence="raceroom_3_6_own_start_group_green";
                    call.TimedValidity=(state,at)=>{
                        int liveMode, livePhase;
                        return RaceRoomStart(state,at,out liveMode,out livePhase) && livePhase==2;
                    };
                }
            }
            var e=gs.EngineData;var p=before.EngineData;
            if(e.EngineWaterTempWarning && !p.EngineWaterTempWarning)
                Add(calls,"engine-water",RadioLanguage.T("Wassertemperatur zu hoch. Behalt die Anzeige im Blick.","Water temperature too high. Keep an eye on the gauge.","Temperatura del agua demasiado alta. Vigila el indicador."),gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineWaterTempWarning);
            if(e.EngineOilPressureWarning && !p.EngineOilPressureWarning)
                Add(calls,"engine-oil",RadioLanguage.T("Öldruckwarnung. Motor schonen und die Anzeige prüfen.","Oil pressure warning. Look after the engine and check the gauge.","Aviso de presión de aceite. Cuida el motor y revisa el indicador."),gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineOilPressureWarning);
            if(e.EngineFuelPressureWarning && !p.EngineFuelPressureWarning)
                Add(calls,"engine-fuel-pressure",RadioLanguage.T("Warnung beim Spritdruck. Schau auf die Anzeige.","Fuel pressure warning. Check the gauge.","Aviso de presión de combustible. Mira el indicador."),gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineFuelPressureWarning);
            if(!before.PitData.CarInFrontIsPitting && gs.PitData.CarInFrontIsPitting &&
                NativeFieldFact.Capture("car_in_front_is_pitting",null,gs)==null)
                Add(calls,"opponent-pit",RadioLanguage.T("Dein bisheriger Vordermann ist auf dem Weg in die Box.","The car that was ahead of you is heading into the pits.","El que iba delante de ti entra en boxes."),gs,now,TeamMember.Ben,3,null);
            return calls;
        }
        private ObservedRaceCall Add(List<ObservedRaceCall> calls,string topic,string text,GameStateData gs,DateTime now,TeamMember member,int priority,Func<GameStateData,bool> valid)
        {
            DateTime previous;if(last.TryGetValue(topic,out previous) && now-previous<TimeSpan.FromSeconds(20))return null;
            last[topic]=now;
            var call=new ObservedRaceCall { Topic=topic,Text=text,Evidence="mapped_state_transition",Member=member,Priority=priority,
                Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=now,ExpiresUtc=now.AddSeconds(topic=="formation"?12:6),AdditionalValidity=valid };
            calls.Add(call);
            return call;
        }
    }
}
