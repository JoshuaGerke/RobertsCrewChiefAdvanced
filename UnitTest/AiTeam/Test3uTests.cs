using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class Test3uTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(DateTime now, int lap = 2, SessionType type = SessionType.Race)
        {
            var state = new GameStateData(now.Ticks) { SourceTelemetryUtc=now };
            var session = state.SessionData;
            session.SessionId=7; session.SessionType=type; session.SessionPhase=SessionPhase.Green;
            session.ClassPosition=4; session.CompletedLaps=lap; session.CurrentLapIsValid=true;
            session.TrackDefinition=new TrackDefinition("Test",0,5000,new float[0],new float[0]);
            state.carClass=new CarData.CarClass();
            return state;
        }

        private static RaceDataContext Context(GameStateData state, DateTime now, TrackAwareness tracks=null) =>
            new RaceDataContext { GameState=state, NowUtc=now, LastTelemetryUtc=state.SourceTelemetryUtc, Tracks=tracks, SendDriverNames=true };

        [TestCase("iRacing", 1)]
        [TestCase("AMS2", 2)]
        [TestCase("LMU", 1)]
        [TestCase("RaceRoom", 2)]
        [TestCase("AC", 1)]
        [TestCase("AC EVO", 2)]
        [TestCase("AC EVO estimated track", 1)]
        public void Common_mapped_split_contract_announces_personal_sector_on_the_sector_boundary(string sim, int sector)
        {
            var before = State(T);
            before.SessionData.SectorNumber=sector;
            before.SessionData.PlayerBestSector1Time=before.SessionData.PlayerBestSector2Time=30;
            var now=T.AddMilliseconds(70);
            var state = State(now);
            state.SessionData.SectorNumber=sector+1;
            state.SessionData.SectorTimingEstimated=sim.EndsWith("estimated track",StringComparison.Ordinal);
            state.SessionData.PlayerLapData.Add(new LapData { LapNumber=3,IsValid=true,SectorTimes=new[] {30f,-1f,-1f} });
            state.SessionData.playerAddCumulativeSectorData(sector,4,sector==1?29.4f:59.4f,100,true,false,20,15);
            var observer = new SessionLapObserver();
            observer.Observe(before,null,T,false);
            observer.Observe(state,null,now,false,before);
            var call=observer.Take(state,now,false);
            if (state.SessionData.SectorTimingEstimated) { Assert.IsNull(call,sim); return; }
            Assert.IsNotNull(call, sim);
            Assert.AreEqual("sector-best", call.Topic);
            StringAssert.Contains("29,4",call.Text);
            state.PitData.OnInLap=true;
            Assert.IsFalse(call.IsValid(state,now));
        }

        [Test]
        public void Fresh_global_packets_do_not_keep_a_frozen_rivals_interval_current()
        {
            var tracks=new TrackAwareness();
            for(int i=0;i<=12;i++)
            {
                var time=T.AddMilliseconds(i*100);
                var state=State(time);
                state.SessionData.OpponentKeyBehind="rival"; state.SessionData.TimeDeltaBehind=.8f;
                state.SessionData.LapsDeltaBehind=0;
                state.PositionAndMotionData.DistanceRoundTrack=200+i*5;
                var car=new OpponentData { DriverRawName="Lara",ClassPosition=5,CompletedLaps=2,DistanceRoundTrack=170,
                    IsActive=true,CarClass=state.carClass };
                state.OpponentData["rival"]=car;
                Assert.IsTrue(tracks.Observe(state,time));
                Assert.AreEqual(i<=10,RaceDataTools.ReadGap(Context(state,time,tracks),car,false).HasValue);
                if(i==12)
                {
                    var next=time.AddMilliseconds(100);
                    state.SourceTelemetryUtc=next;
                    car.DistanceRoundTrack=175;
                    Assert.IsTrue(tracks.Observe(state,next));
                    Assert.IsTrue(RaceDataTools.ReadGap(Context(state,next,tracks),car,false).HasValue);
                }
            }
        }

        [TestCase(false, true)] [TestCase(true, false)]
        public void Stint_feedback_needs_four_consecutive_clean_laps_and_low_spread(bool limitsWarning, bool expected)
        {
            var watch=new RaceFeedbackWatch();
            var before=State(T,0);
            ObservedRaceCall found=null;
            for(int second=0;second<=360;second++)
            {
                int i=second/90;
                var now=T.AddSeconds(second);
                var state=State(now,i); state.SessionData.IsNewLap=second%90==0;
                state.PenaltiesData.CutTrackWarnings=limitsWarning && i>=2?1:0;
                found=watch.Observe(Context(state,now),before,new LapRecord {LapNumber=i,LapTime=90f+(i%2)*.08f,Valid=true},
                    new RaceGapMeasurement(),false,"Rob");
                before=state;
            }
            Assert.AreEqual(expected,found!=null);
            if (expected)
            {
                Assert.AreEqual("stint-consistency", found.Topic);
                Assert.AreEqual(TeamMember.Mik, found.Member);
                Assert.IsTrue(found.IsValid(before, before.Now));
            }
        }

        [TestCase(true, false, .93f, "Sparziel erreicht")]
        [TestCase(true, false, 1f, "über dem Sparziel")]
        [TestCase(false, false, .93f, null)]
        [TestCase(true, true, .93f, null)]
        public void Fuel_review_uses_two_full_green_laps_after_delivered_advice(bool spoken, bool brokenStint, float fuelPerLap, string message)
        {
            var watch = new RaceFeedbackWatch();
            var before = State(T, 0);
            ObservedRaceCall review = null;
            for (int second = 0; second <= 270; second++)
            {
                int lap = second / 90;
                var now = T.AddSeconds(second);
                var state = State(now, lap);
                state.SessionData.IsNewLap = second % 90 == 0;
                state.PitData.InPitlane = brokenStint && second == 120;
                state.FuelData.FuelUseActive = true;
                var result = watch.Observe(Context(state, now), before, new LapRecord { LapNumber=lap, LapTime=90,
                    FuelUsed=lap == 1 ? 3 : fuelPerLap, Valid=true }, new RaceGapMeasurement(), false, "Rob");
                if (second == 30 && spoken)
                    watch.RememberDeliveredFuelPlan(state, .95f);
                if (result?.Topic == "fuel-saving-review")
                {
                    Assert.AreEqual(270, second);
                    Assert.IsTrue(result.IsValid(state, now));
                    review = result;
                }
                before = state;
            }
            if (message == null) Assert.IsNull(review);
            else
            {
                Assert.IsNotNull(review);
                StringAssert.Contains(message, review.Text);
                Assert.AreEqual(TeamMember.Ben, review.Member);
            }
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Chase_feedback_uses_two_full_green_laps_with_the_same_rival(bool swap, bool expected)
        {
            var watch = new RaceFeedbackWatch();
            GameStateData before = State(T, 0);
            ObservedRaceCall review = null;
            for (int second = 0; second <= 270; second++)
            {
                int lap = second / 90;
                var now = T.AddSeconds(second);
                var state = State(now, lap);
                state.SessionData.IsNewLap = second % 90 == 0;
                state.SessionData.OpponentKeyInFront = "rival";
                state.SessionData.TimeDeltaFront = second <= 180 ? 3 : 2;
                state.SessionData.LapsDeltaFront = 0;
                var rival = new OpponentData { DriverRawName="Lara", IsActive=true, CanUseName=true,
                    CarClass=state.carClass, ClassPosition=3, r3eSlotId=swap && second >= 200 ? 9 : 5 };
                state.OpponentData["rival"] = rival;
                string id = RaceReader.Identity("rival", rival);
                var context = Context(state, now);
                if (second == 20) watch.RememberDeliveredChasePlan(context, id);
                var result = watch.Observe(context, before, new LapRecord { LapNumber=lap, LapTime=90,
                    FrontIdentity=id, GapAhead=state.SessionData.TimeDeltaFront, GapAheadKind=RaceGapKind.SimulatorTiming,
                    Valid=true }, new RaceGapMeasurement(), false, "Rob");
                if (result?.Topic == "race-chase-review")
                {
                    Assert.AreEqual(270, second);
                    Assert.IsTrue(result.IsValid(state, now));
                    review = result;
                }
                before = state;
            }
            Assert.AreEqual(expected, review != null);
            if (expected) StringAssert.Contains("Auf Lara hast du", review.Text);
        }

        [Test]
        public void Only_measured_fuel_saving_advice_can_arm_a_follow_up()
        {
            var watch = new FuelWatch();
            Assert.IsNotNull(watch.Evaluate(3, 9.8f, 1, 10, true, false, out float target));
            Assert.AreEqual(.95f, target, .0001f);
            watch.Reset();
            Assert.IsNotNull(watch.Evaluate(3, 8, 1, 10, true, false, out target));
            Assert.AreEqual(-1, target);
            watch.Reset();
            Assert.IsNotNull(watch.Evaluate(3, 9.8f, 1, 10, true, false, out target, provisionalConsumption: true));
            Assert.AreEqual(-1, target);
        }

        [TestCase(false,true)] [TestCase(true,false)]
        public void Defense_requires_same_close_class_car_through_three_full_laps(bool replaceDriver, bool expected)
        {
            var watch=new RaceFeedbackWatch();
            var before=State(T,0);
            ObservedRaceCall found=null;
            for(int second=0;second<=270;second++)
            {
                int i=second/90;
                var now=T.AddSeconds(second);
                var state=State(now,i); state.SessionData.IsNewLap=second%90==0; state.SessionData.OpponentKeyBehind="rival";
                state.SessionData.TimeDeltaBehind=.7f;
                state.OpponentData["rival"]=new OpponentData {IsActive=true,DriverRawName="Lara",ClassPosition=5,
                    CompletedLaps=i,CarClass=state.carClass,r3eSlotId=replaceDriver && i>1?2:1};
                found=watch.Observe(Context(state,now),before,new LapRecord {LapNumber=i,LapTime=90,Valid=true},
                    new RaceGapMeasurement(.7f,RaceGapKind.SimulatorTiming),true,"Rob");
                before=state;
            }
            Assert.AreEqual(expected,found!=null);
            if(expected){Assert.AreEqual("race-defense-hold",found.Topic);Assert.AreEqual(TeamMember.Ben,found.Member);}
        }

        [Test]
        public void Completed_audio_shuffles_per_group_and_deferral_does_not_consume_a_variant()
        {
            var cursor=new RadioVariantCursor();
            var recently=new Queue<int>();
            for(int i=0;i<24;i++)
            {
                int pick=cursor.Select("race-last",6);
                Assert.AreEqual(pick,cursor.Select("race-last",6),"Recomposition before playback should keep the chosen variant.");
                CollectionAssert.DoesNotContain(recently,pick);
                cursor.OnPlaybackCompleted("race-last",pick);
                recently.Enqueue(pick); if (recently.Count>3) recently.Dequeue();
            }
            Assert.AreEqual(24,cursor.Heard);
        }

        [Test]
        public void Deferred_status_keeps_event_id_refreshes_rank_and_does_not_exhaust_attempts()
        {
            var state=State(T);
            var call=new ObservedRaceCall { Topic="status",RaceStatus=true,SessionId=7,Position=4,Text="Platz 4.",
                DetectedUtc=T,ExpiresUtc=T.AddSeconds(10) };
            var eventId=call.EventId;
            for(int i=0;i<5;i++)
            {
                Assert.IsTrue(call.TryReserve(T.AddSeconds(i)));
                call.DeferDelivery(T.AddSeconds(i),"driver_workload");
            }
            var now=T.AddSeconds(5);
            state.SourceTelemetryUtc=now;
            state.SessionData.ClassPosition=3;
            Assert.IsTrue(call.TryReserve(now));
            Assert.IsTrue(call.RefreshBeforeSpeak(Context(state,now)));
            Assert.AreEqual(eventId,call.EventId);
            StringAssert.Contains("Platz 3",call.Text);
            Assert.IsFalse(call.DeliveryFailed);
        }

        [Test]
        public void Rear_pressure_refreshes_the_same_opponents_interval_and_drops_after_box_entry()
        {
            var observer=new ActiveRaceObserver();
            var state=State(T,3);
            state.SessionData.OpponentKeyBehind="rival";
            state.SessionData.LapsDeltaBehind=0;
            var rival=new OpponentData {IsActive=true,DriverRawName="Lara",ClassPosition=5,CompletedLaps=3,CarClass=state.carClass};
            state.OpponentData["rival"]=rival;
            observer.Observe(state,T,"Rob",0);
            state.SessionData.TimeDeltaBehind=2.9f;
            observer.ObserveRearPressure(Context(state,T),new RaceGapMeasurement(2.9f,RaceGapKind.SimulatorTiming));
            var next=T.AddSeconds(2);
            state.SourceTelemetryUtc=next;
            state.SessionData.TimeDeltaBehind=2.1f;
            observer.ObserveRearPressure(Context(state,next),new RaceGapMeasurement(2.1f,RaceGapKind.SimulatorTiming));
            var call=observer.Take(state,next,false,false,false,0,null,"Rob");
            Assert.IsNotNull(call);
            call.DeferDelivery(next,"driver_workload");
            next=next.AddMilliseconds(500);
            state.SourceTelemetryUtc=next;
            state.SessionData.TimeDeltaBehind=1.9f;
            Assert.AreSame(call,observer.Take(state,next,false,false,false,0,null,"Rob"));
            Assert.IsTrue(call.RefreshBeforeSpeak(Context(state,next)));
            Assert.AreEqual(1.9f,call.SpokenGapS);
            StringAssert.Contains("Lara",call.Text);
            rival.InPits=true;
            Assert.IsFalse(call.RefreshBeforeSpeak(Context(state,next)));
            Assert.IsFalse(call.IsValid(state,next));
        }

        [Test]
        public void Targeted_catalog_groups_have_six_templates_and_prompt_openings_cycle_through_four()
        {
            foreach(string group in new[] {"gap-front-measured","gap-rear-measured","own-sector-best","own-best-pace",
                "race-last","race-closing","race-rear-pressure","race-class-best","race-overall-best","race-position-loss",
                "lap-first-valid","lap-near-best","quali-sector-comparison","stint-consistency","race-defense-hold"})
                Assert.AreEqual(6,RadioPhraseCatalog.Current.Count(group),group);
            var openings=Enumerable.Range(0,4).Select(AiTeamService.FreeAnswerOpening).ToArray();
            Assert.AreEqual(4,openings.Distinct().Count());
            Assert.AreEqual(openings[0],AiTeamService.FreeAnswerOpening(4));
        }
    }
}
