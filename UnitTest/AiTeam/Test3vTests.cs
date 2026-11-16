using System;
using System.IO;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class Test3vTests
    {
        private static readonly DateTime T=new DateTime(2026,11,15,12,0,0,DateTimeKind.Utc);
        private static GameStateData State(DateTime utc, int lap=6)
        {
            var gs=new GameStateData(utc.Ticks) { SourceTelemetryUtc=utc, carClass=new CarData.CarClass() };
            gs.SessionData.SessionId=12; gs.SessionData.SessionType=SessionType.Race; gs.SessionData.SessionPhase=SessionPhase.Green;
            gs.SessionData.CompletedLaps=lap; gs.SessionData.ClassPosition=3; gs.SessionData.SectorNumber=1;
            gs.SessionData.CurrentLapIsValid=gs.SessionData.PreviousLapWasValid=true;
            gs.PositionAndMotionData.CarSpeed=45;
            return gs;
        }

        private static void Lap(CornerCoach coach, int lap, float timingStep, float lapTime, bool green=true)
        {
            float time=100f*lap;
            for(int distance=0;distance<1000;distance+=10)
            {
                time+=distance>=300 && distance<=600?timingStep:.15f;
                coach.Tick(distance,35,.2f,0,time,false);
            }
            coach.CompleteLap(lap,lapTime,true,green);
        }

        [TestCase(false,true,90f,false)]
        [TestCase(true,false,90f,false)]
        [TestCase(true,true,91f,false)]
        [TestCase(true,true,90f,true)]
        public void Corner_follow_up_uses_heard_same_segment_two_lap_reference_and_confirmed_total_time(
            bool heard, bool green, float fullLap, bool expected)
        {
            var coach=new CornerCoach();
            coach.SetTrack(new[]{new CornerDefinition{Name="Kurve 4",Start=400,End=500}},1000);
            Lap(coach,1,.15f,89);
            Lap(coach,2,.19f,90);
            Lap(coach,3,.19f,90);
            var hint=coach.FindCoachingHint();
            Assert.IsNotNull(hint);
            var advice=new ObservedRaceCall { DetectedUtc=T,ExpiresUtc=T.AddSeconds(10),
                PlaybackCompleted=_=>coach.RememberDeliveredHint(hint) };
            Assert.IsTrue(advice.TryReserve(T));
            advice.MarkPlaybackStarted(T);
            advice.MarkPlaybackFinished(T.AddSeconds(2),!heard);
            Lap(coach,4,.15f,fullLap,green);
            var current=State(T.AddMinutes(2),4);
            var record=new LapRecord { LapNumber=4,LapTime=fullLap,Valid=true,Green=green };
            var call=coach.TakeImprovement(current,record,current.SourceTelemetryUtc);
            Assert.AreEqual(expected,call!=null);
            if(!expected) return;
            Assert.AreEqual("corner-improvement",call.Topic);
            StringAssert.Contains("Kurve 4",call.Text);
            Assert.IsTrue(call.IsValid(current,current.SourceTelemetryUtc));
            current.SessionData.CurrentLapIsValid=false;
            Assert.IsFalse(call.IsValid(current,current.SourceTelemetryUtc));
            current.SessionData.CurrentLapIsValid=true;
            current.SessionData.SectorNumber=2;
            Assert.IsFalse(call.IsValid(current,current.SourceTelemetryUtc));
            Assert.IsNull(coach.TakeImprovement(current,record,current.SourceTelemetryUtc));
        }

        [Test]
        public void Missing_segment_still_consumes_one_of_the_three_valid_follow_up_laps()
        {
            var coach=new CornerCoach();
            coach.SetTrack(new[]{new CornerDefinition{Name="Kurve 4",Start=400,End=500}},1000);
            Lap(coach,1,.15f,89); Lap(coach,2,.19f,90); Lap(coach,3,.19f,90);
            coach.RememberDeliveredHint(coach.FindCoachingHint());
            for(int lap=4;lap<=6;lap++)
                Assert.IsNull(coach.TakeImprovement(State(T,lap),new LapRecord{LapNumber=lap,Valid=true,Green=true},T));
            Lap(coach,7,.15f,90);
            Assert.IsNull(coach.TakeImprovement(State(T,7),new LapRecord{LapNumber=7,Valid=true,Green=true},T));
        }

        [Test]
        public void Completed_stint_reloads_and_is_only_returned_for_matching_vehicle_track_and_prior_session()
        {
            string root=Path.Combine(Path.GetTempPath(),"rrca-stint-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var identity=new RaceFactContext("RaceRoom","BMW M4","Monza","GP","GT3");
            string oldUid=Guid.NewGuid().ToString("N"), activeUid=Guid.NewGuid().ToString("N");
            using(var store=new StintReferenceStore(root))
            {
                for(int i=1;i<=4;i++)
                    store.Observe(identity,oldUid,new LapRecord { LapNumber=i,LapTime=89+i,FuelUsed=i==4?-1f:3f,
                        FuelAtEnd=50-i,PeakTyreTemps=i==3?new[]{92f,107f,101f,93f}:null,RecordedUtc=T.AddSeconds(i) });
                Assert.IsNull(store.Previous(identity,activeUid,0,T.AddMinutes(1)));
                store.EndStint();
                Assert.IsNull(store.Previous(identity,oldUid,0,T.AddMinutes(1)));
            }
            using(var store=new StintReferenceStore(root))
            {
                var result=store.Previous(identity,activeUid,2,T.AddMinutes(1));
                Assert.IsNotNull(result);
                Assert.AreEqual(3d,(double)result["gemessener_sprit_pro_runde_l"]);
                Assert.AreEqual(3,(int)result["sprit_messrunden"]);
                Assert.AreEqual(90d,(double)result["persoenliche_bestzeit_s"]);
                Assert.AreEqual(91.5d,(double)result["sauberer_rundenschnitt_s"]);
                Assert.AreEqual("vorn rechts",(string)result["staerkste_thermische_belastung_rad"]);
                Assert.IsNull(store.Previous(identity,activeUid,3,T.AddMinutes(1)));
                Assert.IsNull(store.Previous(new RaceFactContext("RaceRoom","BMW M4","Monza","Short","GT3"),activeUid,0,T.AddMinutes(1)));
            }
        }

        [Test]
        public void Short_stint_is_discarded_even_when_the_session_changes()
        {
            string root=Path.Combine(Path.GetTempPath(),"rrca-short-stint-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using(var store=new StintReferenceStore(root))
            {
                var identity=new RaceFactContext("AMS2","BMW M4","Monza","GP","GT3");
                string old=Guid.NewGuid().ToString("N");
                for(int i=0;i<3;i++) store.Observe(identity,old,new LapRecord {LapNumber=i+1,LapTime=92,RecordedUtc=T});
                store.EndStint();
                Assert.IsNull(store.Previous(identity,Guid.NewGuid().ToString("N"),0,T.AddSeconds(1)));
            }
        }

        [TestCase(true,true,"gain")]
        [TestCase(true,true,"loss")]
        [TestCase(true,true,"hold")]
        [TestCase(false,true,"gain")]
        [TestCase(true,false,"gain")]
        [TestCase(true,true,"late")]
        public void Pit_bill_needs_two_completed_stops_and_the_same_rival_within_three_laps(bool stopped, bool same, string outcome)
        {
            var watch=new PitCycleWatch();
            ObservedRaceCall result=null;
            GameStateData state=null;
            for(int i=0;i<25;i++)
            {
                var utc=T.AddMilliseconds(250*i);
                state=State(utc,i>=12 && outcome=="late"?10:6);
                bool ownPit=i>=4 && i<=10;
                bool otherPit=i>=12 && i<=18;
                bool opponentAhead=outcome=="loss"?i>=19:outcome=="hold" || i<19;
                state.PitData.InPitlane=ownPit;
                state.PositionAndMotionData.CarSpeed=ownPit && stopped?0:45;
                string key=opponentAhead?"rival":null;
                state.SessionData.OpponentKeyInFront=key;
                state.SessionData.OpponentKeyBehind=opponentAhead?null:"rival";
                state.SessionData.LapsDeltaFront=state.SessionData.LapsDeltaBehind=0;
                float gap=i<4?2f:1.5f;
                state.SessionData.TimeDeltaFront=state.SessionData.TimeDeltaBehind=gap;
                state.OpponentData["rival"]=new OpponentData { DriverRawName="Lara",IsActive=true,InPits=otherPit,
                    Speed=otherPit?0:45,ClassPosition=opponentAhead?2:4,CarClass=state.carClass,r3eSlotId=same || i<12?7:8 };
                var measured=new RaceGapMeasurement(gap,RaceGapKind.SimulatorTiming,utc);
                var empty=new RaceGapMeasurement();
                result=watch.Observe(new RaceDataContext {GameState=state,NowUtc=utc,LastTelemetryUtc=utc},
                    opponentAhead?measured:empty,opponentAhead?empty:measured) ?? result;
            }
            bool expected=stopped && same && outcome!="late";
            Assert.AreEqual(expected,result!=null);
            if(!expected) return;
            Assert.AreEqual(TeamMember.Ben,result.Member);
            StringAssert.Contains("Lara",result.Text);
            StringAssert.Contains(outcome=="gain"?"an ihm vorbei":outcome=="loss"?"an ihn verloren":"Platz hinter ihm bleibt",result.Text);
            Assert.IsTrue(result.IsValid(state,state.SourceTelemetryUtc));
            state.OpponentData["rival"].r3eSlotId++;
            Assert.IsFalse(result.IsValid(state,state.SourceTelemetryUtc));
        }

        [Test]
        public void All_qualifying_groups_offer_six_unique_valid_variants_and_preserve_result_semantics()
        {
            var catalog=RadioPhraseCatalog.Current;
            foreach(var group in new[]{"quali-first","quali-top3","quali-field","quali-provisional","quali-unknown",
                "corner-improvement","pit-undercut-overcut-result"}) Assert.GreaterOrEqual(catalog.Count(group),6,group); // Rob 16.11.: mehr Auswahl erlaubt
            var first=Enumerable.Range(0,6).Select(i=>QualifyingResultWatch.Compose("Rob",1,true,i)).ToArray();
            Assert.AreEqual(6,first.Distinct().Count());
            Assert.IsTrue(first.All(t=>t.Contains("Platz 1") && !t.Contains("Bestzeit")));
        }
    }
}
