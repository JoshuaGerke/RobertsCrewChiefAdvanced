using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.ACE.aceData;
using CrewChiefV4.AiTeam.Board;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Etappe 2: fester Takt, gemessene Rate, Zeitstempel je Wagen, LMU-Fortschreibung, AC-EVO-Feld.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class FieldRealtimeTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 22, 11, 0, 0, DateTimeKind.Utc);
        private const float L = 4000;

        [Test]
        public void Schedule_keeps_a_fixed_cadence_and_skips_missed_ticks()
        {
            bool late;
            long due = 0;
            due = FieldService.NextDue(due, 30, 50, out late);
            Assert.AreEqual(50, due); Assert.IsFalse(late, "Arbeit dauerte 30 von 50: kein Wandern, nächster Takt bei 50");
            due = FieldService.NextDue(due, 95, 50, out late);
            Assert.AreEqual(100, due); Assert.IsFalse(late);
            due = FieldService.NextDue(due, 290, 50, out late);
            Assert.IsTrue(late); Assert.AreEqual(250, due, "verpasste Takte werden übersprungen, nicht nachgeholt");
        }

        [Test]
        public void Rate_meter_counts_only_frames_with_new_sim_time()
        {
            var meter = new FieldService.RateMeter(1);
            var f = new FieldFrame { Cars = new[] { new FieldCarFrame { Slot = 1, IsPlayer = true } }, PlayerIndex = 0 };
            for (int i = 0; i <= 20; i++)
            {
                f.SimTime = 100 + (i / 4) * 0.2; // neue Sim-Zeit nur jedes vierte Lesen: 5 Hz
                meter.Observe(f, T.AddMilliseconds(i * 50));
            }
            Assert.AreEqual(5, meter.Hz, 1.01);
        }

        [Test]
        public void RaceRoom_copy_counts_only_when_the_tick_is_unchanged_around_it()
        {
            Assert.IsTrue(R3EFieldSource.Consistent(7, 7, 7));
            Assert.IsFalse(R3EFieldSource.Consistent(7, 7, 8), "während des Kopierens neu geschrieben");
            Assert.IsFalse(R3EFieldSource.Consistent(7, 6, 7));
        }

        private static FieldFrame Frame(DateTime utc, params FieldCarFrame[] cars)
        {
            var f = new FieldFrame { Utc = utc, Sim = "Test", Track = "Ring", Layout = "GP", TrackLength = L, Session = FieldSession.Race,
                Phase = FieldPhase.Green, NumCars = cars.Length, Cars = cars };
            f.PlayerIndex = Array.FindIndex(cars, c => c.IsPlayer);
            return f;
        }

        [Test]
        public void Each_car_carries_the_time_the_sim_last_moved_it()
        {
            var picture = new FieldPicture();
            var me = new FieldCarFrame { Slot = 1, Name = "Me", IsPlayer = true, LapDistance = 100, X = 0, Z = 0, Speed = 50, Place = 2 };
            var other = new FieldCarFrame { Slot = 2, Name = "Other", LapDistance = 300, X = 10, Z = 10, Speed = 50, Place = 1 };
            picture.Observe(Frame(T, me, other));
            me.LapDistance = 105; me.X = 5;
            var v = picture.Observe(Frame(T.AddMilliseconds(100), me, other));
            Assert.AreEqual(T.AddMilliseconds(100), v.Player.DataUtc);
            var o = v.Cars.Single(c => c.Slot == 2);
            Assert.AreEqual(T, o.DataUtc, "unverändert: der Zeitstempel bleibt beim letzten echten Wert");
            Assert.AreEqual(100, o.DataAgeMs(T.AddMilliseconds(100)), 1e-6);
        }

        [Test]
        public void Lmu_native_telemetry_moves_cars_between_scoring_frames()
        {
            var f = Frame(T,
                new FieldCarFrame { Slot = 4, LapDistance = 1000, Speed = 40, X = 1, Z = 1 },
                new FieldCarFrame { Slot = 5, LapDistance = 3995, Speed = 60 },
                new FieldCarFrame { Slot = 6, LapDistance = 200, Speed = 50, InPit = true });
            var baseDist = f.Cars.ToDictionary(c => c.Slot, c => c.LapDistance);
            var native = new LMUNativeField { Utc = T, Cars = new[] {
                new LMUNativeField.Car { Id = 4, Et = 100.2, Speed = 50, X = 11, Y = 2, Z = 12 },
                new LMUNativeField.Car { Id = 5, Et = 100.2, Speed = 60 },
                new LMUNativeField.Car { Id = 6, Et = 100.2, Speed = 10 },
                new LMUNativeField.Car { Id = 9, Et = 100.2, Speed = 50 } } };
            Assert.AreEqual(3, LMUNativeField.Apply(f, native, 100.0, baseDist));
            Assert.AreEqual(1010, f.Cars[0].LapDistance, 1e-3, "1000 m + 50 m/s × 0,2 s");
            Assert.AreEqual(11, f.Cars[0].X); Assert.IsTrue(f.Cars[0].Extrapolated);
            Assert.Less(f.Cars[1].LapDistance, L, "nicht über die Ziellinie fortgeschrieben");
            Assert.AreEqual(200, f.Cars[2].LapDistance, "in der Box keine Fortschreibung der Streckenmeter");
            Assert.AreEqual(100.2, f.SimTime, 1e-9);

            var stale = new LMUNativeField { Utc = T, Cars = new[] { new LMUNativeField.Car { Id = 4, Et = 101.5, Speed = 50 } } };
            Assert.AreEqual(0, LMUNativeField.Apply(f, stale, 100.0, baseDist), "mehr als 0,6 s über die Wertung hinaus wird nicht geraten");
        }

        private static void LearnCircle(TrackPathMap map, float radius)
        {
            for (float d = 0; d < L; d += 2)
            {
                double a = d / L * 2 * Math.PI;
                map.Learn((float)(radius * Math.Cos(a)), 0, (float)(radius * Math.Sin(a)), d);
            }
        }

        [Test]
        public void Track_path_gives_lap_distance_for_a_world_position()
        {
            var map = new TrackPathMap();
            map.Prepare("Ring", L);
            Assert.IsNaN(map.Project(1, 0, 1, float.NaN), "noch keine Fahrlinie");
            float r = (float)(L / (2 * Math.PI));
            LearnCircle(map, r);
            Assert.IsTrue(map.Ready);
            double a = 1000 / L * 2 * Math.PI;
            float d = map.Project((float)(r * Math.Cos(a)) + 3, 0, (float)(r * Math.Sin(a)), float.NaN);
            Assert.AreEqual(1000, d, 6);
            Assert.AreEqual(1000, map.Project((float)(r * Math.Cos(a)), 0, (float)(r * Math.Sin(a)), 990), 6);
            Assert.IsNaN(map.Project(0, 0, 0, float.NaN), "mitten im Infield: zu weit von der Linie");
        }

        private static SPageFileGraphicEvo Graphic(int packet, float npos, params aceVec3[] positions)
        {
            var g = new SPageFileGraphicEvo
            {
                packetId = packet, status = ACEVO_STATUS.AC_LIVE, playerCarIdA = 11, playerCarIdB = 1, npos = npos, totalLapCount = 3,
                currentPos = 4, displaySpeedMs = 50, isValidLap = true, totalDrivers = (uint)positions.Length,
                carIds = new carId[64], carCoordinates = new aceVec3[60]
            };
            for (int i = 0; i < positions.Length; i++) { g.carIds[i] = new carId { carIdA = (ulong)(11 + i), carIdB = 1 }; g.carCoordinates[i] = positions[i]; }
            return g;
        }

        [Test]
        public void AcEvo_field_places_opponents_on_the_learned_line_and_measures_their_speed()
        {
            var st = new SPageFileStaticEvo { track = "Ring", trackConfiguration = "GP", trackLengthM = L, session = ACEVO_SESSION_TYPE.AC_RACE, isTimedRace = false };
            var state = new ACEFieldSource.State { SplineProjector = null };
            float r = (float)(L / (2 * Math.PI));
            state.Path.Prepare("Ring|GP", L);
            LearnCircle(state.Path, r);
            Func<float, aceVec3> at = d => { double a = d / L * 2 * Math.PI; return new aceVec3((float)(r * Math.Cos(a)), 0, (float)(r * Math.Sin(a))); };

            var f = new FieldFrame();
            Assert.IsTrue(ACEFieldSource.Fill(Graphic(1, 0.25f, at(1000), at(1100)), st, f, T, state));
            Assert.AreEqual(2, f.Cars.Length);
            Assert.IsTrue(f.Player.IsPlayer); Assert.AreEqual(1000, f.Player.LapDistance, 1e-3); Assert.AreEqual(4, f.Player.Place);
            var other = f.Cars.Single(c => !c.IsPlayer);
            Assert.AreEqual(1100, other.LapDistance, 6);
            Assert.IsNaN(other.Speed, "Tempo erst ab dem zweiten Bild");

            Assert.IsTrue(ACEFieldSource.Fill(Graphic(2, 0.25125f, at(1005), at(1105)), st, f, T.AddMilliseconds(100), state));
            other = f.Cars.Single(c => !c.IsPlayer);
            Assert.AreEqual(50, other.Speed, 3, "5 m in 0,1 s");
            Assert.AreEqual(f.Player.CompletedLaps, other.CompletedLaps, "gleiche Runde, da EVO keine Runden der Gegner liefert");

            var v = new FieldPicture().Observe(f);
            Assert.AreEqual(100, v.Ahead(1).Single().GapM, 7);
        }

        [Test]
        public void Board_names_the_cars_directly_ahead_and_behind_on_track()
        {
            var picture = new FieldPicture();
            var f = Frame(T,
                new FieldCarFrame { Slot = 1, Name = "Me", IsPlayer = true, LapDistance = 1000, Speed = 50, Place = 3 },
                new FieldCarFrame { Slot = 2, Name = "Front", LapDistance = 1100, Speed = 50, Place = 2 },
                new FieldCarFrame { Slot = 3, Name = "Back", LapDistance = 950, Speed = 50, Place = 4 });
            var v = picture.Observe(f);
            var b = RaceBoard.Compose(null, v, T.AddMilliseconds(50));
            Assert.AreEqual("Front", b.TrackAheadName); Assert.AreEqual("Back", b.TrackBehindName);
            Assert.IsTrue(b.TrackAheadS.Known); Assert.Greater(b.TrackAheadS.Value, 0);
            Assert.Greater(b.TrackBehindS.Value, 0);
        }
    }
}
