using System;
using System.Collections.Generic;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.rFactor2;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam")]
    public class WeatherWatchTests
    {
        private static readonly DateTime T=new DateTime(2026,10,31,15,0,0,DateTimeKind.Utc);
        private static GameStateData State()
        {
            var gs=new GameStateData(T.Ticks);gs.SessionData.SessionId=9;gs.SessionData.SessionType=SessionType.Practice;
            gs.SessionData.SessionPhase=SessionPhase.Green;gs.SessionData.ClassPosition=4;return gs;
        }
        private static void Live(GameStateData gs,DateTime now,float rain=0,float air=20,float track=30,ConditionsMonitor.TrackWetness wet=ConditionsMonitor.TrackWetness.Dry)
        {
            gs.Now=now;
            gs.Conditions.UpdateLiveSample(now,0,1,air,track,rain,0,0,0,0,false,ConditionsMonitor.TrackStatus.UNKNOWN,wet);
        }
        private static void Forecast(GameStateData gs,DateTime now,ConditionsMonitor.RainLevel ten,ConditionsMonitor.RainLevel thirty,float chance=-1)
        {
            gs.Now=now;gs.Conditions.RainForecastAvailable=true;gs.Conditions.RainForecastReadUtc=now;
            gs.Conditions.RainForecastSource="test confirmed game forecast";
            gs.Conditions.rainLevelNow=ConditionsMonitor.RainLevel.NONE;
            gs.Conditions.rainLevelIn10Mins=ten;gs.Conditions.rainLevelIn30Mins=thirty;
            gs.Conditions.RainChanceIn10Mins=chance;
        }

        [Test]
        public void Rain_onset_is_detected_on_the_next_mapped_frame_not_after_the_history_timer()
        {
            var gs=State();Live(gs,T);gs.Conditions.addSample(T,0,1,20,30,0,0,0,0,0,false,ConditionsMonitor.TrackStatus.UNKNOWN);
            var watch=new WeatherWatch();watch.Observe(gs,T);
            Live(gs,T.AddMilliseconds(100),.3f);watch.Observe(gs,T.AddMilliseconds(100));
            var call=watch.Take(gs,T.AddMilliseconds(100),false);
            Assert.IsNotNull(call);Assert.AreEqual("weather-rain",call.Topic);Assert.AreEqual(5,call.Priority);
            Assert.AreEqual(T.AddMilliseconds(100),call.DetectedUtc);Assert.IsTrue(call.IsValid(gs,T.AddMilliseconds(100)));
            Assert.AreEqual(0,gs.Conditions.CurrentConditions.RainDensity,"Legacy history remains at its original cadence.");
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik,call.Text));
        }

        [Test]
        public void Live_weather_does_not_grow_history_at_telemetry_frequency()
        {
            var gs=State();gs.Conditions.addSample(T,0,1,20,30,0,0,0,0,0,false,ConditionsMonitor.TrackStatus.UNKNOWN);
            for(int i=1;i<=1000;i++)Live(gs,T.AddMilliseconds(i*50),.2f);
            Assert.AreEqual(1,gs.Conditions.samples.Count);Assert.AreEqual(.2f,gs.Conditions.LatestConditions.RainDensity);
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void Unavailable_rain_does_not_become_a_dry_or_wet_claim(float rain)
        {
            var gs=State();var watch=new WeatherWatch();Live(gs,T,rain);watch.Observe(gs,T);
            Live(gs,T.AddSeconds(1),rain);watch.Observe(gs,T.AddSeconds(1));Assert.IsNull(watch.Take(gs,T.AddSeconds(1),false));
        }

        [Test]
        public void Expired_unheard_rain_onset_is_not_resurrected_as_a_new_event()
        {
            var gs=State();var watch=new WeatherWatch();Live(gs,T);watch.Observe(gs,T);
            Live(gs,T.AddSeconds(1),.3f);watch.Observe(gs,T.AddSeconds(1));
            Assert.IsNull(watch.Take(gs,T.AddSeconds(1),true));
            Live(gs,T.AddSeconds(12),.3f);watch.Observe(gs,T.AddSeconds(12));
            Assert.IsNull(watch.Take(gs,T.AddSeconds(12),false));
        }

        [Test]
        public void Weather_can_be_reported_in_the_pit_without_releasing_ordinary_race_calls()
        {
            var gs=State();gs.PitData.InPitlane=true;gs.SessionData.SessionPhase=SessionPhase.Garage;
            var watch=new WeatherWatch();Live(gs,T);watch.Observe(gs,T);Live(gs,T.AddSeconds(1),.3f);watch.Observe(gs,T.AddSeconds(1));
            var call=watch.Take(gs,T.AddSeconds(1),false);Assert.IsNotNull(call);Assert.IsTrue(call.IsValid(gs,T.AddSeconds(1)));
            call.Topic="position";Assert.IsFalse(call.IsValid(gs,T.AddSeconds(1)));
        }

        [Test]
        public void Stopped_rain_does_not_claim_a_dry_track()
        {
            var gs=State();var watch=new WeatherWatch();Live(gs,T,.4f,wet:ConditionsMonitor.TrackWetness.VeryWet);watch.Observe(gs,T);
            Live(gs,T.AddSeconds(1),0,wet:ConditionsMonitor.TrackWetness.VeryWet);watch.Observe(gs,T.AddSeconds(1));
            var call=watch.Take(gs,T.AddSeconds(1),false);Assert.IsNotNull(call);
            CollectionAssert.Contains(CrewSpeechPolicy.RainStopPhrases,call.Text);StringAssert.DoesNotContain("Strecke ist trocken",call.Text);
        }

        [Test]
        public void Forecast_requires_explicit_provenance_and_freshness_not_default_none_values()
        {
            var gs=State();Assert.IsFalse((bool)WeatherWatch.ForecastData(gs.Conditions,T)["verfuegbar"]);
            Forecast(gs,T,ConditionsMonitor.RainLevel.LIGHT,ConditionsMonitor.RainLevel.HEAVY);
            Assert.IsTrue((bool)WeatherWatch.ForecastData(gs.Conditions,T)["verfuegbar"]);
            Assert.IsFalse((bool)WeatherWatch.ForecastData(gs.Conditions,T.AddSeconds(91))["verfuegbar"]);
            Assert.IsFalse((bool)WeatherWatch.ForecastData(gs.Conditions,T.AddSeconds(-1))["verfuegbar"]);
        }

        [Test]
        public void One_available_forecast_horizon_is_kept_when_the_other_is_unknown()
        {
            var gs=State();Forecast(gs,T,ConditionsMonitor.RainLevel.LIGHT,(ConditionsMonitor.RainLevel)(-1));
            var data=WeatherWatch.ForecastData(gs.Conditions,T);
            Assert.IsTrue((bool)data["in_10_spielminuten"]["verfuegbar"]);Assert.IsFalse((bool)data["in_30_spielminuten"]["verfuegbar"]);
        }

        [Test]
        public void Probability_is_preserved_instead_of_claiming_none_means_guaranteed_no_rain()
        {
            var gs=State();Forecast(gs,T,ConditionsMonitor.RainLevel.NONE,ConditionsMonitor.RainLevel.NONE,30);
            var watch=new WeatherWatch();watch.Observe(gs,T);Assert.IsNull(watch.Take(gs,T,false));
            Forecast(gs,T.AddSeconds(1),ConditionsMonitor.RainLevel.NONE,ConditionsMonitor.RainLevel.NONE,49);
            watch.Observe(gs,T.AddSeconds(1));Assert.IsNull(watch.Take(gs,T.AddSeconds(1),false));
            Forecast(gs,T.AddSeconds(2),ConditionsMonitor.RainLevel.LIGHT,ConditionsMonitor.RainLevel.NONE,55);
            watch.Observe(gs,T.AddSeconds(2));var call=watch.Take(gs,T.AddSeconds(2),false);
            Assert.IsNotNull(call);StringAssert.Contains("55 Prozent",call.Text);StringAssert.Contains("Spielminuten",call.Text);
            Assert.AreEqual(55,(float)WeatherWatch.ForecastData(gs.Conditions,T.AddSeconds(2))["in_10_spielminuten"]["regenwahrscheinlichkeit_pct"]);
        }

        [Test]
        public void Old_forecast_call_is_cancelled_when_the_server_changes_its_prediction()
        {
            var gs=State();Forecast(gs,T,ConditionsMonitor.RainLevel.HEAVY,ConditionsMonitor.RainLevel.NONE);
            var watch=new WeatherWatch();watch.Observe(gs,T);var call=watch.Take(gs,T,false);Assert.IsNotNull(call);
            Forecast(gs,T.AddSeconds(1),ConditionsMonitor.RainLevel.NONE,ConditionsMonitor.RainLevel.NONE);
            Assert.IsFalse(call.IsValid(gs,T.AddSeconds(1)));
            gs.SessionData.SessionId++;Assert.IsFalse(call.IsValid(gs,T.AddSeconds(1)));
        }

        [Test]
        public void Significant_temperature_change_is_local_and_revalidated_before_speech()
        {
            var gs=State();var watch=new WeatherWatch();Live(gs,T);watch.Observe(gs,T);
            Live(gs,T.AddSeconds(1),air:23);watch.Observe(gs,T.AddSeconds(1));var call=watch.Take(gs,T.AddSeconds(1),false);
            Assert.IsNotNull(call);StringAssert.Contains("23",call.Text);
            Live(gs,T.AddSeconds(2),air:20);Assert.IsFalse(call.IsValid(gs,T.AddSeconds(2)));
        }

        [Test]
        public void Incomplete_rest_nodes_are_not_converted_to_a_dry_forecast()
        {
            var root=new JObject();var session=new JObject();root["RACE"]=session;
            foreach(string name in new[]{"START","NODE_25","NODE_50","NODE_75","FINISH"})
                session[name]=new JObject { ["WNV_SKY"]=new JObject { ["currentValue"]=8 } };
            Assert.IsNull(RF2WeatherForecast.parse(root,"RACE"));
            Assert.AreEqual(ConditionsMonitor.RainLevel.NONE,RF2WeatherForecast.getRainLevelAt(new List<RF2WeatherForecast.ForecastNode>(),10,60));
        }

        [Test]
        public void Future_temperature_change_is_reported_as_forecast_not_current_weather()
        {
            var gs=State();Forecast(gs,T,ConditionsMonitor.RainLevel.NONE,ConditionsMonitor.RainLevel.NONE);
            gs.Conditions.ForecastAirTempIn10Mins=20;
            var watch=new WeatherWatch();watch.Observe(gs,T);Assert.IsNull(watch.Take(gs,T,false));
            Forecast(gs,T.AddSeconds(1),ConditionsMonitor.RainLevel.NONE,ConditionsMonitor.RainLevel.NONE);
            gs.Conditions.ForecastAirTempIn10Mins=23;watch.Observe(gs,T.AddSeconds(1));
            var call=watch.Take(gs,T.AddSeconds(1),false);Assert.IsNotNull(call);
            StringAssert.Contains("Spielvorhersage",call.Text);StringAssert.Contains("23 Grad",call.Text);
            gs.Conditions.ForecastAirTempIn10Mins=18;Assert.IsFalse(call.IsValid(gs,T.AddSeconds(2)));
        }

        [Test]
        public void Coverage_tool_reports_mapped_sector_data_without_claiming_complete_sdk_coverage()
        {
            var gs=State();var car=new OpponentData {DriverRawName="Alex",IsActive=true};
            car.OpponentLapData.Add(new LapData {LapNumber=1,SectorTimes=new[]{30f,-1f,-1f}});gs.OpponentData["rival"]=car;
            var ctx=new RaceDataContext {GameState=gs,Memory=new AiRaceMemory(),NowUtc=T,LastTelemetryUtc=T};
            var data=RaceDataTools.Execute("get_data_capabilities",ctx);
            Assert.AreEqual(1,(int)data["gegner_mit_erfassten_sektoren"]);
            Assert.IsFalse((bool)data["vollstaendiger_roh_sdk_audit"]);
            Assert.IsFalse((bool)data["spiel_server_vorhersage"]["verfuegbar"]);
        }

        [Test]
        public void Pit_limit_uses_measured_units_and_overspeed_requires_continuous_confirmation()
        {
            var gs=State();gs.PitData.InPitlane=true;gs.PitData.PitSpeedLimit=80f/3.6f;
            gs.PositionAndMotionData.CarSpeed=gs.PitData.PitSpeedLimit+2;
            var watch=new PitLaneWatch();watch.Observe(gs,T);
            var reminder=watch.Take(gs,T,false,7);Assert.IsNotNull(reminder);Assert.AreEqual("pit-limit",reminder.Topic);
            reminder.MarkPlaybackStarted(T);
            watch.Observe(gs,T.AddMilliseconds(100));Assert.IsNull(watch.Take(gs,T.AddMilliseconds(100),false,7));
            watch.Observe(gs,T.AddMilliseconds(210));var call=watch.Take(gs,T.AddMilliseconds(210),false,7);
            Assert.IsNotNull(call);Assert.AreEqual(7,call.Priority);CollectionAssert.Contains(PitLaneWatch.SpeedPhrases(gs.PitData.PitSpeedLimit),call.Text);
            Assert.IsTrue(call.IsValid(gs,T.AddMilliseconds(220)));
            gs.PositionAndMotionData.CarSpeed=gs.PitData.PitSpeedLimit;Assert.IsFalse(call.IsValid(gs,T.AddMilliseconds(230)));
        }

        [Test]
        public void Unknown_pit_limit_cannot_generate_a_numeric_limit_or_speeding_claim()
        {
            var gs=State();gs.PitData.InPitlane=true;gs.PitData.PitSpeedLimit=-1;gs.PositionAndMotionData.CarSpeed=40;
            var watch=new PitLaneWatch();watch.Observe(gs,T);watch.Observe(gs,T.AddSeconds(1));var unknown=watch.Take(gs,T.AddSeconds(1),false);
            Assert.IsNotNull(unknown);CollectionAssert.Contains(PitLaneWatch.UnknownLimitPhrases,unknown.Text);Assert.IsNull(watch.Take(gs,T.AddSeconds(1),false,7));
            gs.PitData.PitSpeedLimit=60f/3.6f;gs.PositionAndMotionData.CarSpeed=0;watch.Observe(gs,T.AddSeconds(1.1));
            var call=watch.Take(gs,T.AddSeconds(1.1),false);Assert.IsNotNull(call);StringAssert.Contains("60",call.Text);
        }

        [Test]
        public void Frozen_simulation_time_cancels_old_pit_speed_warning()
        {
            var gs=State();gs.PitData.InPitlane=true;gs.PitData.PitSpeedLimit=20;gs.PositionAndMotionData.CarSpeed=25;
            var watch=new PitLaneWatch();watch.Observe(gs,T);watch.Observe(gs,T.AddSeconds(2));
            Assert.IsNull(watch.Take(gs,T.AddSeconds(2),false,7));
        }
    }
}
