using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AMS2;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Fahrdynamik aus der schnellen Abtastung (Rob 17.11.2026): Blockierer, Durchdrehen, Ausritte je Kurve.</summary>
    [TestFixture, Category("AiTeam")]
    public class DrivingDynamicsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 17, 21, 0, 0, DateTimeKind.Utc);
        private const double Dt = .01; // 100 Hz

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private sealed class Drive
        {
            internal readonly DrivingDynamicsAnalyzer A = new DrivingDynamicsAnalyzer { CornerAt = (d, mode) => d >= 900 && d <= 1300 ? "Kurve 5" : null };
            internal readonly DynamicsFrame F = new DynamicsFrame { Driving = true, Gear = 4 };
            internal double Time;

            /// <summary>Fährt <paramref name="seconds"/> lang mit den gegebenen Werten (Raddrehzahl 1 = frei rollend).</summary>
            internal void Run(double seconds, float speed, float throttle, float brake, float[] wheelFactor = null, WheelSurface[] surfaces = null, float distance = 500, int laps = 3)
            {
                for (double t = 0; t < seconds; t += Dt)
                {
                    Time += Dt;
                    F.TimeS = Time; F.Speed = speed; F.Throttle = throttle; F.Brake = brake; F.LapDistance = distance; F.CompletedLaps = laps;
                    for (int i = 0; i < 4; i++)
                    {
                        F.WheelRotation[i] = speed / 2f * (wheelFactor == null ? 1f : wheelFactor[i]);
                        F.Surfaces[i] = surfaces == null ? WheelSurface.Road : surfaces[i];
                    }
                    A.Observe(F);
                }
            }

            internal void Calibrate(int laps = 3) => Run(1.0, 40, .4f, 0, laps: laps);

            /// <summary>Kurvenfahrt mit Querbeschleunigung, Gierrate und Lenkeinschlag.</summary>
            internal void Corner(double seconds, float speed, float lat, float yaw, float steer, float throttle, float distance = 1000, int laps = 3)
            {
                F.LatAccel = lat; F.YawRate = yaw; F.Steering = steer;
                Run(seconds, speed, throttle, 0, distance: distance, laps: laps);
                F.LatAccel = 0; F.YawRate = 0; F.Steering = 0;
            }
        }

        [Test]
        public void Wheels_are_calibrated_while_rolling_and_a_long_lock_is_a_heavy_lockup()
        {
            var d = new Drive();
            Assert.IsFalse(d.A.Calibrated(0));
            d.Calibrate();
            for (int i = 0; i < 4; i++) Assert.IsTrue(d.A.Calibrated(i), "Rad " + i + " geeicht");
            Assert.AreEqual(100, d.A.Hz, 1, "100 Bilder pro Sekunde gemessen");

            d.Run(.4, 35, 0, .9f, new[] { .2f, 1f, 1f, 1f }, distance: 1000);
            d.Run(.2, 30, 0, .3f, distance: 1010);
            var heavy = d.A.TakeHeavy();
            Assert.IsNotNull(heavy, "0,4 s bei 20 % Raddrehzahl");
            Assert.AreEqual(DrivingDynamicsAnalyzer.Kind.HeavyLockup, heavy.Kind);
            Assert.AreEqual(0, heavy.Wheel); Assert.AreEqual("Kurve 5", heavy.Corner);
            Assert.IsNull(d.A.TakeHeavy());
        }

        [Test]
        public void Short_flicker_or_abs_is_not_a_lockup()
        {
            var d = new Drive(); d.Calibrate();
            d.Run(.03, 35, 0, .9f, new[] { .5f, 1f, 1f, 1f }, distance: 1000);
            d.Run(.2, 30, 0, .3f, distance: 1010);
            d.F.AbsActive = true;
            d.Run(.5, 35, 0, .9f, new[] { .5f, 1f, 1f, 1f }, distance: 1000);
            d.F.AbsActive = false;
            d.Run(.2, 30, 0, .3f, distance: 1010);
            Assert.AreEqual(0, d.A.EventCount, "30 ms Zucken und ABS-Regelung zählen nicht");
        }

        [Test]
        public void Without_an_abs_flag_only_longer_and_deeper_locks_count()
        {
            var d = new Drive(); d.Calibrate();
            d.F.AbsUnknown = true;
            d.Run(.1, 35, 0, .9f, new[] { .5f, 1f, 1f, 1f }, distance: 1000);
            d.Run(.2, 30, 0, .3f, distance: 1010);
            d.Run(.3, 35, 0, .9f, new[] { .7f, 1f, 1f, 1f }, distance: 1000);
            d.Run(.2, 30, 0, .3f, distance: 1010);
            Assert.AreEqual(0, d.A.EventCount, "LMU: kurzes Regeln oder 70 % Raddrehzahl kann ABS sein");
            d.Run(.2, 35, 0, .9f, new[] { .5f, 1f, 1f, 1f }, distance: 1000);
            d.Run(.2, 30, 0, .3f, distance: 1010);
            Assert.AreEqual(1, d.A.EventCount, "0,2 s bei halber Raddrehzahl ist ein Blockierer");
        }

        [Test]
        public void Lockup_in_the_same_corner_on_two_of_three_laps_is_a_pattern_once()
        {
            var d = new Drive();
            d.Calibrate(3);
            d.Run(.15, 35, 0, .8f, new[] { 1f, .6f, 1f, 1f }, distance: 1000, laps: 3);
            d.Run(.2, 30, 0, .2f, distance: 1010, laps: 3);
            Assert.IsNull(d.A.FindPattern(4), "eine Runde ist kein Muster");
            d.Run(.5, 40, .4f, 0, distance: 400, laps: 4);
            d.Run(.15, 35, 0, .8f, new[] { 1f, .6f, 1f, 1f }, distance: 1005, laps: 4);
            d.Run(.2, 30, 0, .2f, distance: 1010, laps: 4);
            var p = d.A.FindPattern(5);
            Assert.IsNotNull(p);
            Assert.AreEqual(DrivingDynamicsAnalyzer.Kind.Lockup, p.Kind);
            Assert.AreEqual("Kurve 5", p.Corner); Assert.AreEqual(1, p.Wheel); Assert.AreEqual(2, p.Laps);
            Assert.IsNull(d.A.FindPattern(5), "dasselbe Muster nicht sofort wieder");
            var facts = d.A.Snapshot(5);
            Assert.AreEqual(2, (int)facts["blockierer_letzte_5_runden"]);
        }

        [Test]
        public void Rear_wheelspin_and_two_wheels_off_are_recognised()
        {
            var d = new Drive(); d.Calibrate();
            d.Run(.3, 20, 1f, 0, new[] { 1f, 1f, 1.35f, 1.3f }, distance: 1250);
            d.Run(.2, 22, .6f, 0, distance: 1260);
            var off = new[] { WheelSurface.Off, WheelSurface.Road, WheelSurface.Off, WheelSurface.Road };
            d.Run(.5, 30, .5f, 0, null, off, 1100);
            d.Run(.4, 30, .5f, 0, distance: 1150);
            var facts = d.A.Snapshot(4);
            Assert.AreEqual(2, (int)facts["durchdrehen_letzte_5_runden"], "beide Hinterräder");
            Assert.AreEqual(1, (int)facts["ausritte_letzte_5_runden"]);
            Assert.AreEqual(4, (int)facts["raeder_geeicht"]);
        }

        [Test]
        public void Big_off_with_three_wheels_is_reported_once_for_dirty_tyres()
        {
            TyreGripWatch.LastDirtCallUtc = DateTime.MinValue;
            var d = new Drive(); d.Calibrate();
            var three = new[] { WheelSurface.Off, WheelSurface.Off, WheelSurface.Off, WheelSurface.Road };
            d.Run(.8, 25, .3f, 0, null, three, 1100);
            d.Run(.4, 25, .3f, 0, distance: 1150);
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 4; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.CompletedLaps = 3;
            var watch = new DrivingDynamicsWatch(d.A, 2);
            watch.Observe(gs, T, false, "Rob");
            var call = watch.Take(gs, T, false);
            Assert.IsNotNull(call); Assert.AreEqual("insight-dirty-tyres", call.Topic); Assert.AreEqual(TeamMember.Mik, call.Member);
            Assert.IsNull(d.A.TakeBigOff(), "nur einmal");
            var two = new[] { WheelSurface.Off, WheelSurface.Road, WheelSurface.Off, WheelSurface.Road };
            d.Run(.8, 25, .3f, 0, null, two, 1100);
            d.Run(.4, 25, .3f, 0, distance: 1150);
            Assert.IsNull(d.A.TakeBigOff(), "zwei Räder neben der Strecke ist kein großer Ausritt");
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => DrivingDynamicsWatch.DirtyTyres(4, v)).Distinct().Count(), lang);
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Wheel_off_the_road_does_not_skew_calibration()
        {
            var d = new Drive();
            var grass = new[] { WheelSurface.Off, WheelSurface.Road, WheelSurface.Road, WheelSurface.Road };
            d.Run(1.0, 40, .4f, 0, new[] { 1.4f, 1f, 1f, 1f }, grass);
            Assert.IsFalse(d.A.Calibrated(0), "auf Gras wird nicht geeicht");
            Assert.IsTrue(d.A.Calibrated(1));
        }

        [Test]
        public void Balance_read_finds_understeer_and_oversteer_with_corner_phase()
        {
            var d = new Drive(); d.Calibrate();
            Assert.IsFalse(d.A.SteeringCalibrated);
            d.Corner(2.5, 30, 4f, 4f / 30f, .1f, .3f, distance: 400);       // 0,4 g, Auto folgt der Bahn: Lenkverhalten lernen
            Assert.IsTrue(d.A.SteeringCalibrated);
            d.Corner(.6, 30, 8f, 8f / 30f, .3f, .3f, distance: 1000);       // 0,8 g, aber dreifacher Lenkeinschlag: schiebt
            d.Corner(.2, 30, 3f, .1f, .1f, .3f, distance: 1100);
            d.Corner(.4, 30, 6f, .3f, .05f, .8f, distance: 1200);           // dreht schneller als die Bahn, unter Gas: Heck kommt
            d.Corner(.2, 30, 3f, .1f, .1f, .3f, distance: 1250);
            var facts = d.A.Snapshot(4);
            Assert.AreEqual(1, (int)facts["untersteuern_letzte_5_runden"]);
            Assert.AreEqual(1, (int)facts["uebersteuern_letzte_5_runden"]);
            Assert.IsTrue((bool)facts["lenkverhalten_geeicht"]);
        }

        [Test]
        public void Balance_radio_names_phase_and_gives_matching_advice()
        {
            string exit = DrivingDynamicsWatch.Place("Kurve 7", 0, 2), corner = DrivingDynamicsWatch.Place("Kurve 7", 0, 0);
            Assert.AreEqual("Am Ausgang von Kurve 7 kommt das Heck unter Gas, in zwei der letzten drei Runden. Sanfter ans Gas – oder Traktionskontrolle eine Stufe höher.",
                DrivingDynamicsWatch.Balance(true, 2, exit, 2, 0));
            StringAssert.Contains("In Kurve 7 schiebt das Auto beim Einlenken über die Vorderräder", DrivingDynamicsWatch.Balance(false, 1, corner, 2, 0));
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var all = new[] { true, false }.SelectMany(o => new[] { 0, 1, 2 }.SelectMany(ph => Enumerable.Range(0, 3).Select(v => DrivingDynamicsWatch.Balance(o, ph, corner, 3, v)))).ToList();
                Assert.AreEqual(18, all.Distinct().Count(), lang);
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Sim_surfaces_map_to_road_kerb_off_and_wall()
        {
            Assert.AreEqual(WheelSurface.Road, AMS2DynamicsSource.Surface((uint)eTerrainMaterials.TERRAIN_ROAD));
            Assert.AreEqual(WheelSurface.Kerb, AMS2DynamicsSource.Surface((uint)eTerrainMaterials.TERRAIN_RUMBLE_STRIPS));
            Assert.AreEqual(WheelSurface.Off, AMS2DynamicsSource.Surface((uint)eTerrainMaterials.TERRAIN_GRASS));
            Assert.AreEqual(WheelSurface.Off, AMS2DynamicsSource.Surface((uint)eTerrainMaterials.TERRAIN_GRAVEL));
            Assert.AreEqual(WheelSurface.Wall, AMS2DynamicsSource.Surface((uint)eTerrainMaterials.TERRAIN_GUARDRAILS));
            Assert.AreEqual(WheelSurface.Unknown, AMS2DynamicsSource.Surface((uint)eTerrainMaterials.TERRAIN_RUNOFF_ROAD), "liegt teils auf der Strecke");
            Assert.AreEqual(WheelSurface.Road, R3EDynamicsSource.Surface(1));
            Assert.AreEqual(WheelSurface.Kerb, R3EDynamicsSource.Surface(5));
            Assert.AreEqual(WheelSurface.Off, R3EDynamicsSource.Surface(4));
            Assert.AreEqual(WheelSurface.Unknown, R3EDynamicsSource.Surface(-1));
            Assert.AreEqual(WheelSurface.Road, LMUDynamicsSource.Surface(1), "nass ist trotzdem Strecke");
            Assert.AreEqual(WheelSurface.Off, LMUDynamicsSource.Surface(2));
            Assert.AreEqual(WheelSurface.Kerb, LMUDynamicsSource.Surface(5));
            Assert.AreEqual(WheelSurface.Unknown, LMUDynamicsSource.Surface(6));
        }

        [Test]
        public void Engineer_radio_for_dynamics_varies_and_exists_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                string brake = DrivingDynamicsWatch.Place("Kurve 5", 1000, 1), exit = DrivingDynamicsWatch.Place("Kurve 5", 1000, 2), at = DrivingDynamicsWatch.Place(null, 1234, 0);
                var texts = new[]
                {
                    Enumerable.Range(0, 3).Select(v => DrivingDynamicsWatch.Lockup(brake, DrivingDynamicsWatch.Wheel(1), false, 2, v)).ToArray(),
                    Enumerable.Range(0, 3).Select(v => DrivingDynamicsWatch.Wheelspin(exit, true, 3, v)).ToArray(),
                    Enumerable.Range(0, 3).Select(v => DrivingDynamicsWatch.OffTrack(v == 0 ? "Rob" : null, at, 2, 2, v)).ToArray(),
                    Enumerable.Range(0, 3).Select(v => DrivingDynamicsWatch.FlatSpot(DrivingDynamicsWatch.Wheel(0), "Kurve 5", v)).ToArray()
                };
                foreach (var group in texts)
                {
                    Assert.AreEqual(3, group.Distinct().Count(), lang);
                    foreach (string t in group) { StringAssert.DoesNotContain("  ", t); Assert.IsFalse(string.IsNullOrWhiteSpace(t)); }
                }
                StringAssert.Contains(lang == RadioLanguage.English ? "1250 metres" : lang == RadioLanguage.Spanish ? "1250 metros" : "1250 Metern", at);
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.StartsWith("Beim Anbremsen vor Kurve 5 blockiert vorne rechts, in zwei der letzten drei Runden.",
                DrivingDynamicsWatch.Lockup(DrivingDynamicsWatch.Place("Kurve 5", 0, 1), DrivingDynamicsWatch.Wheel(1), false, 2, 0));
            StringAssert.StartsWith("Am Ausgang von Kurve 5 drehen die Hinterräder durch",
                DrivingDynamicsWatch.Wheelspin(DrivingDynamicsWatch.Place("Kurve 5", 0, 2), true, 2, 0));
        }

        [Test]
        public void Watch_turns_a_pattern_into_a_call_for_Mik()
        {
            var d = new Drive(); d.Calibrate(3);
            d.Run(.15, 35, 0, .8f, new[] { 1f, 1f, .6f, 1f }, distance: 1000, laps: 3);
            d.Run(.2, 30, 0, .2f, distance: 1010, laps: 3);
            d.Run(.15, 35, 0, .8f, new[] { 1f, 1f, .6f, 1f }, distance: 1000, laps: 4);
            d.Run(.2, 30, 0, .2f, distance: 1010, laps: 4);
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 4; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.CompletedLaps = 5;
            var watch = new DrivingDynamicsWatch(d.A, 1);
            watch.Observe(gs, T, true, "Rob");
            var call = watch.Take(gs, T, false);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-lockup", call.Topic); Assert.AreEqual(TeamMember.Mik, call.Member);
            StringAssert.Contains("hinten links", call.Text.ToLowerInvariant());
            Assert.IsNull(new DrivingDynamicsWatch(d.A, 1).Take(gs, T, false), "ohne Muster nichts");
        }

        // ---------- Quali: Anschieben und Zwischenstand ----------

        [Test]
        public void Qualifying_lap_start_and_gap_to_best_have_six_and_five_variants()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(6, Enumerable.Range(0, 6).Select(v => SessionLapObserver.TimedLapStart(0, "Rob", v)).Distinct().Count(), lang);
                Assert.AreEqual(6, Enumerable.Range(0, 6).Select(v => SessionLapObserver.TimedLapStart(2, "Rob", v)).Distinct().Count(), lang);
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => SessionLapObserver.GapToBest(.812f, 5, "Gizmo", v)).Distinct().Count(), lang);
                Assert.AreEqual(4, Enumerable.Range(0, 4).Select(v => SessionLapObserver.GapToBest(0, 1, null, v)).Distinct().Count(), lang);
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.Contains("Auf die Bestzeit fehlen dir noch 0,812 Sekunden", SessionLapObserver.GapToBest(.812f, 5, "Gizmo", 0));
            StringAssert.Contains("So, auf geht's, Rob", SessionLapObserver.TimedLapStart(0, "Rob", 1));
        }

        [Test]
        public void Rear_traffic_is_rate_limited_and_has_enough_variety()
        {
            Assert.GreaterOrEqual(RaceTrafficWatch.RearSameCarSeconds, 300, "dasselbe Auto höchstens alle 5 Minuten");
            Assert.GreaterOrEqual(RaceTrafficWatch.RearAnySeconds, 45);
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.GreaterOrEqual(RaceTrafficWatch.RearPhrases.Distinct().Count(), 6, lang);
                Assert.GreaterOrEqual(RaceTrafficWatch.RearGroupPhrases.Distinct().Count(), 5, lang);
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Safety_car_is_never_a_backmarker_or_traffic()
        {
            Assert.IsTrue(TrackCarPosition.IsSafetyCarName("Safety Car KI"));
            Assert.IsTrue(TrackCarPosition.IsSafetyCarName("Pace Car"));
            Assert.IsFalse(TrackCarPosition.IsSafetyCarName("RenIre"));
            Assert.IsFalse(TrackCarPosition.IsSafetyCarName(null));
            Assert.AreEqual(20f, AiRadioSchedule.MaxRelevantGap, "weiter weg ist kein Ziel");
        }

        [Test]
        public void Provisional_fuel_estimate_is_spoken_only_once()
        {
            var watch = new FuelWatch();
            Assert.IsNotNull(watch.Evaluate(1, 60f, 3.3f, 25, true, true, provisionalConsumption: true));
            Assert.IsNull(watch.Evaluate(1, 59f, 3.4f, 25, true, true, provisionalConsumption: true), "kein „könnte knapp“ 15 s nach „könnte reichen“");
            Assert.IsNotNull(watch.Evaluate(3, 55f, 3.3f, 24, true, true), "gemessener Verbrauch darf wieder warnen");
        }

        [Test]
        public void Start_position_is_latched_once_per_race_session()
        {
            RaceStartLatch.Reset();
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 77; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionStartClassPosition = 16;
            Assert.AreEqual(16, RaceStartLatch.Position(gs.SessionData));
            gs.SessionData.SessionStartClassPosition = 4;
            Assert.AreEqual(16, RaceStartLatch.Position(gs.SessionData), "RaceRoom Monza: Startplatz darf sich im Rennen nicht ändern");
            gs.SessionData.SessionId = 78;
            Assert.AreEqual(4, RaceStartLatch.Position(gs.SessionData), "neue Sitzung, neuer Startplatz");
            RaceStartLatch.Reset();
        }

        // ---------- Kurven-Coaching mit 100 Hz ----------

        /// <summary>Eine Runde auf 1000 m mit einer Kurve 400–500 m, abgetastet mit 100 Hz statt in 5-m-Schritten.</summary>
        private static void FastLap(CornerCoach coach, ref float time, int lap, float brakeAt, float lapTime)
        {
            float d = 0;
            while (d < 1000)
            {
                float kmh = d >= brakeAt && d < 400 ? 200 - 110 * (d - brakeAt) / (400 - brakeAt) : d >= 400 && d < 500 ? 90 : 200;
                float ms = kmh / 3.6f;
                coach.Tick(d, ms, d >= brakeAt && d < 450 ? 0f : 1f, d >= brakeAt && d < 400 ? .9f : 0f, time, false, d >= 400 && d < 500 ? 2 : 5);
                time += .01f; d += ms * .01f;
            }
            coach.CompleteLap(lap, lapTime, true);
        }

        [Test]
        public void Corner_coaching_fed_at_100_hz_resolves_brake_points_below_a_metre()
        {
            var coach = new CornerCoach();
            coach.SetTrack(new[] { new CornerDefinition { Name = "Kurve 1", Start = 400, End = 500 } }, 1000);
            float t = 0;
            FastLap(coach, ref t, 1, 330, 60.0f);
            FastLap(coach, ref t, 2, 327, 60.3f);
            var corner = coach.Analyse()["kurven"][0];
            Assert.AreEqual(3, (double)corner["bremspunkt_m"], .8, "3 m Unterschied sind bei 100 Hz messbar");
        }

        [Test]
        public void Corner_lookup_distinguishes_braking_into_and_accelerating_out_of_a_corner()
        {
            var coach = new CornerCoach();
            coach.SetTrack(new[] { new CornerDefinition { Name = "Kurve 4", Start = 400, End = 500 }, new CornerDefinition { Name = "Kurve 5", Start = 650, End = 720 } }, 2000);
            Assert.AreEqual("Kurve 5", coach.CornerNameAt(560, 1), "Bremszone vor Kurve 5, obwohl Kurve 4 noch hinter uns liegt");
            Assert.AreEqual("Kurve 4", coach.CornerNameAt(560, 2), "Herausbeschleunigen aus Kurve 4");
            Assert.AreEqual("Kurve 4", coach.CornerNameAt(450, 0));
            Assert.IsNull(coach.CornerNameAt(1500, 0), "lange Gerade");
            Assert.IsNull(new CornerCoach().CornerNameAt(450, 0), "ohne Kurvendaten");
            coach.ResetTrackers();
        }
    }
}
