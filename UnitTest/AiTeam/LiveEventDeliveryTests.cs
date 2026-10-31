using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class LiveEventDeliveryTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(SessionType type = SessionType.Race)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 41; s.SessionData.SessionType = type;
            s.SessionData.SessionPhase = SessionPhase.Green; s.SessionData.ClassPosition = 3;
            s.carClass = new CarData.CarClass(); s.PositionAndMotionData.CarSpeed = 40;
            return s;
        }
        private static RaceDataContext Context(GameStateData s, DateTime now)
        { return new RaceDataContext { GameState=s, NowUtc=now, LastTelemetryUtc=now, Memory=new AiRaceMemory() }; }
        private static AiRadioPreferences FuelOnly()
        {
            var p=new AiRadioPreferences(); foreach(var o in AiRadioPreferences.Options)p.Set(o.Id,0);
            p.Set("fuel",30);p.SetTolerance("fuel",0,0);return p;
        }
        private static ObservedRaceCall PositionCall(GameStateData s)
        {
            return new ObservedRaceCall {Topic="position",Text="Sauber vorbeigekommen. Position 3.",Priority=3,
                Position=3,SessionId=41,SessionType=s.SessionData.SessionType,DetectedUtc=T,ExpiresUtc=T.AddSeconds(8)};
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)] [TestCase(SessionType.PrivateQualify)]
        public void Similar_best_times_never_create_phantom_traffic(SessionType type)
        {
            var s=State(type);s.SessionData.PlayerLapTimeSessionBest=90;
            s.OpponentData["rival"]=new OpponentData {ClassPosition=2,CurrentBestLapTime=89,IsActive=true,CarClass=s.carClass};
            s.setPracOrQualiDeltas();Assert.AreEqual(1,s.SessionData.TimeDeltaFront);
            var director=new RaceRadioDirector();var q=new AiRadioSchedule(1);var p=FuelOnly();
            s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;q.Observe(s,T,p);
            for(int i=0;i<=310;i++)director.Observe(T.AddMilliseconds(i*100),s);
            Assert.IsFalse(director.TrafficQuiet(T.AddSeconds(31)));
            Assert.IsNotNull(q.Take(Context(s,T.AddSeconds(31)),p,director.TrafficQuiet(T.AddSeconds(31))));
        }

        [Test]
        public void Real_race_timing_gap_still_defers_optional_reports()
        {
            var s=State();s.SessionData.TimeDeltaFront=1;
            var director=new RaceRadioDirector();director.Observe(T,s);
            Assert.IsTrue(director.TrafficQuiet(T.AddSeconds(1)));
            Assert.IsFalse(director.TrafficQuiet(T.AddSeconds(9)));
        }

        [Test]
        public void Core_overtake_survives_neighbour_gap_change_during_audio_preparation()
        {
            var s=State();s.SessionData.OpponentKeyInFront="rival";s.SessionData.TimeDeltaFront=3;
            s.OpponentData["rival"]=new OpponentData {ClassPosition=2,DriverRawName="Alex",IsActive=true,CarClass=s.carClass};
            var call=PositionCall(s);string core=call.Text;
            call.IncludeNeighbour(Context(s,T),true,true);
            s.SessionData.TimeDeltaFront=3.6f;
            Assert.AreEqual(core,call.Text,"Optional gap must not extend a confirmed event's immutable speech.");
            Assert.IsTrue(call.IsValid(s,T.AddSeconds(1)));
            s.SessionData.ClassPosition=4;
            Assert.IsFalse(call.IsValid(s,T.AddSeconds(2)),"A changed core position must still cancel the old position claim.");
        }

        [Test]
        public void First_qualifying_lap_survives_ranking_updates_before_selection_and_playback()
        {
            var s=State(SessionType.Qualify);var observer=new SessionLapObserver();observer.Observe(s,null,T,true);
            s.SessionData.CompletedLaps=1;
            observer.Observe(s,new LapRecord {LapNumber=1,LapTime=92,Valid=true},T.AddSeconds(90),true);
            s.SessionData.ClassPosition=4;
            var call=observer.Take(s,T.AddSeconds(91),false);
            Assert.IsNotNull(call);StringAssert.Contains(LocalAnswers.LapTime(92),call.Text);
            StringAssert.DoesNotContain("Platz",call.Text);
            s.SessionData.ClassPosition=5;s.SessionData.PlayerClassSessionBestLapTime=88;
            Assert.IsTrue(call.IsValid(s,T.AddSeconds(92)));
            s.SessionData.CompletedLaps=2;
            Assert.IsFalse(call.IsValid(s,T.AddSeconds(93)),"The following lap must not revive an old lap announcement.");
        }

        [Test]
        public void Reservation_is_not_delivery_and_failed_render_can_retry_once()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            var first=observer.Take(s,T,false,false,false,0,null,"Rob");Assert.IsNotNull(first);
            Assert.AreEqual(DateTime.MinValue,first.DeliveryStartedUtc);
            Assert.IsNull(observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Rob"));
            first.ReleaseForRetry(T.AddMilliseconds(200),"synthetic_tts_failure");
            Assert.IsNull(observer.Take(s,T.AddSeconds(1),false,false,false,0,null,"Rob"));
            var retry=observer.Take(s,T.AddSeconds(1.3),false,false,false,0,null,"Rob");
            Assert.AreSame(first,retry);
            retry.ReleaseForRetry(T.AddSeconds(1.4),"synthetic_second_failure");
            Assert.IsTrue(retry.DeliveryFailed);
            Assert.IsNull(observer.Take(s,T.AddSeconds(3),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Expired_call_cannot_retry_after_channel_clears()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            var call=observer.Take(s,T,false,false,false,0,null,"Rob");
            call.ReleaseForRetry(T.AddSeconds(9),"synthetic_late_failure");
            Assert.IsNull(observer.Take(s,T.AddSeconds(10),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Report_interval_is_measured_from_actual_playback_start()
        {
            var s=State(SessionType.Practice);s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;
            var q=new AiRadioSchedule(1);var p=FuelOnly();q.Observe(s,T,p);
            var first=q.Take(Context(s,T.AddSeconds(30)),p,false);Assert.IsNotNull(first);
            Assert.IsNull(q.Take(Context(s,T.AddSeconds(31)),p,false),"Reserved audio must not be duplicated.");
            first.MarkPlaybackStarted(T.AddSeconds(34));
            Assert.IsNull(q.Take(Context(s,T.AddSeconds(60)),p,false),"The interval starts at hearing the report, not selecting it.");
            Assert.IsNotNull(q.Take(Context(s,T.AddSeconds(64)),p,false));
        }

        [Test]
        public void Failed_interval_report_can_retry_within_original_lifetime()
        {
            var s=State(SessionType.Practice);s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;
            var q=new AiRadioSchedule(1);var p=FuelOnly();q.Observe(s,T,p);
            var first=q.Take(Context(s,T.AddSeconds(30)),p,false);first.ReleaseForRetry(T.AddSeconds(31));
            Assert.AreSame(first,q.Take(Context(s,T.AddSeconds(32)),p,false));
            first.MarkPlaybackStarted(T.AddSeconds(33));
            Assert.IsNull(q.Take(Context(s,T.AddSeconds(40)),p,false));
        }

        [Test]
        public void Minimal_mode_does_not_retry_optional_position_praise()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            Assert.IsNull(observer.Take(s,T,false,false,true,0,null,"Rob"));
            Assert.IsNull(observer.Take(s,T.AddSeconds(1),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Confirmed_lead_in_final_lap_gets_context_without_premature_victory()
        {
            var s=State();s.SessionData.ClassPosition=1;s.SessionData.SessionNumberOfLaps=5;s.SessionData.SessionLapsRemaining=2;
            var observer=new ActiveRaceObserver();observer.Observe(s,T,"Rob",0);
            s.SessionData.SessionLapsRemaining=1;observer.Observe(s,T.AddMilliseconds(100),"Rob",0);
            var call=observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Rob");
            Assert.IsNotNull(call);StringAssert.Contains("Letzte Runde",call.Text);StringAssert.Contains("Du führst",call.Text);
            StringAssert.DoesNotContain("gewonnen",call.Text);
        }

        [Test]
        public void Timed_race_never_invents_exact_last_lap_from_numeric_lap_field()
        {
            var s=State();s.SessionData.ClassPosition=1;s.SessionData.SessionNumberOfLaps=5;
            s.SessionData.SessionLapsRemaining=1;s.SessionData.SessionHasFixedTime=true;
            var observer=new ActiveRaceObserver();observer.Observe(s,T,"Rob",0);observer.Observe(s,T.AddMilliseconds(100),"Rob",0);
            Assert.IsNull(observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Old_race_playback_callback_cannot_set_new_session_cooldown()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            var old=observer.Take(s,T,false,false,false,0,null,"Rob");
            observer.Reset();s.SessionData.SessionId=42;
            var fresh=PositionCall(s);fresh.SessionId=42;observer.Enqueue(fresh);
            old.MarkPlaybackStarted(T.AddSeconds(1));
            Assert.AreSame(fresh,observer.Take(s,T.AddSeconds(1),false,false,false,0,null,"Rob"));
            Assert.IsFalse(old.IsValid(s,T.AddSeconds(1)));
        }

        [Test]
        public void Old_lap_playback_callback_cannot_silence_new_session_lap()
        {
            var s=State(SessionType.Qualify);var observer=new SessionLapObserver();observer.Observe(s,null,T,true);
            s.SessionData.CompletedLaps=1;observer.Observe(s,new LapRecord {LapNumber=1,LapTime=90,Valid=true},T.AddSeconds(1),true);
            var old=observer.Take(s,T.AddSeconds(1),false);
            observer.Reset();var fresh=State(SessionType.Practice);fresh.SessionData.SessionId=42;
            observer.Observe(fresh,null,T.AddSeconds(2),true);fresh.SessionData.CompletedLaps=1;
            observer.Observe(fresh,new LapRecord {LapNumber=1,LapTime=92,Valid=true},T.AddSeconds(3),true);
            old.MarkPlaybackStarted(T.AddSeconds(3));
            Assert.IsNotNull(observer.Take(fresh,T.AddSeconds(3),false));
        }

        [Test]
        public void Old_report_playback_callback_cannot_move_new_session_schedule()
        {
            var s=State(SessionType.Practice);s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;
            var q=new AiRadioSchedule(1);var p=FuelOnly();q.Observe(s,T,p);
            var old=q.Take(Context(s,T.AddSeconds(30)),p,false);
            q.Reset();s.SessionData.SessionId=42;q.Observe(s,T.AddSeconds(31),p);
            old.MarkPlaybackStarted(T.AddSeconds(34));
            Assert.IsNotNull(q.Take(Context(s,T.AddSeconds(61)),p,false));
        }
    }
}
