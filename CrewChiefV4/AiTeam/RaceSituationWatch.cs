using System;
using System.Collections.Generic;
using System.Linq;
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
            "Grün, das Rennen läuft. Sauber durch die ersten Kurven.",
            "Jetzt läuft das Rennen. Bleib konzentriert, wir sind bei dir.",
            "Der Start ist freigegeben. Schau nach vorn und lass dir Raum."
        };
        private static readonly string[] StartPhrasesEn = {
            "Green, race is on. Clean through the first corners.",
            "Race is on now. Stay focused, we're with you.",
            "Start is go. Eyes forward and give yourself room."
        };
        private static readonly string[] StartPhrasesEs = {
            "Verde, empieza la carrera. Limpio en las primeras curvas.",
            "Ya estamos en carrera. Concentrado, estamos contigo.",
            "Salida dada. Mira hacia delante y déjate espacio."
        };
        private static string[] StartPhrases => RadioLanguage.Pick(StartPhrasesDe, StartPhrasesEn, StartPhrasesEs);
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
            if(gs.SessionData.SessionPhase==SessionPhase.Formation)
            {
                float speed=gs.PositionAndMotionData.CarSpeed;
                if(!float.IsNaN(speed) && !float.IsInfinity(speed) && speed>=0 && speed<80 && formationCount<3 && now>=nextFormation)
                {
                    Add(calls,"formation",FormationText(driverName),gs,now,TeamMember.Ben,2,null);
                    formationCount++; nextFormation=now.AddSeconds(45+random.Next(-7,9));
                }
                return calls;
            }
            if(before==null || before.SessionData.SessionId!=gs.SessionData.SessionId || before.SessionData.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green) return calls;
            if(before.SessionData.SessionPhase==SessionPhase.Countdown || before.SessionData.SessionPhase==SessionPhase.Formation)
                Add(calls,"race-start",RadioName(driverName)+", "+StartPhrases[random.Next(StartPhrases.Length)],gs,now,TeamMember.Ben,6,null);
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
        private void Add(List<ObservedRaceCall> calls,string topic,string text,GameStateData gs,DateTime now,TeamMember member,int priority,Func<GameStateData,bool> valid)
        {
            DateTime previous;if(last.TryGetValue(topic,out previous) && now-previous<TimeSpan.FromSeconds(20))return;
            last[topic]=now;
            calls.Add(new ObservedRaceCall { Topic=topic,Text=text,Evidence="mapped_state_transition",Member=member,Priority=priority,
                Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=now,ExpiresUtc=now.AddSeconds(topic=="formation"?8:6),AdditionalValidity=valid });
        }
    }
}
