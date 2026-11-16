using System;
using System.Collections.Generic;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam"),NonParallelizable]
    public class NativeFieldEventsTests
    {
        private static readonly DateTime T=new DateTime(2026,11,4,12,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type=SessionType.Race)
        {
            var s=new GameStateData(T.Ticks);s.SessionData.SessionId=9;s.SessionData.SessionType=type;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.ClassPosition=4;s.SessionData.CompletedLaps=3;s.SessionData.CurrentLapIsValid=true;
            var op=new OpponentData {DriverRawName="Nick Heidfeld",ClassPosition=3,CompletedLaps=3,InPits=true,IsActive=true};
            s.OpponentData["driver-17"]=op;s.PitData.OpponentForCarAheadPitting=op;return s;
        }
        private static NativeFieldFact Pit(GameStateData s)
        {return NativeFieldFact.Capture("car_in_front_is_pitting",null,s);}
        [Test]
        public void First_genuinely_completed_class_record_is_named_without_native_call_and_deduplicated()
        {
            var before = State();
            var after = State();
            before.carClass = after.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            var rival = after.OpponentData["driver-17"];
            var old = before.OpponentData["driver-17"];
            old.CarClass = rival.CarClass = after.carClass;
            rival.InPits = false;
            rival.IsNewLap = rival.LastLapValid = true;
            rival.CompletedLaps++;
            rival.LastLapTime = rival.CurrentBestLapTime = after.SessionData.PlayerClassSessionBestLapTime = 123.49365f;
            before.SessionData.PlayerClassSessionBestLapTime = -1;
            var events = new NativeFieldEvents();
            var memory = new AiRaceMemory();
            events.CaptureMappedFastestLap(before, after);
            events.Capture(new NativeFieldFact("fastest", "driver-17", rival.DriverRawName, null,
                "new_fastest_lap", after, rival, 123.494));
            events.Observe(after, T, memory, true);
            Assert.AreEqual(1, memory.GetEvents(TimeSpan.FromMinutes(1), T).Count);
            var call = events.Take(after, T, false, true, false);
            Assert.IsNotNull(call);
            StringAssert.Contains("Nick Heidfeld", call.Text);
            StringAssert.Contains("2:03,494", call.Text);
            after.SessionData.PlayerClassSessionBestLapTime = 122;
            Assert.IsFalse(call.IsValid(after, T));
            var historic = new NativeFieldEvents();
            rival.IsNewLap = false;
            rival.CompletedLaps = old.CompletedLaps;
            after.SessionData.PlayerClassSessionBestLapTime = rival.CurrentBestLapTime;
            historic.CaptureMappedFastestLap(before, after);
            historic.Observe(after, T, null, true);
            Assert.IsNull(historic.Take(after, T, false, true, false));
        }
        [Test]
        public void RaceRoom_shared_opponent_objects_preserve_the_completed_lap_change()
        {
            var before = State();
            var after = State();
            after.OpponentData = before.OpponentData;
            before.carClass = after.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            before.SessionData.PlayerClassSessionBestLapTime = -1;
            var rival = before.OpponentData["driver-17"];
            rival.CarClass = before.carClass;
            rival.InPits = false;
            rival.CurrentBestLapTime = -1;
            var events = new NativeFieldEvents();
            events.CaptureMappedFastestLap(null, before);

            rival.CompletedLaps++;
            rival.IsNewLap = rival.LastLapValid = true;
            rival.CurrentBestLapTime = rival.LastLapTime = after.SessionData.PlayerClassSessionBestLapTime = 124.75f;
            events.CaptureMappedFastestLap(before, after);
            events.Observe(after, T, null, true);
            var call = events.Take(after, T, false, true, false);
            Assert.IsNotNull(call);
            StringAssert.Contains("Nick Heidfeld", call.Text);
            StringAssert.Contains("2:04,750", call.Text);
            StringAssert.DoesNotContain("fehlen", call.Text, "ohne eigene Zeit kein Rückstand");
        }

        [Test]
        public void Overall_fastest_lap_can_belong_to_a_different_class()
        {
            var before = State();
            var after = State();
            after.OpponentData = before.OpponentData;
            before.carClass = after.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            before.SessionData.OverallSessionBestLapTime = 125;
            after.SessionData.OverallSessionBestLapTime = 117.5f;
            before.SessionData.PlayerClassSessionBestLapTime = after.SessionData.PlayerClassSessionBestLapTime = 125;
            after.SessionData.OverallPosition = 10;
            var rival = before.OpponentData["driver-17"];
            rival.CarClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.LMP2 };
            rival.InPits = false;
            rival.CurrentBestLapTime = 126;
            var events = new NativeFieldEvents();
            events.CaptureMappedFastestLap(null, before);

            rival.CompletedLaps++;
            rival.IsNewLap = rival.LastLapValid = true;
            rival.CurrentBestLapTime = rival.LastLapTime = 117.5f;
            events.CaptureMappedFastestLap(before, after);
            events.Observe(after, T, null, true);
            var call = events.Take(after, T, false, true, false);
            Assert.IsNotNull(call);
            Assert.AreEqual("field-fastest-overall", call.Topic);
            StringAssert.Contains("Nick Heidfeld", call.Text);
            StringAssert.Contains("1:57,500", call.Text);
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void Record_attribution_accepts_separate_timing_updates_in_either_order(bool fieldFirst, bool hasLapFlag)
        {
            var before = State();
            var intermediate = State();
            var after = State();
            intermediate.OpponentData = after.OpponentData = before.OpponentData;
            before.carClass = intermediate.carClass = after.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            before.SessionData.PlayerClassSessionBestLapTime = 126;
            intermediate.SessionData.PlayerClassSessionBestLapTime = fieldFirst ? 124 : 126;
            after.SessionData.PlayerClassSessionBestLapTime = 124;
            intermediate.Now = T.AddMilliseconds(200);
            after.Now = T.AddMilliseconds(500);
            after.SessionData.PlayerLapTimeSessionBest = 125.3f;
            var rival = before.OpponentData["driver-17"];
            rival.CarClass = before.carClass;
            rival.InPits = false;
            rival.CurrentBestLapTime = 127;
            var events = new NativeFieldEvents();
            events.CaptureMappedFastestLap(null, before);

            if (!fieldFirst) CompleteLap(rival, hasLapFlag);
            events.CaptureMappedFastestLap(before, intermediate);
            events.Observe(intermediate, intermediate.Now, null, true);
            Assert.IsNull(events.Take(intermediate, intermediate.Now, false, true, false));
            if (fieldFirst) CompleteLap(rival, hasLapFlag);
            else rival.IsNewLap = false;

            events.CaptureMappedFastestLap(intermediate, after);
            events.Observe(after, after.Now, null, true);
            var call = events.Take(after, after.Now, false, true, false);
            Assert.IsNotNull(call);
            StringAssert.Contains("Nick Heidfeld", call.Text);
            StringAssert.Contains("2:04,000", call.Text);
            StringAssert.Contains("Dir fehlen 1,3 Sekunden.", call.Text);
        }

        private static void CompleteLap(OpponentData rival, bool hasLapFlag)
        {
            rival.CompletedLaps++;
            rival.IsNewLap = hasLapFlag;
            rival.LastLapValid = true;
            rival.CurrentBestLapTime = rival.LastLapTime = 124;
        }

        [Test]
        public void Rival_enters_pits_without_a_dedicated_mapper_flag()
        {
            var before = State();
            var after = State();
            before.carClass = after.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            before.OpponentData["driver-17"].CarClass = after.OpponentData["driver-17"].CarClass = after.carClass;
            before.OpponentData["driver-17"].InPits = false;
            var events = new NativeFieldEvents();
            events.CaptureMappedPits(before, after);
            events.Observe(after, T, null, true);
            var call = events.Take(after, T, false, true, false);
            Assert.IsNotNull(call);
            StringAssert.Contains("Nick Heidfeld", call.Text);
            after.OpponentData["driver-17"].r3eSlotId = 23;
            Assert.IsFalse(call.IsValid(after, T));
        }
        [TestCase(SessionType.Race)] [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void Named_pit_event_keeps_identity_and_session_and_does_not_claim_position_gain(SessionType type)
        {
            var s=State(type);var q=new NativeFieldEvents();var m=new AiRaceMemory();q.Capture(Pit(s));q.Observe(s,T,m,true);
            var call=q.Take(s,T,false,true,false);Assert.IsNotNull(call);Assert.AreEqual(type,call.SessionType);
            StringAssert.Contains("Nick Heidfeld",call.Text);StringAssert.DoesNotContain("Platz gewonnen",call.Text);
            Assert.IsTrue(call.IsValid(s,T));Assert.AreEqual(1,m.GetEvents(TimeSpan.FromMinutes(1),T).Count);
        }
        [Test]
        public void Native_and_mapped_pit_notifications_are_one_event_not_two()
        {
            var s=State();var before=State();s.PitData.CarInFrontIsPitting=true;var q=new NativeFieldEvents();var m=new AiRaceMemory();
            q.CaptureMappedPits(before,s);q.Capture(Pit(s));q.Observe(s,T,m,true);Assert.AreEqual(1,m.GetEvents(TimeSpan.FromMinutes(1),T).Count);
            var call=q.Take(s,T,false,true,false);call.MarkPlaybackStarted(T);q.Observe(s,T.AddSeconds(.1),m,true);
            Assert.IsNull(q.Take(s,T.AddSeconds(9),false,true,false));
        }
        [Test]
        public void Captured_driver_is_not_reassigned_when_neighbor_changes_and_pit_exit_cancels_message()
        {
            var s=State();var f=Pit(s);var q=new NativeFieldEvents();q.Capture(f);q.Observe(s,T,null,true);
            s.PitData.OpponentForCarAheadPitting=new OpponentData {DriverRawName="Other"};
            var call=q.Take(s,T,false,true,false);StringAssert.Contains("Nick Heidfeld",call.Text);
            s.OpponentData["driver-17"].InPits=false;Assert.IsFalse(call.IsValid(s,T.AddSeconds(.1)));
            s.OpponentData["driver-17"].DriverRawName="Reused slot";Assert.IsFalse(NativeFieldEvents.StillCurrent(f,s));
        }
        [TestCase(true,false,false)] [TestCase(false,true,false)] [TestCase(false,false,true)]
        public void Busy_disabled_and_minimal_do_not_produce_optional_speech(bool busy,bool disabled,bool minimal)
        {var s=State();var q=new NativeFieldEvents();q.Capture(Pit(s));q.Observe(s,T,null,true);Assert.IsNull(q.Take(s,T,busy,!disabled,minimal));}
        [Test]
        public void Qualifying_push_defers_optional_news_but_outlap_can_report()
        {
            var s=State(SessionType.Qualify);s.PositionAndMotionData.CarSpeed=40;var q=new NativeFieldEvents();q.Capture(Pit(s));q.Observe(s,T,null,true);
            Assert.IsNull(q.Take(s,T,false,true,false));s.PitData.OnOutLap=true;Assert.IsNotNull(q.Take(s,T.AddSeconds(1),false,true,false));
        }
        [Test]
        public void Old_session_and_stale_incoming_events_never_enter_current_memory()
        {
            var s=State();var q=new NativeFieldEvents();var m=new AiRaceMemory();q.Capture(Pit(s));s.SessionData.SessionId++;
            q.Observe(s,T,m,true);Assert.IsNull(q.Take(s,T,false,true,false));Assert.AreEqual(0,m.GetEvents(TimeSpan.FromMinutes(1),T).Count);
            q.Capture(Pit(s));q.Observe(s,T.AddSeconds(3),m,true);Assert.AreEqual(0,m.GetEvents(TimeSpan.FromMinutes(1),T.AddSeconds(3)).Count);
        }
        [Test]
        public void No_names_setting_and_missing_identity_are_respected()
        {
            var s=State();var q=new NativeFieldEvents();q.Capture(Pit(s));q.Observe(s,T,null,false);
            StringAssert.DoesNotContain("Heidfeld",q.Take(s,T,false,true,false).Text);
            s.PitData.OpponentForCarAheadPitting=null;Assert.IsNull(Pit(s));
            Assert.IsNull(NativeFieldFact.Capture("incident_corner",null,s),"Never turn remote or heuristic incident sound paths into an invented accident ahead.");
        }
        [Test]
        public void Snapshot_is_immutable_and_queue_is_bounded()
        {
            var s=State();var f=Pit(s);s.OpponentData["driver-17"].DriverRawName="Changed";Assert.AreEqual("Nick Heidfeld",f.Name);
            var q=new NativeFieldEvents();for(int i=0;i<1000;i++)q.Capture(f);q.Observe(s,T,null,true);Assert.IsNull(q.Take(s,T,false,true,false));
        }
    }
}
