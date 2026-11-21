using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Feldleser für AMS2, LMU, AC und iRacing (Rob 21.11.2026: „danach für die anderen Sims“): jedes Rohbild wird aus einem
    /// selbst gebauten Speicherabbild mit den Offsets der Crew-Chief-Strukturen gelesen.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class FieldSourceTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        private string wasLanguage;
        [SetUp] public void Setup() { wasLanguage = RadioLanguage.Current; RadioLanguage.Current = RadioLanguage.German; }
        [TearDown] public void TearDown() { RadioLanguage.Current = wasLanguage; }

        private static void I(byte[] b, int o, int v) => BitConverter.GetBytes(v).CopyTo(b, o);
        private static void U(byte[] b, int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
        private static void F(byte[] b, int o, float v) => BitConverter.GetBytes(v).CopyTo(b, o);
        private static void D(byte[] b, int o, double v) => BitConverter.GetBytes(v).CopyTo(b, o);
        private static void S(byte[] b, int o, string s) => Encoding.UTF8.GetBytes(s).CopyTo(b, o);

        [Test]
        public void Ams2_reads_all_participants_session_and_temperatures()
        {
            var b = new byte[AMS2FieldSource.StructSize];
            U(b, AMS2FieldSource.oGame, 2); U(b, AMS2FieldSource.oSession, 5); U(b, AMS2FieldSource.oRace, 2);
            I(b, AMS2FieldSource.oViewed, 1); I(b, AMS2FieldSource.oNum, 2);
            S(b, AMS2FieldSource.oTrack, "Interlagos"); S(b, AMS2FieldSource.oVariation, "GP"); F(b, AMS2FieldSource.oLength, 4309f);
            F(b, AMS2FieldSource.oDuration, 1200f); F(b, AMS2FieldSource.oRemaining, 900f); F(b, AMS2FieldSource.oAir, 24f); F(b, AMS2FieldSource.oTrackTemp, 36f);
            for (int i = 0; i < 2; i++)
            {
                int o = AMS2FieldSource.oParts + i * AMS2FieldSource.PartSize;
                b[o + AMS2FieldSource.pActive] = 1; S(b, o + AMS2FieldSource.pName, i == 0 ? "Ayrton" : "Rob");
                F(b, o + AMS2FieldSource.pDist, 1000f + i * 50); U(b, o + AMS2FieldSource.pPlace, (uint)(i + 1)); U(b, o + AMS2FieldSource.pLaps, 3);
                F(b, AMS2FieldSource.oSpeeds + 4 * i, 60f + i); F(b, AMS2FieldSource.oFastLap + 4 * i, 82.5f + i);
                F(b, AMS2FieldSource.oFast1 + 4 * i, 25f); F(b, AMS2FieldSource.oFast2 + 4 * i, 30f); F(b, AMS2FieldSource.oFast3 + 4 * i, 27f);
            }
            b[AMS2FieldSource.oInvalid + 1] = 1;
            U(b, AMS2FieldSource.oPitModes, 2);
            var f = new FieldFrame();
            Assert.IsTrue(AMS2FieldSource.ParseBuffer(b, f, T));
            Assert.AreEqual("Interlagos", f.Track); Assert.AreEqual(FieldSession.Race, f.Session); Assert.AreEqual(FieldPhase.Green, f.Phase);
            Assert.IsTrue(f.TimedSession); Assert.AreEqual(900f, f.TimeRemaining); Assert.AreEqual(20, f.RaceMinutes[0]);
            Assert.AreEqual(24f, f.AirTempC); Assert.AreEqual(36f, f.TrackTempC);
            Assert.AreEqual(2, f.Cars.Length); Assert.AreEqual("Rob", f.Player.Name); Assert.AreEqual(2, f.Player.Place);
            Assert.AreEqual(61f, f.Player.Speed); Assert.IsFalse(f.Player.CurrentLapValid);
            Assert.IsTrue(f.Cars[0].InPit); Assert.AreEqual(27f, f.Cars[0].SplitBest3);
            var view = new FieldPicture().Observe(f);
            Assert.AreEqual(30f, view.Cars[0].BestS2, "getrennte Bestsektoren werden direkt übernommen");
            Assert.That(view.Cars[0].GapM, Is.EqualTo(-50).Within(.01));
        }

        [Test]
        public void Ams2_without_game_is_no_frame()
        {
            var b = new byte[AMS2FieldSource.StructSize];
            Assert.IsFalse(AMS2FieldSource.ParseBuffer(b, new FieldFrame(), T));
        }

        [Test]
        public void Lmu_reads_scoring_with_menu_state_and_lap_based_race()
        {
            var b = new byte[LMUFieldSource.StructSize];
            int info = LMUFieldSource.oInfo;
            S(b, info + LMUFieldSource.iTrack, "Circuit de la Sarthe"); I(b, info + LMUFieldSource.iSession, 10);
            D(b, info + LMUFieldSource.iEt, 30); D(b, info + LMUFieldSource.iEnd, 0); I(b, info + LMUFieldSource.iMaxLaps, 12);
            D(b, info + LMUFieldSource.iLapDist, 13626); I(b, info + LMUFieldSource.iNum, 2); b[info + LMUFieldSource.iPhase] = 5;
            b[info + LMUFieldSource.iRealtime] = 0; D(b, info + LMUFieldSource.iAir, 21); D(b, info + LMUFieldSource.iTrackTemp, 29);
            for (int i = 0; i < 2; i++)
            {
                int o = LMUFieldSource.oVeh + i * LMUFieldSource.VehSize;
                I(b, o + LMUFieldSource.vId, 10 + i); S(b, o + LMUFieldSource.vName, i == 0 ? "Kubica" : "Rob");
                BitConverter.GetBytes((short)4).CopyTo(b, o + LMUFieldSource.vLaps); D(b, o + LMUFieldSource.vDist, 5000 + i * 100);
                D(b, o + LMUFieldSource.vBest1, 60); D(b, o + LMUFieldSource.vBest2, 140); D(b, o + LMUFieldSource.vBest, 215);
                b[o + LMUFieldSource.vPlayer] = (byte)i; b[o + LMUFieldSource.vPlace] = (byte)(i + 1); b[o + LMUFieldSource.vCount] = 2;
                D(b, o + LMUFieldSource.vVel, 3); D(b, o + LMUFieldSource.vVel + 16, 4);
            }
            var f = new FieldFrame();
            Assert.IsTrue(LMUFieldSource.ParseBuffer(b, f, T));
            Assert.AreEqual("Circuit de la Sarthe", f.Track); Assert.AreEqual(FieldSession.Race, f.Session);
            Assert.IsTrue(f.InMenus, "Garagenmonitor"); Assert.IsTrue(f.LapsSession); Assert.IsFalse(f.TimedSession); Assert.AreEqual(12, f.RaceLaps[0]);
            Assert.AreEqual(21f, f.AirTempC); Assert.AreEqual("Rob", f.Player.Name); Assert.AreEqual(5f, f.Player.Speed, 1e-4);
            Assert.AreEqual(140f, f.Player.BestS12);
        }

        [Test]
        public void Ac_reads_plugin_field_and_needs_the_plugin()
        {
            var g = new byte[ACFieldSource.gSize]; var s = new byte[ACFieldSource.sSize]; var p = new byte[ACFieldSource.pSize]; var c = new byte[ACFieldSource.cSize];
            I(g, ACFieldSource.gStatus, 2); I(g, ACFieldSource.gSession, 1); F(g, ACFieldSource.gTimeLeft, 600000f);
            F(s, ACFieldSource.sLength, 5793f); F(p, ACFieldSource.pAir, 26f); F(p, ACFieldSource.pRoad, 38f);
            string key = null; float max = float.NaN;
            Assert.IsFalse(ACFieldSource.Parse(g, s, p, c, "monza", "", new FieldFrame(), T, ref key, ref max), "ohne Plugin kein Feld");
            I(c, ACFieldSource.cNum, 2); I(c, ACFieldSource.cFocus, 0);
            for (int i = 0; i < 2; i++)
            {
                int o = ACFieldSource.cVeh + i * ACFieldSource.vSize;
                I(c, o + ACFieldSource.vId, i); S(c, o + ACFieldSource.vName, i == 0 ? "Rob" : "Lucas Auer"); F(c, o + ACFieldSource.vSpeed, 70f);
                I(c, o + ACFieldSource.vBest, 107238); I(c, o + ACFieldSource.vLaps, 2); I(c, o + ACFieldSource.vPlace, i + 1);
                F(c, o + ACFieldSource.vSpline, .5f + i * .01f); I(c, o + ACFieldSource.vConnected, 1);
            }
            var f = new FieldFrame();
            Assert.IsTrue(ACFieldSource.Parse(g, s, p, c, "monza", "", f, T, ref key, ref max));
            Assert.AreEqual(FieldSession.Qualify, f.Session); Assert.AreEqual(600f, f.TimeRemaining); Assert.AreEqual(600f, f.TimeDuration);
            Assert.AreEqual(26f, f.AirTempC); Assert.AreEqual("Rob", f.Player.Name);
            Assert.That(f.Cars[1].LapDistance, Is.EqualTo(5793f * .51f).Within(.1));
            Assert.That(f.Player.BestLap, Is.EqualTo(107.238f).Within(1e-3));
        }

        private const string Yaml = @"WeekendInfo:
 TrackDisplayName: Spa-Francorchamps
 TrackConfigName: Grand Prix
 TrackLength: 7.00 km
 TrackAirTemp: 19.40 C
 TrackSurfaceTemp: 27.10 C
SessionInfo:
 Sessions:
 - SessionNum: 0
   SessionLaps: unlimited
   SessionTime: 600.0000 sec
   SessionType: Practice
 - SessionNum: 1
   SessionLaps: unlimited
   SessionTime: 900.0000 sec
   SessionType: Lone Qualify
 - SessionNum: 2
   SessionLaps: unlimited
   SessionTime: 1800.0000 sec
   SessionType: Race
DriverInfo:
 DriverCarIdx: 1
 Drivers:
 - CarIdx: 0
   UserName: Pace Car
   CarIsPaceCar: 1
 - CarIdx: 1
   UserName: Rob Driver
   CarIsPaceCar: 0
 - CarIdx: 2
   UserName: Max Mustermann
   CarIsPaceCar: 0
";

        [Test]
        public void Iracing_session_info_gives_names_length_and_whole_schedule()
        {
            var info = IRacingFieldSource.ParseInfo(Yaml);
            Assert.AreEqual("Spa-Francorchamps", info.Track); Assert.AreEqual(7000f, info.LengthM, .5f);
            Assert.AreEqual(19.4f, info.AirC, 1e-3); Assert.AreEqual(1, info.PlayerIdx);
            Assert.IsFalse(info.Names.ContainsKey(0), "Pace Car ist kein Gegner");
            Assert.AreEqual("Max Mustermann", info.Names[2]);
            Assert.AreEqual(3, info.Sessions.Count); Assert.AreEqual(FieldSession.Qualify, info.Sessions[1].Type); Assert.AreEqual(1800f, info.Sessions[2].Seconds);
        }

        private static void Car(Dictionary<string, IRacingDynamicsSource.Slot> map, byte[] b, int i, float pct, int pos)
        {
            F(b, map["CarIdxLapDistPct"].Offset + 4 * i, pct); I(b, map["CarIdxTrackSurface"].Offset + 4 * i, 3); I(b, map["CarIdxPosition"].Offset + 4 * i, pos);
        }

        [Test]
        public void Iracing_fill_derives_speed_from_track_position_and_lists_upcoming_sessions()
        {
            var info = IRacingFieldSource.ParseInfo(Yaml);
            var map = new Dictionary<string, IRacingDynamicsSource.Slot>();
            int at = 0;
            foreach (var v in new[] { Tuple.Create("SessionNum", 2, 1), Tuple.Create("SessionState", 2, 1), Tuple.Create("SessionTimeRemain", 5, 1),
                Tuple.Create("PlayerCarIdx", 2, 1), Tuple.Create("IsOnTrack", 1, 1), Tuple.Create("CarIdxLapDistPct", 4, 64), Tuple.Create("CarIdxLapCompleted", 2, 64),
                Tuple.Create("CarIdxPosition", 2, 64), Tuple.Create("CarIdxTrackSurface", 2, 64) })
            {
                map[v.Item1] = new IRacingDynamicsSource.Slot { Type = v.Item2, Offset = at, Count = v.Item3 };
                at += v.Item3 * (v.Item2 == 5 ? 8 : v.Item2 <= 1 ? 1 : 4);
            }
            var b = new byte[at];
            I(b, map["SessionNum"].Offset, 0); I(b, map["SessionState"].Offset, 4); D(b, map["SessionTimeRemain"].Offset, 300); I(b, map["PlayerCarIdx"].Offset, 1); b[map["IsOnTrack"].Offset] = 1;
            for (int i = 0; i < 64; i++) { F(b, map["CarIdxLapDistPct"].Offset + 4 * i, -1); I(b, map["CarIdxTrackSurface"].Offset + 4 * i, -1); }
            Car(map, b, 1, .10f, 2); Car(map, b, 2, .20f, 1);
            var lastPct = Enumerable.Repeat(float.NaN, 64).ToArray(); var lastAt = new DateTime[64]; var speed = Enumerable.Repeat(float.NaN, 64).ToArray();
            var f = new FieldFrame();
            Assert.IsTrue(IRacingFieldSource.Fill(map, b, info, f, T, lastPct, lastAt, speed));
            Assert.AreEqual(2, f.Cars.Length, "Pace Car und leere Plätze fehlen");
            Assert.AreEqual(FieldSession.Practice, f.Session); Assert.AreEqual(600f, f.TimeDuration); Assert.AreEqual(300f, f.TimeRemaining);
            Assert.AreEqual(2, f.Upcoming.Length); Assert.AreEqual(FieldSession.Qualify, f.Upcoming[0].Session); Assert.AreEqual(30, f.Upcoming[1].Minutes);
            Assert.AreEqual(30, f.RaceMinutes[0]);
            Car(map, b, 1, .101f, 2); // 7 m in 0,1 s = 70 m/s
            Assert.IsTrue(IRacingFieldSource.Fill(map, b, info, f, T.AddMilliseconds(100), lastPct, lastAt, speed));
            Assert.That(f.Player.Speed, Is.EqualTo(70f).Within(.5));
            Assert.IsTrue(float.IsNaN(f.Player.X), "iRacing meldet keine Weltposition");
        }

        [Test]
        public void Menu_briefing_with_temperatures_and_whole_schedule()
        {
            var cars = new List<FieldCarView> { new FieldCarView { Name = "Rob", Place = 1, IsPlayer = true } };
            var v = new FieldView { Utc = T, Sim = "iRacing", Track = "Spa-Francorchamps", Session = FieldSession.Practice, Phase = FieldPhase.Green, InMenus = true,
                TimedSession = true, TimeDuration = 600, NumCars = 29, AirTempC = 19.4f, TrackTempC = 36.2f, RaceMinutes = new[] { 30, -1, -1 }, RaceLaps = new[] { -1, -1, -1 },
                Upcoming = new[] { new FieldUpcoming { Session = FieldSession.Qualify, Minutes = 10 }, new FieldUpcoming { Session = FieldSession.Race, Minutes = 20 } },
                Cars = cars, Player = cars[0] };
            string text = MenuBriefing.Compose(v, "Rob", "Ben", 0);
            StringAssert.Contains("Luft 19 Grad, Strecke 36 Grad.", text);
            StringAssert.Contains("29 Fahrer im Feld.", text);
            StringAssert.Contains("Vor uns liegen 10 Minuten Training.", text);
            StringAssert.Contains("Danach Qualifying über 10 Minuten und das Rennen über 20 Minuten.", text);
        }
    }
}
