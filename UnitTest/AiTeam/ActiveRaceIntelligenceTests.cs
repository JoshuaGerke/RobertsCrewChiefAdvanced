using System;
using System.Diagnostics;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class ActiveRaceIntelligenceTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        private static GameStateData Race(int position = 10)
        {
            var gs = new GameStateData(1);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Race;
            gs.SessionData.SessionPhase = SessionPhase.Green; gs.SessionData.ClassPosition = position;
            gs.SessionData.SessionStartClassPosition = 10; gs.SessionData.CompletedLaps = 1;
            gs.SessionData.TrackDefinition = new TrackDefinition("Test", 4000);
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 40;
            gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            gs.OpponentData["timo"] = new OpponentData { DriverRawName = "Timo Glock", ClassPosition = 9, OverallPosition = 9,
                CarNumber = "16", LastLapTime = 88.5f, CarClass = gs.carClass, CompletedLaps = 1, DistanceRoundTrack = 1010, Speed = 40 };
            return gs;
        }
        private static RaceDataContext Context(GameStateData gs) { return new RaceDataContext { GameState = gs, NowUtc = Now, LastTelemetryUtc = Now, SendDriverNames = true }; }
        private static void Change(GameStateData gs) { gs.SessionData.ClassPosition = 9; gs.OpponentData["timo"].ClassPosition = 10; gs.OpponentData["timo"].DistanceRoundTrack = 990; }
        private static void Confirm(ActiveRaceObserver d, GameStateData gs)
        { foreach (int ms in new[] {100, 200, 350}) d.Observe(gs, Now.AddMilliseconds(ms), "Rob", 120); }
        private static ObservedRaceCall Take(ActiveRaceObserver d, GameStateData gs, double seconds = .35, bool overlap = false)
        { return d.Take(gs, Now.AddSeconds(seconds), overlap, false, false, 120, null, "Rob"); }

        [TestCase("fresh", true)]
        [TestCase("names_off", false)]
        [TestCase("stale", false)]
        [TestCase("lapped", false)]
        [TestCase("pit", false)]
        [TestCase("different_key", false)]
        [TestCase("too_far", false)]
        public void Confirmed_pass_names_only_a_current_close_class_rival(string scenario, bool named)
        {
            var gs = Race();
            var observer = new ActiveRaceObserver();
            observer.Observe(gs, Now, "Rob", 120);
            Change(gs);
            Confirm(observer, gs);
            var call = Take(observer, gs);
            Assert.IsNotNull(call);
            var at = Now.AddMilliseconds(350);
            gs.SourceTelemetryUtc = scenario == "stale" ? at.AddSeconds(-2) : at;
            gs.SessionData.OpponentKeyInFront = scenario == "different_key" ? "timo" : "nick";
            gs.SessionData.LapsDeltaFront = scenario == "lapped" ? 1 : 0;
            gs.SessionData.TimeDeltaFront = scenario == "too_far" ? 2.7f : 0.7f;
            gs.OpponentData["nick"] = new OpponentData { DriverRawName = "Nick Heidfeld", CarNumber = "23",
                ClassPosition = 8, CarClass = gs.carClass, IsActive = true, InPits = scenario == "pit" };
            var context = new RaceDataContext { GameState = gs, NowUtc = at, LastTelemetryUtc = at,
                SendDriverNames = scenario != "names_off" };

            var text = call.ComposeForDelivery(context, out var check);
            Assert.AreEqual(named, text.Contains("knapp hinter Nick Heidfeld"));
            Assert.AreEqual(named, check != null);
            if (!named) return;

            Assert.IsTrue(check(gs, at));
            gs.OpponentData["nick"].CarNumber = "46";
            Assert.IsFalse(check(gs, at));
            call.ReleaseForRetry(at, "opponent_changed_during_render");
            Assert.IsTrue(call.TryReserve(at.AddSeconds(1)));
            Assert.AreEqual(call.Text, call.ComposeForDelivery(context, out var retryCheck));
            Assert.IsNull(retryCheck);
        }

        [Test]
        public void Named_opponent_fuel_never_returns_own_fuel()
        {
            var gs=Race();gs.FuelData.FuelUseActive=true;gs.FuelData.FuelLeft=42;
            Assert.IsNull(LocalAnswers.TryAnswer("Wie viel Sprit hat Timo Glock noch?",gs,null));
            Assert.IsNull(LocalAnswers.TryAnswer("Wie viel Sprit hat Max Unbekannt noch?",gs,null));
            Assert.IsNull(LocalAnswers.TryAnswer("Wie viel Sprit hat der Vordermann noch?",gs,null));
        }
        [Test]
        public void Invalid_own_lap_time_is_not_spoken()
        {
            var gs=Race();gs.SessionData.LapTimePrevious=float.NaN;
            Assert.AreEqual("Noch keine gezeitete Runde.",LocalAnswers.TryAnswer("Wie war die letzte Runde?",gs,null));
        }
        [Test]
        public void Shortening_interval_reclamps_tolerance()
        {
            var p=new AiRadioPreferences();p.Set("status",600);p.SetTolerance("status",300,300);p.Set("status",30);
            Assert.AreEqual(15,p.Minus("status"));Assert.AreEqual(15,p.Plus("status"));
            Assert.GreaterOrEqual(p.NextDelaySeconds("status",new Random(1)),15);
        }

        [Test]
        public void Named_opponent_lap_does_not_use_own_time()
        {
            var gs = Race(); gs.SessionData.LapTimePrevious = 113.192f;
            StringAssert.Contains("1:28,500", LocalAnswers.TryAnswer("Welche Rundenzeit hat Timo Glock?", gs, null));
            StringAssert.DoesNotContain("1:53", LocalAnswers.TryAnswer("Welche Rundenzeit hat Timo Glock?", gs, null));
            Assert.IsNull(LocalAnswers.TryAnswer("Welche Rundenzeit hat Max Müller?", gs, null));
        }
        [Test]
        public void Named_opponent_missing_time_never_substitutes_self()
        {
            var gs = Race(); gs.SessionData.LapTimePrevious = 99; gs.OpponentData["timo"].LastLapTime = -1;
            StringAssert.Contains("keine Rundenzeit", LocalAnswers.TryAnswer("Rundenzeit Timo Glock?", gs, null));
        }
        [Test]
        public void Ambiguous_surname_requires_clarification()
        {
            var gs = Race(); gs.OpponentData["other"] = new OpponentData { DriverRawName = "Tim Glock" };
            StringAssert.Contains("nicht eindeutig", LocalAnswers.TryAnswer("Rundenzeit Glock?", gs, null));
            Assert.AreEqual(2, (int)RaceDataTools.Execute("get_opponent", Context(gs), "{\"name\":\"Glock\"}")["trefferzahl"]);
        }
        [TestCase("{\"name\":\"Timo Glock\"}")]
        [TestCase("{\"car_number\":\"16\"}")]
        [TestCase("{\"overall_position\":9}")]
        public void Opponent_lookup_outside_nearby_field(string args)
        {
            var result = RaceDataTools.Execute("get_opponent", Context(Race(2)), args);
            Assert.IsTrue((bool)result["eindeutig"]);
            Assert.AreEqual(88.5, (double)result["fahrer"][0]["letzte_runde_s"]);
        }
        [TestCase("[]")][TestCase("{\"limit\":21}")][TestCase("{\"limit\":1.5}")]
        [TestCase("{\"offset\":-1}")][TestCase("{\"secret\":\"x\"}")][TestCase("{\"limit\":\"2\"}")]
        public void Invalid_field_arguments_rejected(string args)
        { Assert.IsNotNull(RaceDataTools.Execute("get_race_field", Context(Race()), args)["fehler"]); }
        [Test]
        public void Pagination_complete_bounded_and_private()
        {
            var gs = Race();
            for (int i = 0; i < 46; i++) gs.OpponentData["id" + i] = new OpponentData { DriverRawName = "Driver " + i, OverallPosition = i + 20 };
            var ctx = Context(gs); ctx.SendDriverNames = false;
            int total = 0; int? offset = 0;
            do {
                var page = RaceDataTools.Execute("get_race_field", ctx, "{\"offset\":" + offset + "}");
                Assert.LessOrEqual(((JArray)page["fahrer"]).Count, 20);
                StringAssert.DoesNotContain("Glock", page.ToString());
                total += ((JArray)page["fahrer"]).Count; offset = (int?)page["naechster_offset"];
            } while (offset.HasValue);
            Assert.AreEqual(47, total);
            Assert.IsNotNull(RaceDataTools.Execute("get_opponent", ctx, "{\"name\":\"Glock\"}")["fehler"]);
        }
        [Test]
        public void Field_reports_timestamp_and_staleness()
        {
            var ctx = Context(Race()); ctx.LastTelemetryUtc = Now.AddSeconds(-5);
            var result = RaceDataTools.Execute("get_race_field", ctx, "{}");
            Assert.IsTrue((bool)result["veraltet"]); Assert.AreEqual(5, (double)result["datenalter_s"]);
            Assert.IsNotNull(result["zeitpunkt_utc"]);
        }
        [Test]
        public void Confirmed_pass_in_first_lap_has_local_call_after_250ms()
        {
            var gs = Race(); gs.SessionData.CompletedLaps = 0; gs.OpponentData["timo"].CompletedLaps = 0;
            var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120); Change(gs); Confirm(d, gs);
            var call = Take(d, gs);
            Assert.IsNotNull(call); StringAssert.Contains("Sauber", call.Text); StringAssert.Contains("Position 9", call.Text);
            Assert.AreEqual("stable_rank_and_same_active_rival_crossing", call.Evidence);
            Assert.IsNull(Take(d, gs, 1));
        }
        [Test]
        public void Position_flicker_produces_no_call()
        {
            var gs = Race(); var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120);
            Change(gs); d.Observe(gs, Now.AddMilliseconds(100), "Rob", 120);
            gs.SessionData.ClassPosition = 10; d.Observe(gs, Now.AddMilliseconds(400), "Rob", 120);
            Assert.IsNull(Take(d, gs, .4));
        }
        [TestCase(true, true, 40)][TestCase(false, false, 40)][TestCase(false, true, 0)]
        public void Pit_retired_or_stopped_rival_not_praised(bool pit, bool active, int speed)
        {
            var gs = Race(); gs.OpponentData["timo"].InPits = pit; gs.OpponentData["timo"].IsActive = active;
            gs.OpponentData["timo"].Speed = speed;
            var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120); Change(gs); Confirm(d, gs);
            var call = Take(d, gs); Assert.IsNotNull(call);
            StringAssert.DoesNotContain("Sauber", call.Text); StringAssert.DoesNotContain("erkämpft", call.Text);
        }
        [Test]
        public void Verified_pit_gain_combines_only_the_same_rival_and_keeps_a_safe_retry()
        {
            var gs = Race();
            var rival = gs.OpponentData["timo"];
            gs.SessionData.OpponentKeyInFront = "timo";
            var observer = new ActiveRaceObserver();
            observer.Observe(gs, Now, "Rob", 120);
            gs.Now = gs.SourceTelemetryUtc = Now.AddMilliseconds(100);
            gs.PitData.OpponentForCarAheadPitting = rival;
            var pit = NativeFieldFact.Capture("car_in_front_is_pitting", null, gs);
            Assert.IsNotNull(pit);
            rival.InPits = true;
            Change(gs);
            Confirm(observer, gs);

            var call = Take(observer, gs);
            Assert.IsNotNull(call);
            Assert.AreEqual("same_class_front_rival_entered_pit_before_rank_gain", call.Evidence);
            var at = Now.AddMilliseconds(350);
            Assert.IsTrue(observer.CoversPit(pit, gs, at));

            var context = Context(gs);
            StringAssert.Contains("Timo Glock ist in der Box", call.ComposeForDelivery(context, out var current));
            Assert.IsTrue(current(gs, at));
            context.SendDriverNames = false;
            StringAssert.DoesNotContain("Timo", call.ComposeForDelivery(context, out _));
            Assert.AreEqual(call.Text, call.ComposeForDelivery(context, out _, standalonePitClaimed: true));
            rival.CarNumber = "99";
            Assert.IsFalse(current(gs, at));
            Assert.IsFalse(observer.CoversPit(pit, gs, at));
            rival.CarNumber = "16";
            call.ReleaseForRetry(at, "opponent_changed_during_render");
            Assert.IsTrue(call.TryReserve(at.AddSeconds(1)));
            Assert.AreEqual(call.Text, call.ComposeForDelivery(context, out var retryCheck));
            Assert.IsNull(retryCheck);
            call.MarkPlaybackStarted(at.AddSeconds(1));
            Assert.AreEqual(0, call.VariantCursor.Heard);
        }

        [Test]
        public void Overlap_defers_then_releases_without_70_second_gate()
        {
            var gs = Race(); var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120); Change(gs); Confirm(d, gs);
            Assert.IsNull(Take(d, gs, .35, true)); Assert.IsNotNull(Take(d, gs, .5));
        }
        [Test]
        public void Expired_or_changed_position_never_spoken()
        {
            var gs = Race(); var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120); Change(gs); Confirm(d, gs);
            var call = Take(d, gs); Assert.IsFalse(call.IsValid(gs, Now.AddSeconds(10)));
            gs.SessionData.ClassPosition = 11; Assert.IsFalse(call.IsValid(gs, Now.AddSeconds(1)));
        }
        [Test]
        public void Reconnect_does_not_invent_position_gain()
        {
            var gs = Race(); var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120); Change(gs);
            d.Observe(gs, Now.AddSeconds(10), "Rob", 120); Assert.IsNull(Take(d, gs, 10));
        }
        [Test]
        public void Summary_due_at_120_seconds_current_data()
        {
            var gs = Race(); var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120);
            Assert.IsNull(Take(d, gs, 119));
            var call = Take(d, gs, 120); Assert.AreEqual("summary", call.Topic);
            StringAssert.Contains("Position 10", call.Text); Assert.IsNull(Take(d, gs, 130));
        }
        [Test]
        public void Summary_cannot_claim_clean_consistency_through_pit_or_invalid_lap()
        {
            var gs = Race(7);
            var laps = Enumerable.Range(1, 3).Select(i => new LapRecord { LapNumber = i, LapTime = 90, Valid = true }).ToArray();
            StringAssert.Contains("drei sauberen", ActiveRaceObserver.Summary(gs, laps, 10, "Rob"));
            laps[1].InPitLap = true;
            var text = ActiveRaceObserver.Summary(gs, laps, 10, "Rob");
            StringAssert.DoesNotContain("sauberen", text); StringAssert.DoesNotContain("Plan", text);
            StringAssert.Contains("3 Plätze", text);
        }
        [Test]
        public void Timed_race_never_invents_remaining_laps()
        {
            var gs = Race(); gs.SessionData.SessionHasFixedTime = true; gs.SessionData.SessionNumberOfLaps = 10;
            gs.SessionData.SessionLapsRemaining = 2;
            var d = new ActiveRaceObserver(); d.Observe(gs, Now, "Rob", 120); d.Observe(gs, Now.AddSeconds(1), "Rob", 120);
            Assert.IsNull(Take(d, gs, 1));
        }
        [Test]
        public void Observer_cost_is_measured_without_network_or_audio()
        {
            var gs = Race(); var d = new ActiveRaceObserver();
            var samples = new double[10000]; var watch = new Stopwatch();
            for (int i = 0; i < samples.Length; i++) {
                watch.Restart(); d.Observe(gs, Now.AddMilliseconds(i * 50), "Rob", 120); watch.Stop();
                samples[i] = watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            TestContext.WriteLine("Observer only ms: p50=" + samples[5000] + ", p95=" + samples[9500] + ", p99=" + samples[9900]);
            Assert.Less(samples[9500], 20, "Telemetry-thread processing budget; not physical audio latency");
        }

        [Test]
        public void Ten_preferences_have_seconds_defaults_and_clamps()
        {
            var p = new AiRadioPreferences();
            Assert.AreEqual(10, AiRadioPreferences.Options.Length);
            Assert.AreEqual(120, p["status"]); Assert.AreEqual(300, p["weather"]);
            p.Set("weather", 0); Assert.AreEqual(0, p["weather"]);
            p.Set("status", -20); Assert.AreEqual(30, p["status"]);
            p.Set("fuel", 999999); Assert.AreEqual(3600, p["fuel"]);
        }
        [Test]
        public void Disabled_topics_and_safety_pause_apply_to_schedule()
        {
            var gs = Race(); gs.FuelData.FuelUseActive=true; gs.FuelData.FuelLeft=40;
            var ctx=Context(gs); var prefs=new AiRadioPreferences(); var d=new AiRadioSchedule();
            foreach(var o in AiRadioPreferences.Options.Where(o=>o.CanDisable)) prefs.Set(o.Id, 0);
            prefs.Set("fuel", 30);prefs.SetTolerance("fuel",0,0); d.Observe(gs, Now, prefs);
            ctx.NowUtc=ctx.LastTelemetryUtc=Now.AddSeconds(29); Assert.IsNull(d.Take(ctx,prefs,false));
            ctx.NowUtc=ctx.LastTelemetryUtc=Now.AddSeconds(30); Assert.IsNull(d.Take(ctx,prefs,true));
            var call=d.Take(ctx,prefs,false); Assert.AreEqual("fuel",call.Topic); StringAssert.Contains("40,0 Liter",call.Text);
            Assert.IsNull(d.Take(ctx,prefs,false));
            prefs.Set("fuel",0);ctx.NowUtc=ctx.LastTelemetryUtc=Now.AddSeconds(60); Assert.IsNull(d.Take(ctx,prefs,false));
        }
        [Test]
        public void Optional_reports_do_not_invent_missing_tyres_or_pace()
        {
            var ctx=Context(Race());
            Assert.IsNull(AiRadioSchedule.Compose("tyres",ctx)); Assert.IsNull(AiRadioSchedule.Compose("pace",ctx));
            Assert.IsNull(AiRadioSchedule.Compose("weather",ctx)); Assert.IsNull(AiRadioSchedule.Compose("stint",ctx));
        }
        [Test]
        public void Audio_history_changes_only_on_driver_acceptance_once()
        {
            int count=0; string id=AiAudioStore.Put(new byte[44],"Test",()=>true,()=>count++);
            byte[] wav; string subtitle; Assert.IsTrue(AiAudioStore.TryGet(id,out wav,out subtitle)); Assert.AreEqual(0,count);
            AiAudioStore.MarkStarted(id);AiAudioStore.MarkStarted(id);Assert.AreEqual(1,count);AiAudioStore.Remove(id);
        }
        [Test]
        public void Suppressed_audio_does_not_enter_spoken_history()
        {
            int count=0;string id=AiAudioStore.Put(new byte[44],"Old",()=>false,()=>count++);
            byte[] wav; string subtitle;Assert.IsFalse(AiAudioStore.TryGet(id,out wav,out subtitle));
            AiAudioStore.MarkStarted(id);Assert.AreEqual(0,count);
        }
        [Test]
        public void Updated_roles_have_consistent_personas()
        {
            StringAssert.Contains("Du bist BEN, Rennstratege",AiPersonas.SystemPrompt(TeamMember.Ben,"Rob",false));
            StringAssert.Contains("Du bist MIK, Renningenieur",AiPersonas.SystemPrompt(TeamMember.Mik,"Rob",false));
        }

        [Test]
        public void Optional_jitter_varies_inside_exact_window_and_can_be_zero()
        {
            var p=new AiRadioPreferences();var random=new Random(42);
            var times=Enumerable.Range(0,1000).Select(_=>p.NextDelaySeconds("status",random)).ToArray();
            Assert.AreEqual(113,times.Min());Assert.AreEqual(128,times.Max());Assert.Greater(times.Distinct().Count(),10);
            p.SetTolerance("status",0,0);Assert.AreEqual(120,p.NextDelaySeconds("status",random));
            p.SetTolerance("status",999,-1);Assert.AreEqual(60,p.Minus("status"));Assert.AreEqual(0,p.Plus("status"));
        }
        [Test]
        public void All_optional_reports_off_does_not_disable_autonomous_overtake_or_engine_warning()
        {
            var p=new AiRadioPreferences();foreach(var o in AiRadioPreferences.Options)p.Set(o.Id,0);
            var gs=Race();var d=new ActiveRaceObserver();d.Observe(gs,Now,"Rob",0);Change(gs);Confirm(d,gs);
            Assert.AreEqual("position",d.Take(gs,Now.AddSeconds(.35),false,false,false,0,null,"Rob").Topic);
            var before=Race();var after=Race();after.EngineData.EngineOilPressureWarning=true;
            var warnings=new RaceSituationWatch().Evaluate(before,after,Now);
            Assert.AreEqual("engine-oil",warnings.Single().Topic);
            var timer=new AiRadioSchedule(1);timer.Observe(gs,Now,p);var ctx=Context(gs);ctx.NowUtc=ctx.LastTelemetryUtc=Now.AddMinutes(5);
            Assert.IsNull(timer.Take(ctx,p,false));
        }
        [Test]
        public void Resolved_engine_warning_expires_without_waiting_for_periodic_status()
        {
            var before=Race();var after=Race();after.EngineData.EngineWaterTempWarning=true;
            var call=new RaceSituationWatch().Evaluate(before,after,Now).Single();
            Assert.IsTrue(call.IsValid(after,Now.AddMilliseconds(200)));
            after.EngineData.EngineWaterTempWarning=false;
            Assert.IsFalse(call.IsValid(after,Now.AddMilliseconds(300)));
        }
        [Test]
        public void Vehicle_status_uses_available_values_without_inventing_health()
        {
            var gs=Race();gs.CarDamageData.DamageEnabled=true;
            gs.EngineData.EngineWaterTemp=94;gs.TyreData.TyreWearActive=true;gs.TyreData.FrontLeftPercentWear=21;
            string text=AiRadioSchedule.VehicleReport(gs);
            StringAssert.Contains("94,0",text);StringAssert.Contains("21,0",text);
            StringAssert.Contains("fehlen Messwerte",text);StringAssert.DoesNotContain("alles",text);
        }
    }
}
