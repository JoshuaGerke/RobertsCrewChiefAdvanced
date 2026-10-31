using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class RaceDataAccuracyTests
    {
        private static readonly DateTime T = new DateTime(2026,10,30,14,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type = SessionType.Race)
        {
            var s = new GameStateData(T.Ticks);
            s.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.AUDI_TT_CUP };
            s.SessionData.SessionType = type; s.SessionData.ClassPosition = 3;
            s.SessionData.LapsDeltaFront = s.SessionData.LapsDeltaBehind = 0;
            s.SessionData.SessionPhase = SessionPhase.Green;
            return s;
        }
        private static OpponentData Rival(GameStateData s, string key, CarData.CarClass carClass = null)
        {
            var r = new OpponentData { ClassPosition=2, IsActive=true, CarClass=carClass ?? s.carClass, DriverRawName=key };
            s.OpponentData[key]=r;
            return r;
        }
        private static RaceDataContext Context(GameStateData s)
        { return new RaceDataContext { GameState=s, NowUtc=T, LastTelemetryUtc=T, Memory=new AiRaceMemory() }; }

        [Test]
        public void Multiclass_fallback_never_takes_another_classes_same_rank()
        {
            var s=State(); Rival(s,"Wrong",new CarData.CarClass {carClassEnum=CarData.CarClassEnum.ALPINE});
            Assert.IsNull(RaceDataTools.Neighbour(s,true));
            var own=Rival(s,"Correct");Assert.AreSame(own,RaceDataTools.Neighbour(s,true));
        }
        [Test]
        public void Wrong_explicit_id_does_not_override_classification()
        {
            var s=State();Rival(s,"Wrong",new CarData.CarClass {carClassEnum=CarData.CarClassEnum.ALPINE});
            var own=Rival(s,"Correct");s.SessionData.OpponentKeyInFront="Wrong";
            Assert.AreSame(own,RaceDataTools.Neighbour(s,true));
        }
        [Test]
        public void Inactive_or_ambiguous_fallback_neighbours_remain_unknown()
        {
            var s=State();var a=Rival(s,"A");a.IsActive=false;
            Assert.IsNull(RaceDataTools.Neighbour(s,true));
            a.IsActive=true;Rival(s,"B");Assert.IsNull(RaceDataTools.Neighbour(s,true));
        }
        [Test]
        public void Missing_opponent_does_not_receive_an_unrelated_timing_number()
        {
            var s=State();s.SessionData.TimeDeltaFront=1;
            Assert.IsFalse(RaceDataTools.ReadGap(s,null,true).HasValue);
        }
        [Test]
        public void Distance_estimate_has_separate_field_and_provenance()
        {
            var s=State();var r=Rival(s,"Alex");
            s.SessionData.TrackDefinition=new TrackDefinition("Test",0,5000,new float[]{0,0},new float[]{0,0});
            s.SessionData.LapTimePrevious=100;s.PositionAndMotionData.DistanceRoundTrack=1000;r.DistanceRoundTrack=1100;
            s.SessionData.TimeDeltaFront=-1;
            var gap=RaceDataTools.ReadGap(s,r,true);
            Assert.AreEqual(RaceGapKind.TrackDistanceEstimate,gap.Kind);Assert.AreEqual(2,gap.Seconds,.001);
            var data=RaceDataTools.Execute("get_opponents_nearby",Context(s));
            Assert.IsTrue(data["vor_uns"]["abstand_s"].Type==Newtonsoft.Json.Linq.JTokenType.Null);
            Assert.AreEqual(2,(float)data["vor_uns"]["abstand_ungefaehr_s"],.001);
            Assert.IsTrue((bool)data["vor_uns"]["abstand_messung"]["geschaetzt"]);
            Assert.IsFalse((bool)data["vor_uns"]["abstand_messung"]["fuer_praezise_taktik_geeignet"]);
            var briefing=RaceDataTools.BuildBriefing(Context(s));Assert.IsNull(briefing["abstand_vorne_s"]);
        }
        [Test]
        public void Adapter_timing_is_preserved_and_marked_as_timing()
        {
            var s=State();var r=Rival(s,"Alex");s.SessionData.TimeDeltaFront=1.432f;
            var gap=RaceDataTools.ReadGap(s,r,true);
            Assert.AreEqual(RaceGapKind.SimulatorTiming,gap.Kind);Assert.AreEqual(1.432f,gap.Seconds,.0001);
            Assert.IsFalse(gap.IsEstimated);
        }
        [Test]
        public void Qualifying_comparison_is_a_best_lap_difference_not_a_track_gap()
        {
            var s=State(SessionType.Qualify);var r=Rival(s,"Alex");
            s.SessionData.PlayerLapTimeSessionBest=90;r.CurrentBestLapTime=89;s.SessionData.TimeDeltaFront=999;
            var gap=RaceDataTools.ReadGap(s,r,true);
            Assert.AreEqual(RaceGapKind.BestLapDifference,gap.Kind);Assert.IsFalse(gap.IsRaceGap);Assert.AreEqual(1,gap.Seconds);
            var data=RaceDataTools.Execute("get_opponents_nearby",Context(s));
            Assert.AreEqual(1,(float)data["vor_uns"]["bestzeitdifferenz_s"]);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,data["vor_uns"]["abstand_s"].Type);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1)]
        public void Missing_or_invalid_best_time_never_becomes_a_timing_comparison(float time)
        {
            var s=State(SessionType.Qualify);var r=Rival(s,"Alex");r.CurrentBestLapTime=time;s.SessionData.PlayerLapTimeSessionBest=90;
            Assert.IsFalse(RaceDataTools.ReadGap(s,r,true).HasValue);
        }
        [Test]
        public void A_lapped_car_is_not_presented_as_a_same_lap_seconds_gap()
        {
            var s=State();var r=Rival(s,"Alex");s.SessionData.TimeDeltaFront=3;s.SessionData.LapsDeltaFront=1;
            Assert.IsFalse(RaceDataTools.ReadGap(s,r,true).HasValue);
        }
        [Test]
        public void Null_context_produces_unavailable_instead_of_exception()
        { Assert.IsFalse((bool)RaceDataTools.Execute("get_race_state",null)["verfuegbar"]); }

        [Test]
        public void Estimated_final_lap_is_not_exported_as_confirmed()
        {
            int old=CrewChiefV4.Events.Fuel.EstimatedLapsRemaining, oldLap=CrewChiefV4.Events.Fuel.EstimatedLapsRemainingAtLap;
            try {
                var s=State();s.SessionData.SessionHasFixedTime=true;s.SessionData.CompletedLaps=9;s.SessionData.SessionTimeRemaining=60;
                CrewChiefV4.Events.Fuel.EstimatedLapsRemaining=1;CrewChiefV4.Events.Fuel.EstimatedLapsRemainingAtLap=9;
                var data=RaceDataTools.Execute("get_race_state",Context(s));
                Assert.IsFalse((bool)data["letzte_runde_des_rennens"]);Assert.IsTrue((bool)data["letzte_runde_geschaetzt"]);
                s.SessionData.IsLastLap=true;data=RaceDataTools.Execute("get_race_state",Context(s));
                Assert.IsTrue((bool)data["letzte_runde_des_rennens"]);Assert.IsFalse((bool)data["restrunden_geschaetzt"]);
            } finally {CrewChiefV4.Events.Fuel.EstimatedLapsRemaining=old;CrewChiefV4.Events.Fuel.EstimatedLapsRemainingAtLap=oldLap;}
        }
        [Test]
        public void Unknown_clock_does_not_export_zero_or_elapsed()
        {
            var s=State();s.SessionData.SessionHasFixedTime=true;s.SessionData.SessionTimeRemaining=-1;s.SessionData.SessionRunningTime=900;
            var data=RaceDataTools.Execute("get_race_state",Context(s));
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,data["restzeit_s"].Type);Assert.IsFalse((bool)data["uhr_abgelaufen"]);
        }
        [TestCase(-1)] [TestCase(5001)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void Impossible_track_coordinates_are_not_turned_into_estimates(float distance)
        {
            var s=State();var r=Rival(s,"Rival");s.SessionData.TimeDeltaFront=-1;
            s.SessionData.TrackDefinition=new TrackDefinition("Test",0,5000,new float[]{0,0},new float[]{0,0});
            s.SessionData.LapTimePrevious=100;s.PositionAndMotionData.DistanceRoundTrack=100;r.DistanceRoundTrack=distance;
            Assert.IsFalse(RaceDataTools.ReadGap(s,r,true).HasValue);
        }
        [Test]
        public void Stale_or_missing_context_never_claims_fresh_precise_timing()
        {
            var s=State();var r=Rival(s,"Rival");s.SessionData.TimeDeltaFront=2;var gap=RaceDataTools.ReadGap(s,r,true);
            var ctx=Context(s);ctx.NowUtc=T.AddSeconds(4);
            Assert.IsFalse((bool)gap.Describe(ctx)["snapshot_aktuell"]);
            Assert.IsFalse((bool)gap.Describe(ctx)["fuer_praezise_taktik_geeignet"]);
            Assert.IsFalse((bool)gap.Describe(null)["snapshot_aktuell"]);
            Assert.IsFalse((bool)RaceDataTools.BuildBriefing(null)["verfuegbar"]);
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1)]
        public void Invalid_fuel_is_not_exposed_as_available(float value)
        {
            var s=State();s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=value;
            Assert.IsNull(RaceDataTools.Execute("get_fuel_status",Context(s))["tankinhalt_l"]);
            Assert.IsNull(RaceDataTools.BuildBriefing(Context(s))["tankinhalt_l"]);
        }
        [Test]
        public void Missing_individual_wheels_and_damage_values_remain_unknown()
        {
            var s=State();s.TyreData.LeftFrontBrakeTemp=500;s.TyreData.RightFrontBrakeTemp=-1;
            s.TyreData.TyreWearActive=true;s.TyreData.FrontLeftPercentWear=float.NaN;
            var tyres=RaceDataTools.Execute("get_tyre_status",Context(s));
            Assert.AreEqual(500,(int)tyres["bremsentemperatur_c"][0]);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,tyres["bremsentemperatur_c"][1].Type);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,tyres["verschleiss_prozent"][0].Type);
            s.CarDamageData.DamageEnabled=true;s.CarDamageData.AeroDamagePercent=5;s.CarDamageData.EngineDamagePercent=-1;
            var damage=RaceDataTools.Execute("get_damage_and_engine",Context(s));
            Assert.AreEqual(5,(int)damage["schaden_prozent"]["aero"]);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,damage["schaden_prozent"]["motor"].Type);
        }

        [TestCase(59.9999f,"1:00,000")] [TestCase(119.9999f,"2:00,000")]
        [TestCase(90.123f,"1:30,123")] [TestCase(12.345f,"12,345")]
        public void Lap_time_format_rounds_before_splitting_minutes(float seconds,string expected)
        { Assert.AreEqual(expected,LocalAnswers.LapTime(seconds)); }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1)]
        public void Invalid_lap_time_has_no_invented_numeric_output(float seconds)
        { Assert.AreEqual("Zeit nicht verfügbar",LocalAnswers.LapTime(seconds)); }
    }
}
