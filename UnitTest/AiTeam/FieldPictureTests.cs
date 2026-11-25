using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.AiTeam.Probe;
using CrewChiefV4.RaceRoom.RaceRoomData;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Lagebild (Rob 21.11.2026, Monza): eigenes Bild des Feldes direkt aus der Sim – Relativabstände, Dreher nur gegen das
    /// Feldtempo an derselben Stelle, Restrunden nach Uhr, gestrichene Runden und Höchsttempo je Fahrer.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class FieldPictureTests
    {
        private const float L = 1000f;
        private static readonly DateTime T0 = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);

        private sealed class SimCar
        {
            public int Slot; public string Name; public float Dist; public int Laps; public bool Player, Pit;
            public Func<float, float> Pace = d => 50f; // Tempo nach Streckenmeter
            public float? Override; public bool Backwards; public bool Valid = true; public float Best = float.NaN;
            public int Place = 1, Finish;
            public float LapClock;
        }

        private static float X(float d) => (float)(L / (2 * Math.PI) * Math.Cos(d / L * 2 * Math.PI));
        private static float Z(float d) => (float)(L / (2 * Math.PI) * Math.Sin(d / L * 2 * Math.PI));

        private static FieldFrame Frame(DateTime utc, IList<SimCar> cars, FieldSession session = FieldSession.Race, FieldPhase phase = FieldPhase.Green,
            float timeLeft = float.NaN, bool timed = true)
        {
            var f = new FieldFrame { Utc = utc, Sim = "Test", Track = "Ring", Layout = "GP", TrackLength = L, Session = session, Phase = phase,
                RawSession = (int)session, TimedSession = timed, TimeRemaining = timeLeft, NumCars = cars.Count, LayoutVerified = true };
            f.Cars = cars.Select((c, i) => new FieldCarFrame { Slot = c.Slot, Name = c.Name, LapDistance = c.Dist, CompletedLaps = c.Laps,
                Speed = c.Override ?? c.Pace(c.Dist), X = X(c.Dist), Z = Z(c.Dist), InPit = c.Pit, IsPlayer = c.Player, CurrentLapValid = c.Valid,
                BestLap = c.Best, Place = c.Place, FinishStatus = c.Finish, LapTimeCurrent = c.LapClock }).ToArray();
            f.PlayerIndex = cars.ToList().FindIndex(c => c.Player);
            return f;
        }

        /// <summary>Fährt alle Wagen dt Sekunden weiter (rückwärts, wenn so gewünscht) und liefert das neue Lagebild.</summary>
        private static FieldView Step(FieldPicture p, ref DateTime t, IList<SimCar> cars, double seconds, FieldSession session = FieldSession.Race,
            FieldPhase phase = FieldPhase.Green, Func<double, float> timeLeft = null)
        {
            FieldView v = null;
            int steps = (int)Math.Round(seconds / .05);
            for (int i = 0; i < steps; i++)
            {
                t = t.AddMilliseconds(50);
                foreach (var c in cars)
                {
                    float sp = c.Override ?? c.Pace(c.Dist);
                    float d = c.Dist + (c.Backwards ? -1 : 1) * sp * .05f;
                    c.LapClock += .05f;
                    if (d >= L) { d -= L; c.Laps++; c.LapClock = 0; } else if (d < 0) d += L;
                    c.Dist = d;
                }
                v = p.Observe(Frame(t, cars, session, phase, timeLeft == null ? float.NaN : timeLeft((t - T0).TotalSeconds)));
            }
            return v;
        }

        private static List<SimCar> Field(Func<float, float> pace, int n = 6)
        {
            var list = new List<SimCar> { new SimCar { Slot = 0, Name = "Rob", Player = true, Dist = 0, Pace = pace } };
            for (int i = 1; i < n; i++) list.Add(new SimCar { Slot = i, Name = "Fahrer " + i, Dist = i * (L / n), Pace = pace, Place = i + 1 });
            return list;
        }

        [Test]
        public void Relative_GapsInSecondsFromFieldPace()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 50f);
            Step(p, ref t, cars, 30);
            cars[0].Dist = 0; cars[1].Dist = 100; cars[2].Dist = 950; // 100 m voraus, 50 m dahinter
            cars[3].Dist = 300; cars[4].Dist = 500; cars[5].Dist = 700;
            var v = p.Observe(Frame(t.AddMilliseconds(50), cars));
            var a = v.Cars.Single(c => c.Name == "Fahrer 1"); var b = v.Cars.Single(c => c.Name == "Fahrer 2");
            Assert.That(a.GapS, Is.EqualTo(2.0).Within(.1));
            Assert.That(b.GapS, Is.EqualTo(-1.0).Within(.1));
            Assert.AreEqual("Fahrer 1", v.Ahead(1).Single().Name);
            Assert.AreEqual("Fahrer 2", v.Behind(1).Single().Name);
            Assert.That(v.ProfileCoverage, Is.GreaterThan(.9));
        }

        [Test]
        public void BrakingForChicane_IsNotASpinOrSlowCar()
        {
            // Monza 21.11.: In der Startrunde galten zehn Fahrer als „Dreher“, weil sie vor der Schikane bremsten.
            Func<float, float> pace = d => d >= 400 && d < 500 ? 20f : 60f;
            var p = new FieldPicture(); var t = T0;
            var cars = Field(pace, 8);
            var v = Step(p, ref t, cars, 40);
            Assert.IsFalse(v.Cars.Any(c => c.Motion == CarMotion.Slow || c.Motion == CarMotion.Spun || c.Motion == CarMotion.Stopped),
                string.Join(", ", v.Cars.Select(c => c.Name + ":" + c.Motion)));
        }

        [Test]
        public void SlowCar_AgainstFieldPaceAtThatSpot()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 60f);
            Step(p, ref t, cars, 30);
            cars[3].Override = 15f;
            var v = Step(p, ref t, cars, 1);
            Assert.AreEqual(CarMotion.Slow, v.Cars.Single(c => c.Name == "Fahrer 3").Motion);
            Assert.AreEqual(CarMotion.Normal, v.Cars.Single(c => c.Name == "Fahrer 1").Motion);
        }

        [Test]
        public void StoppedCar_AfterOneSecond()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 60f);
            Step(p, ref t, cars, 20);
            cars[2].Override = 0f;
            var v = Step(p, ref t, cars, .5);
            Assert.AreNotEqual(CarMotion.Stopped, v.Cars.Single(c => c.Name == "Fahrer 2").Motion);
            v = Step(p, ref t, cars, 1);
            Assert.AreEqual(CarMotion.Stopped, v.Cars.Single(c => c.Name == "Fahrer 2").Motion);
        }

        [Test]
        public void Spin_WhenCarMovesAgainstTrackDirection()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 50f);
            Step(p, ref t, cars, 30);
            cars[4].Backwards = true; cars[4].Override = 10f;
            var v = Step(p, ref t, cars, .5);
            Assert.AreEqual(CarMotion.Spun, v.Cars.Single(c => c.Name == "Fahrer 4").Motion);
        }

        /// <summary>
        /// Review 24.11.: LMU meldete FCY und Rennunterbrechung dem Lagebild als Grün. Grün → FCY → Unterbrechung → Neustart:
        /// In jedem Schritt muss die Phase stimmen, unter FCY und Unterbrechung gilt kein Wagen als langsam oder stehend.
        /// </summary>
        [Test]
        public void Lmu_green_fcy_stop_and_restart_keep_phase_and_hazards_apart()
        {
            Assert.AreEqual(FieldPhase.Green, LMUFieldSource.MapPhase(5));
            Assert.AreEqual(FieldPhase.FullCourseYellow, LMUFieldSource.MapPhase(6));
            Assert.AreEqual(FieldPhase.Stopped, LMUFieldSource.MapPhase(7));
            Assert.AreEqual(FieldPhase.Checkered, LMUFieldSource.MapPhase(8));

            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 60f);
            var v = Step(p, ref t, cars, 30);
            Assert.AreEqual(FieldPhase.Green, v.Phase);

            foreach (var c in cars) c.Override = 20f; // FCY: alle mit einem Drittel des Tempos
            v = Step(p, ref t, cars, 2, phase: FieldPhase.FullCourseYellow);
            Assert.AreEqual(FieldPhase.FullCourseYellow, v.Phase);
            Assert.IsTrue(v.Cars.All(c => c.Motion == CarMotion.Normal), "unter FCY ist niemand eine Gefahr: " +
                string.Join(", ", v.Cars.Select(c => c.Name + ":" + c.Motion)));

            foreach (var c in cars) c.Override = 0f; // Rennunterbrechung: alle stehen
            v = Step(p, ref t, cars, 3, phase: FieldPhase.Stopped);
            Assert.AreEqual(FieldPhase.Stopped, v.Phase);
            Assert.IsTrue(v.Cars.All(c => c.Motion == CarMotion.Normal), "bei Unterbrechung steht das Feld, das ist kein Unfall");
            Assert.IsTrue(MenuBriefing.RaceUnderway(Underway(v)), "eine Unterbrechung ist kein Rennstart");

            foreach (var c in cars) c.Override = null; // Neustart
            cars[2].Override = 0f;
            Step(p, ref t, cars, .5);
            v = Step(p, ref t, cars, 1.5);
            Assert.AreEqual(FieldPhase.Green, v.Phase);
            Assert.AreEqual(CarMotion.Stopped, v.Cars.Single(c => c.Name == "Fahrer 2").Motion, "nach dem Neustart wird ein stehendes Auto wieder erkannt");
            Assert.AreEqual(60f, v.Cars.Single(c => c.Name == "Fahrer 1").Speed, .01f);
        }

        private static FieldView Underway(FieldView v)
        {
            v.Player.CompletedLaps = Math.Max(1, v.Player.CompletedLaps);
            return v;
        }

        [Test]
        public void Board_takes_crew_chiefs_fcy_when_the_sim_itself_only_knows_green()
        {
            var gs = new CrewChiefV4.GameState.GameStateData(T0.Ticks);
            gs.SessionData.SessionType = CrewChiefV4.GameState.SessionType.Race;
            gs.SessionData.SessionPhase = CrewChiefV4.GameState.SessionPhase.FullCourseYellow;
            var v = new FieldView { Utc = T0, Sim = "AMS2", Session = FieldSession.Race, Phase = FieldPhase.Green, Cars = new List<FieldCarView>() };
            Assert.AreEqual(FieldPhase.FullCourseYellow, CrewChiefV4.AiTeam.Board.RaceBoard.Compose(gs, v, T0).Phase.Value);
            v.Phase = FieldPhase.Stopped;
            Assert.AreEqual(FieldPhase.Stopped, CrewChiefV4.AiTeam.Board.RaceBoard.Compose(gs, v, T0).Phase.Value, "die Sim weiß mehr: Unterbrechung");
            gs.SessionData.SessionPhase = CrewChiefV4.GameState.SessionPhase.Green; v.Phase = FieldPhase.Green;
            Assert.AreEqual(FieldPhase.Green, CrewChiefV4.AiTeam.Board.RaceBoard.Compose(gs, v, T0).Phase.Value);
        }

        [Test]
        public void StandingOnGridBeforeGreen_IsNoHazard()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 0f);
            var v = Step(p, ref t, cars, 3, phase: FieldPhase.Countdown);
            Assert.IsTrue(v.Cars.All(c => c.Motion == CarMotion.Normal));
        }

        [Test]
        public void TimedPractice_ClockAtZeroMeansCurrentLapIsLast()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 50f, 3); // 20 s pro Runde
            foreach (var c in cars) c.Best = 20f;
            var v = Step(p, ref t, cars, 2, FieldSession.Practice, timeLeft: s => (float)Math.Max(0, 100 - s));
            Assert.Greater(v.PlayerLapsLeft, 1);
            // Uhr läuft ab, Rob ist mitten in der Runde: genau noch diese eine.
            cars[0].Dist = 500;
            v = p.Observe(Frame(t.AddMilliseconds(50), cars, FieldSession.Practice, timeLeft: 0f));
            Assert.AreEqual(1, v.PlayerLapsLeft);
            Assert.IsTrue(v.PlayerOnLastLap);
            // Über die Linie: fertig.
            cars[0].Dist = 5; cars[0].Laps++;
            v = p.Observe(Frame(t.AddMilliseconds(100), cars, FieldSession.Practice, timeLeft: 0f));
            Assert.AreEqual(0, v.PlayerLapsLeft);
        }

        [Test]
        public void TimedRace_FinishesOnLapAfterLeaderCrossesAtZero()
        {
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 50f, 3);
            var leader = cars[1]; leader.Place = 1; cars[0].Place = 3; cars[2].Place = 2;
            foreach (var c in cars) c.Best = 20f;
            Step(p, ref t, cars, 2);
            // Rob bei 100 m (noch 18 s bis zur Linie), Führender bei 500 m (10 s bis zur Linie).
            cars[0].Dist = 100; leader.Dist = 500;
            var v = p.Observe(Frame(t.AddMilliseconds(50), cars, timeLeft: 5f));
            Assert.AreEqual(1, v.PlayerLapsLeft, "Führender kreuzt nach Ablauf, Rob danach: nur noch die laufende Runde");
            v = p.Observe(Frame(t.AddMilliseconds(100), cars, timeLeft: 35f));
            // Führender: 10 s, dann 30 s – Ziel bei 50 s; Rob: 18, 38, 58 s → drei Runden inklusive der laufenden.
            Assert.AreEqual(3, v.PlayerLapsLeft);
            // Uhr bei null darf nie „zwei Runden“ ergeben, solange der Führende vor Rob die Linie kreuzt.
            v = p.Observe(Frame(t.AddMilliseconds(150), cars, timeLeft: 0f));
            Assert.AreEqual(1, v.PlayerLapsLeft);
        }

        [Test]
        public void InvalidLapIsKeptAsFastestAnyLap_AndTopSpeedRecorded()
        {
            var p = new FieldPicture(); var t = T0;
            Func<float, float> pace = d => d < 500 ? 70f : 40f;
            var cars = Field(pace, 3);
            Step(p, ref t, cars, 5);
            cars[0].Valid = false;
            var v = Step(p, ref t, cars, 30);
            var rob = v.Player;
            Assert.IsFalse(float.IsNaN(rob.FastestAnyLap));
            Assert.That(rob.TopSpeed, Is.EqualTo(70f).Within(.01));
            Assert.That(rob.TopSpeedAtM, Is.LessThan(500));
        }

        private static readonly Type Root = typeof(RaceRoomShared);
        private static void I(byte[] b, string path, int value) { BitConverter.GetBytes(value).CopyTo(b, PackedLayout.Offset(Root, path)); }
        private static void F(byte[] b, string path, float value) { BitConverter.GetBytes(value).CopyTo(b, PackedLayout.Offset(Root, path)); }
        private static void S(byte[] b, string path, string text) { System.Text.Encoding.UTF8.GetBytes(text).CopyTo(b, PackedLayout.Offset(Root, path)); }

        [Test]
        public void RaceRoomParse_ReadsHeaderDriversAndMenuState()
        {
            var b = new byte[R3EFieldSource.Size];
            I(b, "VersionMajor", 3); I(b, "AllDriversOffset", R3EFieldSource.NumCarsOffset); I(b, "DriverDataSize", R3EFieldSource.DriverSize);
            I(b, "GameInMenus", 1); I(b, "GamePlayerInGarage", 1); S(b, "TrackName", "Autodromo di Monza"); S(b, "LayoutName", "Grand Prix");
            F(b, "LayoutLength", 5778.8f); I(b, "SessionType", 2); I(b, "SessionPhase", 1); I(b, "SessionLengthFormat", 0); F(b, "SessionTimeDuration", 1200f);
            F(b, "SessionTimeRemaining", 1200f); I(b, "RaceSessionMinutes.Race1", 20); I(b, "NumCars", 2); I(b, "VehicleInfo.SlotId", 7);
            S(b, "DriverData[0].DriverInfo.Name", "Mirko Bortolotti"); I(b, "DriverData[0].DriverInfo.SlotId", 3); I(b, "DriverData[0].Place", 1);
            S(b, "DriverData[1].DriverInfo.Name", "Rob"); I(b, "DriverData[1].DriverInfo.SlotId", 7); I(b, "DriverData[1].Place", 31);
            F(b, "DriverData[1].CarSpeed", 12.5f); F(b, "DriverData[1].Penalties.SlowDown", 8f);
            var f = new FieldFrame();
            Assert.IsTrue(R3EFieldSource.Parse(b, b.Length, f, T0));
            Assert.IsTrue(f.LayoutVerified);
            Assert.IsTrue(f.InMenus); Assert.IsTrue(f.PlayerInGarage);
            Assert.AreEqual("Autodromo di Monza", f.Track); Assert.AreEqual(FieldSession.Race, f.Session); Assert.AreEqual(FieldPhase.Garage, f.Phase);
            Assert.IsTrue(f.TimedSession); Assert.AreEqual(20, f.RaceMinutes[0]); Assert.AreEqual(2, f.Cars.Length);
            Assert.AreEqual(1, f.PlayerIndex); Assert.AreEqual("Rob", f.Player.Name); Assert.AreEqual(31, f.Player.Place);
            Assert.AreEqual(12.5f, f.Player.Speed); Assert.AreEqual(8f, f.Player.SlowDown);
            Assert.AreEqual("Mirko Bortolotti", f.Cars[0].Name);
        }

        [Test]
        public void WithoutWorldPosition_FieldPaceAndBackwardsSpinStillWork()
        {
            // iRacing meldet keine Weltposition: Lernen über die Streckenposition, Dreher über Rückwärtsfahrt.
            var p = new FieldPicture(); var t = T0;
            var cars = Field(d => 60f);
            FieldView v = null;
            for (int i = 0; i < 600; i++)
            {
                t = t.AddMilliseconds(50);
                foreach (var c in cars) { float d = c.Dist + (c.Backwards ? -1 : 1) * (c.Override ?? 60f) * .05f; if (d >= L) { d -= L; c.Laps++; } else if (d < 0) d += L; c.Dist = d; }
                var f = Frame(t, cars);
                foreach (var c in f.Cars) { c.X = c.Y = c.Z = float.NaN; }
                v = p.Observe(f);
                if (i == 500) { cars[2].Override = 15f; }
                if (i == 560) { cars[2].Override = null; cars[4].Backwards = true; cars[4].Override = 8f; }
                if (i == 530) Assert.AreEqual(CarMotion.Slow, v.Cars.Single(c => c.Name == "Fahrer 2").Motion, "langsam gegen das Feldtempo");
            }
            Assert.That(v.ProfileCoverage, Is.GreaterThan(.9));
            Assert.AreEqual(CarMotion.Spun, v.Cars.Single(c => c.Name == "Fahrer 4").Motion);
            Assert.AreEqual(CarMotion.Normal, v.Cars.Single(c => c.Name == "Fahrer 1").Motion);
        }

        [Test]
        public void RaceRoomParse_RejectsUnwrittenMemory()
        {
            var b = new byte[R3EFieldSource.Size];
            Assert.IsFalse(R3EFieldSource.Parse(b, b.Length, new FieldFrame(), T0));
        }
    }
}
