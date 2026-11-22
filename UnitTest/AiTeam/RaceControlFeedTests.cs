using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using CrewChiefV4.RaceRoom;
using CrewChiefV4.RaceRoom.RaceRoomData;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RaceControlFeedTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

        private static GameStateData State(SessionPhase phase = SessionPhase.Green, int session = 3)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = session; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = phase;
            gs.SessionData.ClassPosition = 5; gs.SessionData.CompletedLaps = 2;
            return gs;
        }

        private static List<RaceControlEvent> Run<TIn>(Action<TIn, DateTime, List<RaceControlEvent>> step, TIn input, int ms)
        {
            var into = new List<RaceControlEvent>(); step(input, T.AddMilliseconds(ms), into); return into;
        }

        [Test]
        public void RaceRoom_36_fields_sit_where_the_reserved_35_fields_were()
        {
            Assert.AreEqual(Marshal.OffsetOf(typeof(RaceRoomShared), "MaxIncidentPoints").ToInt32() + 4, Marshal.OffsetOf(typeof(RaceRoomShared), "StartMode").ToInt32());
            Assert.AreEqual(Marshal.OffsetOf(typeof(RaceRoomShared), "StartMode").ToInt32() + 4, Marshal.OffsetOf(typeof(RaceRoomShared), "StartGroupCount").ToInt32());
            Assert.AreEqual(Marshal.OffsetOf(typeof(DriverData), "Orientation").ToInt32() + 12, Marshal.OffsetOf(typeof(DriverData), "StartGroup").ToInt32());
            Assert.AreEqual(Marshal.OffsetOf(typeof(DriverData), "FormationSlot").ToInt32() + 4, Marshal.SizeOf(typeof(DriverData)));
            int tail = Marshal.OffsetOf(typeof(RaceRoomShared), "DriverData").ToInt32() + 128 * Marshal.SizeOf(typeof(DriverData));
            Assert.AreEqual(tail, Marshal.OffsetOf(typeof(RaceRoomShared), "FormationSpeedLimit").ToInt32());
            Assert.AreEqual(tail + 7 * 4, Marshal.SizeOf(typeof(RaceRoomShared)));
        }

        [Test]
        public void RaceRoom_35_map_is_padded_with_zeroes_instead_of_failing()
        {
            var target = Enumerable.Repeat((byte)0xEE, 10).ToArray();
            R3ESharedMemoryReader.CopyPadded(new byte[] { 1, 2, 3 }, target);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0, 0, 0 }, target);
            R3ESharedMemoryReader.CopyPadded(null, target);
            Assert.IsTrue(target.All(b => b == 0));
        }

        [Test]
        public void RaceRoom_start_offence_is_reported_once_and_only_from_36()
        {
            var d = new R3ERaceControlDetector();
            var s = new R3ERaceControlDetector.Input { Valid = true, Race = true, StartPhase = 0, FormationSide = -1, PitlaneStartDelay = -1 };
            Assert.IsEmpty(Run<R3ERaceControlDetector.Input>(d.Step, s, 0));
            s.StartPenaltyPending = 1;
            var e = Run<R3ERaceControlDetector.Input>(d.Step, s, 50).Single();
            Assert.AreEqual("start-jump", e.Code); Assert.AreEqual(RaceControlKind.StartOffence, e.Kind); Assert.IsTrue(e.IsPlayer);
            Assert.IsEmpty(Run<R3ERaceControlDetector.Input>(d.Step, s, 100));
            s.StartPenaltyPending = 2;
            Assert.AreEqual("start-side", Run<R3ERaceControlDetector.Input>(d.Step, s, 150).Single().Code);

            var old = new R3ERaceControlDetector();
            var v35 = new R3ERaceControlDetector.Input { Valid = false, Race = true };
            Assert.IsEmpty(Run<R3ERaceControlDetector.Input>(old.Step, v35, 0));
            v35.StartPenaltyPending = 1;
            Assert.IsEmpty(Run<R3ERaceControlDetector.Input>(old.Step, v35, 50));
        }

        [Test]
        public void RaceRoom_formation_side_is_said_once_while_the_group_waits()
        {
            var d = new R3ERaceControlDetector();
            var s = new R3ERaceControlDetector.Input { Valid = true, Race = true, StartPhase = 0, FormationSide = 1, PitlaneStartDelay = -1 };
            Assert.IsEmpty(Run<R3ERaceControlDetector.Input>(d.Step, s, 0));
            var e = Run<R3ERaceControlDetector.Input>(d.Step, s, 50).Single();
            Assert.AreEqual("formation-side", e.Code); Assert.AreEqual("rechts", e.Reason);
            Assert.IsEmpty(Run<R3ERaceControlDetector.Input>(d.Step, s, 100));
            StringAssert.Contains(RadioLanguage.T("recht", "right", "derech"), RaceControlRadio.Call(e, null).Text);
        }

        [TestCase(1, true, "neben_der_strecke")]
        [TestCase(1, false, "1x")]
        [TestCase(2, false, "kontrollverlust")]
        [TestCase(4, true, "schwerer_kontakt")]
        [TestCase(3, false, "3x")]
        public void IRacing_incident_type_follows_the_points_and_the_surface(int delta, bool offTrack, string expected)
        {
            Assert.AreEqual(expected, IRacingRaceControlDetector.IncidentReason(delta, offTrack));
        }

        [Test]
        public void IRacing_incident_delta_carries_count_limit_and_what_is_left()
        {
            var d = new IRacingRaceControlDetector();
            var s = new IRacingRaceControlDetector.Input { Valid = true, Race = true, Incidents = 5, Limit = 17, FastRepairUsed = 0, FastRepairAvailable = 1 };
            Assert.IsEmpty(Run<IRacingRaceControlDetector.Input>(d.Step, s, 0));
            s.OffTrack = true;
            Run<IRacingRaceControlDetector.Input>(d.Step, s, 50);
            s.OffTrack = false; s.Incidents = 6;
            var e = Run<IRacingRaceControlDetector.Input>(d.Step, s, 1000).Single();
            Assert.AreEqual("inc", e.Code); Assert.AreEqual("neben_der_strecke", e.Reason);
            Assert.AreEqual(6, e.Count); Assert.AreEqual(17, e.Limit); Assert.AreEqual(11, e.Remaining); Assert.AreEqual(1, e.Value);
            Assert.IsNull(RaceControlRadio.Call(e, null), "Vorfallpunkte meldet bereits die Punktebeobachtung");
            s.FastRepairUsed = 1; s.FastRepairAvailable = 0;
            var r = Run<IRacingRaceControlDetector.Input>(d.Step, s, 1100).Single();
            Assert.AreEqual("fast-repair", r.Code); Assert.AreEqual(1, r.Count); Assert.AreEqual(0, r.Remaining);
        }

        [Test]
        public void IRacing_flags_report_rising_edges_only()
        {
            var d = new IRacingRaceControlDetector();
            var s = new IRacingRaceControlDetector.Input { Valid = true, Race = true, Flags = IRacingRaceControlDetector.Repair };
            Assert.IsEmpty(Run<IRacingRaceControlDetector.Input>(d.Step, s, 0), "beim Verbinden gesetzte Flaggen sind Ausgangsstand");
            s.Flags |= IRacingRaceControlDetector.Disqualify;
            var e = Run<IRacingRaceControlDetector.Input>(d.Step, s, 50).Single();
            Assert.AreEqual(RaceControlKind.Disqualified, e.Kind);
            Assert.AreEqual(9, RaceControlRadio.Call(e, null).Priority);
            Assert.IsEmpty(Run<IRacingRaceControlDetector.Input>(d.Step, s, 100));
            s.Flags |= IRacingRaceControlDetector.OneLapToGreen;
            Assert.AreEqual("one-to-green", Run<IRacingRaceControlDetector.Input>(d.Step, s, 150).Single().Code);
        }

        [Test]
        public void AcEvo_cut_gain_is_told_once_with_the_deadline_and_cleared_when_given_back()
        {
            var d = new AceRaceControlDetector();
            var s = new AceRaceControlDetector.Input { Valid = true };
            Assert.IsEmpty(Run<AceRaceControlDetector.Input>(d.Step, s, 0));
            s.CutDelta = 0.83f; s.DistanceToDeadline = 420;
            var e = Run<AceRaceControlDetector.Input>(d.Step, s, 50).Single();
            Assert.AreEqual("cut-giveback", e.Code); Assert.AreEqual(RaceControlKind.TrackLimit, e.Kind);
            Assert.AreEqual(0.8, e.Value, 1e-9); Assert.AreEqual(420, e.Remaining);
            StringAssert.Contains("420", RaceControlRadio.Call(e, null).Text);
            s.CutDelta = 0.5f; s.DistanceToDeadline = 200;
            Assert.IsEmpty(Run<AceRaceControlDetector.Input>(d.Step, s, 100), "derselbe Vorgang");
            s.CutDelta = 0; s.DistanceToDeadline = 0;
            Assert.IsEmpty(Run<AceRaceControlDetector.Input>(d.Step, s, 150));
            s.CutDelta = 0.4f; s.DistanceToDeadline = 380;
            Assert.AreEqual("cut-giveback", Run<AceRaceControlDetector.Input>(d.Step, s, 200).Single().Code, "neuer Vorgang");
        }

        [Test]
        public void AcEvo_penalty_time_input_lock_and_flags()
        {
            var d = new AceRaceControlDetector();
            var s = new AceRaceControlDetector.Input { Valid = true };
            Run<AceRaceControlDetector.Input>(d.Step, s, 0);
            s.CutPenaltyMs = 2500; s.ControlLock = 3f; s.GlobalFlag = AceRaceControlDetector.RedFlag; s.Flag = AceRaceControlDetector.OrangeCircleFlag;
            var codes = Run<AceRaceControlDetector.Input>(d.Step, s, 50).Select(e => e.Code).ToArray();
            CollectionAssert.AreEquivalent(new[] { "cut-penalty", "input-lock", "red", "meatball" }, codes);
            Assert.IsEmpty(Run<AceRaceControlDetector.Input>(d.Step, s, 100));
        }

        [Test]
        public void Ams2_reason_for_the_yellow_and_own_crash_state()
        {
            var d = new Ams2RaceControlDetector();
            var s = new Ams2RaceControlDetector.Input { Valid = true };
            Run<Ams2RaceControlDetector.Input>(d.Step, s, 0);
            s.FlagReason = 2; s.CrashState = 3;
            var list = Run<Ams2RaceControlDetector.Input>(d.Step, s, 50);
            Assert.AreEqual("kollision", list.Single(e => e.Code == "yellow-reason").Reason);
            Assert.AreEqual("dreher", list.Single(e => e.Code == "crash-state").Reason);
        }

        [Test]
        public void Lmu_rival_penalty_is_recorded_and_only_told_for_a_direct_neighbour()
        {
            var d = new LmuRaceControlDetector();
            var cars = new[] { new LmuRaceControlDetector.CarInput { Id = 4, Name = "Opponent", Penalties = 0 },
                new LmuRaceControlDetector.CarInput { Id = 9, Name = "Me", Player = true } };
            var into = new List<RaceControlEvent>();
            d.Step(cars, T, into); Assert.IsEmpty(into);
            cars[0].Penalties = 1; cars[1].Penalties = 1;
            d.Step(cars.Select(c => new LmuRaceControlDetector.CarInput { Id = c.Id, Name = c.Name, Penalties = c.Penalties, Player = c.Player }), T.AddSeconds(1), into);
            var e = into.Single();
            Assert.AreEqual("rival-penalty", e.Code); Assert.AreEqual("Opponent", e.Car);

            var gs = State();
            gs.carClass = new CarData.CarClass();
            gs.OpponentData["x"] = new OpponentData { DriverRawName = "Someone else", ClassPosition = 4, CarClass = gs.carClass, IsActive = true };
            Assert.IsNull(RaceControlRadio.Call(e, gs));
            gs.OpponentData["x"].DriverRawName = "Opponent";
            var call = RaceControlRadio.Call(e, gs);
            Assert.AreEqual("warning-rc-rival-penalty", call.Topic);
            Assert.IsTrue(new[] { "vor dir", "Vordermann", "ahead", "in front", "delante" }.Any(w => call.Text.Contains(w)), call.Text);
        }

        [Test]
        public void Lmu_stream_contact_lines_name_the_other_car_and_rival_incidents_stay_background()
        {
            var me = new LMURaceControlStream.Line { Utc = T, Tag = "Incident", Player = "Rob Gerke", PlayerId = 7,
                Text = "<Incident et=\"312.5\">Rob Gerke(7) reported contact (0.33) with another vehicle Max Muster(4)</Incident>" };
            var e = LmuStreamEvents.FromLine(me, T);
            Assert.AreEqual("contact", e.Code); Assert.AreEqual("Max Muster", e.Car); Assert.AreEqual(0.33, e.Value, 1e-9);
            Assert.IsNull(RaceControlRadio.Call(e, null));

            var wall = new LMURaceControlStream.Line { Utc = T, Tag = "Incident", Player = "Rob Gerke", PlayerId = 7,
                Text = "<Incident et=\"1\">Rob Gerke(7) reported contact (0.9) with Immovable</Incident>" };
            var w = LmuStreamEvents.FromLine(wall, T);
            Assert.AreEqual("contact-object", w.Code); Assert.IsTrue(w.IsPlayer); Assert.AreEqual("Immovable", w.Reason);

            var other = new LMURaceControlStream.Line { Utc = T, Tag = "Incident", Player = "Rob Gerke", PlayerId = 7,
                Text = "<Incident et=\"1\">Anna A(2) reported contact (0.12) with another vehicle Ben B(3)</Incident>" };
            var o = LmuStreamEvents.FromLine(other, T);
            Assert.AreEqual("rival-contact", o.Code); Assert.AreEqual("Anna A", o.Car); Assert.AreEqual("mit Ben B", o.Reason);

            Assert.IsNull(LmuStreamEvents.FromLine(new LMURaceControlStream.Line { Text = "Something else" }, T));
            LmuStreamEvents.RememberPenaltyReason(new LMURaceControlStream.Line { Utc = T, Tag = "Penalty",
                Text = "<Penalty et=\"5\" driver=\"Anna A\" id=\"2\">Drive Thru Penalty: Speeding</Penalty>" });
            StringAssert.Contains("Speeding", LmuStreamEvents.PenaltyReason("Anna A", T.AddSeconds(2)));
            Assert.IsNull(LmuStreamEvents.PenaltyReason("Anna A", T.AddSeconds(60)));
            LmuStreamEvents.Reset();
        }

        [Test]
        public void Common_detector_reports_safety_car_sub_phases_and_disqualification()
        {
            var feed = new RaceControlFeed(new CommonRaceControlDetector());
            var gs = State();
            Assert.IsEmpty(feed.Observe(gs, T));
            gs.SessionData.SessionPhase = SessionPhase.FullCourseYellow; gs.FlagData.isFullCourseYellow = true;
            gs.FlagData.fcyPhase = FullCourseYellowPhase.PITS_CLOSED;
            Assert.AreEqual("fcy-pits-closed", feed.Observe(gs, T.AddSeconds(1)).Single().Code);
            Assert.IsEmpty(feed.Observe(gs, T.AddSeconds(2)));
            gs.FlagData.fcyPhase = FullCourseYellowPhase.LAST_LAP_CURRENT;
            var last = feed.Observe(gs, T.AddSeconds(3)).Single();
            Assert.AreEqual("fcy-last-lap", last.Code);
            Assert.IsTrue(RaceControlRadio.StillCurrent(last)(gs));
            gs.SessionData.IsDisqualified = true;
            Assert.AreEqual("dq", feed.Observe(gs, T.AddSeconds(4)).Single().Code);

            var json = RaceControlFeed.Latest(3, T.AddSeconds(5));
            Assert.AreEqual(3, ((Newtonsoft.Json.Linq.JArray)json["meldungen"]).Count);
            Assert.IsNull(RaceControlFeed.Latest(99, T.AddSeconds(5)));
            Assert.IsTrue(RaceChronicle.Current.All().Any(c => c.Kind == ChronicleKind.RaceControl && c.Detail.StartsWith("fcy-pits-closed")));
            feed.Reset();
        }

        [Test]
        public void A_failing_detector_does_not_stop_the_others()
        {
            var feed = new RaceControlFeed(new Throwing(), new CommonRaceControlDetector());
            var gs = State();
            feed.Observe(gs, T);
            gs.SessionData.IsDisqualified = true;
            Assert.AreEqual("dq", feed.Observe(gs, T.AddSeconds(1)).Single().Code);
            feed.Reset();
        }

        private sealed class Throwing : IRaceControlDetector
        {
            public void Reset() { }
            public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into) { throw new InvalidOperationException("boom"); }
        }
    }
}
