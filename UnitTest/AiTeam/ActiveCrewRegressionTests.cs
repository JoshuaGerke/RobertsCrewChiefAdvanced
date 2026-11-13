using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class ActiveCrewRegressionTests
    {
        private static readonly DateTime T = new DateTime(2026,10,27,18,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type=SessionType.Qualify, int laps=0)
        {
            var s=new GameStateData(1);
            s.SessionData.SessionId=41; s.SessionData.SessionType=type; s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.ClassPosition=3; s.SessionData.CompletedLaps=laps; s.carClass=new CarData.CarClass();
            s.PositionAndMotionData.CarSpeed=40; return s;
        }
        private static RaceDataContext Context(GameStateData gs, DateTime time)
        { return new RaceDataContext { GameState=gs, NowUtc=time, LastTelemetryUtc=time, Memory=new AiRaceMemory() }; }

        [Test]
        public void Finished_reaches_the_actual_event_dispatcher()
        {
            // No Attach/audio side effects: inspect the real event's applicability property.
            var ev=(AiTeamEvent)FormatterServices.GetUninitializedObject(typeof(AiTeamEvent));
            Assert.Contains(SessionPhase.Finished,ev.applicableSessionPhases);
            Assert.Contains(SessionType.Qualify,ev.applicableSessionTypes);
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)] [TestCase(SessionType.HotLap)]
        public void Optional_reports_work_in_nonrace_sessions_before_first_lap(SessionType type)
        {
            var gs=State(type); gs.FuelData.FuelUseActive=true; gs.FuelData.FuelLeft=40; gs.PitData.OnOutLap=true;
            var p=new AiRadioPreferences(); foreach(var o in AiRadioPreferences.Options) p.Set(o.Id,0); p.Set("fuel",30);
            var schedule=new AiRadioSchedule(1); schedule.Observe(gs,T,p);
            var call=schedule.Take(Context(gs,T.AddSeconds(60)),p,false);
            Assert.IsNotNull(call); Assert.AreEqual(type,call.SessionType); Assert.IsTrue(call.IsValid(gs,T.AddSeconds(60)));
            Assert.IsFalse(call.IsValid(gs,T.AddSeconds(67)),"stale report cannot play");
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void First_valid_lap_is_automatic_and_once(SessionType type)
        {
            var gs=State(type); var observer=new SessionLapObserver(); observer.Observe(gs,null,T,true);
            gs.SessionData.CompletedLaps=1; gs.SessionData.PlayerLapTimeSessionBest=92; gs.SessionData.PlayerClassSessionBestLapTime=90;
            observer.Observe(gs,new LapRecord { LapNumber=1,LapTime=92,Valid=true },T.AddSeconds(100),true);
            var call=observer.Take(gs,T.AddSeconds(100),false);
            Assert.IsNotNull(call); StringAssert.Contains("Erste gültige",call.Text); StringAssert.Contains(LocalAnswers.LapTime(92),call.Text);
            Assert.IsTrue(call.IsValid(gs,T.AddSeconds(101))); Assert.IsNull(observer.Take(gs,T.AddSeconds(110),false));
        }

        [TestCase(false,false)] [TestCase(true,true)]
        public void Invalid_or_pit_laps_never_receive_best_lap_praise(bool valid,bool pit)
        {
            var gs=State(); var observer=new SessionLapObserver(); observer.Observe(gs,null,T,true);
            gs.SessionData.CompletedLaps=1;
            observer.Observe(gs,new LapRecord { LapNumber=1,LapTime=90,Valid=valid,InPitLap=pit },T.AddSeconds(90),true);
            Assert.IsNull(observer.Take(gs,T.AddSeconds(90),false));
        }

        [Test]
        public void Rival_best_lap_names_only_an_identified_same_class_driver()
        {
            var gs=State(); gs.SessionData.PlayerClassSessionBestLapTime=90;
            var observer=new SessionLapObserver(); observer.Observe(gs,null,T,true);
            gs.OpponentData["car1"]=new OpponentData { DriverRawName="Alex",CarClass=gs.carClass,IsActive=true,CurrentBestLapTime=89 };
            gs.SessionData.PlayerClassSessionBestLapTime=89;
            observer.Observe(gs,null,T.AddSeconds(1),true);
            var call=observer.Take(gs,T.AddSeconds(1),false);
            Assert.IsNotNull(call); StringAssert.Contains("Alex",call.Text); StringAssert.Contains("schnellste Runde",call.Text);
        }

        [Test]
        public void Overlap_defers_lap_comment_but_stale_comment_expires()
        {
            var gs=State(); var o=new SessionLapObserver();o.Observe(gs,null,T,true);gs.SessionData.CompletedLaps=1;
            o.Observe(gs,new LapRecord {LapNumber=1,LapTime=90,Valid=true},T.AddSeconds(90),true);
            Assert.IsNull(o.Take(gs,T.AddSeconds(90),true));Assert.IsNull(o.Take(gs,T.AddSeconds(104),false));
        }

        [Test]
        public void Invalidity_observed_midlap_survives_adapter_reset_at_line()
        {
            var mem=new AiRaceMemory(); var a=State();mem.Update(null,a,T);
            var cut=State();cut.SessionData.CurrentLapIsValid=false;mem.Update(a,cut,T.AddSeconds(30));
            var line=State(SessionType.Qualify,1);line.SessionData.IsNewLap=true;line.SessionData.PreviousLapWasValid=true;line.SessionData.LapTimePrevious=90;
            mem.Update(cut,line,T.AddSeconds(90));Assert.IsFalse(mem.GetLaps().Single().Valid);
            var next=State(SessionType.Qualify,2);next.SessionData.IsNewLap=true;next.SessionData.PreviousLapWasValid=true;next.SessionData.LapTimePrevious=89;
            mem.Update(line,next,T.AddSeconds(179));Assert.IsTrue(mem.GetLaps().Last().Valid);
        }

        [Test]
        public void Confirmed_winner_is_congratulated_only_after_finished()
        {
            var w=new LapWatch();var before=State(SessionType.Race,5); before.SessionData.ClassPosition=1;
            var flag=State(SessionType.Race,5);flag.SessionData.ClassPosition=1;flag.SessionData.SessionPhase=SessionPhase.Checkered;
            string approach=w.Evaluate(before,flag,-1,4,false);StringAssert.DoesNotContain("gewonnen",approach);StringAssert.Contains("führst",approach);
            var finish=State(SessionType.Race,6);finish.SessionData.ClassPosition=1;finish.SessionData.SessionPhase=SessionPhase.Finished;
            string result=w.Evaluate(flag,finish,-1,4,false);Assert.Contains(result,CrewSpeechPolicy.FinishPhrases);
            Assert.IsNull(w.Evaluate(finish,finish,-1,4,false));
        }

        [TestCase(true,false)] [TestCase(false,true)]
        public void Dnf_or_disqualification_never_celebrates_victory(bool dnf,bool dq)
        {
            var a=State(SessionType.Race);var b=State(SessionType.Race);b.SessionData.SessionPhase=SessionPhase.Finished;b.SessionData.ClassPosition=1;
            b.SessionData.IsDNF=dnf;b.SessionData.IsDisqualified=dq;
            Assert.IsFalse(CrewSpeechPolicy.FinishPhrases.Contains(new LapWatch().Evaluate(a,b,-1,-1,false)));
        }

        [Test]
        public void Selected_voice_and_filter_are_part_of_cache_identity()
        {
            var s=new AiTeamSettings();string a=CrewSpeechPolicy.CacheKey(s,TeamMember.Sara,"Links frei.");
            s.ElevenLabsVoiceSara="new-voice";Assert.AreNotEqual(a,CrewSpeechPolicy.CacheKey(s,TeamMember.Sara,"Links frei."));
            a=CrewSpeechPolicy.CacheKey(s,TeamMember.Sara,"Links frei.");s.RadioEffect=!s.RadioEffect;
            Assert.AreNotEqual(a,CrewSpeechPolicy.CacheKey(s,TeamMember.Sara,"Links frei."));
        }

        [Test]
        public void Critical_sara_alerts_are_cache_only_and_have_two_second_lifetime()
        { Assert.IsTrue(CrewSpeechPolicy.CacheOnly(true,8,TeamMember.Sara));Assert.AreEqual(2,CrewSpeechPolicy.LifetimeSeconds(true,8)); }

        [Test]
        public void A_cleared_incident_cannot_be_announced_late()
        {
            var gs=State();gs.FlagData.distanceToNearestIncident=120;Assert.IsTrue(FlagWatch.StillRelevant("Unfall voraus!",gs));
            gs.FlagData.distanceToNearestIncident=-1;Assert.IsFalse(FlagWatch.StillRelevant("Unfall voraus!",gs));
        }

        [Test]
        public void Reserve_shortage_is_not_reported_as_inability_to_finish()
        {
            string text=new FuelWatch().Evaluate(3,20,2,10,true,false,-.2f);
            StringAssert.Contains("Reserve",text);StringAssert.DoesNotContain("Tankstopp",text);StringAssert.DoesNotContain("nicht durch",text);
        }

        [Test]
        public void Original_chief_radio_check_is_muted_but_sara_check_is_allowed()
        { Assert.IsFalse(CrewChiefVoiceFilter.Allow("test",new[]{"radio_check/test"}));Assert.IsTrue(CrewChiefVoiceFilter.Allow("test",new[]{"radio_check_Sara/test"})); }

        [TestCase(true,true,true)] [TestCase(false,false,true)] [TestCase(true,false,false)] [TestCase(false,true,false)] [TestCase(false,false,false)]
        public void Members_can_be_selected_independently(bool ben,bool mik,bool sara)
        {
            var s=new AiTeamSettings { BenEnabled=ben,MikEnabled=mik,SaraEnabled=sara };
            Assert.AreEqual(ben,s.IsMemberEnabled(TeamMember.Ben));Assert.AreEqual(mik,s.IsMemberEnabled(TeamMember.Mik));Assert.AreEqual(sara,s.IsMemberEnabled(TeamMember.Sara));
            s.Enabled=false;foreach(TeamMember m in Enum.GetValues(typeof(TeamMember)))Assert.IsFalse(s.IsMemberEnabled(m));
        }

        [Test]
        public async Task Disabled_member_does_not_start_a_question_or_speech_request()
        {
            var oldFilter=CrewChiefV4.Audio.AudioPlayer.messageFilter;
            bool oldSpotterMute=CrewChiefV4.Audio.AudioPlayer.aiTeamSpotterMuted;
            var service=AiTeamService.Instance;var s=service.Settings;bool was=s.BenEnabled;bool wasEnabled=s.Enabled;
            try { s.Enabled=true;s.BenEnabled=false;string reply=await service.AskAsync("Ben, wie ist unsere Strategie?");StringAssert.Contains("deaktiviert",reply);
                await service.SpeakAsync(TeamMember.Ben,"This must never be synthesized",true,CancellationToken.None); }
            finally {s.Enabled=wasEnabled;s.BenEnabled=was;CrewChiefV4.Audio.AudioPlayer.messageFilter=oldFilter;CrewChiefV4.Audio.AudioPlayer.aiTeamSpotterMuted=oldSpotterMute;}
        }
    }
}
