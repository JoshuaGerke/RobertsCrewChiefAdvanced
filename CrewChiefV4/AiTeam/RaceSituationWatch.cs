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
        // RaceRoom 3.6 start information (reserved slots in 3.5). Only trusted for 3.6+, a fresh
        // frame, the documented layout and an unambiguous current vehicle slot.
        internal static bool RaceRoomStart(GameStateData gs, DateTime now, out int mode, out int phase)
        {
            mode = phase = -1;
            var wrapper = gs?.rawGameData as CrewChiefV4.RaceRoom.R3ESharedMemoryReader.R3EStructWrapper;
            if (wrapper == null || wrapper.data.VersionMajor != 3 || wrapper.data.VersionMinor < 6) return false;
            var raw = wrapper.data;
            long source = wrapper.sourceSampleTicks>0 ? wrapper.sourceSampleTicks : wrapper.ticksWhenRead;
            if(now.Ticks<source || now.Ticks-source>TimeSpan.TicksPerSecond) return true;
            if (wrapper.ticksWhenRead <= 0 || now.Ticks < wrapper.ticksWhenRead ||
                now.Ticks - wrapper.ticksWhenRead > TimeSpan.TicksPerSecond || raw.GamePaused != 0 || raw.GameInReplay != 0 ||
                raw.AllDriversOffset != R3eCarsOffset || raw.DriverDataSize != R3eDriverSize) return true;
            int reportedMode = raw.StartMode;
            if (reportedMode >= 0 && reportedMode <= 2) mode = reportedMode;
            if (raw.VehicleInfo.SlotId < 0 || raw.NumCars < 1 || raw.DriverData == null || raw.NumCars > raw.DriverData.Length) return true;
            int matches = 0;
            for (int i = 0; i < raw.NumCars; i++)
                if (raw.DriverData[i].DriverInfo.SlotId == raw.VehicleInfo.SlotId)
                {
                    matches++;
                    int reportedPhase = raw.DriverData[i].StartPhase;
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
        // Folgen auf „Rob, “ – deshalb klein, außer Substantiven (LMU 18.11.: „Rob, Ruhig durch die Formation“).
        private static readonly string[] FormationPhrasesDe = {
            "Formation. Ruhig bleiben und sauber in Position halten.",
            "konzentriert in den Start. Bleib aufmerksam, wir sind bei dir.",
            "noch Formation. Lass dir Raum und geh ohne unnötiges Risiko in den Start.",
            "ruhig durch die Formation. Wir behalten den Verkehr im Blick.",
            "Fokus auf den Start. Bleib bei deinem Rhythmus und lass dir Platz.",
            "wir sind bei dir. Formation sauber zu Ende bringen, dann geht es los."
        };
        private static readonly string[] FormationPhrasesEn = {
            "formation. Stay calm and hold your position cleanly.",
            "focused into the start. Stay alert, we're with you.",
            "still formation. Give yourself room and go into the start without needless risk.",
            "easy through the formation. We're watching the traffic.",
            "focus on the start. Keep your rhythm and leave yourself space.",
            "we're with you. Finish the formation cleanly, then we go."
        };
        private static readonly string[] FormationPhrasesEs = {
            "vuelta de formación. Tranquilo y mantén la posición limpio.",
            "concentrado para la salida. Atento, estamos contigo.",
            "todavía en formación. Déjate espacio y sal sin riesgos innecesarios.",
            "tranquilo en la formación. Vigilamos el tráfico.",
            "céntrate en la salida. Mantén tu ritmo y déjate espacio.",
            "estamos contigo. Termina bien la formación y luego empezamos."
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
                Add(calls,"engine-water",RadioVariants.Pick("engine-water",RadioLanguage.T("Wassertemperatur zu hoch. Behalt die Anzeige im Blick.","Water temperature too high. Keep an eye on the gauge.","Temperatura del agua demasiado alta. Vigila el indicador."),
                    RadioLanguage.T("Wasser wird zu heiß. Raus aus dem Windschatten.","Water is getting too hot. Get out of the tow.","El agua se calienta demasiado. Sal del rebufo."),
                    RadioLanguage.T("Warnung Kühlwasser. Kurz freie Luft suchen.","Coolant warning. Find some clean air.","Aviso de refrigerante. Busca aire limpio."),
                    RadioLanguage.T("Wassertemperatur-Warnung. Motor ein paar Kurven schonen.","Water temperature warning. Go easy on the engine for a few corners.","Aviso de temperatura del agua. Cuida el motor unas curvas.")),gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineWaterTempWarning);
            if(e.EngineOilPressureWarning && !p.EngineOilPressureWarning)
                Add(calls,"engine-oil",RadioVariants.Pick("engine-oil",RadioLanguage.T("Öldruckwarnung. Motor schonen und die Anzeige prüfen.","Oil pressure warning. Look after the engine and check the gauge.","Aviso de presión de aceite. Cuida el motor y revisa el indicador."),
                    RadioLanguage.T("Öldruck fällt ab. Drehzahl runter, früher schalten.","Oil pressure dropping. Lower revs, shift earlier.","Baja la presión de aceite. Menos vueltas, cambia antes."),
                    RadioLanguage.T("Warnung beim Öldruck. Den Motor jetzt entlasten.","Oil pressure warning. Take load off the engine now.","Aviso de presión de aceite. Alivia el motor ahora."),
                    RadioLanguage.T("Achtung Öldruck. Schonend fahren, bis die Warnung weg ist.","Watch the oil pressure. Drive gently until the warning clears.","Ojo a la presión de aceite. Suave hasta que se apague el aviso.")),gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineOilPressureWarning);
            if(e.EngineFuelPressureWarning && !p.EngineFuelPressureWarning)
                Add(calls,"engine-fuel-pressure",RadioVariants.Pick("engine-fuel-pressure",RadioLanguage.T("Warnung beim Spritdruck. Schau auf die Anzeige.","Fuel pressure warning. Check the gauge.","Aviso de presión de combustible. Mira el indicador."),
                    RadioLanguage.T("Spritdruck-Warnung. Melde dich, wenn der Motor stottert.","Fuel pressure warning. Tell us if the engine stutters.","Aviso de presión de combustible. Avisa si el motor falla."),
                    RadioLanguage.T("Achtung, Spritdruck niedrig. Behalte die Anzeige im Blick.","Careful, fuel pressure low. Keep an eye on the gauge.","Cuidado, presión de combustible baja. Vigila el indicador."),
                    RadioLanguage.T("Der Spritdruck meldet sich. Gleichmäßig fahren und beobachten.","Fuel pressure is flagging. Drive smoothly and watch it.","La presión de combustible da aviso. Conduce suave y vigílala.")),gs,now,TeamMember.Mik,7,s=>s.EngineData.EngineFuelPressureWarning);
            if(!before.PitData.CarInFrontIsPitting && gs.PitData.CarInFrontIsPitting &&
                NativeFieldFact.Capture("car_in_front_is_pitting",null,gs)==null)
                Add(calls,"opponent-pit",RadioVariants.Pick("opponent-pit",RadioLanguage.T("Dein bisheriger Vordermann ist auf dem Weg in die Box.","The car that was ahead of you is heading into the pits.","El que iba delante de ti entra en boxes."),
                    RadioLanguage.T("Der Vordermann biegt in die Box ab.","The car ahead is turning into the pits.","El de delante entra en boxes."),
                    RadioLanguage.T("Vor dir geht einer an die Box – freie Bahn.","The car ahead is pitting – clear track.","El de delante va a boxes: pista libre."),
                    RadioLanguage.T("Box für den Vordermann. Jetzt Tempo machen.","The car ahead is boxing. Push now.","Parada para el de delante. Ahora aprieta.")),gs,now,TeamMember.Ben,3,null);
            return calls;
        }
        private ObservedRaceCall Add(List<ObservedRaceCall> calls,string topic,string text,GameStateData gs,DateTime now,TeamMember member,int priority,Func<GameStateData,bool> valid)
        {
            DateTime previous;if(last.TryGetValue(topic,out previous) && now-previous<TimeSpan.FromSeconds(20))return null;
            last[topic]=now;
            // LMU Sebring 23.11.: „Rob, grün“ verfiel nach 6 s, die Formationsmeldung nach 12 s, beide hinter Saras Startmeldungen.
            var call=new ObservedRaceCall { Topic=topic,Text=text,Evidence="mapped_state_transition",Member=member,Priority=priority,
                Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=now,ExpiresUtc=now.AddSeconds(topic=="formation"?20:topic=="race-start"?12:6),AdditionalValidity=valid };
            calls.Add(call);
            return call;
        }
    }
}
