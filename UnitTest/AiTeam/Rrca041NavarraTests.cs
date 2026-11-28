using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>iRacing Navarra 28.11.2026 und Robs Regeln vom selben Tag (Arbeitsmappe 0.41: IR1–IR5, P7, N1–N12).</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class Rrca041NavarraTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 28, 8, 15, 17, DateTimeKind.Utc);
        [SetUp] public void Setup() { RadioLanguage.Current = RadioLanguage.German; }

        private static List<KeyValuePair<DateTime, float>> History(params (double s, float v)[] points) =>
            points.Select(p => new KeyValuePair<DateTime, float>(T.AddSeconds(p.s), p.v)).ToList();

        // IR1 / S1: 09:15:17 bremsten drei Autos 150 m vor Rob gemeinsam von 53 auf 25 m/s in die Kurve (üblich dort 30).
        [Test]
        public void Pack_braking_in_lap_one_is_no_spin()
        {
            var braking = History((-2.5, 53f), (-1.5, 44f), (-.5, 30f));
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(braking, 23f, T, null, 52f), "ohne Vergleichstempo der Stelle kein Dreher (früher: gegen Robs Tempo auf der Geraden)");
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(braking, 25f, T, 30f, 52f), "Feldtempo dort 30: normales Bremsen");
            Assert.IsTrue(RaceTrafficWatch.SuddenSlowdown(braking, 6f, T, 30f, 52f), "Einbruch auf 6 m/s, wo 30 üblich ist: Dreher");
        }

        [Test]
        public void Hazards_reach_200_metres_and_a_stall_after_launch_is_called()
        {
            Assert.AreEqual(200, RaceTrafficWatch.HazardReachM);
            Assert.AreEqual(200, RaceTrafficWatch.SpinReachM);
            Assert.AreEqual(200f, RearIncidentWatch.MaxBehindM);
            Assert.IsFalse(YellowAheadWatch.FarWarnings, "Rob 28.11.: keine Fernwarnung über 200 m");
            Assert.IsFalse(RaceTrafficWatch.StallCallable(0, DateTime.MinValue, T), "noch nicht losgefahren");
            Assert.IsFalse(RaceTrafficWatch.StallCallable(0, T, T.AddSeconds(1)), "erste Sekunde");
            Assert.IsTrue(RaceTrafficWatch.StallCallable(0, T, T.AddSeconds(2.5)), "Abwürger 2,5 s nach dem Losfahren");
            Assert.IsTrue(RaceTrafficWatch.StartGridQuiet(0, T, T.AddSeconds(2.5)), "sonst bleibt die Startphase ruhig");
        }

        // IR1: iRacing meldet „neben der Strecke“ je Auto – Abflug ohne Rückwärtsbewegung.
        [Test]
        public void Off_track_and_slow_is_a_spin_in_the_field_picture()
        {
            Assert.IsTrue(FieldPicture.OffTrackSpin(true, 10f, 40f));
            Assert.IsFalse(FieldPicture.OffTrackSpin(true, 35f, 40f), "nur weit gelaufen, kaum langsamer");
            Assert.IsFalse(FieldPicture.OffTrackSpin(false, 5f, 40f), "auf der Strecke entscheidet das Tempo allein");
            Assert.IsTrue(FieldPicture.OffTrackSpin(true, 8f, float.NaN), "Startrunde ohne Feldtempo: unter 15 m/s");
            Assert.IsFalse(FieldPicture.OffTrackSpin(true, 20f, float.NaN));
        }

        private static FieldCarView Car(string name, float gapM, float gapS, CarMotion motion) =>
            new FieldCarView { Name = name, GapM = gapM, GapS = gapS, Motion = motion, MotionSinceUtc = T.AddSeconds(-1) };

        private static FieldView View(params FieldCarView[] cars)
        {
            var player = new FieldCarView { Name = "Rob", IsPlayer = true, GapM = 0, GapS = 0 };
            return new FieldView { Utc = T, Session = FieldSession.Race, Cars = cars.Concat(new[] { player }).ToList(), Player = player };
        }

        // IR1 / S2: Raine 70 m und Yüksel 90 m hinter Rob blieben ungesagt.
        [Test]
        public void Slow_and_spun_cars_within_200_metres_behind_are_reported()
        {
            var slow = RearIncidentWatch.Nearest(View(Car("Lucas Raine", -73, -1.9f, CarMotion.Slow)), T);
            Assert.IsNotNull(slow); Assert.IsTrue(slow.Slow); Assert.IsFalse(slow.Stopped);
            StringAssert.Contains("Lucas Raine", new RearIncidentWatch().Text("Lucas Raine", false, true));
            Assert.IsNotNull(RearIncidentWatch.Nearest(View(Car("Ali Yüksel", -150, -5.5f, CarMotion.Spun)), T), "200 m zählen, nicht 4 s");
            Assert.IsNull(RearIncidentWatch.Nearest(View(Car("A", -250, -6f, CarMotion.Spun)), T), "über 200 m: nichts");
            var crash = RearIncidentWatch.Nearest(View(Car("A", -60, -1f, CarMotion.Stopped), Car("B", -80, -1.5f, CarMotion.Slow)), T);
            Assert.AreEqual(0, crash.Others.Count, "ein langsamer Wagen daneben ist kein Unfallbeteiligter");
            var texts = Enumerable.Range(0, 4).Select(_ => new RearIncidentWatch()).Select((w, i) => { for (int k = 0; k < i; k++) w.Text("X", false, true); return w.Text("X", false, true); });
            Assert.GreaterOrEqual(texts.Distinct().Count(), 4, "abwechselnde Formulierungen");
        }

        // P7: Drittel, zwei Drittel, kurz vor dem Limit, letzter Punkt, erreicht.
        [Test]
        public void Incident_limit_is_warned_by_thirds_and_shortly_before()
        {
            Assert.AreEqual(0, IncidentPointsWatch.Level(5, 17));
            Assert.AreEqual(1, IncidentPointsWatch.Level(6, 17));
            Assert.AreEqual(2, IncidentPointsWatch.Level(12, 17));
            Assert.AreEqual(3, IncidentPointsWatch.Level(15, 17));
            Assert.AreEqual(4, IncidentPointsWatch.Level(16, 17));
            Assert.AreEqual(5, IncidentPointsWatch.Level(17, 17));
            StringAssert.Contains("nicht mehr viel erlauben", IncidentPointsWatch.Text(3, 15, 17, 0));
            Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => IncidentPointsWatch.Text(1, 6, 17, v)).Distinct().Count());

            var watch = new IncidentPointsWatch();
            var gs = Race(); gs.SessionData.HasLimitedIncidents = true; gs.SessionData.MaxIncidentCount = 17; gs.SessionData.CurrentIncidentCount = 0;
            watch.Observe(gs, T);
            var said = new List<string>();
            foreach (int c in new[] { 2, 4, 11, 13, 15, 17 })
            {
                gs.SessionData.CurrentIncidentCount = c; watch.Observe(gs, T.AddSeconds(c));
                var call = watch.Take(gs, T.AddSeconds(c), false, true);
                if (call != null) { said.Add(call.Text); call.MarkPlaybackStarted(T.AddSeconds(c)); }
            }
            Assert.AreEqual(4, said.Count, "11 (Drittel), 13 (zwei Drittel), 15 (kurz vor), 17 (erreicht)");
            Assert.IsFalse(said.Any(t => t.Contains("Erster Vorfallpunkt")), "T11");
            Assert.IsTrue(IncidentPointsWatch.SpokeRecently(T.AddSeconds(20), TimeSpan.FromSeconds(90)), "Ben schweigt dazu (N10)");
        }

        private static GameStateData Race(int laps = 12)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 9; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.SessionNumberOfLaps = laps; s.SessionData.CompletedLaps = 0; s.SessionData.ClassPosition = 8;
            s.SessionData.TrackDefinition = new TrackDefinition("Navarra", 3887f);
            s.PositionAndMotionData.CarSpeed = 45;
            return s;
        }

        // P7 ohne Limit: 2–3 Hinweise nach Renndritteln, nur wenn etwas dazukam, ohne ein Limit zu behaupten.
        [Test]
        public void Without_a_limit_hints_come_by_race_thirds()
        {
            var watch = new IncidentPointsWatch();
            var gs = Race(); gs.SessionData.HasLimitedIncidents = false; gs.SessionData.MaxIncidentCount = -1;
            gs.SessionData.CurrentIncidentCount = 0; gs.PenaltiesData.CutTrackWarnings = 0;
            watch.Observe(gs, T);
            gs.SessionData.CompletedLaps = 3; gs.SessionData.CurrentIncidentCount = 2; watch.Observe(gs, T.AddMinutes(5));
            Assert.IsNull(watch.Take(gs, T.AddMinutes(5), false, true), "Drittel noch nicht erreicht");
            gs.SessionData.CompletedLaps = 4; watch.Observe(gs, T.AddMinutes(6));
            var first = watch.Take(gs, T.AddMinutes(6), false, true);
            Assert.IsNotNull(first); StringAssert.Contains("2 Vorfallpunkte", first.Text); StringAssert.Contains("ersten Renndrittel", first.Text);
            StringAssert.DoesNotContain(" von ", first.Text, "kein Limit behaupten");
            first.MarkPlaybackStarted(T.AddMinutes(6));
            gs.SessionData.CompletedLaps = 8; watch.Observe(gs, T.AddMinutes(12));
            Assert.IsNull(watch.Take(gs, T.AddMinutes(12), false, true), "sauberes zweites Drittel: nichts");
            gs.SessionData.CompletedLaps = 11; gs.PenaltiesData.CutTrackWarnings = 3; watch.Observe(gs, T.AddMinutes(17));
            var last = watch.Take(gs, T.AddMinutes(17), false, true);
            Assert.IsNotNull(last); StringAssert.Contains("3 Tracklimit-Verstöße", last.Text);
        }

        // N1 / N5: Platzhalter 1000 und unvollständige Zeitenliste.
        [Test]
        public void Timesheet_rank_only_when_it_is_complete()
        {
            Assert.IsFalse(FieldFacts.RealPlace(1000)); Assert.IsTrue(FieldFacts.RealPlace(10));
            var timed = Enumerable.Range(0, 7).Select(i => new FieldCarView { Name = "C" + i, BestLap = 105 + i * .2f }).ToList();
            timed.Add(new FieldCarView { Name = "Rob", IsPlayer = true, BestLap = 106.404f });
            Assert.AreEqual(10, FieldFacts.TrustedRank(timed, 106.404f, 10), "8 Zeiten, Spielplatz 10: Spielplatz gilt");
            Assert.AreEqual(FieldFacts.RankOf(timed, 106.404f), FieldFacts.TrustedRank(timed, 106.404f, 1000), "ohne echten Spielplatz die Liste");
            Assert.AreEqual(FieldFacts.RankOf(timed, 106.404f), FieldFacts.TrustedRank(timed, 106.404f, 5), "Liste vollständig genug");
        }

        // N6: Startrunde mit Zwischenfall ist kein Bezug.
        [Test]
        public void A_lap_one_incident_lap_is_no_reference()
        {
            Assert.IsFalse(SessionLapObserver.RaceLapRepresentative(121.865f, 106.404f));
            Assert.IsTrue(SessionLapObserver.RaceLapRepresentative(108.5f, 106.404f));
            Assert.IsTrue(SessionLapObserver.RaceLapRepresentative(121.865f, -1));
        }

        // N12: Nach der Disqualifikation nur noch Meldung und Bilanz.
        [Test]
        public void After_disqualification_only_the_summary_speaks()
        {
            var gs = Race(); gs.SessionData.IsDisqualified = true;
            Assert.IsFalse(CrewSpeechPolicy.AllowedAfterDisqualification("incident-points", gs));
            Assert.IsFalse(CrewSpeechPolicy.AllowedAfterDisqualification("position-lost", gs));
            Assert.IsTrue(CrewSpeechPolicy.AllowedAfterDisqualification("race-disqualified", gs));
            Assert.IsTrue(CrewSpeechPolicy.AllowedAfterDisqualification("race-summary", gs));
            gs.SessionData.IsDisqualified = false;
            Assert.IsTrue(CrewSpeechPolicy.AllowedAfterDisqualification("incident-points", gs));
        }

        // IR3: Reifenbild beim Boxenhalt.
        [Test]
        public void Pit_tyre_report_names_averages_distribution_and_wear()
        {
            var gs = Race();
            var t = gs.TyreData;
            t.FrontLeft_RightTemp = 95; t.FrontLeft_CenterTemp = 88; t.FrontLeft_LeftTemp = 80;      // VL: innen 95, außen 80
            t.FrontRight_LeftTemp = 86; t.FrontRight_CenterTemp = 85; t.FrontRight_RightTemp = 82;
            t.RearLeft_RightTemp = 78; t.RearLeft_CenterTemp = 77; t.RearLeft_LeftTemp = 75;
            t.RearRight_LeftTemp = 78; t.RearRight_CenterTemp = 77; t.RearRight_RightTemp = 75;
            t.TyreWearActive = true; t.FrontLeftPercentWear = 8; t.FrontRightPercentWear = 5; t.RearLeftPercentWear = 3; t.RearRightPercentWear = 3;
            var r = PitTyreReport.Read(gs);
            Assert.IsNotNull(r);
            string text = PitTyreReport.Compose(r, 0);
            StringAssert.Contains("vorn im Schnitt 86 Grad, hinten 77", text);
            StringAssert.Contains("Vorne links: innen deutlich heißer als außen, viel Sturz.", text);
            StringAssert.Contains("Am meisten abgebaut hat vorne links, noch 92 Prozent.", text);
            Assert.IsFalse(PitTyreReport.Changed(r, PitTyreReport.Read(gs)), "gleiches Bild: keine neue Meldung");
            t.RearRight_CenterTemp = 90;
            Assert.IsTrue(PitTyreReport.Changed(r, PitTyreReport.Read(gs)));
            t.RearRight_CenterTemp = 0;
            Assert.IsNull(PitTyreReport.Read(gs), "unplausible Werte: nichts sagen");
        }

        // IR2: Zeitverlust nach dem Treffer und ob sich die Reparatur lohnt.
        [Test]
        public void Impact_time_loss_weighs_repair_against_remaining_laps()
        {
            var d = new CarDamageData { DamageEnabled = true, RepairRequired = false, RepairOptional = true, RepairSecondsOptional = 45 };
            string stay = PitDamageWatch.ImpactLossText(.8f, d, 12, 0);
            StringAssert.Contains("0,8 Sekunden pro Runde", stay); StringAssert.Contains("Draußen bleiben", stay);
            StringAssert.Contains("lohnt sich", PitDamageWatch.ImpactLossText(2f, d, 40, 0));
            StringAssert.Contains("ohnehin an die Box", PitDamageWatch.ImpactLossText(4f, d, 15, 0));
            StringAssert.Contains("kaum Zeit", PitDamageWatch.ImpactLossText(.1f, d, 12, 0));
            var mandatory = new CarDamageData { DamageEnabled = true, RepairRequired = true, RepairSecondsRequired = 30 };
            StringAssert.Contains("Pflichtreparatur kostet 30", PitDamageWatch.ImpactLossText(1f, mandatory, 10, 0));
            var s = Race(20).SessionData; s.CompletedLaps = 8;
            Assert.AreEqual(12, PitDamageWatch.LapsLeft(s, 106f));
            StringAssert.Contains("vorn", PitDamageWatch.ServiceError(102, 0));
            Assert.IsTrue(PitDamageWatch.ImpactDamage(d));
            Assert.IsFalse(PitDamageWatch.ImpactDamage(new CarDamageData { DamageEnabled = true, RepairRequired = false, RepairOptional = false }));
            StringAssert.Contains("fehlen rund 8 km/h", PitDamageWatch.ImpactLossText(.8f, d, 12, 0, 8f), "IR2.4 Topspeed");
            StringAssert.DoesNotContain("km/h", PitDamageWatch.ImpactLossText(.8f, d, 12, 0, 3f), "Windschatten-Rauschen bleibt still");
            StringAssert.DoesNotContain("km/h", PitDamageWatch.ImpactLossText(.1f, d, 12, 0, 9f), "ohne Zeitverlust kein Topspeed-Urteil");
        }

        // IR3.4: Beim Einfahren bestätigt Mik den eingestellten Service.
        [Test]
        public void Pit_service_plan_is_confirmed_in_words()
        {
            Assert.AreEqual("Service eingestellt: vier neue Reifen, 25 Liter, Fast Repair.", PitDamageWatch.ServicePlan(0x1F | 0x40, 24.6f, 0));
            StringAssert.Contains("zwei neue vorn, kein Sprit", PitDamageWatch.ServicePlan(0x03, 0, 0));
            StringAssert.Contains("zwei neue links", PitDamageWatch.ServicePlan(0x05, 0, 1));
            StringAssert.Contains("keine Reifen, 10 Liter", PitDamageWatch.ServicePlan(0x10, 10, 2));
            Assert.IsNull(PitDamageWatch.ServicePlan(0, 30, 0), "nichts eingestellt: nichts sagen");
            Assert.IsNull(PitDamageWatch.ServicePlan(-1, 30, 0), "Sim liefert es nicht");
        }
    }
}
